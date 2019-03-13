using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Script.Realtime;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english List of own trades
    /// \~russian Таблица своих трейдов в одной опционной серии
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Single Series Position List", Language = Constants.En)]
    [HelperName("Список трейдов (одна серия)", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Таблица своих трейдов в одной опционной серии")]
    [HelperDescription("A list of all user's trades in one option series", Constants.En)]
    public class SingleSeriesPositionList : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const int ShiftMs = 3;
        private const string DefaultMaxPositions = "100";
        private const string DefaultTooltipFormat = "0";

        private bool m_countFutures = false;
        private string m_tooltipFormat = DefaultTooltipFormat;
        private int m_maxPositions = Int32.Parse(DefaultMaxPositions);
        private PositionGridDisplayMode m_displayMode = PositionGridDisplayMode.Symbol;

        private static readonly DateTimeUtils.DescendDateTimeComparer s_comparer = new DateTimeUtils.DescendDateTimeComparer();

        #region Parameters
        /// <summary>
        /// \~english Position property to be displayed
        /// \~russian Какое свойство позиции надо показать?
        /// </summary>
        [HelperName("Display Property", Constants.En)]
        [HelperName("Что показывать", Constants.Ru)]
        [Description("Какое свойство позиции надо показать?")]
        [HelperDescription("Position property to be displayed", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Symbol")]
        public PositionGridDisplayMode DisplayMode
        {
            get { return m_displayMode; }
            set { m_displayMode = value; }
        }

        /// <summary>
        /// \~english Count base asset
        /// \~russian Подсчитывать ли количество БА?
        /// </summary>
        [HelperName("Count Futures", Constants.En)]
        [HelperName("Учитывать БА", Constants.Ru)]
        [Description("Подсчитывать ли количество БА?")]
        [HelperDescription("Count base asset", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool CountFutures
        {
            get { return m_countFutures; }
            set { m_countFutures = value; }
        }

        /// <summary>
        /// \~english Limit number of positions to show
        /// \~russian Максимальное отображаемое число позиций
        /// </summary>
        [HelperName("Max Positions", Constants.En)]
        [HelperName("Кол-во позиций", Constants.Ru)]
        [Description("Максимальное отображаемое число позиций")]
        [HelperDescription("Limit number of positions to show", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Name = "Max Positions",
            Default = DefaultMaxPositions, Min = "1", Max = "1000000", Step = "1")]
        public int MaxPositions
        {
            get { return m_maxPositions; }
            set { m_maxPositions = value; }
        }

        /// <summary>
        /// \~english Tooltip format (i.e. '0.00', '0.0##' etc)
        /// \~russian Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.
        /// </summary>
        [HelperName("Tooltip Format", Constants.En)]
        [HelperName("Формат подсказки", Constants.Ru)]
        [Description("Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.")]
        [HelperDescription("Tooltip format (i.e. '0.00', '0.0##' etc)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = DefaultTooltipFormat)]
        public string TooltipFormat
        {
            get { return m_tooltipFormat; }
            set
            {
                if (!String.IsNullOrWhiteSpace(value))
                {
                    try
                    {
                        string yStr = Math.PI.ToString(value);
                        m_tooltipFormat = value;
                    }
                    catch
                    {
                        m_context.Log("Tooltip format error. I'll keep old one: " + m_tooltipFormat, MessageType.Warning, true);
                    }
                }
            }
        }
        #endregion Parameters

        public InteractiveSeries Execute(IOptionSeries optSer, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return Constants.EmptySeries;

            int lastBarIndex = optSer.UnderlyingAsset.Bars.Count - 1;
            DateTime now = optSer.UnderlyingAsset.Bars[Math.Min(barNum, lastBarIndex)].Date;
            bool wasInitialized = HandlerInitializedToday(now);

            IOptionStrike[] options = optSer.GetStrikes().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);

            //if (Context.Runtime.IsAgentMode)
            //{
            // PROD-6089 - Если мы в режиме агента, значит все инструменты уже ISecurityRt
            //    SortedList<DateTime, IOrder> sortedOrders = new SortedList<DateTime, IOrder>(s_comparer);
            //}

            SortedList<DateTime, IPosition> sortedPos = new SortedList<DateTime, IPosition>(s_comparer);
            if (m_countFutures)
            {
                ReadOnlyCollection<IPosition> futPositions = posMan.GetActiveForBar(optSer.UnderlyingAsset);
                if (futPositions.Count > 0)
                {
                    for (int j = 0; j < futPositions.Count; j++)
                    {
                        IPosition pos = futPositions[j];
                        AddSafely(sortedPos, pos);
                    }
                }
            }

            for (int m = 0; m < options.Length; m++)
            {
                IOptionStrike optStrike = options[m];
                ReadOnlyCollection<IPosition> optPositions = posMan.GetActiveForBar(optStrike.Security);
                if (optPositions.Count > 0)
                {
                    for (int j = 0; j < optPositions.Count; j++)
                    {
                        IPosition pos = optPositions[j];
                        AddSafely(sortedPos, pos);
                    }
                }
            }

            int counter = 0;
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (var node in sortedPos)
            {
                InteractivePointActive ip = new InteractivePointActive();
                ip.IsActive = true;
                //ip.DragableMode = DragableMode.None;
                ip.DateTime = node.Key;
                ip.ValueX = 0; // все одинаковые 'страйки' имеют
                ip.ValueY = PrepareValue(node, barNum, m_displayMode);
                ip.Tooltip = PrepareTooltip(node, barNum, m_displayMode);

                controlPoints.Add(new InteractiveObject(ip));

                counter++;
                if (counter >= m_maxPositions)
                    break;
            }

            InteractiveSeries res = new InteractiveSeries(); // Здесь правильно делать new
            // Задаю свойство для отображения
            switch (m_displayMode)
            {
                case PositionGridDisplayMode.Px:
                case PositionGridDisplayMode.Qty:
                    res.DisplayProperty.Name = nameof(IInteractivePointLight.ValueY);
                    break;

                default:
                    res.DisplayProperty.Name = nameof(InteractivePointActive.Tooltip);
                    break;
            }
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            SetHandlerInitialized(now);

            return res;
        }

        private static double PrepareValue(KeyValuePair<DateTime, IPosition> node, int curBar, PositionGridDisplayMode mode)
        {
            double res;
            IPosition pos = node.Value;
            switch (mode)
            {
                //case PositionGridDisplayMode.Iv:
                //    res = "Not implemented";
                //    break;

                case PositionGridDisplayMode.Px:
                    res = pos.GetBalancePrice(curBar);
                    break;

                case PositionGridDisplayMode.Qty:
                    int sign = pos.IsLong ? 1 : -1;
                    res = sign * Math.Abs(pos.Shares);
                    break;

                //case PositionGridDisplayMode.Symbol:
                //    res = pos.Security.Symbol;
                //    break;

                default:
                    res = Constants.NaN;
                    break;
            }

            return res;
        }

        private static string PrepareTooltip(KeyValuePair<DateTime, IPosition> node, int curBar, PositionGridDisplayMode mode)
        {
            string res;
            IPosition pos = node.Value;
            switch (mode)
            {
                case PositionGridDisplayMode.Iv:
                    res = "-"; // Как у Алексея сделано
                    if (pos.Security.SecurityDescription.IsOption)
                    {
                        res = "na";
                    }
                    break;

                case PositionGridDisplayMode.Px:
                    res = pos.GetBalancePrice(curBar).ToString(CultureInfo.InvariantCulture);
                    break;

                case PositionGridDisplayMode.Dir:
                    res = pos.IsLong ? "Long" : "Short";
                    break;

                case PositionGridDisplayMode.Qty:
                    res = pos.Shares.ToString(CultureInfo.InvariantCulture);
                    break;

                case PositionGridDisplayMode.Symbol:
                    res = pos.Security.Symbol;
                    break;

                case PositionGridDisplayMode.IsVirtual:
                    res = pos.IsVirtual.ToString(CultureInfo.InvariantCulture);
                    break;

                default:
                    res = "Mode '" + mode + "' is not implemented.";
                    break;
            }

            return res;
        }

        /// <summary>
        /// Безопасное добавление позиций в общий список.
        /// В случае конфликта по времени входа, позиция будет сдвинута на ShiftMs миллисекунд.
        /// </summary>
        /// <param name="sortedPos">сортированный список, в который нужно вставить новую позицию</param>
        /// <param name="pos">вставляемая позиция</param>
        private static void AddSafely(SortedList<DateTime, IPosition> sortedPos, IPosition pos)
        {
            int shift = 0;
            var dt = pos.EntryBar.Date;
            while (sortedPos.ContainsKey(dt.AddMilliseconds(shift)))
            {
                shift += ShiftMs;
            }
            try
            {
                sortedPos.Add(dt.AddMilliseconds(shift), pos);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Безопасное добавление позиций в общий список.
        /// В случае конфликта по времени входа, позиция будет сдвинута на ShiftMs миллисекунд.
        /// </summary>
        /// <param name="sortedPos">сортированный список, в который нужно вставить новую позицию</param>
        /// <param name="pos">вставляемая позиция</param>
        private static void AddSafely(SortedList<DateTime, IOrder> sortedOrders, IOrder order)
        {
            int shift = 0;
            var dt = order.Date;
            while (sortedOrders.ContainsKey(dt.AddMilliseconds(shift)))
            {
                shift += ShiftMs;
            }
            try
            {
                sortedOrders.Add(dt.AddMilliseconds(shift), order);
            }
            catch
            {
            }
        }

        //public IEnumerable<string> GetValuesForParameter(string paramName)
        //{
        //    if (paramName.Equals("TextString", StringComparison.InvariantCultureIgnoreCase))
        //    {
        //        return new[] { "/*", "Change Me!", "*/" };
        //    }
        //    else if (paramName.Equals("ExpirationMode", StringComparison.InvariantCultureIgnoreCase))
        //    {
        //        return new[] { ExpiryMode.FixedExpiry.ToString() };
        //    }
        //    else
        //        throw new ArgumentException("Parameter '" + paramName + "' is not supported.", "paramName");
        //}
    }
}
