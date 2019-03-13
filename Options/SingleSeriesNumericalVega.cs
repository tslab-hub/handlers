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
    /// \~english Estimate vega profile with numerical differentiation
    /// \~russian Численный расчет веги позиции (строит сразу профиль веги)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Single Series Numerical Vega", Language = Constants.En)]
    [HelperName("Численная вега (одна серия)", Language = Constants.Ru)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Численный расчет веги позиции (строит сразу профиль веги)")]
    [HelperDescription("Estimates a vega profile with numerical differentiation (build a vega profile)", Constants.En)]
    public class SingleSeriesNumericalVega : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0.00";

        private double m_sigmaStep = 0.0001;
        private string m_tooltipFormat = DefaultTooltipFormat;
        private NumericalGreekAlgo m_greekAlgo = NumericalGreekAlgo.ShiftingSmile;

        #region Parameters
        /// <summary>
        /// \~english FrozenSmile - smile is frozen; ShiftingSmile - smile shifts horizontally without modification
        /// \~russian FrozenSmile - улыбка заморожена; ShiftingSmile - улыбка без искажений сдвигается по горизонтали вслед за БА
        /// </summary>
        [HelperName("Greek Algo", Constants.En)]
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
        /// \~english Sigma step for numerical derivative
        /// \~russian Шаг варьирования сигмы при дифференцировании
        /// </summary>
        [HelperName("Sigma Step", Constants.En)]
        [HelperName("Шаг сигмы", Constants.Ru)]
        [Description("Шаг варьирования сигмы при дифференцировании")]
        [HelperDescription("Sigma step for numerical derivative", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0.0001", Min = "0", Max = "1000000", Step = "0.0001")]
        public double SigmaStep
        {
            get { return m_sigmaStep; }
            set { m_sigmaStep = value; }
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
            //InteractiveSeries res = context.LoadObject(cashKey + "positionDelta") as InteractiveSeries;
            //if (res == null)
            //{
            //    res = new InteractiveSeries();
            //    context.StoreObject(cashKey + "positionDelta", res);
            //}

            InteractiveSeries res = new InteractiveSeries();
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return res;

            //double F = prices[prices.Count - 1];
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

            //// TODO: переписаться на обновление старых значений
            //res.ControlPoints.Clear();

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            var smilePoints = smile.ControlPoints;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (InteractiveObject iob in smilePoints)
            {
                double rawVega, f = iob.Anchor.ValueX;
                if (TryEstimateVega(posMan, optSer, pairs, smile, m_greekAlgo, f, m_sigmaStep, dT, out rawVega))
                {
                    // Переводим вегу в дифференциал 'изменение цены за 1% волы'.
                    rawVega /= Constants.PctMult;

                    InteractivePointActive ip = new InteractivePointActive();
                    ip.IsActive = m_showNodes;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect;
                    //ip.Color = System.Windows.Media.Colors.Green;
                    double y = rawVega;
                    ip.Value = new Point(f, y);
                    string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                    ip.Tooltip = String.Format("F:{0}; V:{1}", f, yStr);

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
        /// Вега будет иметь размерность 'пункты за 100% волатильности'.
        /// Обычно же опционщики любят смотреть размерность 'пункты за 1% волатильности'.
        /// Поэтому полученное сырое значение ещё надо делить на 100%.
        /// (Эквивалентно умножению на интересующий набег волы для получения дифференциала).
        /// </summary>
        internal static bool TryEstimateVega(PositionsManager posMan, IOptionSeries optSer, IOptionStrikePair[] pairs,
            InteractiveSeries smile, NumericalGreekAlgo greekAlgo,
            double f, double dSigma, double timeToExpiry, out double rawVega)
        {
            rawVega = Double.NaN;

            if (timeToExpiry < Double.Epsilon)
                throw new ArgumentOutOfRangeException("timeToExpiry", "timeToExpiry must be above zero. timeToExpiry:" + timeToExpiry);

            SmileInfo sInfo = smile.GetTag<SmileInfo>();
            if (sInfo == null)
                return false;

            double cash1 = 0, pnl1 = 0;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect1 = true;
            {
                // фьюч на даёт вклад в вегу
                //SingleSeriesProfile.GetBasePnl(posMan, optSer.UnderlyingAsset, optSer.UnderlyingAsset.Bars.Count - 1, f, out cash1, out pnl1);

                //InteractiveSeries actualSmile;
                //// 1. Изменение положения БА
                //actualSmile = SingleSeriesProfile.GetActualSmile(smile, greekAlgo, f);

                SmileInfo actualSmile;
                // 1. Изменение положения БА
                actualSmile = SingleSeriesProfile.GetActualSmile(sInfo, greekAlgo, f);

                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double pairPnl, pairCash;
                    //double putPx = FinMath.GetOptionPrice(f, pair.Strike, dT, sigma.Value, 0, false);
                    //SingleSeriesProfile.GetPairPnl(posMan, actualSmile, pair, f, timeToExpiry, out pairCash, out pairPnl);
                    pnlIsCorrect1 &= SingleSeriesProfile.TryGetPairPnl(posMan, actualSmile, pair, f, timeToExpiry, out pairCash, out pairPnl);
                    cash1 += pairCash;
                    pnl1 += pairPnl;
                }
            }

            double cash2 = 0, pnl2 = 0;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect2 = true;
            {
                // фьюч на даёт вклад в вегу
                //SingleSeriesProfile.GetBasePnl(posMan, optSer.UnderlyingAsset, optSer.UnderlyingAsset.Bars.Count - 1, f, out cash2, out pnl2);

                //InteractiveSeries actualSmile;
                //// 1. Изменение положения БА
                //actualSmile = SingleSeriesProfile.GetActualSmile(smile, greekAlgo, f);
                //// 2. Подъём улыбки на dSigma
                //actualSmile = SingleSeriesProfile.GetRaisedSmile(actualSmile, greekAlgo, dSigma);

                SmileInfo actualSmile;
                // 1. Изменение положения БА
                actualSmile = SingleSeriesProfile.GetActualSmile(sInfo, greekAlgo, f);
                // 2. Подъём улыбки на dSigma
                actualSmile = SingleSeriesProfile.GetRaisedSmile(actualSmile, greekAlgo, dSigma);

                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double pairPnl, pairCash;
                    //double putPx = FinMath.GetOptionPrice(f, pair.Strike, dT, sigma.Value, 0, false);
                    //SingleSeriesProfile.GetPairPnl(posMan, actualSmile, pair, f, timeToExpiry, out pairCash, out pairPnl);
                    pnlIsCorrect2 &= SingleSeriesProfile.TryGetPairPnl(posMan, actualSmile, pair, f, timeToExpiry, out pairCash, out pairPnl);
                    cash2 += pairCash;
                    pnl2 += pairPnl;
                }
            }

            if (pnlIsCorrect1 && pnlIsCorrect2)
            {
                // Первая точка совпадает с текущей, поэтому нет деления на 2.
                rawVega = ((cash2 + pnl2) - (cash1 + pnl1)) / dSigma;
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Вомма будет иметь размерность 'пункты за 100% волатильности'.
        /// Обычно же опционщики любят смотреть размерность 'пункты за 1% волатильности'.
        /// Поэтому полученное сырое значение ещё надо делить на 100%.
        /// (Эквивалентно умножению на интересующий набег волы для получения дифференциала).
        /// </summary>
        internal static bool TryEstimateVomma(PositionsManager posMan, IOptionSeries optSer, IOptionStrikePair[] pairs,
            InteractiveSeries smile, NumericalGreekAlgo greekAlgo,
            double f, double dSigma, double timeToExpiry, out double rawVomma)
        {
            rawVomma = Double.NaN;

            if (timeToExpiry < Double.Epsilon)
                throw new ArgumentOutOfRangeException("timeToExpiry", "timeToExpiry must be above zero. timeToExpiry:" + timeToExpiry);

            SmileInfo sInfo = smile.GetTag<SmileInfo>();
            if (sInfo == null)
                return false;

            double cash1 = 0, pnl1 = 0;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect1 = true;
            {
                // фьюч на даёт вклад в вегу
                //SingleSeriesProfile.GetBasePnl(posMan, optSer.UnderlyingAsset, optSer.UnderlyingAsset.Bars.Count - 1, f, out cash1, out pnl1);

                //InteractiveSeries actualSmile;
                //// 1. Изменение положения БА
                //actualSmile = SingleSeriesProfile.GetActualSmile(smile, greekAlgo, f);

                SmileInfo actualSmile;
                // 1. Изменение положения БА
                actualSmile = SingleSeriesProfile.GetActualSmile(sInfo, greekAlgo, f);

                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double pairPnl, pairCash;
                    pnlIsCorrect1 &= SingleSeriesProfile.TryGetPairPnl(posMan, actualSmile, pair, f, timeToExpiry, out pairCash, out pairPnl);
                    cash1 += pairCash;
                    pnl1 += pairPnl;
                }
            }

            double cash2 = 0, pnl2 = 0;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect2 = true;
            {
                // фьюч на даёт вклад в вегу
                //SingleSeriesProfile.GetBasePnl(posMan, optSer.UnderlyingAsset, optSer.UnderlyingAsset.Bars.Count - 1, f, out cash2, out pnl2);

                //InteractiveSeries actualSmile;
                //// 1. Изменение положения БА
                //actualSmile = SingleSeriesProfile.GetActualSmile(smile, greekAlgo, f);
                //// 2. Подъём улыбки на dSigma
                //actualSmile = SingleSeriesProfile.GetRaisedSmile(actualSmile, greekAlgo, dSigma);

                SmileInfo actualSmile;
                // 1. Изменение положения БА
                actualSmile = SingleSeriesProfile.GetActualSmile(sInfo, greekAlgo, f);
                // 2. Подъём улыбки на dSigma
                actualSmile = SingleSeriesProfile.GetRaisedSmile(actualSmile, greekAlgo, dSigma);

                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double pairPnl, pairCash;
                    pnlIsCorrect2 &= SingleSeriesProfile.TryGetPairPnl(posMan, actualSmile, pair, f, timeToExpiry, out pairCash, out pairPnl);
                    cash2 += pairCash;
                    pnl2 += pairPnl;
                }
            }

            double cash3 = 0, pnl3 = 0;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect3 = true;
            {
                // фьюч на даёт вклад в вегу
                //SingleSeriesProfile.GetBasePnl(posMan, optSer.UnderlyingAsset, optSer.UnderlyingAsset.Bars.Count - 1, f, out cash2, out pnl2);

                //InteractiveSeries actualSmile;
                //// 1. Изменение положения БА
                //actualSmile = SingleSeriesProfile.GetActualSmile(smile, greekAlgo, f);
                //// 2. Подъём улыбки на dSigma
                //actualSmile = SingleSeriesProfile.GetRaisedSmile(actualSmile, greekAlgo, dSigma);

                SmileInfo actualSmile;
                // 1. Изменение положения БА
                actualSmile = SingleSeriesProfile.GetActualSmile(sInfo, greekAlgo, f);
                // 2. Подъём улыбки на 2*dSigma
                actualSmile = SingleSeriesProfile.GetRaisedSmile(actualSmile, greekAlgo, 2 * dSigma);

                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double pairPnl, pairCash;
                    pnlIsCorrect2 &= SingleSeriesProfile.TryGetPairPnl(posMan, actualSmile, pair, f, timeToExpiry, out pairCash, out pairPnl);
                    cash3 += pairCash;
                    pnl3 += pairPnl;
                }
            }

            if (pnlIsCorrect1 && pnlIsCorrect2 && pnlIsCorrect3)
            {
                // См. Вики случай r=2, N=2: https://ru.wikipedia.org/wiki/Численное_дифференцирование
                // f''(x0) ~= (f0 - 2*f1 + f2)/h/h
                rawVomma = ((cash1 + pnl1) - 2 * (cash2 + pnl2) + (cash3 + pnl3)) / dSigma / dSigma;
                return true;
            }
            else
                return false;
        }

        internal static void GetPairVega(PositionsManager posMan, InteractiveSeries smile, IOptionStrikePair pair,
            double f, double dT, out double totalVega)
        {
            totalVega = 0;

            ISecurity putSec = pair.Put.Security, callSec = pair.Call.Security;
            // закрытые позы не дают в клада в дельту, поэтому беру только активные
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
                double putVega;
                GetOptVega(putPositions,
                    f, pair.Strike, dT, sigma.Value, 0.0, false, out putVega);
                totalVega += putVega;
            }

            {
                double callVega;
                GetOptVega(callPositions,
                    f, pair.Strike, dT, sigma.Value, 0.0, true, out callVega);
                totalVega += callVega;
            }
        }

        internal static void GetOptVega(IEnumerable<IPosition> positions,
            double f, double k, double dT, double sigma, double r, bool isCall,
            out double vega)
        {
            vega = 0;
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
                    double optVega = FinMath.GetOptionVega(f, k, dT, sigma, r, isCall);
                    vega += sign * optVega * qty;
                }
            }
        }
    }
}
