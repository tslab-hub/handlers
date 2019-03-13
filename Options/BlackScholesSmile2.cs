using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Horizontal line spanning a few Sigma
    /// \~russian Горизонтальная линия шириной несколько сигм
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Black-Scholes 'Smile'", Language = Constants.En)]
    [HelperName("'Улыбка' Блека-Шолза", Language = Constants.Ru)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.DOUBLE, Name = "Sigma")]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Горизонтальная линия шириной несколько сигм")]
    [HelperDescription("Horizontal line spanning a few Sigma", Constants.En)]
    public class BlackScholesSmile2 : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const int NumControlPoints = 11;

        private string m_label = "IV";
        /// <summary>Формат для меток (например, 'IV:{0:0.00}%')</summary>
        private string m_labelFormat = @"IV:{0:0.00}%";
        /// <summary>Формат для тултипов (например, 'IV:{0:0.00}%')</summary>
        private string m_tooltipFormat = @"K:{0}; IV:{1:0.00}%";

        #region Parameters
        /// <summary>
        /// \~english Label to mark a nodes
        /// \~russian Метка для подписи узлов
        /// </summary>
        [HelperName("Label", Constants.En)]
        [HelperName("Метка", Constants.Ru)]
        [Description("Метка для подписи узлов")]
        [HelperDescription("Label to mark a nodes", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "IV")]
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

        public InteractiveSeries Execute(double price, double time, double sigma, int barNum)
        {
            int barsCount = ContextBarsCount;
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            double futPx = price;
            double dT = time;
            //double sigma = sigmas[sigmas.Count - 1];

            if (Double.IsNaN(dT) || (dT < Double.Epsilon) ||
                Double.IsNaN(futPx) || (futPx < Double.Epsilon) ||
                Double.IsNaN(sigma) || (sigma < Double.Epsilon))
                return Constants.EmptySeries;

            double width = (SigmaMult * sigma * Math.Sqrt(dT)) * futPx;

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
                if (m_showNodes || edgePoint) // На крайние точки повешу тултипы для удобства. Лейблы пока не буду делать.
                {
                    InteractivePointActive tmp = new InteractivePointActive();

                    tmp.IsActive = m_showNodes || edgePoint;
                    tmp.DragableMode = DragableMode.None;
                    tmp.Geometry = Geometries.Rect;
                    tmp.Color = AlphaColors.DarkOrange;
                    tmp.Tooltip = String.Format(CultureInfo.InvariantCulture,
                        m_tooltipFormat, k, sigma * Constants.PctMult); // "K:{0}; IV:{1:0.00}%"

                    if (edgePoint)
                    {
                        tmp.Label = String.Format(CultureInfo.InvariantCulture,
                            m_labelFormat, sigma * Constants.PctMult); // "IV:{0:0.00}%"
                    }

                    ip = tmp;
                }
                else
                    ip = new InteractivePointLight();

                ip.Value = new Point(k, sigma);

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
                ConstantFunction spline = new ConstantFunction(sigma, futPx, dT);

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
