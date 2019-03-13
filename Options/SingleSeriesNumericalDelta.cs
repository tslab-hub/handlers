using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Estimate delta profile with numerical differentiation
    /// \~russian Численный расчет дельты позиции (строит сразу профиль дельты)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Single Series Numerical Delta", Language = Constants.En)]
    [HelperName("Численная дельта (одна серия)", Language = Constants.Ru)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Численный расчет дельты позиции (строит сразу профиль дельты)")]
    [HelperDescription("Estimates a delta profile with numerical differentiation (build a delta profile)", Constants.En)]
    public class SingleSeriesNumericalDelta : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0.00";

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
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return Constants.EmptySeries;

            //double F = prices[prices.Count - 1];
            double dT = time;
            if (Double.IsNaN(dT) || (dT < Double.Epsilon))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (smile == null)
            {
                string msg = String.Format("[{0}] Argument 'smile' must be filled with InteractiveSeries.", GetType().Name);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if (oldInfo == null)
            {
                string msg = String.Format("[{0}] Property Tag of object smile must be filled with SmileInfo. Tag:{1}", GetType().Name, smile.Tag);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            double dF = optSer.UnderlyingAsset.Tick;
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            var smilePoints = smile.ControlPoints;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (InteractiveObject iob in smilePoints)
            {
                double rawDelta, f = iob.Anchor.ValueX;
                if (TryEstimateDelta(posMan, optSer, pairs, smile, m_greekAlgo, f, dF, dT, out rawDelta))
                {
                    InteractivePointActive ip = new InteractivePointActive();
                    ip.IsActive = m_showNodes;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect;
                    //ip.Color = System.Windows.Media.Colors.Green;
                    double y = rawDelta;
                    ip.Value = new Point(f, y);
                    string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                    ip.Tooltip = String.Format("F:{0}; D:{1}", f, yStr);

                    controlPoints.Add(new InteractiveObject(ip));

                    xs.Add(f);
                    ys.Add(y);
                }
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
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

        internal static bool TryEstimateDelta(PositionsManager posMan, IOptionSeries optSer, IOptionStrikePair[] pairs,
            InteractiveSeries smile, NumericalGreekAlgo greekAlgo,
            double f, double dF, double timeToExpiry, out double rawDelta)
        {
            rawDelta = Double.NaN;

            if (timeToExpiry < Double.Epsilon)
                throw new ArgumentOutOfRangeException("timeToExpiry", "timeToExpiry must be above zero. timeToExpiry:" + timeToExpiry);

            SmileInfo sInfo = smile.GetTag<SmileInfo>();
            if (sInfo == null)
            {
                Contract.Assert(false, $"[{nameof(SingleSeriesNumericalDelta)}.{nameof(TryEstimateDelta)}] #1 Каким образом получили неподготовленную улыбку? (sInfo == null)");

                return false;
            }

            double cash1, pnl1;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect1 = true;
            {
                SingleSeriesProfile.GetBasePnl(posMan, optSer.UnderlyingAsset, optSer.UnderlyingAsset.Bars.Count - 1, f - dF, out cash1, out pnl1);

                // PROD-5746 -- Убираю использование старого неэффективного кода
                //InteractiveSeries actualSmile = SingleSeriesProfile.GetActualSmile(smile, greekAlgo, f - dF);
                SmileInfo actualSmile = SingleSeriesProfile.GetActualSmile(sInfo, greekAlgo, f - dF);

                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double pairPnl, pairCash;
                    //double putPx = FinMath.GetOptionPrice(f, pair.Strike, dT, sigma.Value, 0, false);
                    //SingleSeriesProfile.GetPairPnl(posMan, actualSmile, pair, f - dF, timeToExpiry, out pairCash, out pairPnl);
                    pnlIsCorrect1 &= SingleSeriesProfile.TryGetPairPnl(posMan, actualSmile, pair, f - dF, timeToExpiry, out pairCash, out pairPnl);
                    cash1 += pairCash;
                    pnl1 += pairPnl;
                }
            }

            double cash2, pnl2;
            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect2 = true;
            {
                SingleSeriesProfile.GetBasePnl(posMan, optSer.UnderlyingAsset, optSer.UnderlyingAsset.Bars.Count - 1, f + dF, out cash2, out pnl2);

                // PROD-5746 -- Убираю использование старого неэффективного кода
                //InteractiveSeries actualSmile = SingleSeriesProfile.GetActualSmile(smile, greekAlgo, f + dF);
                SmileInfo actualSmile = SingleSeriesProfile.GetActualSmile(sInfo, greekAlgo, f + dF);

                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double pairPnl, pairCash;
                    //double putPx = FinMath.GetOptionPrice(f, pair.Strike, dT, sigma.Value, 0, false);
                    //SingleSeriesProfile.GetPairPnl(posMan, actualSmile, pair, f + dF, timeToExpiry, out pairCash, out pairPnl);
                    pnlIsCorrect2 &= SingleSeriesProfile.TryGetPairPnl(posMan, actualSmile, pair, f + dF, timeToExpiry, out pairCash, out pairPnl);
                    cash2 += pairCash;
                    pnl2 += pairPnl;
                }
            }

            if (pnlIsCorrect1 && pnlIsCorrect2)
            {
                rawDelta = ((cash2 + pnl2) - (cash1 + pnl1)) / 2.0 / dF;
                return true;
            }
            else
                return false;
        }

        internal static void GetBaseDelta(PositionsManager posMan, ISecurity sec, int barNum, double f, out double rawDelta)
        {
            rawDelta = 0;

            // дельта БА всегда 1?
            const double Delta = 1;

            // закрытые позы не дают в клада в дельту, поэтому беру только активные
            var positions = posMan.GetActiveForBar(sec);
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
                    rawDelta += sign * qty * Delta;
                }
            }
        }

        internal static void GetPairDelta(PositionsManager posMan, InteractiveSeries smile, IOptionStrikePair pair, double f, double dT, out double totalDelta)
        {
            totalDelta = 0;

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
                double putDelta;
                GetOptDelta(putPositions,
                    f, pair.Strike, dT, sigma.Value, 0.0, false, out putDelta);
                totalDelta += putDelta;
            }

            {
                double callDelta;
                GetOptDelta(callPositions,
                    f, pair.Strike, dT, sigma.Value, 0.0, true, out callDelta);
                totalDelta += callDelta;
            }
        }

        internal static void GetOptDelta(IEnumerable<IPosition> positions,
            double f, double k, double dT, double sigma, double r, bool isCall,
            out double delta)
        {
            delta = 0;
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
                    double optDelta = FinMath.GetOptionDelta(f, k, dT, sigma, r, isCall);
                    delta += sign * optDelta * qty;
                }
            }
        }
    }
}
