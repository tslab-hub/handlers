using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Grid of entire option position prices (average price)
    /// \~russian Таблица цен набранной позиции для одиночной опционной серии (средняя цена)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Single Series Position Prices", Language = Constants.En)]
    [HelperName("Средние цены позиции (одна серия)", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Таблица средних цен набранной позиции для одиночной опционной серии")]
    [HelperDescription("Grid of average position prices (single option series)", Constants.En)]
    public class SingleSeriesPositionPrices : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0";

        /// <summary>Суммировать количество, а не цену</summary>
        private bool m_countQty = false;
        /// <summary>Длинные позиции</summary>
        private bool m_longPositions = true;
        private bool m_countFutures = false;
        private StrikeType m_optionType = StrikeType.Call;
        private string m_tooltipFormat = DefaultTooltipFormat;

        #region Parameters
        /// <summary>
        /// \~english Long positions
        /// \~russian Длинные позиции
        /// </summary>
        [HelperName("Long positions", Constants.En)]
        [HelperName("Длинные позиции", Constants.Ru)]
        [Description("Цены длинных позиций")]
        [HelperDescription("Prices of long positions", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "true")]
        public bool LongPositions
        {
            get { return m_longPositions; }
            set { m_longPositions = value; }
        }

        /// <summary>
        /// \~english Option type to be used by handler (call, put, sum of both)
        /// \~russian Тип опционов для расчетов (колл, пут, сумма)
        /// </summary>
        [HelperName("Option Type", Constants.En)]
        [HelperName("Тип опциона", Constants.Ru)]
        [Description("Тип опционов для расчетов (колл, пут, сумма)")]
        [HelperDescription("Option type to be used by handler (call, put, sum of both)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Call")]
        public StrikeType OptionType
        {
            get { return m_optionType; }
            set { m_optionType = value; }
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
        /// \~english Count lot size
        /// \~russian Подсчитывать объём в лотах
        /// </summary>
        [HelperName("Count lot size", Constants.En)]
        [HelperName("Считать лоты", Constants.Ru)]
        [Description("Подсчитывать объём в лотах")]
        [HelperDescription("Count lot size", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool CountQty
        {
            get { return m_countQty; }
            set { m_countQty = value; }
        }

        /// <summary>
        /// \~english Tooltip format (i.e. '0.00', '0.0##' etc)
        /// \~russian Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.
        /// </summary>
        [HelperName("Tooltip Format", Constants.En)]
        [HelperName("Формат подсказки", Constants.Ru)]
        [Description("Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.")]
        [HelperDescription("Tooltip format (i.e. '0.00', '0.0##' etc)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultTooltipFormat)]
        public string TooltipFormat
        {
            get { return m_tooltipFormat; }
            set
            {
                if (!String.IsNullOrWhiteSpace(value))
                {
                    try
                    {
                        string yStr = Math.PI.ToString(value, CultureInfo.InvariantCulture);
                        m_tooltipFormat = value + yStr;
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

            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            if (m_countFutures)
            {
                var futPositions = posMan.GetClosedOrActiveForBar(optSer.UnderlyingAsset);
                if (futPositions.Count > 0)
                {
                    double futQty, futAvgPx;
                    SingleSeriesProfile.GetAveragePrice(futPositions, barNum, m_longPositions, out futAvgPx, out futQty);

                    if (!DoubleUtil.IsZero(futQty))
                    {
                        double valueToDisplay = m_countQty ? futQty : futAvgPx;

                        // ReSharper disable once UseObjectOrCollectionInitializer
                        InteractivePointActive ip = new InteractivePointActive(0, valueToDisplay);
                        ip.IsActive = true;
                        //ip.DragableMode = DragableMode.None;
                        //ip.Geometry = Geometries.Rect;
                        //ip.Color = Colors.DarkOrange;
                        ip.Tooltip = String.Format("AvgPx:{0}; Qty:{1}", futAvgPx, futQty);

                        controlPoints.Add(new InteractiveObject(ip));
                    }
                }
            }

            for (int j = 0; j < pairs.Length; j++)
            {
                IOptionStrikePair pair = pairs[j];
                double putQty = 0, putAvgPx = Double.NaN;
                {
                    var putPositions = posMan.GetClosedOrActiveForBar(pair.Put.Security);
                    if (putPositions.Count > 0)
                        SingleSeriesProfile.GetAveragePrice(putPositions, barNum, m_longPositions, out putAvgPx, out putQty);
                }

                double callQty = 0, callAvgPx = Double.NaN;
                {
                    var callPositions = posMan.GetClosedOrActiveForBar(pair.Call.Security);
                    if (callPositions.Count > 0)
                        SingleSeriesProfile.GetAveragePrice(callPositions, barNum, m_longPositions, out callAvgPx, out callQty);
                }

                if ((!DoubleUtil.IsZero(putQty)) || (!DoubleUtil.IsZero(callQty)))
                {
                    double averagePrice = 0, lotSize = 0;
                    switch (m_optionType)
                    {
                        case StrikeType.Put:
                            averagePrice = putAvgPx;
                            lotSize = putQty;
                            break;

                        case StrikeType.Call:
                            averagePrice = callAvgPx;
                            lotSize = callQty;
                            break;

                        //case StrikeType.Any:
                        //    y = putQty + callQty;
                        //    break;

                        default:
                            throw new NotSupportedException("OptionType: " + m_optionType);
                    }

                    // Не хочу видеть ячейки таблицы с NaN
                    if (Double.IsNaN(averagePrice))
                        continue;

                    if (!DoubleUtil.IsZero(lotSize))
                    {
                        double valueToDisplay = m_countQty ? lotSize : averagePrice;

                        // ReSharper disable once UseObjectOrCollectionInitializer
                        InteractivePointActive ip = new InteractivePointActive(pair.Strike, valueToDisplay);
                        ip.IsActive = true;
                        //ip.DragableMode = DragableMode.None;
                        //ip.Geometry = Geometries.Rect;
                        //ip.Color = Colors.DarkOrange;
                        ip.Tooltip = String.Format("K:{0}; AvgPx:{1}; Qty:{2}", pair.Strike, averagePrice, lotSize);

                        controlPoints.Add(new InteractiveObject(ip));
                    }
                }
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь правильно делать new
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            SetHandlerInitialized(now);

            return res;
        }
    }
}
