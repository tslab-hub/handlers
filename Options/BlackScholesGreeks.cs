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
    /// \~english Straigtforward calculation of greeks (as in books)
    /// \~russian Лобовой (книжный) расчет греков позиции
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Single Series Greeks (book)", Language = Constants.En)]
    [HelperName("Греки одиночной серии (по учебнику)", Language = Constants.Ru)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Лобовой (книжный) расчет греков позиции")]
    [HelperDescription("Straigtforward calculation of greeks (as in books)", Constants.En)]
    public class BlackScholesGreeks : BaseCanvasDrawing /* BaseSmileDrawing */, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0.000";

        private Greeks m_greek = Greeks.Theta;
        private string m_tooltipFormat = DefaultTooltipFormat;

        #region Parameters
        /// <summary>
        /// \~english Greek to be calculated (delta, theta, vega, gamma, etc)
        /// \~russian Тип грека для расчетов (дельта, тета, вега, гамма и т.д.)
        /// </summary>
        [HelperName("Greek", Constants.En)]
        [HelperName("Грек", Constants.Ru)]
        [Description("Тип грека для расчетов (дельта, тета, вега, гамма и т.д.)")]
        [HelperDescription("Greek to be calculated (delta, theta, vega, gamma, etc)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Theta")]
        public Greeks Greek
        {
            get { return m_greek; }
            set { m_greek = value; }
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
                        m_context.Log("Tooltip format error. I'll keep old one: " + m_tooltipFormat, MessageType.Warning);
                    }
                }
            }
        }
        #endregion Parameters

        public InteractiveSeries Execute(double time, InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (smile == null) || (optSer == null))
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

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if (oldInfo == null)
            {
                string msg = String.Format("[{0}] Property Tag of object smile must be filled with SmileInfo. Tag:{1}", GetType().Name, smile.Tag);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.EmptySeries;
            }

            double futPx = oldInfo.F;

            double ivAtm;
            if (!oldInfo.ContinuousFunction.TryGetValue(futPx, out ivAtm))
            {
                string msg = String.Format("[{0}] Unable to get IV ATM from smile. Tag:{1}", GetType().Name, smile.Tag);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            if (pairs.Length < 2)
            {
                string msg = String.Format("[{0}] optSer must contain few strike pairs. pairs.Length:{1}", GetType().Name, pairs.Length);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptySeries;
            }

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            double futStep = optSer.UnderlyingAsset.Tick;
            PositionsManager posMan = PositionsManager.GetManager(m_context);

            SortedDictionary<double, IOptionStrikePair> futPrices;
            if (!SmileImitation5.TryPrepareImportantPoints(pairs, futPx, futStep, -1, out futPrices))
            {
                string msg = String.Format("[{0}] It looks like there is no suitable points for the smile. pairs.Length:{1}", GetType().Name, pairs.Length);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptySeries;
            }

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (var kvp in futPrices)
            {
                double f = kvp.Key;
                bool tradableStrike = (kvp.Value != null);

                double rawGreek;
                GetBaseGreek(posMan, optSer.UnderlyingAsset, optSer.UnderlyingAsset.Bars.Count - 1, f, m_greek, out rawGreek);

                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double totalGreek;
                    //double putDelta = FinMath.GetOptionDelta(f, pair.Strike, dT, sigma.Value, 0, false);
                    GetPairGreek(posMan, smile, pair, f, dT, m_greek, out totalGreek);
                    rawGreek += totalGreek;
                }

                InteractivePointActive ip = new InteractivePointActive();
                ip.IsActive = m_showNodes;
                //ip.DragableMode = DragableMode.None;
                //ip.Geometry = Geometries.Rect;
                //ip.Color = AlphaColors.Green;
                double y = rawGreek;
                ip.Value = new Point(f, y);
                string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "F:{0}; {1}:{2}", f, m_greek, yStr);

                controlPoints.Add(new InteractiveObject(ip));

                xs.Add(f);
                ys.Add(y);
            }

            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            SmileInfo info = new SmileInfo();
            info.F = oldInfo.F;
            info.dT = oldInfo.dT;
            info.Expiry = optSer.ExpirationDate;
            info.ScriptTime = now;
            info.RiskFreeRate = oldInfo.RiskFreeRate;
            info.BaseTicker = optSer.UnderlyingAsset.Symbol;

            try
            {
                if (xs.Count >= BaseCubicSpline.MinNumberOfNodes)
                {
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

        internal static void GetBaseGreek(PositionsManager posMan, ISecurity sec, int barNum, double f, Greeks greek, out double rawGreek)
        {
            rawGreek = 0;

            switch (greek)
            {
                case Greeks.Delta:
                    BlackScholesDelta.GetBaseDelta(posMan, sec, barNum, f, out rawGreek);
                    break;

                default:
                    // на первый взгляд все греки БА кроме дельты равны 0
                    return;
            }
        }

        internal static void GetPairGreek(PositionsManager posMan, InteractiveSeries smile, IOptionStrikePair pair, double f, double dT, Greeks greek, out double totalGreek)
        {
            totalGreek = 0;

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
                double putGreek;
                GetOptGreek(putPositions,
                    f, pair.Strike, dT, sigma.Value, 0.0, false, greek, out putGreek);
                totalGreek += putGreek;
            }

            {
                double callGreek;
                GetOptGreek(callPositions,
                    f, pair.Strike, dT, sigma.Value, 0.0, true, greek, out callGreek);
                totalGreek += callGreek;
            }
        }

        internal static void GetOptGreek(IEnumerable<IPosition> positions,
            double f, double k, double dT, double sigma, double ratePct, bool isCall,
            Greeks greek, out double optGreek)
        {
            optGreek = 0;
            foreach (IPosition pos in positions)
            {
                // Пока что State лучше не трогать 
                //if (pos.PositionState == PositionState.HaveError)
                {
                    int sign = pos.IsLong ? 1 : -1;
                    double qty = Math.Abs(pos.Shares);
                    double tmp;
                    switch (greek)
                    {
                        case Greeks.Delta:
                            tmp = FinMath.GetOptionDelta(f, k, dT, sigma, ratePct, isCall);
                            break;

                        case Greeks.Theta:
                            tmp = FinMath.GetOptionTheta(f, k, dT, sigma, ratePct, isCall);
                            break;

                        case Greeks.Vega:
                            tmp = FinMath.GetOptionVega(f, k, dT, sigma, ratePct, isCall);
                            break;

                        case Greeks.Gamma:
                            tmp = FinMath.GetOptionGamma(f, k, dT, sigma, ratePct, isCall);
                            break;

                        default:
                            throw new NotImplementedException("Greek '" + greek + "' is not yet supported.");
                    }
                    optGreek += sign * tmp * qty;
                }
            }
        }
    }
}
