using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Estimate theta as function of strikes with numerical differentiation
    /// \~russian Численный расчет теты одного опциона на каждом страйке
    /// </summary>
    //[HandlerCategory(HandlerCategories.OptionsPositions)]
    //[HelperName("Options board numerical theta", Language = Constants.En)]
    //[HelperName("Численная тета для доски", Language = Constants.Ru)]
    //[InputsCount(4)]
    //[Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    //[Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    //[Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    //[Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    //[OutputType(TemplateTypes.INTERACTIVESPLINE)]
    //[Description("Численный расчет теты одного опциона на каждом страйке")]
    //[HelperDescription("Estimate theta as function of strikes with numerical differentiation", Constants.En)]
#if !DEBUG
    [HandlerInvisible]
#endif
    public class OptionsBoardNumericalTheta : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultQty = "100";
        private const string DefaultTooltipFormat = "0.00";

        private double m_tStep = 0.00001;
        private int m_fixedQty = Int32.Parse(DefaultQty);
        private StrikeType m_optionType = StrikeType.Call;
        private string m_tooltipFormat = DefaultTooltipFormat;
        private NumericalGreekAlgo m_greekAlgo = NumericalGreekAlgo.FrozenSmile;

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
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "FrozenSmile")]
        public NumericalGreekAlgo GreekAlgo
        {
            get { return m_greekAlgo; }
            set { m_greekAlgo = value; }
        }

        /// <summary>
        /// \~english Time step for numerical derivative
        /// \~russian Шаг варьирования времени при дифференцировании
        /// </summary>
        [HelperName("Time step", Constants.En)]
        [HelperName("Шаг времени", Constants.Ru)]
        [Description("Шаг варьирования времени при дифференцировании")]
        [HelperDescription("Time step for numerical derivative", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0.00001", Min = "0", Max = "1000000", Step = "0.00001")]
        public double TStep
        {
            get { return m_tStep; }
            set { m_tStep = value; }
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
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            var smilePoints = smile.ControlPoints;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (IOptionStrikePair pair in pairs)
            {
                double rawTheta;
                if (TryEstimateTheta(putQty, callQty, optSer, pair, smile, m_greekAlgo, futPx, dT, m_tStep, riskFreeRate, out rawTheta))
                {
                    // Переводим тету в дифференциал 'изменение цены за 1 сутки'.
                    rawTheta /= OptionUtils.DaysInYear;

                    InteractivePointActive ip = new InteractivePointActive();
                    ip.IsActive = m_showNodes;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect;
                    //ip.Color = System.Windows.Media.Colors.Green;
                    double y = rawTheta;
                    ip.Value = new Point(pair.Strike, y);
                    string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                    ip.Tooltip = String.Format("K:{0}; Th:{1}", pair.Strike, yStr);

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

        /// <summary>
        /// Тета будет иметь размерность 'пункты за год'.
        /// Обычно же опционщики любят смотреть размерность 'пункты за день'.
        /// Поэтому полученное сырое значение ещё надо делить на количество дней в году.
        /// (Эквивалентно умножению на интересующий набег времени для получения дифференциала).
        /// </summary>
        internal static bool TryEstimateTheta(double putQty, double callQty, IOptionSeries optSer, IOptionStrikePair pair,
            InteractiveSeries smile, NumericalGreekAlgo greekAlgo,
            double f, double timeToExpiry, double tStep, double riskFreeRate, out double rawTheta)
        {
            rawTheta = Double.NaN;

            if (timeToExpiry < Double.Epsilon)
                throw new ArgumentOutOfRangeException("timeToExpiry", "timeToExpiry must be above zero. timeToExpiry:" + timeToExpiry);

            double t1 = (timeToExpiry - tStep > Double.Epsilon) ? (timeToExpiry - tStep) : (0.5 * timeToExpiry);
            double pnl1 = 0;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect1 = true;
            {
                // 2. Изменение времени
                // ВАЖНО: нормальный алгоритм сдвига улыбки во времени будет в платной версии "Пакета Каленковича"
                InteractiveSeries actualSmile = SingleSeriesProfile.GetSmileAtTime(smile, NumericalGreekAlgo.FrozenSmile, t1);

                {
                    double pairPnl;
                    pnlIsCorrect1 &= SingleSeriesProfile.TryGetPairPrice(
                        putQty, callQty, actualSmile, pair, f, t1, riskFreeRate, out pairPnl);
                    pnl1 += pairPnl;
                }
            }

            double t2 = timeToExpiry + tStep;
            double pnl2 = 0;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect2 = true;
            {
                // ВАЖНО: нормальный алгоритм сдвига улыбки во времени будет в платной версии "Пакета Каленковича"
                InteractiveSeries actualSmile = SingleSeriesProfile.GetSmileAtTime(smile, NumericalGreekAlgo.FrozenSmile, t2);

                {
                    double pairPnl;
                    pnlIsCorrect2 &= SingleSeriesProfile.TryGetPairPrice(
                        putQty, callQty, actualSmile, pair, f, t2, riskFreeRate, out pairPnl);
                    pnl2 += pairPnl;
                }
            }

            if (pnlIsCorrect1 && pnlIsCorrect2)
            {
                //rawTheta = ((cash2 + pnl2) - (cash1 + pnl1)) / (t2 - t1);
                rawTheta = (pnl2 - pnl1) / (t2 - t1);
                // Переворачиваю тету, чтобы жить в календарном времени
                rawTheta = -rawTheta;
                return true;
            }
            else
                return false;
        }
    }
}
