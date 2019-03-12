using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TSLab.DataSource;
using TSLab.Script.Handlers.Options;
using TSLab.Script.Realtime;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global
namespace TSLab.Script.Handlers
{
    //[HandlerName("Shares Initial")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Quantity (Initial)", Language = Constants.En)]
    [HelperName("Количество (Начальное)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Количество оригинальных лотов в позиции (до изменения их в режиме симуляции портфеля).")]
    [HelperDescription("Position shares origin in lots.", Constants.En)]
    public class PositionShares : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null || pos.EntryBarNum > barNum)
            {
                return 0;
            }
            return pos.SharesOrigin;
        }
    }

    //[HandlerName("Shares")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Quantity", Language = Constants.En)]
    [HelperName("Количество", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("На каждом баре возвращает текущий размер позиции в лотах.")]
    [HelperDescription("Returns the current size of the position in lots at every bar.", Constants.En)]
    public class PositionSharesByBar : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null || pos.EntryBarNum > barNum)
            {
                return 0;
            }
            return pos.GetShares(barNum);
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Average entry price", Language = Constants.En)]
    [HelperName("Средняя цена входа", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Средняя цена входа в позицию. Если вход был один, то равна цене входа, если были изменения позиции, то равна средневзвешенной цене всех приращений позиции. При уменьшении позиции цена входа не меняется, но изменяется фиксированная часть п/у.")]
    [HelperDescription("An average position entry price.", Constants.En)]
    public class BalancedPrice : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null || pos.EntryBarNum > barNum)
            {
                return 0;
            }
            return pos.GetBalancePrice(barNum);
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Entry price", Language = Constants.En)]
    [HelperName("Цена входа", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Цена сделки, по которой открылась позиция. Для режима реальных торгов это цена по которой была выставлена заявка на открытие позиции.")]
    [HelperDescription("The price of the trade at which the position was opened. In the trading mode this is the price at which the position opening order was placed.", Constants.En)]
    public class EntryPrice : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null || pos.EntryBarNum > barNum)
            {
                return 0;
            }
            return pos.EntryPrice;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Entry price (estimated)", Language = Constants.En)]
    [HelperName("Цена входа (расчетная)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Расчетная цена сделки, по которой открылась позиция. Для режима лаборатории это цена открытия следующего за сигналом бара. Внимание! Если вход произошел не по расчетной свече, то расчетная цена не может быть восстановлена.")]
    [HelperDescription("The calculated price of the trade at which the position was opened. In the laboratory mode it is equal to the opening price of the bar following the signal.", Constants.En)]
    public class CalcEntryPrice : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            return EntryPrice(pos, barNum);
        }

        public static double EntryPrice(IPosition pos, int barNum)
        {
            if (pos == null || pos.EntryBarNum > barNum)
            {
                return 0;
            }
            var posRt = pos as IPositionRt;
            if (posRt != null && posRt.EntryOrders.Any())
            {
                var order = posRt.EntryOrders.First();
                var orderPrice = order.OrderPrice;
                // ReSharper disable CompareOfFloatsByEqualityOperator
                var useOpen = order.OrderType == OrderType.Market || orderPrice == 0;
                // ReSharper restore CompareOfFloatsByEqualityOperator
                var entryPrice = pos.EntryBar.Open;
                useOpen |= order.OrderType == OrderType.Limit &&
                           (order.IsBuy && orderPrice > entryPrice || !order.IsBuy && orderPrice < entryPrice);
                return useOpen ? entryPrice : orderPrice;
            }
            return pos.EntryPrice;
        }
    }

    //[HandlerName("Entry Date")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Entry date", Language = Constants.En)]
    [HelperName("Дата входа", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Дата входа в позицию, представленная как число в формате ГГММДД.")]
    [HelperDescription("A date of position entry presented as number in YYMMDD format.", Constants.En)]
    public class EntryDate : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null || pos.EntryBarNum > barNum)
            {
                return 0;
            }
            var dt = pos.EntryBar.Date;
            return (dt.Year % 100) * 10000.0 + dt.Month * 100.0 + dt.Day;
        }
    }

    //[HandlerName("Entry Time")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Entry time", Language = Constants.En)]
    [HelperName("Время входа", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Время входа в позицию, представленное как число в формате ЧЧММСС.")]
    [HelperDescription("Time of  position entry presented as number in HHMMSS format.", Constants.En)]
    public class EntryTime : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null || pos.EntryBarNum > barNum)
            {
                return 0;
            }
            var dt = pos.EntryBar.Date;
            return dt.Hour * 10000.0 + dt.Minute * 100.0 + dt.Second;
        }
    }

    //[HandlerName("Bars Held")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Bars held", Language = Constants.En)]
    [HelperName("Удерживалось баров", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Возвращает количество баров удержания позиции.")]
    [HelperDescription("Returns the number of bars to hold the position.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD-BarsHeld.tscript", Name = "Модифицированный пример по индикатору MACD")]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD-BarsHeld.tscript", Name = "Modified example of MACD", Language = Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class BarsHeld : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null || pos.EntryBarNum >= barNum)
            {
                return 0;
            }
            if (pos.IsActive || pos.ExitBarNum > barNum)
            {
                return barNum - pos.EntryBarNum;
            }
            return pos.ExitBarNum - pos.EntryBarNum;
        }
    }

    // TODO: а точно прибыль идет в расчете на 1 лот?
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Profit", Language = Constants.En)]
    [HelperName("Доход", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Доход (убыток) приносимый позицией в абсолютных величинах. В расчете на один контракт/лот.")]
    [HelperDescription("Profit (loss) given by the position in absolute values. Calculated per one contract/lot.", Constants.En)]
    public class Profit : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
            {
                return 0;
            }
            return pos.CurrentProfit(barNum);
        }
    }

    // TODO: а точно прибыль идет в расчете на 1 лот?
    //[HandlerName("Profit %")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Profit %", Language = Constants.En)]
    [HelperName("Доход %", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Доход (убыток) приносимый позицией в процентах. В расчете на один контракт/лот.")]
    [HelperDescription("Profit (loss) given by the position in percentage valu.Calculated per one contract/lot.", Constants.En)]
    public class ProfitPct : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
            {
                return 0;
            }
            // ReSharper disable CompareOfFloatsByEqualityOperator
            return pos.Shares == 0 ? 0 : pos.CurrentProfitPct(barNum);
            // ReSharper restore CompareOfFloatsByEqualityOperator
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("MAE", Language = Constants.En)]
    [HelperName("MAE", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Maximum Adverse Excursion - максимальное неблагоприятное отклонение цены от позиции в абсолютных величинах. В расчете на один контракт/лот.")]
    [HelperDescription("Maximum Adverse Excursion per one contract/lot.", Constants.En)]
    public class MAE : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
            {
                return 0;
            }
            return pos.OpenMAE(barNum);
        }
    }

    //[HandlerName("MAE %")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("MAE %", Language = Constants.En)]
    [HelperName("MAE %", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Maximum Adverse Excursion - максимальное неблагоприятное отклонение цены от позиции в процентах. В расчете на один контракт/лот.")]
    [HelperDescription("Maximum Adverse Excursion per one contract/lot (as percents).", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/2MA_customStop.tscript", Name = "Пример стратегии 2МА с нестандартным стопом")]
    [HelperLink(@"http://www.tslab.ru/files/script/2MA_customStop.tscript", Name = "Example of 2МА strategy with nonstandard stop", Language = Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class MAEPct : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
            {
                return 0;
            }
            return pos.OpenMAEPct(barNum);
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("MFE", Language = Constants.En)]
    [HelperName("MFE", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Maximum Favorable Excursion - максимальное благоприятное отклонение цены от позиции в абсолютных величинах. В расчете на один контракт/лот.")]
    [HelperDescription("Maximum Favorable Excursion per one contract/lot.", Constants.En)]
    public class MFE : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
            {
                return 0;
            }
            return pos.OpenMFE(barNum);
        }
    }

    //[HandlerName("MFE %")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("MFE %", Language = Constants.En)]
    [HelperName("MFE %", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Maximum Favorable Excursion Максимальное благоприятное отклонение цены от позиции в процентах. В расчете на один контракт/лот.")]
    [HelperDescription("Maximum Favorable Excursion per one contract/lot (as percents).", Constants.En)]
    public class MFEPct : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
            {
                return 0;
            }
            return pos.OpenMFEPct(barNum);
        }
    }

    // TODO: по идее, на русском языке это называется 'Следящий стоп'
    //[HandlerName("Trail Stop")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Trail stop", Language = Constants.En)]
    [HelperName("Трейл Стоп", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("То же, что и 'Следящий стоп абс.', но параметры ведения задаются в процентах.")]
    [HelperDescription("This block is identical to 'Trailing stop absolute', but its parameters are in the percentage value.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Alligator_tradable.tscript", Name = "Пример по индикатору Alligator")]
    [HelperLink(@"http://www.tslab.ru/files/script/Alligator_tradable.tscript", Name = "Example of Alligator", Language = Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo.tscript", Name = "Пример стратегии Hi - Low")]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo.tscript", Name = "Example of strategy Hi - Low", Language = Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Momentum.tscript", Name = "Пример по индикатору Momentum")]
    [HelperLink(@"http://www.tslab.ru/files/script/Momentum.tscript", Name = "Example  of Momentum", Language = Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class TrailStop : IPosition2Double
    {
        /// <summary>
        /// \~english Initial stop loss
        /// \~russian Начальный уровень стоплосса
        /// </summary>
        [HelperName("Stop loss", Constants.En)]
        [HelperName("Стоплосс", Constants.Ru)]
        [Description("Начальный уровень стоплосса")]
        [HelperDescription("Initial stop loss", Constants.En)]
        [HandlerParameter(true, "1.5", Min = "0.1", Max = "5", Step = "0.1", Name = "Stop Loss")]
        public double StopLoss { get; set; }

        /// <summary>
        /// \~english Where to start actual trailing
        /// \~russian На каком уровне начинать двигать стоп
        /// </summary>
        [HelperName("Trail enable", Constants.En)]
        [HelperName("Включение трейла", Constants.Ru)]
        [Description("На каком уровне начинать двигать стоп")]
        [HelperDescription("Where to start actual trailing", Constants.En)]
        [HandlerParameter(true, "0.5", Min = "0.1", Max = "3", Step = "0.1", Name = "Trail Enable")]
        public double TrailEnable { get; set; }

        /// <summary>
        /// \~english Trail loss
        /// \~russian Сколько должна пройти цена, чтобы стоп передвинулся
        /// </summary>
        [HelperName("Trail loss", Constants.En)]
        [HelperName("Подтягивать стоп", Constants.Ru)]
        [Description("Сколько должна пройти цена, чтобы стоп передвинулся")]
        [HelperDescription("Trail loss", Constants.En)]
        [HandlerParameter(true, "0.5", Min = "0.1", Max = "3", Step = "0.1", Name = "Trail Loss")]
        public double TrailLoss { get; set; }

        /// <summary>
        /// \~english Use calculated price
        /// \~russian Использовать расчетную цену
        /// </summary>
        [HelperName("Use calc price", Constants.En)]
        [HelperName("Исп. расчетную цену", Constants.Ru)]
        [Description("Использовать расчетную цену")]
        [HelperDescription("Use calculated price", Constants.En)]
        [HandlerParameter(true, "false", NotOptimized = true, Name = "Use Calc Price")]
        public bool UseCalcPrice { get; set; }

        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
            {
                return 0;
            }
            double stop;
            var curProfit = pos.OpenMFE(barNum);
            var entryPrice = pos.EntryPrice;
            if (UseCalcPrice)
            {
                var ep2 = CalcEntryPrice.EntryPrice(pos, barNum);
                var diff = (entryPrice - ep2) * (pos.IsLong ? 1 : -1);
                curProfit += diff;
                entryPrice = ep2;
            }
            curProfit *= 100 / entryPrice * pos.Security.Margin;

            if (curProfit > TrailEnable)
            {
                double shift = (curProfit - TrailLoss) / 100;
                stop = entryPrice * (1 + (pos.IsLong ? shift : -shift));
            }
            else
            {
                double shift = (0 - StopLoss) / 100;
                stop = entryPrice * (1 + (pos.IsLong ? shift : -shift));
            }
            var lastStop = pos.GetStop(barNum);
            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (lastStop == 0)
            // ReSharper restore CompareOfFloatsByEqualityOperator
            {
                return stop;
            }
            return pos.IsLong ? Math.Max(stop, lastStop) : Math.Min(stop, lastStop);
        }
    }

    // TODO: по идее, на русском языке это называется 'Следящий стоп'
    //[HandlerName("Trail Stop Abs")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Trail stop absolute", Language = Constants.En)]
    [HelperName("Трейл Стоп Абс.", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Следящий стоп, значения ведения задаются в абсолютных величинах. У блока 3 параметра, которые описывают два режима работы: " +
        "[br]1й режим - Стоп - лосс описывается параметром 'Стоп лосс', который задает максимальное падение (для шорта - рост) от цены входа, которые мы готовы принять. Это падение задается числом. " +
        "[br]Во 2й режим блок переходит, если было зафиксировано увеличение цены (в случае шорта уменьшение) на величину заданную параметром 'Вкл. трейл'. " +
        "[br]Важно! Переход в режим ведения происходит только в случае превышения ценой заданного уровня! В случае касания цены без превышения ведение не включается. В этом случае уровень поддержки на следующем баре вычисляется, как MFE(этого бара) минус параметр 'трейл лосс'. Иными словами, начинается 'ведение' прибыли. " +
        "[br]Параметр 'Исп. расч. цену' позволяет вести расчет стопа от расчетной цены открытия. Для режима лаборатории это цена открытия, следующего за сигналом бара. Для режима реальных торгов это цена, по которой была выставлена заявка на открытие позиции. Отключение данного параметра приводит к использованию реальной цены открытия полученной в ходе торгов. " +
        "[br]Расчетную цену невозможно рассчитать, если включена опция 'По рынку с фикс ценой' и задано проскальзывание отличное от 0.")]
    [HelperDescription("The trailing stop, the values are given in absolute numbers. The block has 3 parameters describing 2 modes of functioning. " +
        "[br]The 1st mode: Stop-loss is described as stop-loss, which sets the maximum fall (in case of short - growth) of the price you can accept. This fall is set in numbers. " +
        "[br]The 2nd mode is selected if the price grows (in case of short the price falls) according to the value set in the parameter 'Enable Trail'. In other word he profit is being trailed. " +
        "[br]The parameter 'Use the Calculated Price' allows to calculate Stop from the calculated opening price. In the Laboratory mode this is the opening price of the bar following the signal. In the real trade mode this is the price of position opening. Disabling this parameter causes using the real price received during the trading session. " +
        "[br]The calculated price cannot be calculated if the box 'By Market at Fixed Price' is selected and slippage higher than 0 is set.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/IndexTrade.tscript", Name = "Пример по ряду приемов проектирования и 2ум источникам данных")]
    [HelperLink(@"http://www.tslab.ru/files/script/IndexTrade.tscript", Name = "Example of script with 2 sources", Language = Constants.En)]
    [HelperLink(@"http://forum.tslab.ru/ubb/ubbthreads.php?ubb=showflat&Number=31773#Post31773", Name = "Обсуждение на форуме TSLab")]
    [HelperLink(@"http://forum.tslab.ru/ubb/ubbthreads.php?ubb=download&Number=8063&filename=Пример_Трейл.xml", Name = "Пример скрипта с трейлингом")]
    #endregion Атрибуты с описанием и ссылками
    public class TrailStopAbs : IPosition2Double
    {
        /// <summary>
        /// \~english Initial stop loss
        /// \~russian Начальный уровень стоплосса
        /// </summary>
        [HelperName("Stop loss", Constants.En)]
        [HelperName("Стоплосс", Constants.Ru)]
        [Description("Начальный уровень стоплосса")]
        [HelperDescription("Initial stop loss", Constants.En)]
        [HandlerParameter(true, "150", Min = "10", Max = "500", Step = "5", Name = "Stop Loss")]
        public double StopLoss { get; set; }

        /// <summary>
        /// \~english Where to start actual trailing
        /// \~russian На каком уровне начинать двигать стоп
        /// </summary>
        [HelperName("Trail enable", Constants.En)]
        [HelperName("Включение трейла", Constants.Ru)]
        [Description("На каком уровне начинать двигать стоп")]
        [HelperDescription("Where to start actual trailing", Constants.En)]
        [HandlerParameter(true, "50", Min = "10", Max = "500", Step = "5", Name = "Trail Enable")]
        public double TrailEnable { get; set; }

        /// <summary>
        /// \~english Trail loss
        /// \~russian Сколько должна пройти цена, чтобы стоп передвинулся
        /// </summary>
        [HelperName("Trail loss", Constants.En)]
        [HelperName("Подтягивать стоп", Constants.Ru)]
        [Description("Сколько должна пройти цена, чтобы стоп передвинулся")]
        [HelperDescription("Trail loss", Constants.En)]
        [HandlerParameter(true, "50", Min = "10", Max = "500", Step = "5", Name = "Trail Loss")]
        public double TrailLoss { get; set; }

        /// <summary>
        /// \~english Use calculated price
        /// \~russian Использовать расчетную цену
        /// </summary>
        [HelperName("Use calc price", Constants.En)]
        [HelperName("Исп. расчетную цену", Constants.Ru)]
        [Description("Использовать расчетную цену")]
        [HelperDescription("Use calculated price", Constants.En)]
        [HandlerParameter(true, "false", NotOptimized = true, Name = "Use Calc Price")]
        public bool UseCalcPrice { get; set; }

        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
            {
                return 0;
            }
            double stop;
            var curProfit = pos.OpenMFE(barNum);
            var entryPrice = pos.EntryPrice;
            if (UseCalcPrice)
            {
                var ep1 = entryPrice;
                var ep2 = CalcEntryPrice.EntryPrice(pos, barNum);
                var diff = (ep1 - ep2) * (pos.IsLong ? 1 : -1);
                curProfit += diff;
                entryPrice = ep2;
            }
            if (curProfit > TrailEnable)
            {
                double shift = curProfit - TrailLoss;
                stop = entryPrice + (pos.IsLong ? shift : -shift);
            }
            else
            {
                double shift = -StopLoss;
                stop = entryPrice + (pos.IsLong ? shift : -shift);
            }
            var lastStop = pos.GetStop(barNum);
            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (lastStop == 0)
            // ReSharper restore CompareOfFloatsByEqualityOperator
            {
                return stop;
            }
            return pos.IsLong ? Math.Max(stop, lastStop) : Math.Min(stop, lastStop);
        }
    }

    //[HandlerName("Has Position Active")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("There is active position", Language = Constants.En)]
    [HelperName("Есть активная позиция", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Логическая функция проверяющая наличие активной позиции.")]
    [HelperDescription("The Boolean function verifying that there is an active position.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Alligator_tradable.tscript", Name = "Пример по индикатору Alligator")]
    [HelperLink(@"http://www.tslab.ru/files/script/Alligator_tradable.tscript", Name = "Example of Alligator", Language = Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Momentum.tscript", Name = "Пример по индикатору Momentum")]
    [HelperLink(@"http://www.tslab.ru/files/script/Momentum.tscript", Name = "Example of Momentum", Language = Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class HasPositionActive : IBar2BoolHandler
    {
        public bool Execute(ISecurity source, int barNum)
        {
            var res = source.Positions.GetLastPositionActive(barNum) != null;
            if (res || !source.IsRealtime || source.SimulatePositionOrdering)
                return res;
            var lp = source.Positions.GetLastPosition(source.Positions.BarsCount);
            return lp != null && (lp.IsActive || lp.ExitBarNum > barNum);
        }
    }

    //[HandlerName("Has Long Position Active")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("There is active long position", Language = Constants.En)]
    [HelperName("Есть активная длинная поз.", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Логическая функция проверяющая наличие активной длинной позиции.")]
    [HelperDescription("The Boolean function verifying if there is an active long position.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo_1pos.tscript", Name = "Пример модифицированной стратегии Hi - Low")]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo_1pos.tscript", Name = "Example of modified strategy Hi - Low", Language = Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class HasLongPositionActive : IBar2BoolHandler
    {
        public bool Execute(ISecurity source, int barNum)
        {
            var res = source.Positions.GetLastLongPositionActive(barNum) != null;
            if (res || !source.IsRealtime || source.SimulatePositionOrdering)
                return res;
            var lp = source.Positions.GetLastLongPositionActive(source.Positions.BarsCount);
            if (lp != null)
                return true;
            lp = source.Positions.GetLastLongPositionClosed(source.Positions.BarsCount);
            return lp != null && lp.ExitBarNum > barNum;
        }
    }

    //[HandlerName("Has Short Position Active")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("There is active short position", Language = Constants.En)]
    [HelperName("Есть активная короткая поз.", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Логическая функция проверяющая наличие активной короткой позиции.")]
    [HelperDescription("The Boolean function verifying that there is an active short position.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo_1pos.tscript", Name = "Пример модифицированной стратегии Hi - Low")]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo_1pos.tscript", Name = "Example of modified strategy Hi - Low", Language = Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class HasShortPositionActive : IBar2BoolHandler
    {
        public bool Execute(ISecurity source, int barNum)
        {
            var res = source.Positions.GetLastShortPositionActive(barNum) != null;
            if (res || !source.IsRealtime || source.SimulatePositionOrdering)
                return res;
            var lp = source.Positions.GetLastShortPositionActive(source.Positions.BarsCount);
            if (lp != null)
                return true;
            lp = source.Positions.GetLastShortPositionClosed(source.Positions.BarsCount);
            return lp != null && lp.ExitBarNum > barNum;
        }
    }

    //[HandlerName("Last is Closed and was Long")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Last position has been closed and it was long", Language = Constants.En)]
    [HelperName("Посл. поз. закрыта и длинная", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Последняя позиция была закрыта и она длинная.")]
    [HelperDescription("The latest position has been closed and it was long.", Constants.En)]
    public class LastClosedIsLong : IBar2BoolHandler
    {
        public bool Execute(ISecurity source, int barNum)
        {
            var closed = source.Positions.GetLastPosition(barNum);
            return closed != null && closed.IsLong && !closed.IsActiveForBar(barNum);
        }
    }

    //[HandlerName("Last is Closed and was Short")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Last position has been closed and it was short", Language = Constants.En)]
    [HelperName("Посл. поз. закрыта и короткая", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Последняя позиция была закрыта и она короткая.")]
    [HelperDescription("The latest position has been closed and it was short.", Constants.En)]
    public class LastClosedIsShort : IBar2BoolHandler
    {
        public bool Execute(ISecurity source, int barNum)
        {
            var closed = source.Positions.GetLastPosition(barNum);
            return closed != null && closed.IsShort && !closed.IsActiveForBar(barNum);
        }
    }

    //[HandlerName("Last Closed Is Long")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Last closed position was long", Language = Constants.En)]
    [HelperName("Посл. закр. поз. была длинной", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Логическая функция проверяющая, что последняя закрытая позиция была длинной.")]
    [HelperDescription("The Boolean function verifying that the latest closed position was long.", Constants.En)]
    public class LastClosedIsLong2 : IBar2BoolHandler
    {
        public bool Execute(ISecurity source, int barNum)
        {
            var closed = source.Positions.GetLastPositionClosed(barNum);
            return closed != null && closed.IsLong;
        }
    }

    //[HandlerName("Last Closed Is Short")]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Last closed position was short", Language = Constants.En)]
    [HelperName("Посл. закр. поз. была короткой", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Логическая функция проверяющая, что последняя закрытая позиция была короткой.")]
    [HelperDescription("The Boolean function verifying that the latest closed position was short.", Constants.En)]
    public class LastClosedIsShort2 : IBar2BoolHandler
    {
        public bool Execute(ISecurity source, int barNum)
        {
            var closed = source.Positions.GetLastPositionClosed(barNum);
            return closed != null && closed.IsShort;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Position Entry Bar Number", Language = Constants.En)]
    [HelperName("Номер бара входа в позицию", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает порядковый номер бара в момент входа в позицию.")]
    [HelperDescription("Shows a complex position entry bar number.", Constants.En)]
    public sealed class PositionEntryBarNumber : IPosition2Double
    {
        public double Execute(IPosition position, int barNum)
        {
            return position?.EntryBarNum ?? -1;
        }
    }

    [HandlerInvisible]
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Position Exit Bar Number", Language = Constants.En)]
    [HelperName("Номер бара выхода из позиции", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает порядковый номер бара в момент выхода из позиции.")]
    [HelperDescription("Shows a complex position exit bar number.", Constants.En)]
    public sealed class PositionExitBarNumber : IPosition2Double
    {
        public double Execute(IPosition position, int barNum)
        {
            return position?.IsActive == false ? position.ExitBarNum : -1;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Changed Position Entry Bar Number", Language = Constants.En)]
    [HelperName("Номер бара входа в измененную позицию", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает номер бара входа в измененную сложносоставную позицию.")]
    [HelperDescription("Shows entry bar number of a changed complex position.", Constants.En)]
    public sealed class PositionChangeEntryBarNumber : IPosition2Double
    {
        public double Execute(IPosition position, int barNum)
        {
            if (position == null)
                return -1;

            var changeInfos = position.ChangeInfos;
            for (var i = changeInfos.Count - 1; i >= 0; i--)
            {
                var changeInfo = changeInfos[i];
                if (changeInfo.EntryBarNum >= 0)
                    return changeInfo.EntryBarNum;
            }
            return -1;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Changed Position Exit Bar Number", Language = Constants.En)]
    [HelperName("Номер бара выхода из измененной позиции", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает номер бара выхода из измененной сложносоставной позиции.")]
    [HelperDescription("Shows exit bar number of a changed complex position.", Constants.En)]
    public sealed class PositionChangeExitBarNumber : IPosition2Double
    {
        public double Execute(IPosition position, int barNum)
        {
            if (position == null)
                return -1;

            var changeInfos = position.ChangeInfos;
            for (var i = changeInfos.Count - 1; i >= 0; i--)
            {
                var changeInfo = changeInfos[i];
                if (changeInfo.ExitBarNum >= 0)
                    return changeInfo.ExitBarNum;
            }
            return -1;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Changed Position Entry Price", Language = Constants.En)]
    [HelperName("Цена входа в измененную позицию", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает цену последнего входа в сложносоставную позицию. Если определить цену нельзя, возвращает нуль.")]
    [HelperDescription("Shows last entry price of a changed complex position. If none handler returns zero.", Constants.En)]
    public sealed class PositionChangeEntryPrice : IPosition2Double
    {
        public double Execute(IPosition position, int barNum)
        {
            if (position == null)
                return 0;

            var changeInfos = position.ChangeInfos;
            for (var i = changeInfos.Count - 1; i >= 0; i--)
            {
                var changeInfo = changeInfos[i];
                if (changeInfo.EntryBarNum >= 0)
                    return changeInfo.EntryPrice;
            }
            return 0;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Changed Position Exit Price", Language = Constants.En)]
    [HelperName("Цена выхода из измененной позиции", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает цену последнего выхода из сложносоставной позиции.")]
    [HelperDescription("Shows last exit price of a changed complex position. If none handler returns zero.", Constants.En)]
    public sealed class PositionChangeExitPrice : IPosition2Double
    {
        public double Execute(IPosition position, int barNum)
        {
            if (position == null)
                return 0;

            var changeInfos = position.ChangeInfos;
            for (var i = changeInfos.Count - 1; i >= 0; i--)
            {
                var changeInfo = changeInfos[i];
                if (changeInfo.ExitBarNum >= 0)
                    return changeInfo.ExitPrice;
            }
            return 0;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Position Is Virtual", Language = Constants.En)]
    [HelperName("Виртуальная позиция?", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Возвращает значение Истина, если позиция на входе является виртуальной.")]
    [HelperDescription("A handler returns TRUE if input position is virtual.", Constants.En)]
    public sealed class PositionIsVirtualHandler : IPosition2Boolean
    {
        public bool Execute(IPosition pos, int barNum)
        {
            return pos?.IsVirtual ?? false;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Position Is Virtually Closed", Language = Constants.En)]
    [HelperName("Виртуальный выход из позиции?", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Позиция закрыта виртуально (рассчетно, сделок еще не было)?")]
    [HelperDescription("Is position has virtual close (calculated only)?", Constants.En)]
    public sealed class PositionIsVirtualClosedHandler : IPosition2Boolean
    {
        public bool Execute(IPosition pos, int barNum)
        {
            return pos?.IsVirtualClosed ?? false;
        }
    }

    // TODO: '(кривая) прибыль' звучит крайне странно. Есть устоявшийся термин 'кривая эквити'. Нельзя ли придумать название получше?
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Equity Drawdown", Language = Constants.En)]
    [HelperName("Просадка (кривой) прибыли", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает отклонение кривой прибыли от медианы.")]
    [HelperDescription("Shows deviation of the income curved line from the median.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Alligator_tradable.xml", "Пример по индикатору Alligator", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/Alligator_tradable.xml", "Example of Alligator", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class Median : IBar2ValueDoubleHandler
    {
        private int m_lastBarNum = -1;
        private double m_lastProfit;
        private double m_lastMaxProfit;

        public double Execute(ISecurity source, int barNum)
        {
            if (barNum - m_lastBarNum == 1)
            {
                m_lastProfit = source.GetProfit(barNum);
                if (m_lastMaxProfit < m_lastProfit)
                    m_lastMaxProfit = m_lastProfit;
            }
            else if (m_lastBarNum != barNum)
            {
                double profit = 0, maxProfit = double.NegativeInfinity;
                for (var i = 0; i <= barNum; i++)
                {
                    profit = source.GetProfit(i);
                    if (maxProfit < profit)
                        maxProfit = profit;
                }
                m_lastProfit = profit;
                m_lastMaxProfit = maxProfit;
            }
            m_lastBarNum = barNum;
            return m_lastMaxProfit - m_lastProfit;
        }
    }
}
// ReSharper restore MemberCanBePrivate.Global
// ReSharper restore UnusedAutoPropertyAccessor.Global
// ReSharper restore UnusedMember.Global
