using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Horizontal line at smile chart. Position is defined by 'Value' parameter.
    /// \~russian Горизонтальная линия на графике улыбки. Положение задаётся параметром 'Уровень'.
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Horizontal line", Language = Constants.En)]
    [HelperName("Горизонтальная линия", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Горизонтальная линия на графике улыбки. Положение задаётся параметром 'Уровень'.")]
    [HelperDescription("Horizontal line at smile chart. Position is defined by 'Value' parameter.", Constants.En)]
    public class ConstSmileLevel2 : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const int NumControlPoints = 11;

        private double m_sigma = 0.3;
        private double m_value = 0;
        private bool m_showEdgeLabels = true;

        private string m_label = "V";
        /// <summary>Формат для меток (например, 'IV:{0:0.00}%')</summary>
        private string m_labelFormat = @"V:{0:0.00}%";
        /// <summary>Формат для тултипов (например, 'IV:{0:0.00}%')</summary>
        private string m_tooltipFormat = @"K:{0}; V:{1:0.00}%";

        #region Parameters
        /// <summary>
        /// \~english Value (percents)
        /// \~russian Уровень (в процентах)
        /// </summary>
        [HelperName("Value, %", Constants.En)]
        [HelperName("Уровень, %", Constants.Ru)]
        [Description("Уровень (в процентах)")]
        [HelperDescription("Value (percents)", Language = Constants.En)]
        [HandlerParameter(true, "30", Min = "0", Max = "1000000", Step = "0.5", NotOptimized = false)]
        public double ValuePct
        {
            get { return m_value * Constants.PctMult; }
            set { m_value = value / Constants.PctMult; }
        }

        /// <summary>
        /// \~english Show edge labels
        /// \~russian Показывать подписи крайних узлов
        /// </summary>
        [HelperName("Show edge labels", Constants.En)]
        [HelperName("Подписи на краях", Constants.Ru)]
        [Description("Показывать подписи крайних узлов")]
        [HelperDescription("Show edge labels", Language = Constants.En)]
        [HandlerParameter(true, "true", NotOptimized = false)]
        public bool ShowEdgeLabels
        {
            get { return m_showEdgeLabels; }
            set { m_showEdgeLabels = value; }
        }

        /// <summary>
        /// \~english Volatility (percents)
        /// \~russian Волатильность (в процентах)
        /// </summary>
        [HelperName("Sigma, %", Constants.En)]
        [HelperName("Волатильность, %", Constants.Ru)]
        [Description("Волатильность (в процентах)")]
        [HelperDescription("Volatility (percents)", Language = Constants.En)]
        [HandlerParameter(true, "30", Min = "0", Max = "1000000", Step = "0.5", NotOptimized = false)]
        public double SigmaPct
        {
            get { return m_sigma * Constants.PctMult; }
            set
            {
                if (value > 0)
                    m_sigma = value / Constants.PctMult;
            }
        }

        /// <summary>
        /// \~english Label to mark a nodes
        /// \~russian Метка для подписи узлов
        /// </summary>
        [HelperName("Label", Constants.En)]
        [HelperName("Метка", Constants.Ru)]
        [Description("Метка для подписи узлов")]
        [HelperDescription("Label to mark a nodes", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "V")]
        public string Label
        {
            get { return m_label; }
            set
            {
                if ((value == null) || (value.Equals(m_label)))
                    return;

                // Лишние знаки форматирования нам не нужны.
                if (value.Contains("{") || value.Contains("}") || value.Contains(@"\"))
                    return;

                m_label = value ?? "";
                m_labelFormat = m_label + ":{0:0.00}%";
                m_tooltipFormat = "K:{0}; " + m_label + ":{1:0.00}%";
            }
        }
        #endregion Parameters

        public InteractiveSeries Execute(double price, double time, int barNum)
        {
            int barsCount = ContextBarsCount;
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            double futPx = price;
            double dT = time;

            if (Double.IsNaN(dT) || (dT < Double.Epsilon) ||
                Double.IsNaN(futPx) || (futPx < Double.Epsilon))
                return Constants.EmptySeries;

            double width = (SigmaMult * m_sigma * Math.Sqrt(dT)) * futPx;

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            int half = NumControlPoints / 2; // Целочисленное деление!
            double dK = width / half;
            // Сдвигаю точки, чтобы избежать отрицательных значений
            while ((futPx - half * dK) <= Double.Epsilon)
                half--;
            for (int j = 0; j < NumControlPoints; j++)
            {
                double k = futPx + (j - half) * dK;

                InteractivePointLight ip;
                bool edgePoint = (j == 0) || (j == NumControlPoints - 1);
                if (m_showNodes ||
                    (edgePoint && m_showEdgeLabels)) // На крайние точки повешу Лейблы
                {
                    InteractivePointActive tmp = new InteractivePointActive();

                    tmp.IsActive = m_showNodes || (edgePoint && m_showEdgeLabels);
                    //tmp.DragableMode = DragableMode.None;
                    //tmp.Geometry = Geometries.Rect;
                    //tmp.Color = Colors.DarkOrange;
                    tmp.Tooltip = String.Format(CultureInfo.InvariantCulture,
                        m_tooltipFormat, k, m_value * Constants.PctMult); // "K:{0}; V:{1:0.00}%"

                    if (edgePoint)
                    {
                        tmp.Label = String.Format(CultureInfo.InvariantCulture,
                            m_labelFormat, m_value * Constants.PctMult); // "V:{0:0.00}%"
                    }

                    ip = tmp;
                }
                else
                    ip = new InteractivePointLight();

                ip.Value = new Point(k, m_value);

                controlPoints.Add(new InteractiveObject(ip));
            }

            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            SmileInfo info = new SmileInfo();
            info.F = futPx;
            info.dT = dT;
            info.RiskFreeRate = 0;

            try
            {
                ConstantFunction spline = new ConstantFunction(m_value, futPx, dT);

                info.ContinuousFunction = spline;
                info.ContinuousFunctionD1 = spline.DeriveD1();

                res.Tag = info;
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
