using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Grid of entire option position commissions
    /// \~russian Таблица комиссий набранной позиции для одиночной опционной серии
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Single Series Position Commission", Language = Constants.En)]
    [HelperName("Комиссия позиции (одна серия)", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Таблица комиссий набранной позиции для одиночной опционной серии")]
    [HelperDescription("Grid of entire option position commissions", Constants.En)]
    public class SingleSeriesPositionCommissions : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0";

        private bool m_longPositions = true;
        private bool m_countFutures = false;
        private StrikeType m_optionType = StrikeType.Any;
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
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Any")]
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

            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            if (m_countFutures)
            {
                var futPositions = posMan.GetClosedOrActiveForBar(optSer.UnderlyingAsset);
                if (futPositions.Count > 0)
                {
                    double futQty, futCommission;
                    SingleSeriesProfile.GetTotalCommission(futPositions, barNum, m_longPositions, out futCommission, out futQty);

                    if (!DoubleUtil.IsZero(futQty))
                    {
                        InteractivePointActive ip = new InteractivePointActive(0, futQty);
                        ip.IsActive = true;
                        //ip.DragableMode = DragableMode.None;
                        //ip.Geometry = Geometries.Rect;
                        //ip.Color = Colors.DarkOrange;
                        ip.Tooltip = String.Format("Fut commission:{0}", futCommission);

                        controlPoints.Add(new InteractiveObject(ip));
                    }
                }
            }

            for (int j = 0; j < pairs.Length; j++)
            {
                IOptionStrikePair pair = pairs[j];
                double putQty = 0, putCommission = Double.NaN;
                {
                    var putPositions = posMan.GetClosedOrActiveForBar(pair.Put.Security);
                    if (putPositions.Count > 0)
                        SingleSeriesProfile.GetTotalCommission(putPositions, barNum, m_longPositions, out putCommission, out putQty);
                }

                double callQty = 0, callCommission = Double.NaN;
                {
                    var callPositions = posMan.GetClosedOrActiveForBar(pair.Call.Security);
                    if (callPositions.Count > 0)
                        SingleSeriesProfile.GetTotalCommission(callPositions, barNum, m_longPositions, out callCommission, out callQty);
                }

                if ((!DoubleUtil.IsZero(putQty)) || (!DoubleUtil.IsZero(callQty)))
                {
                    double y = 0;
                    switch (m_optionType)
                    {
                        case StrikeType.Put:
                            y = putCommission;
                            break;

                        case StrikeType.Call:
                            y = callCommission;
                            break;

                        case StrikeType.Any:
                            y = putCommission + callCommission;
                            break;

                        default:
                            throw new NotImplementedException("OptionType: " + m_optionType);
                    }

                    // Не хочу видеть ячейки таблицы с NaN
                    if (Double.IsNaN(y))
                        continue;

                    if (
                        (!DoubleUtil.IsZero(y)) ||
                        ((m_optionType == StrikeType.Any) && ((!DoubleUtil.IsZero(putQty)) || (!DoubleUtil.IsZero(callQty))))) // Хотя вообще-то мы внутри if и второй блок проверок всегда true...
                    {
                        InteractivePointActive ip = new InteractivePointActive(pair.Strike, y);
                        ip.IsActive = true;
                        //ip.DragableMode = DragableMode.None;
                        //ip.Geometry = Geometries.Rect;
                        //ip.Color = Colors.DarkOrange;
                        ip.Tooltip = String.Format("K:{0}; Commission:{1}", pair.Strike, y);

                        controlPoints.Add(new InteractiveObject(ip));
                    }
                }
            }

            InteractiveSeries res = new InteractiveSeries(); // Здесь правильно делать new
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            return res;
        }
    }
}
