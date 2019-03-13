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
    /// \~english Estimate theta profile with numerical differentiation
    /// \~russian Численный расчет теты позиции (строит сразу профиль теты)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Single Series Numerical Theta", Language = Constants.En)]
    [HelperName("Численная тета (одна серия)", Language = Constants.Ru)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Численный расчет теты позиции (строит сразу профиль теты)")]
    [HelperDescription("Estimates a theta profile with numerical differentiation (build a theta profile)", Constants.En)]
    public class SingleSeriesNumericalTheta : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0.00";

        private double m_tStep = 0.00001;
        private string m_tooltipFormat = DefaultTooltipFormat;
        private NumericalGreekAlgo m_greekAlgo = NumericalGreekAlgo.FrozenSmile;
        private TimeRemainMode m_tRemainMode = TimeRemainMode.PlainCalendar;

        #region Parameters
        /// <summary>
        /// \~english FrozenSmile - smile is frozen; ShiftingSmile - smile shifts horizontally without modification
        /// \~russian FrozenSmile - улыбка заморожена; ShiftingSmile - улыбка без искажений сдвигается по горизонтали вслед за БА
        /// </summary>
        [HelperName("Greek Algo", Constants.En)]
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
        [HelperName("Time Step", Constants.En)]
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
        /// \~english Algorythm to estimate time-to-expiry
        /// \~russian Алгоритм расчета времени до экспирации
        /// </summary>
        [HelperName("Estimation algo", Constants.En)]
        [HelperName("Алгоритм расчета", Constants.Ru)]
        [Description("Алгоритм расчета времени до экспирации")]
        [HelperDescription("Algorythm to estimate time-to-expiry", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "PlainCalendar")]
        public TimeRemainMode DistanceMode
        {
            get { return m_tRemainMode; }
            set { m_tRemainMode = value; }
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

        public InteractiveSeries Execute(double time, InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            InteractiveSeries res = new InteractiveSeries();
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return res;

            double dT = time;
            if (Double.IsNaN(dT) || (dT < Double.Epsilon))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                m_context.Log(msg, MessageType.Error, true);
                return res;
            }

            if (smile == null)
            {
                string msg = String.Format("[{0}] Argument 'smile' must be filled with InteractiveSeries.", GetType().Name);
                m_context.Log(msg, MessageType.Error, true);
                return res;
            }

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if (oldInfo == null)
            {
                string msg = String.Format("[{0}] Property Tag of object smile must be filled with SmileInfo. Tag:{1}", GetType().Name, smile.Tag);
                m_context.Log(msg, MessageType.Error, true);
                return res;
            }

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            var smilePoints = smile.ControlPoints;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (InteractiveObject iob in smilePoints)
            {
                double rawTheta, f = iob.Anchor.ValueX;
                if (TryEstimateTheta(posMan, pairs, smile, m_greekAlgo, f, dT, m_tStep, out rawTheta))
                {
                    // Переводим тету в дифференциал 'изменение цены за 1 сутки'.
                    rawTheta = RescaleThetaToDays(m_tRemainMode, rawTheta);

                    // ReSharper disable once UseObjectOrCollectionInitializer
                    InteractivePointActive ip = new InteractivePointActive();
                    ip.IsActive = m_showNodes;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect;
                    //ip.Color = System.Windows.Media.Colors.Green;
                    double y = rawTheta;
                    ip.Value = new Point(f, y);
                    string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                    ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "F:{0}; Th:{1}", f, yStr);

                    controlPoints.Add(new InteractiveObject(ip));

                    xs.Add(f);
                    ys.Add(y);
                }
            }

            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            try
            {
                if (xs.Count >= BaseCubicSpline.MinNumberOfNodes)
                {
                    // ReSharper disable once UseObjectOrCollectionInitializer
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

            return res;
        }

        /// <summary>
        /// Делим Тету (размерности 'пунктов за год') на число дней в году с учетом конкретного алгоритма расчета времени
        /// </summary>
        internal static double RescaleThetaToDays(TimeRemainMode tRemainMode, double rawTheta)
        {
            double res;
            switch (tRemainMode)
            {
                case TimeRemainMode.PlainCalendar:
                    res = rawTheta / TimeToExpiry.DaysInYearPlainCalendar;
                    break;

                case TimeRemainMode.PlainCalendarWithoutHolidays:
                    res = rawTheta / TimeToExpiry.DaysInYearPlainCalendarWithoutHolidays;
                    break;

                case TimeRemainMode.PlainCalendarWithoutWeekends:
                    res = rawTheta / TimeToExpiry.DaysInYearPlainCalendarWithoutWeekends;
                    break;

                case TimeRemainMode.RtsTradingTime:
                    res = rawTheta / TimeToExpiry.DaysInYearRts;
                    break;

                case TimeRemainMode.LiquidProRtsTradingTime:
                    res = rawTheta / TimeToExpiry.DaysInYearLiquidProRts;
                    break;

                default:
                    throw new NotImplementedException("tRemainMode: " + tRemainMode);
            }
            return res;
        }

        /// <summary>
        /// Тета будет иметь размерность 'пункты за год'.
        /// Обычно же опционщики любят смотреть размерность 'пункты за день'.
        /// Поэтому полученное сырое значение ещё надо делить на количество дней в году.
        /// (Эквивалентно умножению на интересующий набег времени для получения дифференциала).
        /// </summary>
        internal static bool TryEstimateTheta(PositionsManager posMan, IOptionStrikePair[] pairs,
            InteractiveSeries smile, NumericalGreekAlgo greekAlgo,
            double f, double timeToExpiry, double tStep, out double rawTheta)
        {
            rawTheta = Double.NaN;

            if (timeToExpiry < Double.Epsilon)
                throw new ArgumentOutOfRangeException("timeToExpiry", "timeToExpiry must be above zero. timeToExpiry:" + timeToExpiry);

            SmileInfo sInfo = smile.GetTag<SmileInfo>();
            if (sInfo == null)
                return false;

            double t1 = (timeToExpiry - tStep > Double.Epsilon) ? (timeToExpiry - tStep) : (0.5 * timeToExpiry);
            double cash1 = 0, pnl1 = 0;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect1 = true;
            {
                // TODO: фьюч на даёт вклад в тету???
                //SingleSeriesProfile.GetBasePnl(posMan, optSer.UnderlyingAsset, optSer.UnderlyingAsset.Bars.Count - 1, f, out cash1, out pnl1);

                //// 1. Изменение положения БА
                //InteractiveSeries actualSmile = SingleSeriesProfile.GetActualSmile(smile, greekAlgo, f);

                SmileInfo actualSmile;
                // 1. Изменение положения БА
                actualSmile = SingleSeriesProfile.GetActualSmile(sInfo, greekAlgo, f);

                // 2. Изменение времени
                // ВАЖНО: нормальный алгоритм сдвига улыбки во времени будет в платной версии "Пакета Каленковича"
                actualSmile = SingleSeriesProfile.GetSmileAtTime(actualSmile, NumericalGreekAlgo.FrozenSmile, t1);

                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double pairPnl, pairCash;
                    //double putPx = FinMath.GetOptionPrice(f, pair.Strike, dT, sigma.Value, 0, false);
                    //SingleSeriesProfile.GetPairPnl(posMan, actualSmile, pair, f, t1, out pairCash, out pairPnl);
                    pnlIsCorrect1 &= SingleSeriesProfile.TryGetPairPnl(posMan, actualSmile, pair, f, t1, out pairCash, out pairPnl);
                    cash1 += pairCash;
                    pnl1 += pairPnl;
                }
            }

            double t2 = timeToExpiry + tStep;
            double cash2 = 0, pnl2 = 0;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect2 = true;
            {
                // фьюч на даёт вклад в вегу
                //SingleSeriesProfile.GetBasePnl(posMan, optSer.UnderlyingAsset, optSer.UnderlyingAsset.Bars.Count - 1, f, out cash2, out pnl2);

                //// 1. Изменение положения БА
                //InteractiveSeries actualSmile = SingleSeriesProfile.GetActualSmile(smile, greekAlgo, f);

                SmileInfo actualSmile;
                // 1. Изменение положения БА
                actualSmile = SingleSeriesProfile.GetActualSmile(sInfo, greekAlgo, f);

                // 2. Изменение времени
                // ВАЖНО: нормальный алгоритм сдвига улыбки во времени будет в платной версии "Пакета Каленковича"
                actualSmile = SingleSeriesProfile.GetSmileAtTime(actualSmile, NumericalGreekAlgo.FrozenSmile, t2);

                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double pairPnl, pairCash;
                    //double putPx = FinMath.GetOptionPrice(f, pair.Strike, dT, sigma.Value, 0, false);
                    //SingleSeriesProfile.GetPairPnl(posMan, actualSmile, pair, f, t2, out pairCash, out pairPnl);
                    pnlIsCorrect2 &= SingleSeriesProfile.TryGetPairPnl(posMan, actualSmile, pair, f, t2, out pairCash, out pairPnl);
                    cash2 += pairCash;
                    pnl2 += pairPnl;
                }
            }

            if (pnlIsCorrect1 && pnlIsCorrect2)
            {
                rawTheta = ((cash2 + pnl2) - (cash1 + pnl1)) / (t2 - t1);
                // Переворачиваю тету, чтобы жить в календарном времени
                rawTheta = -rawTheta;
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Тета опционной пары по формуле Блека-Шолза
        /// </summary>
        internal static void GetPairTheta(PositionsManager posMan, InteractiveSeries smile, IOptionStrikePair pair,
            double f, double dT, out double totalTheta)
        {
            totalTheta = 0;

            ISecurity putSec = pair.Put.Security, callSec = pair.Call.Security;
            // закрытые позы не дают в клада в тету, поэтому беру только активные
            ReadOnlyCollection<IPosition> putPositions = posMan.GetActiveForBar(putSec);
            ReadOnlyCollection<IPosition> callPositions = posMan.GetActiveForBar(callSec);
            if ((putPositions.Count <= 0) && (callPositions.Count <= 0))
                return;

            double? sigma = null;
            if ((smile.Tag != null) && (smile.Tag is SmileInfo))
            {
                double tmp;
                SmileInfo info = smile.GetTag<SmileInfo>();
                if (info.ContinuousFunction.TryGetValue(pair.Strike, out tmp))
                    sigma = tmp;
            }

            if (sigma == null)
            {
                sigma = (from d2 in smile.ControlPoints
                            let point = d2.Anchor.Value
                         where (DoubleUtil.AreClose(pair.Strike, point.X))
                         select (double?)point.Y).FirstOrDefault();
            }

            if (sigma == null)
                return;

            {
                double putTheta;
                GetOptTheta(putPositions,
                    f, pair.Strike, dT, sigma.Value, 0.0, false, out putTheta);
                totalTheta += putTheta;
            }

            {
                double callTheta;
                GetOptTheta(callPositions,
                    f, pair.Strike, dT, sigma.Value, 0.0, true, out callTheta);
                totalTheta += callTheta;
            }
        }

        /// <summary>
        /// Тета опциона по формуле Блека-Шолза
        /// </summary>
        internal static void GetOptTheta(IEnumerable<IPosition> positions,
            double f, double k, double dT, double sigma, double r, bool isCall,
            out double theta)
        {
            theta = 0;
            foreach (IPosition pos in positions)
            {
                //if (pos.EntrySignalName.StartsWith("CHT-RI-03.", StringComparison.InvariantCultureIgnoreCase))
                //{
                //    string str = "";
                //}

                // Пока что State лучше не трогать 
                //if (pos.PositionState == PositionState.HaveError)
                {
                    int sign = pos.IsLong ? 1 : -1;
                    double qty = Math.Abs(pos.Shares);
                    double optVega = FinMath.GetOptionTheta(f, k, dT, sigma, r, isCall);
                    theta += sign * optVega * qty;
                }
            }
        }
    }
}
