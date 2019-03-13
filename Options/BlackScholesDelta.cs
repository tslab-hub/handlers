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
    /// \~english Straigtforward calculation of delta (as in books)
    /// \~russian Лобовой (книжный) расчет дельты позиции
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Single Series Delta (book)", Language = Constants.En)]
    [HelperName("Дельта одиночной серии (по учебнику)", Language = Constants.Ru)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Лобовой (книжный) расчет дельты позиции")]
    [HelperDescription("Straigtforward calculation of delta (as in books)", Constants.En)]
    public class BlackScholesDelta : BaseSmileDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0.00";

        private string m_tooltipFormat = DefaultTooltipFormat;

        #region Parameters
        /// <summary>
        /// \~english Tooltip format (i.e. '0.00', '0.0##' etc)
        /// \~russian Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.
        /// </summary>
        [HelperName("Tooltip format", Constants.En)]
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
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            //double F = prices[prices.Count - 1];
            double dT = time;
            if (!DoubleUtil.IsPositive(dT))
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

            double f = m_minStrike;
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            while (f <= m_maxStrike)
            {
                double rawDelta;
                GetBaseDelta(posMan, optSer.UnderlyingAsset, optSer.UnderlyingAsset.Bars.Count - 1, f, out rawDelta);

                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double totalDelta;
                    //double putDelta = FinMath.GetOptionDelta(f, pair.Strike, dT, sigma.Value, 0, false);
                    GetPairDelta(posMan, smile, pair, f, dT, out totalDelta);
                    rawDelta += totalDelta;
                }

                InteractivePointActive ip = new InteractivePointActive();
                ip.IsActive = m_showNodes;
                //ip.DragableMode = DragableMode.None;
                //ip.Geometry = Geometries.Rect;
                //ip.Color = AlphaColors.Green;
                double y = rawDelta;
                ip.Value = new Point(f, y);
                string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "F:{0}; D:{1}", f, yStr);

                controlPoints.Add(new InteractiveObject(ip));

                xs.Add(f);
                ys.Add(y);

                f += m_strikeStep;
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

            return res;
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
