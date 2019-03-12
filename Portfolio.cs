using System;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Handlers.Options;
using TSLab.Script.Realtime;
using TSLab.Utils;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.Portfolio)]
    [HelperName("Free money", Language = Constants.En)]
    [HelperName("Свободные деньги", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает наличие свободных денег на счету. В режиме агента информация транслируется со счета. В режиме лаборатории рассчитывается на основании позиций по формуле: " +
        "Свободные деньги = деньги - позиции - деньги блокированные в заявках.")]
    [HelperDescription("Shows free money in your account. In agent mode information about free money is received from your account. In laboratory mode information about free money is calculated according to the following formula: " +
        "Free Money = money - (minus)positions - (minus)money blocked in orders.", Constants.En)]
    public class FreeMoney : IBar2ValueDoubleHandler
    {
        public double Execute(ISecurity source, int barNum)
        {
            var srt = source as ISecurityRt;
            if (srt != null)
            {
                return srt.CurrencyBalance;
            }
            double profit = source.InitDeposit;
            foreach (var pos in source.Positions.GetClosedOrActiveForBar(barNum))
            {
                if (pos.IsActiveForBar(barNum))
                {
                    profit -= pos.EntryPrice * pos.Shares * source.LotSize;
                }
                else
                {
                    profit += pos.Profit();
                }
            }
            return profit;
        }
    }

    [HandlerCategory(HandlerCategories.Portfolio)]
    [HelperName("Portfolio estimation", Language = Constants.En)]
    [HelperName("Оценка портфеля", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает оценку портфеля. В режиме агента информация транслируется со счета. В режиме лаборатории рассчитывается на основании позиции по формуле: " +
        "Оценка портфеля = деньги + позиции.")]
    [HelperDescription("Shows your portfolio estimation. In agent mode portfolio estimation is received from your account. In laboratory mode portfolio estimation is calculated according the following formula: " +
        "Portfolio Estimation = money + positions.", Constants.En)]
    public sealed class EstimatedMoney : IBar2ValueDoubleHandler
    {
        public double Execute(ISecurity source, int barNum)
        {
            var securityRt = source as ISecurityRt;
            return securityRt?.EstimatedBalance ?? source.InitDeposit + source.GetProfit(barNum);
        }
    }

    [HandlerCategory(HandlerCategories.Portfolio)]
    [HelperName("Current position", Language = Constants.En)]
    [HelperName("Текущая позиция", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает совокупную позицию по бумаге. В режиме лаборатории отображается расчетная позиция скрипта. " +
        "В режиме агента отображается значение из колонки 'Текущая' окна 'Позиции' для торгуемых источников.")]
    [HelperDescription("Shows a total position involving an instrument. In laboratory mode this block shows a calculated position of a script. " +
        "In agent mode this block shows a value of the Current column(in the Positions window) for tradable sources.", Constants.En)]
    public class CurrentPosition : IBar2ValueDoubleHandler
    {
        public double Execute(ISecurity source, int barNum)
        {
            var srt = source as ISecurityRt;
            if (srt != null)
            {
                return srt.BalanceQuantity;
            }
            var activePos = source.Positions.GetActiveForBar(barNum);
            return activePos.Sum(pos => pos.Shares * (pos.IsLong ? 1 : -1));
        }
    }

    [HandlerCategory(HandlerCategories.Portfolio)]
    [HelperName("Agent current position", Language = Constants.En)]
    [HelperName("Текущая позиция агента", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Отображает расчетную позицию агента.")]
    [HelperDescription("This block shows a calculated position of an agent.", Constants.En)]
    public class AgentCurrentPosition : IBar2ValueDoubleHandler
    {
        public double Execute(ISecurity source, int barNum)
        {
            var activePos = source.Positions.GetActiveForBar(barNum);
            return activePos.Sum(pos => pos.Shares * (pos.IsLong ? 1 : -1));
        }
    }

    public enum ProfitKind
    {
        [LocalizeDescription("ProfitKind.Unfixed")]
        Unfixed,
        [LocalizeDescription("ProfitKind.Fixed")]
        Fixed,
    }

    // Лучше не вешать категорию на базовые абстрактные классы. Это снижает гибкость дальнейшего управления ими.
    public abstract class BaseProfitHandler : IBar2ValueDoubleHandler
    {
        /// <summary>
        /// \~english Profit kind (fixed or unfixed)
        /// \~russian Тип прибыли (фиксированная или плавающая)
        /// </summary>
        [HelperName("Profit kind", Constants.En)]
        [HelperName("Тип прибыли", Constants.Ru)]
        [Description("Тип прибыли (фиксированная или плавающая)")]
        [HelperDescription("Profit kind (fixed or unfixed)", Constants.En)]
        [HandlerParameter(true, nameof(ProfitKind.Unfixed))]
        public ProfitKind ProfitKind { get; set; }

        public abstract double Execute(ISecurity source, int barNum);
    }

    [HandlerCategory(HandlerCategories.Portfolio)]
    [HelperName("Profit (whole period)", Language = Constants.En)]
    [HelperName("Доход (за все время)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Считает доход по бумаге по сделкам за все время.")]
    [HelperDescription("Calculates profit involving an instrument received in all trades of the whole period.", Constants.En)]
    public sealed class WholeTimeProfit : BaseProfitHandler
    {
        public override double Execute(ISecurity source, int barNum)
        {
            switch (ProfitKind)
            {
                case ProfitKind.Unfixed:
                    return source.GetProfit(barNum);
                case ProfitKind.Fixed:
                    return source.GetAccumulatedProfit(barNum);
                default:
                    throw new InvalidEnumArgumentException(nameof(ProfitKind), (int)ProfitKind, ProfitKind.GetType());
            }
        }
    }

    [HandlerCategory(HandlerCategories.Portfolio)]
    [HelperName("Profit (one day period)", Language = Constants.En)]
    [HelperName("Доход (за день)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Считает доход по бумаге по сделкам за день.")]
    [HelperDescription("Calculates profit involving an instrument received in all trades of the day.", Constants.En)]
    public sealed class WholeDayProfit : BaseProfitHandler
    {
        private int m_oldStartBarNum = -1;

        private DateTime m_oldStartDate;

        /// <summary>
        /// \~english Trading session start (format HH:MM:SS)
        /// \~russian Время начала торговой сессии (формат ЧЧ:ММ:СС)
        /// </summary>
        [HelperName("Session start", Constants.En)]
        [HelperName("Начало сессии", Constants.Ru)]
        [Description("Время начала торговой сессии (формат ЧЧ:ММ:СС)")]
        [HelperDescription("Trading session start (format HH:MM:SS)", Constants.En)]
        [HandlerParameter(true, "0:0:0", Min = "0:0:0", Max = "23:59:59", Step = "1:0:0", EditorMin = "0:0:0", EditorMax = "23:59:59")]
        public TimeSpan SessionStart { get; set; }

        public override double Execute(ISecurity source, int barNum)
        {
            var barDate = source.Bars[barNum].Date;
            var startDay = barDate.Date.Add(SessionStart);

            if (startDay > barDate)
                startDay = startDay.AddDays(-1);

            var endDay = startDay.AddDays(1);
            if (startDay > m_oldStartDate)
            {
                m_oldStartDate = startDay;
                m_oldStartBarNum = barNum;
            }
            Func<IPosition, int, double> getProfitFunc;
            switch (ProfitKind)
            {
                case ProfitKind.Unfixed:
                    getProfitFunc = ProfitExtensions.GetProfit;
                    break;
                case ProfitKind.Fixed:
                    getProfitFunc = ProfitExtensions.GetAccumulatedProfit;
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(ProfitKind), (int)ProfitKind, ProfitKind.GetType());
            }
            var profit = CalcProfit(source, barNum, getProfitFunc, startDay, endDay, m_oldStartBarNum);
            return profit;
        }

        public static double CalcProfit(ISecurity source, int barNum, Func<IPosition, int, double> getProfitFunc, int days, ref DateTime oldStartDate, ref int oldStartBarNum)
        {
            var dt = source.Bars[barNum].Date;
            var startDay = dt.Date;
            var endDay = startDay.AddDays(1);
            var startBarNum = barNum;
            if (days > 1)
            {
                startDay = startDay.AddDays(1 - days);
                if (startDay < source.Bars[0].Date)
                {
                    startDay = source.Bars[0].Date;
                    startBarNum = 0;
                }
            }
            if (startDay > oldStartDate)
            {
                oldStartDate = startDay;
                if (days > 1)
                {
                    for (int i = barNum; i >= 0; i--)
                    {
                        if (source.Bars[i].Date >= startDay) continue;
                        startBarNum = i + 1;
                        break;
                    }
                }
                oldStartBarNum = startBarNum;
            }
            var profit = CalcProfit(source, barNum, getProfitFunc, startDay, endDay, oldStartBarNum);
            return profit;
        }

        private static double CalcProfit(ISecurity source, int barNum, Func<IPosition, int, double> getProfitFunc, DateTime startDay, DateTime endDay, int oldStartBarNum)
        {
            double profit = 0;
            foreach (var pos in source.Positions)
            {
                if (pos.EntryBarNum <= barNum && (pos.IsActive || (pos.ExitBar.Date >= startDay && pos.EntryBar.Date < endDay)))
                {
                    profit += getProfitFunc(pos, barNum);
                    if (pos.EntryBarNum < oldStartBarNum)
                    {
                        profit -= pos.CurrentProfitByOpenPrice(oldStartBarNum);
                    }
                }
            }
            return profit;
        }
    }

    //[HandlerCategory(HandlerCategories.Portfolio)]
    // Лучше не вешать категорию на базовые абстрактные классы. Это снижает гибкость дальнейшего управления ими.
    public abstract class BasePeriodProfitHandler : BasePeriodIndicatorHandler, IBar2ValueDoubleHandler
    {
        /// <summary>
        /// \~english Profit kind (fixed or unfixed)
        /// \~russian Тип прибыли (фиксированная или плавающая)
        /// </summary>
        [HelperName("Profit kind", Constants.En)]
        [HelperName("Тип прибыли", Constants.Ru)]
        [Description("Тип прибыли (фиксированная или плавающая)")]
        [HelperDescription("Profit kind (fixed or unfixed)", Constants.En)]
        [HandlerParameter(true, nameof(ProfitKind.Unfixed))]
        public ProfitKind ProfitKind { get; set; }

        public abstract double Execute(ISecurity source, int barNum);

        protected Func<IPosition, int, double> GetProfitFunc()
        {
            switch (ProfitKind)
            {
                case ProfitKind.Unfixed:
                    return ProfitExtensions.GetProfit;
                case ProfitKind.Fixed:
                    return ProfitExtensions.GetAccumulatedProfit;
                default:
                    throw new InvalidEnumArgumentException(nameof(ProfitKind), (int)ProfitKind, ProfitKind.GetType());
            }
        }
    }

    [HandlerCategory(HandlerCategories.Portfolio)]
    [HelperName("Profit (in N days)", Language = Constants.En)]
    [HelperName("Доход (за N дней)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Считает доход по бумаге за указанное количество дней.")]
    [HelperDescription("Calculates profit involving an instrument received during a specified number of days.", Constants.En)]
    public sealed class NumDaysProfit : BasePeriodProfitHandler
    {
        private int m_oldDayStart = -1;

        private DateTime m_oldStartDay;

        public override double Execute(ISecurity source, int barNum)
        {
            return WholeDayProfit.CalcProfit(source, barNum, GetProfitFunc(), Period, ref m_oldStartDay, ref m_oldDayStart);
        }
    }

    [HandlerCategory(HandlerCategories.Portfolio)]
    [HelperName("Profit (in N minutes)", Language = Constants.En)]
    [HelperName("Доход (за N минут)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Считает доход по бумаге за указанное количество минут.")]
    [HelperDescription("Calculates profit involving an instrument received during a specified number of minutes.", Constants.En)]
    public sealed class NumMinutesProfit : BasePeriodProfitHandler
    {
        public override double Execute(ISecurity source, int barNum)
        {
            var getProfitFunc = GetProfitFunc();
            var endDate = source.Bars[barNum].Date;
            var startDate = endDate.AddMinutes(-Period);

            return source.Positions.GetClosedOrActiveForBar(barNum)
                .Where(pos => pos.IsActive || (pos.ExitBar.Date >= startDate && pos.EntryBar.Date < endDate))
                .Sum(pos => getProfitFunc(pos, barNum));
        }
    }

    [HandlerCategory(HandlerCategories.Portfolio)]
    [HelperName("Profit (in N positions)", Language = Constants.En)]
    [HelperName("Доход (за N позиций)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Считает доход по бумаге за указанное количество позиций.")]
    [HelperDescription("Calculates profit involving an instrument received during a specified number of positions.", Constants.En)]
    public sealed class NumPositionsProfit : BasePeriodProfitHandler
    {
        public override double Execute(ISecurity source, int barNum)
        {
            var getProfitFunc = GetProfitFunc();
            double profit = 0;
            double trades = Period;

            foreach (var pos in source.Positions
                                    .Where(pos => !pos.IsActiveForBar(barNum))
                                    .OrderByDescending(pos => pos.ExitBarNum))
            {
                profit += getProfitFunc(pos, barNum);
                if (--trades <= 0)
                {
                    break;
                }
            }
            return profit;
        }
    }
}
