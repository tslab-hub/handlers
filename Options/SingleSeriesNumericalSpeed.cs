using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
#if DEBUG

    /// <summary>
    /// \~english Estimate 'Speed' profile with numerical differentiation
    /// \~russian Численный расчет грека 'Скорость' позиции (строит сразу профиль скорости)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Single Series Numerical Speed", Language = Constants.En)]
    [HelperName("Численная скорость (одна серия)", Language = Constants.Ru)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Численный расчет грека 'Скорость' позиции (строит сразу профиль скорости)")]
    [HelperDescription("Estimate 'Speed' profile with numerical differentiation", Constants.En)]
    public class SingleSeriesNumericalSpeed : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0.000000";

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

            double dF = optSer.UnderlyingAsset.Tick * 10;
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            var smilePoints = smile.ControlPoints;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (InteractiveObject iob in smilePoints)
            {
                double rawSpeed, f = iob.Anchor.ValueX;
                if (TryEstimateSpeed(posMan, optSer, pairs, smile, m_greekAlgo, f, dF, dT, out rawSpeed))
                {
                    // ReSharper disable once UseObjectOrCollectionInitializer
                    InteractivePointActive ip = new InteractivePointActive();
                    ip.IsActive = m_showNodes;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect;
                    //ip.Color = System.Windows.Media.Colors.Green;
                    double y = rawSpeed;
                    ip.Value = new Point(f, y);
                    string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                    ip.Tooltip = String.Format("F:{0}; Speed:{1}", f, yStr);

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

        internal static bool TryEstimateSpeed(PositionsManager posMan, IOptionSeries optSer, IOptionStrikePair[] pairs,
            InteractiveSeries smile, NumericalGreekAlgo greekAlgo,
            double f, double dF, double timeToExpiry, out double rawSpeed)
        {
            rawSpeed = Double.NaN;

            if (timeToExpiry < Double.Epsilon)
                throw new ArgumentOutOfRangeException("timeToExpiry", "timeToExpiry must be above zero. timeToExpiry:" + timeToExpiry);

            double gamma1, gamma2;
            bool ok1 = SingleSeriesNumericalGamma.TryEstimateGamma(posMan, optSer, pairs, smile, greekAlgo,
                f - dF, dF, timeToExpiry, out gamma1);
            if (!ok1)
                return false;

            bool ok2 = SingleSeriesNumericalGamma.TryEstimateGamma(posMan, optSer, pairs, smile, greekAlgo,
                f + dF, dF, timeToExpiry, out gamma2);
            if (!ok2)
                return false;

            rawSpeed = (gamma2 - gamma1) / 2.0 / dF;
            return true;
        }
    }

#endif
}
