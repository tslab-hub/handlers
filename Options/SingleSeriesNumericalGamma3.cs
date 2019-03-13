using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Estimate gamma using delta profile
    /// \~russian Численный расчет гаммы позиции непосредственно на основе её профиля дельты
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Numerical Gamma (IntSer)", Language = Constants.En)]
    [HelperName("Численная гамма (IntSer)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = "DeltaProfile")]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Численный расчет гаммы позиции непосредственно на основе её профиля дельты")]
    [HelperDescription("Estimates a gamma position using its delta profile", Constants.En)]
    public class SingleSeriesNumericalGamma3 : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0.0000000";

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

        public InteractiveSeries Execute(InteractiveSeries deltaProfile, int barNum)
        {
            //InteractiveSeries res = context.LoadObject(cashKey + "positionDelta") as InteractiveSeries;
            //if (res == null)
            //{
            //    res = new InteractiveSeries();
            //    context.StoreObject(cashKey + "positionDelta", res);
            //}

            InteractiveSeries res = new InteractiveSeries();
            int barsCount = ContextBarsCount;
            if (barNum < barsCount - 1)
                return res;

            if (deltaProfile == null)
                return res;

            SmileInfo sInfo = deltaProfile.GetTag<SmileInfo>();
            if ((sInfo == null) ||
                (sInfo.ContinuousFunction == null) || (sInfo.ContinuousFunctionD1 == null))
                return res;

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            var deltaPoints = deltaProfile.ControlPoints;
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (InteractiveObject iob in deltaPoints)
            {
                double rawGamma, f = iob.Anchor.ValueX;
                if (sInfo.ContinuousFunctionD1.TryGetValue(f, out rawGamma))
                {
                    InteractivePointActive ip = new InteractivePointActive();
                    ip.IsActive = m_showNodes;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect;
                    //ip.Color = System.Windows.Media.Colors.Orange;
                    double y = rawGamma;
                    ip.Value = new Point(f, y);
                    string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                    ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "F:{0}; G:{1}", f, yStr);

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
                    info.F = sInfo.F;
                    info.dT = sInfo.dT;
                    info.RiskFreeRate = sInfo.RiskFreeRate;

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
    }
}
