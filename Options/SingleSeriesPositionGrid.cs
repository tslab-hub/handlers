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
    /// \~english Grid of entire option position (count semistraddles)
    /// \~russian Таблица набранной позиции для одиночной опционной серии (в полустреддлах)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Single Series Position Grid", Language = Constants.En)]
    [HelperName("Таблица позиции (одна серия)", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(3)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Таблица набранной позиции для одиночной опционной серии (в полустреддлах)")]
    [HelperDescription("Grid of entire option position (count semistraddles)", Constants.En)]
    public class SingleSeriesPositionGrid : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0";

        private bool m_countFutures = false;
        private StrikeType m_optionType = StrikeType.Any;
        private string m_tooltipFormat = DefaultTooltipFormat;

        #region Parameters
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

        public InteractiveSeries Execute(double time, InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return Constants.EmptySeries;

            int lastBarIndex = optSer.UnderlyingAsset.Bars.Count - 1;
            DateTime now = optSer.UnderlyingAsset.Bars[Math.Min(barNum, lastBarIndex)].Date;
            bool wasInitialized = HandlerInitializedToday(now);

            double dT = time;
            if (!DoubleUtil.IsPositive(dT))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (smile == null)
            {
                string msg = String.Format("[{0}] Argument 'smile' must be filled with InteractiveSeries.", GetType().Name);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if (oldInfo == null)
            {
                string msg = String.Format("[{0}] Property Tag of object smile must be filled with SmileInfo. Tag:{1}", GetType().Name, smile.Tag);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.EmptySeries;
            }

            double futPx;
            if (DoubleUtil.IsPositive(oldInfo.F))
                futPx = oldInfo.F;
            else
                futPx = optSer.UnderlyingAsset.Bars[Math.Min(barNum, lastBarIndex)].Close;

            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            if (m_countFutures)
            {
                ReadOnlyCollection<IPosition> futPositions = posMan.GetActiveForBar(optSer.UnderlyingAsset);
                if (futPositions.Count > 0)
                {
                    double futQty;
                    GetTotalQty(futPositions, out futQty);

                    if (!DoubleUtil.IsZero(futQty))
                    {
                        // ReSharper disable once UseObjectOrCollectionInitializer
                        InteractivePointActive ip = new InteractivePointActive(0, futQty);
                        ip.IsActive = true;
                        //ip.DragableMode = DragableMode.None;
                        //ip.Geometry = Geometries.Rect;
                        //ip.Color = Colors.DarkOrange;

                        //// PROD-1194 - Цвет ячеек и фона
                        //if (futQty > 0)
                        //    ip.BackColor = ScriptColors.Aquamarine;
                        //else if (futQty < 0)
                        //    ip.BackColor = ScriptColors.LightSalmon;
                        //if (ip.BackColor != null)
                        //    ip.ForeColor = ScriptColors.Black;

                        // TODO: Ждем решения PROD-6009
                        //// PROD-1194 - Цвет ячеек и фона -- фон красится по принципу "в деньгах или нет", шрифт -- по знаку
                        //if (futQty > 0)
                        //    ip.ForeColor = ScriptColors.LightGreen;
                        //else if (futQty < 0)
                        //    ip.ForeColor = ScriptColors.Red;
                        ////if () // Фьючерс не красится!
                        ////    ip.ForeColor = ScriptColors.Black;

                        ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "Fut Qty:{0}", futQty);

                        controlPoints.Add(new InteractiveObject(ip));
                    }
                }
            }

            for (int j = 0; j < pairs.Length; j++)
            {
                IOptionStrikePair pair = pairs[j];
                double putQty, callQty;
                GetPairQty(posMan, pair, out putQty, out callQty);

                if ((!DoubleUtil.IsZero(putQty)) || (!DoubleUtil.IsZero(callQty)))
                {
                    double y = 0;
                    Color? backCol = null;
                    switch (m_optionType)
                    {
                        case StrikeType.Put:
                            y = putQty;
                            backCol = (pair.Strike > futPx) ? ScriptColors.LightCyan : ScriptColors.LightYellow;
                            break;

                        case StrikeType.Call:
                            y = callQty;
                            backCol = (pair.Strike < futPx) ? ScriptColors.LightCyan : ScriptColors.LightYellow;
                            break;

                        case StrikeType.Any:
                            y = putQty + callQty;
                            break;

                        default:
                            throw new NotImplementedException("OptionType: " + m_optionType);
                    }

                    if (
                        (!DoubleUtil.IsZero(y)) ||
                        ((m_optionType == StrikeType.Any) && ((!DoubleUtil.IsZero(putQty)) || (!DoubleUtil.IsZero(callQty))))) // Хотя вообще-то мы внутри if и второй блок проверок всегда true...
                    {
                        // ReSharper disable once UseObjectOrCollectionInitializer
                        InteractivePointActive ip = new InteractivePointActive(pair.Strike, y);
                        ip.IsActive = true;
                        //ip.DragableMode = DragableMode.None;
                        //ip.Geometry = Geometries.Rect;
                        //ip.Color = Colors.DarkOrange;

                        //// PROD-1194 - Цвет ячеек и фона
                        //if (y > 0)
                        //    ip.BackColor = ScriptColors.Aquamarine;
                        //else if (y < 0)
                        //    ip.BackColor = ScriptColors.LightSalmon;
                        //if (ip.BackColor != null)
                        //    ip.ForeColor = ScriptColors.Black;

                        // TODO: Ждем решения PROD-6009
                        //// PROD-1194 - Цвет ячеек и фона -- фон красится по принципу "в деньгах или нет", шрифт -- по знаку
                        //if (y > 0)
                        //    ip.ForeColor = ScriptColors.LightGreen;
                        //else if (y < 0)
                        //    ip.ForeColor = ScriptColors.Red;
                        ////if (backCol != null)
                        ////    ip.BackColor = backCol;

                        ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "K:{0}; Qty:{1}", pair.Strike, y);

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

        public static void GetPairQty(PositionsManager posMan, IOptionStrikePair pair, out double putQty, out double callQty)
        {
            putQty = 0;
            callQty = 0;

            ISecurity putSec = pair.Put.Security, callSec = pair.Call.Security;
            ReadOnlyCollection<IPosition> putPositions = posMan.GetActiveForBar(putSec);
            ReadOnlyCollection<IPosition> callPositions = posMan.GetActiveForBar(callSec);
            if ((putPositions.Count <= 0) && (callPositions.Count <= 0))
                return;

            GetTotalQty(putPositions, out putQty);
            GetTotalQty(callPositions, out callQty);
        }

        public static void GetTotalQty(IEnumerable<IPosition> positions,
            out double totalQty)
        {
            totalQty = 0;
            foreach (IPosition pos in positions)
            {
                // Пока что State лучше не трогать 
                //if (pos.PositionState == PositionState.HaveError)
                {
                    int sign = pos.IsLong ? 1 : -1;
                    double qty = Math.Abs(pos.Shares);
                    totalQty += sign * qty;
                }
            }
        }
    }
}
