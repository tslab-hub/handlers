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
    /// \~english Flat smile as in Black-Scholes option model. Volatility is constant.
    /// \~russian Тривиальная улыбка по версии Блека-Шолза. Уровень волатильности постоянен и задаётся параметром SigmaPct.
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Black-Scholes Const", Language = Constants.En)]
    [HelperName("Константа по Блеку-Шолзу", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Тривиальная улыбка по версии Блека-Шолза. Уровень волатильности постоянен и задаётся параметром 'Волатильность, %'.")]
    [HelperDescription("A flat smile as in the Black-Scholes option model. Volatility is a constant and is defined by the 'Sigma, %' parameter.", Constants.En)]
    public class BlackScholesConstSmile2 : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const int NumControlPoints = 11;

        private double m_sigma = 0.22;

        private string m_label = "IV";
        /// <summary>Формат для меток (например, 'IV:{0:0.00}%')</summary>
        private string m_labelFormat = @"IV:{0:0.00}%";
        /// <summary>Формат для тултипов (например, 'IV:{0:0.00}%')</summary>
        private string m_tooltipFormat = @"K:{0}; IV:{1:0.00}%";

        #region Parameters
        /// <summary>
        /// \~english Volatility (percents)
        /// \~russian Волатильность (в процентах)
        /// </summary>
        [HelperName("Sigma, %", Constants.En)]
        [HelperName("Волатильность, %", Constants.Ru)]
        [Description("Волатильность (в процентах)")]
        [HelperDescription("Volatility (percents)", Language = Constants.En)]
        [HandlerParameter(true, "22", Min = "0", Max = "1000000", Step = "0.5", NotOptimized = false)]
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
                if (m_showNodes || edgePoint) // На крайние точки повешу Лейблы
                {
                    InteractivePointActive tmp = new InteractivePointActive();

                    tmp.IsActive = m_showNodes || edgePoint;
                    //tmp.DragableMode = DragableMode.None;
                    //tmp.Geometry = Geometries.Rect;
                    //tmp.Color = Colors.DarkOrange;
                    tmp.Tooltip = String.Format(CultureInfo.InvariantCulture,
                        m_tooltipFormat, k, m_sigma * Constants.PctMult); // "K:{0}; IV:{1:0.00}%"

                    if (edgePoint)
                    {
                        tmp.Label = String.Format(CultureInfo.InvariantCulture,
                            m_labelFormat, m_sigma * Constants.PctMult); // "IV:{0:0.00}%"
                    }

                    ip = tmp;
                }
                else
                    ip = new InteractivePointLight();

                ip.Value = new Point(k, m_sigma);

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
                ConstantFunction spline = new ConstantFunction(m_sigma, futPx, dT);

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
