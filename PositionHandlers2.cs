using System;
using System.ComponentModel;
using System.Linq;
using TSLab.Script.Handlers.Options;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
namespace TSLab.Script.Handlers
{
    /// <summary>
    /// Небольшой базовый класс для кеширования списка закрытых позиций, чтобы не делать их выборку и сортировку на каждом баре.
    /// Формирование списка позиций выполняется только если последней закрытой позиции еще не было или если она изменилась.
    /// </summary>
    public abstract class ClosedPositionCache
    {
        private IPosition[] m_lastCache;
        private IPosition m_lastClosed;

        /// <summary>
        /// Метод формирует список закрытых позиций на момент бара barNum и сортирует их в порядке убывания дат закрытия
        /// (по параметру pos.ExitBar.Date, если быть точным).
        /// </summary>
        /// <param name="source">инструмент</param>
        /// <param name="barNum">номер бара для которого должен быть сформирован список</param>
        /// <returns>список закрытых позиций, отсортированный по убыванию дат закрытия</returns>
        protected IPosition[] GetClosedPositions(ISecurity source, int barNum)
        {
            var last = source.Positions.GetLastPositionClosed(barNum);
            if (m_lastCache == null || last != m_lastClosed)
            {
                m_lastClosed = last;
                m_lastCache = source.Positions.GetClosedForBar(barNum).OrderByDescending(pos => pos.ExitBar.Date).ToArray();
            }
            return m_lastCache;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Losses running", Language = Constants.En)]
    [HelperName("Убытков подряд", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Подсчет количества убыточных позиций подряд.")]
    [HelperDescription("Calculates the number of consecutive loss positions.", Constants.En)]
    public sealed class DrowdownCount : ClosedPositionCache, IBar2ValueDoubleHandler
    {
        public double Execute(ISecurity source, int barNum)
        {
            return GetClosedPositions(source, barNum).TakeWhile(pos => pos.Profit() <= 0).Count();
        }
    }

    // TODO: удачно ли для слова '2 убытка ПОДРЯД' использовать термин 'successively'?
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("2 losses successively", Language = Constants.En)]
    [HelperName("2 убытка подряд", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Наличие двух или более убыточных позиций подряд.")]
    [HelperDescription("2 or more consecutive loss positions.", Constants.En)]
    public sealed class HasTwoLoss : ClosedPositionCache, IBar2BoolHandler
    {
        public bool Execute(ISecurity source, int barNum)
        {
            var list = GetClosedPositions(source, barNum);
            if (list.Length < 2)
            {
                return false;
            }
            return list[0].Profit() < 0 && list[1].Profit() < 0;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Last closed position was unprofitable", Language = Constants.En)]
    [HelperName("Последняя закрытая позиция убыточна", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Проверяет наличие убытка по закрытой позиции.")]
    [HelperDescription("Check if a closed position was unprofitable.", Constants.En)]
    public sealed class IsItLossAtLastPosition : ClosedPositionCache, IBar2BoolHandler
    {
        public bool Execute(ISecurity source, int barNum)
        {
            var positions = GetClosedPositions(source, barNum);
            return positions.Length > 0 && positions[0].Profit() < 0;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    //[HandlerName("Last Exit has this name")]
    [HelperName("Last exit has such name", Language = Constants.En)]
    [HelperName("Последний выход имеет такое имя", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("В параметре задается имя блока закрытия позиции. Значение данного блока верно, если последним закрытием по финансовому инструменту было закрытие с заданным именем.")]
    [HelperDescription("The parameter allows to give name the block Position Close. The value of this block is true if the last close for the instrument had this name.", Constants.En)]
    public sealed class PosActiveNameExit : IBar2BoolHandler
    {
        /// <summary>
        /// \~english Close signal name
        /// \~russian Имя сигнала закрытия
        /// </summary>
        [HelperName("Name", Constants.En)]
        [HelperName("Имя", Constants.Ru)]
        [Description("Имя сигнала закрытия")]
        [HelperDescription("Close signal name", Constants.En)]
        [HandlerParameter(true, NotOptimized = true)]
        public string Name { get; set; }

        #region IBar2BoolHandler Members

        public bool Execute(ISecurity source, int barNum)
        {
            var pos = source.Positions.GetLastPositionClosed(barNum);
            var names = pos?.ExitSignalName.Split('$');
            return names != null && names.Contains(Name);
        }

        #endregion
    }

    [HandlerCategory(HandlerCategories.Position)]
    //[HandlerName("Last Closed Position Time")]
    [HelperName("Time of last closed position", Language = Constants.En)]
    [HelperName("Время последней закрытой позиции", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Время последней закрытой позиции (число в формате ЧЧММСС).")]
    [HelperDescription("The time of the latest closed position (a number with format HHMMSS).", Constants.En)]
    public sealed class LastClosedPositionTime : IBar2ValueDoubleHandler
    {
        public double Execute(ISecurity source, int barNum)
        {
            var pos = source.Positions.GetLastPositionClosed(barNum);
            var dt = pos == null ? DateTime.MinValue : pos.EntryBar.Date;
            return dt.Hour * 10000.0 + dt.Minute * 100.0 + dt.Second;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    //[HandlerName("Last Closed Position Date")]
    [HelperName("Date of last closed position", Language = Constants.En)]
    [HelperName("Дата последней закрытой позиции", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Дата последней закрытой позиции (число в формате ГГММДД).")]
    [HelperDescription("The date of the latest closed position (a number with format YYMMDD).", Constants.En)]
    public sealed class LastClosedPositionDate : IBar2ValueDoubleHandler
    {
        public double Execute(ISecurity source, int barNum)
        {
            var pos = source.Positions.GetLastPositionClosed(barNum);
            var dt = pos == null ? DateTime.MinValue : pos.EntryBar.Date;
            return (dt.Year % 100) * 10000.0 + dt.Month * 100.0 + dt.Day;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    //[HandlerName("Last Closed Position Exit Time")]
    [HelperName("Exit time of last closed position", Language = Constants.En)]
    [HelperName("Время выхода последней закрытой позиции", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Время выхода последней закрытой позиции (число в формате ЧЧММСС).")]
    [HelperDescription("The exit time of the latest closed position (a number with format HHMMSS).", Constants.En)]
    public sealed class LastClosedPositionExitTime : IBar2ValueDoubleHandler
    {
        public double Execute(ISecurity source, int barNum)
        {
            var pos = source.Positions.GetLastPositionClosed(barNum);
            var dt = pos == null ? DateTime.MinValue : pos.ExitBar.Date;
            return dt.Hour * 10000.0 + dt.Minute * 100.0 + dt.Second;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    //[HandlerName("Last Closed Position Exit Date")]
    [HelperName("Exit date of last closed position", Language = Constants.En)]
    [HelperName("Дата выхода последней закрытой позиции", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Время выхода последней закрытой позиции (число в формате ГГММДД).")]
    [HelperDescription("The exit date of the latest closed position (a number with format YYMMDD).", Constants.En)]
    public sealed class LastClosedPositionExitDate : IBar2ValueDoubleHandler
    {
        public double Execute(ISecurity source, int barNum)
        {
            var pos = source.Positions.GetLastPositionClosed(barNum);
            var dt = pos == null ? DateTime.MinValue : pos.ExitBar.Date;
            return (dt.Year % 100) * 10000.0 + dt.Month * 100.0 + dt.Day;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    //[HandlerName("Last Closed Position Exit Bar Number")]
    [HelperName("Exit bar number of last closed position", Language = Constants.En)]
    [HelperName("Номер бара выхода из последней закрытой позиции", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Номер бара выхода из последней закрытой позиции.")]
    [HelperDescription("The exit bar number of the latest closed position.", Constants.En)]
    public sealed class LastClosedPositionExitBarNumber : IBar2ValueDoubleHandler
    {
        public double Execute(ISecurity source, int barNum)
        {
            var pos = source.Positions.GetLastPositionClosed(barNum);
            return pos?.ExitBarNum ?? -1;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    //[HandlerName("Last Closed Named Position Exit Time")]
    [HelperName("Exit time of last closed named position", Language = Constants.En)]
    [HelperName("Время выхода последней закрытой позиции по имени", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Время выхода последней закрытой позиции по имени (число в формате ЧЧММСС).")]
    [HelperDescription("The exit time of the latest closed named position (a number with format HHMMSS).", Constants.En)]
    public sealed class LastClosedNamePositionExitTime : IBar2ValueDoubleHandler
    {
        /// <summary>
        /// \~english Close signal name
        /// \~russian Имя сигнала закрытия
        /// </summary>
        [HelperName("Name", Constants.En)]
        [HelperName("Имя", Constants.Ru)]
        [Description("Имя сигнала закрытия")]
        [HelperDescription("Close signal name", Constants.En)]
        [HandlerParameter(true, NotOptimized = true)]
        public string Name { get; set; }

        public double Execute(ISecurity source, int barNum)
        {
            var pos = source.Positions.GetLastClosedForSignal(Name, barNum);
            if (pos != null && (pos.IsActive || pos.ExitBarNum > barNum))
            {
                pos = null;
            }
            var dt = pos == null ? DateTime.MinValue : pos.ExitBar.Date;
            return dt.Hour * 10000.0 + dt.Minute * 100.0 + dt.Second;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    //[HandlerName("Last Closed Named Position Exit Date")]
    [HelperName("Exit date of last closed named position", Language = Constants.En)]
    [HelperName("Дата выхода последней закрытой позиции по имени", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Дата выхода последней закрытой позиции по имени (число в формате ГГММДД).")]
    [HelperDescription("The exit date of the latest closed named position (a number with format YYMMDD).", Constants.En)]
    public sealed class LastClosedNamePositionExitDate : IBar2ValueDoubleHandler
    {
        /// <summary>
        /// \~english Close signal name
        /// \~russian Имя сигнала закрытия
        /// </summary>
        [HelperName("Name", Constants.En)]
        [HelperName("Имя", Constants.Ru)]
        [Description("Имя сигнала закрытия")]
        [HelperDescription("Close signal name", Constants.En)]
        [HandlerParameter(true, NotOptimized = true)]
        public string Name { get; set; }

        public double Execute(ISecurity source, int barNum)
        {
            var pos = source.Positions.GetLastClosedForSignal(Name, barNum);
            if (pos != null && (pos.IsActive || pos.ExitBarNum > barNum))
            {
                pos = null;
            }
            var dt = pos == null ? DateTime.MinValue : pos.ExitBar.Date;
            return (dt.Year % 100) * 10000.0 + dt.Month * 100.0 + dt.Day;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    //[HandlerName("Days In Position")]
    [HelperName("Days in position", Language = Constants.En)]
    [HelperName("Дней в позиции", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Количество дней в позиции с момента входа.")]
    [HelperDescription("The number of days in position.", Constants.En)]
    public sealed class DaysInPosition : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
            {
                return 0;
            }
            var dt = pos.EntryBar.Date;
            var curDt = pos.Security.Bars[barNum].Date;
            return (curDt - dt).TotalDays;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    //[HandlerName("Minutes In Position")]
    [HelperName("Minutes in position", Language = Constants.En)]
    [HelperName("Минут в позиции", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION, Name = Constants.PositionSource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Количество минут в позиции с момента входа.")]
    [HelperDescription("The number of minutes in position.", Constants.En)]
    public sealed class MinutesInPosition : IPosition2Double
    {
        public double Execute(IPosition pos, int barNum)
        {
            if (pos == null)
            {
                return 0;
            }
            var dt = pos.EntryBar.Date;
            var curDt = pos.Security.Bars[barNum].Date;
            return (curDt - dt).TotalMinutes;
        }
    }

    [HandlerCategory(HandlerCategories.Position)]
    //[HandlerName("Last Exit Price")]
    [HelperName("Last exit price", Language = Constants.En)]
    [HelperName("Цена последнего выхода", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Цена последнего выхода.")]
    [HelperDescription("The price of the latest exit.", Constants.En)]
    public sealed class LastExitPrice : IBar2ValueDoubleHandler
    {
        public double Execute(ISecurity source, int barNum)
        {
            var pos = source.Positions.GetLastPositionClosed(barNum);
            return pos == null ? 0 : pos.ExitPrice;
        }
    }
}
