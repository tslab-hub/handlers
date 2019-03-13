using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Windows;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Estimate delta as function of strikes with numerical differentiation
    /// \~russian Численный расчет дельты одного опциона на каждом страйке
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Options board numerical delta", Language = Constants.En)]
    [HelperName("Численная дельта для доски", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Численный расчет дельты одного опциона на каждом страйке")]
    [HelperDescription("Estimate delta as function of strikes with numerical differentiation", Constants.En)]
//#if !DEBUG
//    [HandlerInvisible]
//#endif
    public class OptionsBoardNumericalDelta : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultQty = "100";
        private const string DefaultTooltipFormat = "0.00";

        private int m_fixedQty = Int32.Parse(DefaultQty);
        private StrikeType m_optionType = StrikeType.Call;
        private string m_tooltipFormat = DefaultTooltipFormat;
        private NumericalGreekAlgo m_greekAlgo = NumericalGreekAlgo.ShiftingSmile;

        #region Parameters
        /// <summary>
        /// \~english Option type (parameter Any is not recommended)
        /// \~russian Тип опционов (использование типа Any может привести к неожиданному поведению)
        /// </summary>
        [HelperName("Option Type", Constants.En)]
        [HelperName("Вид опционов", Constants.Ru)]
        [Description("Тип опционов (использование типа Any может привести к неожиданному поведению)")]
        [HelperDescription("Option type (parameter Any is not recommended)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "Call")]
        public StrikeType OptionType
        {
            get { return m_optionType; }
            set { m_optionType = value; }
        }

        /// <summary>
        /// \~english Entry size of this virtual position
        /// \~russian Объём открытия этой виртуальной позиции
        /// </summary>
        [HelperName("Size", Constants.En)]
        [HelperName("Кол-во", Constants.Ru)]
        [Description("Объём открытия этой виртуальной позиции")]
        [HelperDescription("Entry size of this virtual position", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = DefaultQty)]
        public int FixedQty
        {
            get { return m_fixedQty; }
            set { m_fixedQty = value; }
        }

        /// <summary>
        /// \~english FrozenSmile - smile is frozen; ShiftingSmile - smile shifts horizontally without modification
        /// \~russian FrozenSmile - улыбка заморожена; ShiftingSmile - улыбка без искажений сдвигается по горизонтали вслед за БА
        /// </summary>
        [HelperName("Greek algo", Constants.En)]
        [HelperName("Алгоритм улыбки", Constants.Ru)]
        [Description("FrozenSmile -- улыбка заморожена; ShiftingSmile -- улыбка без искажений сдвигается по горизонтали вслед за БА")]
        [HelperDescription("FrozenSmile - smile is frozen; ShiftingSmile - smile shifts horizontally without modification", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "ShiftingSmile")]
        public NumericalGreekAlgo GreekAlgo
        {
            get { return m_greekAlgo; }
            set { m_greekAlgo = value; }
        }

        /// <summary>
        /// \~english Tooltip format (i.e. '0.00', '0.0##' etc)
        /// \~russian Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.
        /// </summary>
        [HelperName("Tooltip format", Constants.En)]
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

        public InteractiveSeries Execute(double price, double time, InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return Constants.EmptySeries;

            int lastBarIndex = optSer.UnderlyingAsset.Bars.Count - 1;
            DateTime now = optSer.UnderlyingAsset.Bars[Math.Min(barNum, lastBarIndex)].Date;
            bool wasInitialized = HandlerInitializedToday(now);

            double futPx = price;
            double dT = time;
            if (!DoubleUtil.IsPositive(dT))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.EmptySeries;
            }

            if (!DoubleUtil.IsPositive(futPx))
            {
                // [{0}] Base asset price must be positive value. F:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.FutPxMustBePositive", GetType().Name, futPx);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.EmptySeries;
            }

            if (smile == null)
            {
                string msg = String.Format("[{0}] Argument 'smile' must be filled with InteractiveSeries.", GetType().Name);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.EmptySeries;
            }

            if (m_optionType == StrikeType.Any)
            {
                string msg = String.Format("[{0}] OptionType '{1}' is not supported.", GetType().Name, m_optionType);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
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

            bool isCall = m_optionType == StrikeType.Call;
            double putQty = isCall ? 0 : m_fixedQty;
            double callQty = isCall ? m_fixedQty : 0;
            double riskFreeRate = 0;
            double dF = optSer.UnderlyingAsset.Tick;
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            var smilePoints = smile.ControlPoints;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (IOptionStrikePair pair in pairs)
            {
                double rawDelta;
                if (TryEstimateDelta(putQty, callQty, optSer, pair, smile, m_greekAlgo, futPx, dF, dT, riskFreeRate, out rawDelta))
                {
                    InteractivePointActive ip = new InteractivePointActive();
                    ip.IsActive = m_showNodes;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect;
                    //ip.Color = System.Windows.Media.Colors.Green;
                    double y = rawDelta;
                    ip.Value = new Point(pair.Strike, y);
                    string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                    ip.Tooltip = String.Format("K:{0}; D:{1}", pair.Strike, yStr);

                    controlPoints.Add(new InteractiveObject(ip));

                    xs.Add(pair.Strike);
                    ys.Add(y);
                }
            }

            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            try
            {
                if (xs.Count >= BaseCubicSpline.MinNumberOfNodes)
                {
                    SmileInfo info = new SmileInfo();
                    info.F = oldInfo.F;
                    info.dT = oldInfo.dT;
                    info.RiskFreeRate = oldInfo.RiskFreeRate;

                    NotAKnotCubicSpline spline = new NotAKnotCubicSpline(xs, ys);

                    info.ContinuousFunction = spline;
                    info.ContinuousFunctionD1 = spline.DeriveD1();

                    res.Tag = info;
                }
            }
            catch (Exception ex)
            {
                m_context.Log(ex.ToString(), MessageType.Error, true);
                return Constants.EmptySeries;
            }

            SetHandlerInitialized(now);

            return res;
        }

        internal static bool TryEstimateDelta(double putQty, double callQty, IOptionSeries optSer, IOptionStrikePair pair,
            InteractiveSeries smile, NumericalGreekAlgo greekAlgo,
            double f, double dF, double timeToExpiry, double riskFreeRate, out double rawDelta)
        {
            rawDelta = Double.NaN;

            if (timeToExpiry < Double.Epsilon)
                throw new ArgumentOutOfRangeException("timeToExpiry", "timeToExpiry must be above zero. timeToExpiry:" + timeToExpiry);

            SmileInfo sInfo = smile.Tag as SmileInfo;

            double pnl1 = 0;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect1 = true;
            {
                if (sInfo != null)
                {
                    SmileInfo actualSmile = SingleSeriesProfile.GetActualSmile(sInfo, greekAlgo, f - dF);

                    double pairPnl;
                    pnlIsCorrect1 &= SingleSeriesProfile.TryGetPairPrice(
                        putQty, callQty, actualSmile, pair, f - dF, timeToExpiry, riskFreeRate, out pairPnl);
                    pnl1 += pairPnl;
                }
                else
                {
                    // PROD-5746 -- Убираю использование старого неэффективного кода
                    pnlIsCorrect1 = false;

                    Contract.Assert(pnlIsCorrect1, $"[{nameof(OptionsBoardNumericalDelta)}.{nameof(TryEstimateDelta)}] #1 Каким образом получили неподготовленную улыбку? (sInfo == null)");

                    //InteractiveSeries actualSmile = SingleSeriesProfile.GetActualSmile(smile, greekAlgo, f - dF);

                    //double pairPnl;
                    //pnlIsCorrect1 &= SingleSeriesProfile.TryGetPairPrice(
                    //    putQty, callQty, actualSmile, pair, f - dF, timeToExpiry, riskFreeRate, out pairPnl);
                    //pnl1 += pairPnl;
                }
            }

            double pnl2 = 0;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect2 = true;
            {
                if (sInfo != null)
                {
                    SmileInfo actualSmile = SingleSeriesProfile.GetActualSmile(sInfo, greekAlgo, f + dF);

                    double pairPnl;
                    pnlIsCorrect2 &= SingleSeriesProfile.TryGetPairPrice(
                        putQty, callQty, actualSmile, pair, f + dF, timeToExpiry, riskFreeRate, out pairPnl);
                    pnl2 += pairPnl;
                }
                else
                {
                    // PROD-5746 -- Убираю использование старого неэффективного кода
                    pnlIsCorrect2 = false;

                    Contract.Assert(pnlIsCorrect2, $"[{nameof(OptionsBoardNumericalDelta)}.{nameof(TryEstimateDelta)}] #2 Каким образом получили неподготовленную улыбку? (sInfo == null)");

                    //InteractiveSeries actualSmile = SingleSeriesProfile.GetActualSmile(smile, greekAlgo, f + dF);

                    //double pairPnl;
                    //pnlIsCorrect2 &= SingleSeriesProfile.TryGetPairPrice(
                    //    putQty, callQty, actualSmile, pair, f + dF, timeToExpiry, riskFreeRate, out pairPnl);
                    //pnl2 += pairPnl;
                }
            }

            if (pnlIsCorrect1 && pnlIsCorrect2)
            {
                //rawDelta = ((cash2 + pnl2) - (cash1 + pnl1)) / 2.0 / dF;
                rawDelta = (pnl2 - pnl1) / 2.0 / dF;
                return true;
            }
            else
                return false;
        }
    }
}
