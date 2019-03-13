using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Estimate delta using position profile
    /// \~russian Численный расчет дельты позиции непосредственно на основе её профиля
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Numerical Delta (IntSer)", Language = Constants.En)]
    [HelperName("Численная дельта (IntSer)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = "Position Profile")]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Численный расчет дельты позиции непосредственно на основе её профиля")]
    [HelperDescription("Estimates a delta using a position profile", Constants.En)]
    public class SingleSeriesNumericalDelta3 : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0.000";

        private string m_tooltipFormat = DefaultTooltipFormat;

        #region Parameters
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

        public InteractiveSeries Execute(InteractiveSeries positionProfile, int barNum)
        {
            int barsCount = ContextBarsCount;
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            if (positionProfile == null)
                return Constants.EmptySeries;

            SmileInfo sInfo = positionProfile.GetTag<SmileInfo>();
            if ((sInfo == null) ||
                (sInfo.ContinuousFunction == null) || (sInfo.ContinuousFunctionD1 == null))
                return Constants.EmptySeries;

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            var profilePoints = positionProfile.ControlPoints;
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (InteractiveObject iob in profilePoints)
            {
                double rawDelta, f = iob.Anchor.ValueX;
                if (sInfo.ContinuousFunctionD1.TryGetValue(f, out rawDelta))
                {
                    // ReSharper disable once UseObjectOrCollectionInitializer
                    InteractivePointActive ip = new InteractivePointActive();
                    ip.IsActive = m_showNodes;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect;
                    //ip.Color = System.Windows.Media.Colors.Orange;
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
                    info.F = sInfo.F;
                    info.dT = sInfo.dT;
                    info.RiskFreeRate = sInfo.RiskFreeRate;

                    NotAKnotCubicSpline spline = new NotAKnotCubicSpline(xs, ys);

                    info.ContinuousFunction = spline;
                    info.ContinuousFunctionD1 = spline.DeriveD1();

                    res.Tag = info;
                }
            }
            catch (DivideByZeroException dvbEx)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("Divide-by-zero exception. Probably it is from NotAKnotCubicSpline:");
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine(dvbEx.ToString());
                sb.AppendLine();

                sb.AppendLine("Content of arguments...");
                sb.AppendLine("xs;ys");
                for (int tmpIndex = 0; tmpIndex < xs.Count; tmpIndex++)
                {
                    sb.Append(xs[tmpIndex].ToString(CultureInfo.InvariantCulture) + ";");
                    sb.Append(ys[tmpIndex].ToString(CultureInfo.InvariantCulture));

                    sb.AppendLine();
                }
                sb.AppendLine();

                m_context.Log(sb.ToString(), MessageType.Error, false);
                m_context.Log(dvbEx.ToString(), MessageType.Error, true);
                return Constants.EmptySeries;
            }
            catch (Exception ex)
            {
                m_context.Log(ex.ToString(), MessageType.Error, true);
                return Constants.EmptySeries;
            }

            return res;
        }
    }
}
