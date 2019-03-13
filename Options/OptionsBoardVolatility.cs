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
    /// \~english Option volatility as function of strikes
    /// \~russian Расчет волатильности одного опциона на каждом страйке
    /// </summary>
    //[HandlerCategory(HandlerCategories.OptionsPositions)]
    //[HelperName("Options board volatility", Language = Constants.En)]
    //[HelperName("Волатильность опциона для доски", Language = Constants.Ru)]
    //[InputsCount(4)]
    //[Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    //[Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    //[Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    //[Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    //[OutputType(TemplateTypes.INTERACTIVESPLINE)]
    //[Description("Расчет волатильности одного опциона на каждом страйке")]
    //[HelperDescription("Option volatility as function of strikes", Constants.En)]
#if !DEBUG
    [HandlerInvisible]
#endif
    public class OptionsBoardVolatility : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0.0";

        private StrikeType m_optionType = StrikeType.Call;
        private string m_tooltipFormat = DefaultTooltipFormat;

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

            //if (m_optionType == StrikeType.Any)
            //{
            //    string msg = String.Format("[DEBUG:{0}] OptionType '{1}' is not supported.", GetType().Name, m_optionType);
            //    m_context.Log(msg, MessageType.Error, true);
            //    return Constants.EmptySeries;
            //}

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if (oldInfo == null)
            {
                string msg = String.Format("[{0}] Property Tag of object smile must be filled with SmileInfo. Tag:{1}", GetType().Name, smile.Tag);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.EmptySeries;
            }

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            var smilePoints = smile.ControlPoints;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (IOptionStrikePair pair in pairs)
            {
                double rawIv;
                if (oldInfo.ContinuousFunction.TryGetValue(pair.Strike, out rawIv))
                {
                    InteractivePointActive ip = new InteractivePointActive();
                    ip.IsActive = m_showNodes;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect;
                    //ip.Color = System.Windows.Media.Colors.Green;
                    double y = rawIv * Constants.PctMult;
                    ip.Value = new Point(pair.Strike, y);
                    string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                    ip.Tooltip = String.Format("K:{0}; IV:{1}%", pair.Strike, yStr);

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
    }
}
