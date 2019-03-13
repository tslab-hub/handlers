using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Vertical line for CanvasPane (no interaction with user)
    /// \~russian Вертикальная линия без возможности взаимодействия с пользователем
    /// </summary>
    [HandlerCategory(HandlerCategories.Options)]
    [HelperName("Vertical Line", Language = Constants.En)]
    [HelperName("Вертикальная линия", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputType(TemplateTypes.DOUBLE2)]
    //[Obsolete("Недостаточно просто рисовать линию. Надо ещё взаимодействовать с пользователем. Используйте VerticalLine2.")]
    [Description("Вертикальная линия без возможности взаимодействия с пользователем")]
    [HelperDescription("Vertical line for CanvasPane (no interaction with user)", Constants.En)]
    public class VerticalLine : IContextUses, IStreamHandler
    {
        private IContext m_context;
        private double m_sigmaLow = 0.10, m_sigmaHigh = 0.50;

        public IContext Context
        {
            get { return m_context; }
            set { m_context = value; }
        }

        #region Parameters
        /// <summary>
        /// \~english Low level of this marker (in percents)
        /// \~russian Нижний уровень маркера (в процентах)
        /// </summary>
        [HelperName("Sigma Low, %", Constants.En)]
        [HelperName("Нижний уровень, %", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Нижний уровень маркера (в процентах)")]
        [HelperDescription("Low level of this marker (in percents)", Language = Constants.En)]
        [HandlerParameter(true, "10", Min = "0", Max = "10000000", Step = "0.01", NotOptimized = true)]
        public double SigmaLowPct
        {
            get { return m_sigmaLow * Constants.PctMult; }
            set
            {
                if (value > 0)
                    m_sigmaLow = value / Constants.PctMult;
            }
        }

        /// <summary>
        /// \~english High level of this marker (in percents)
        /// \~russian Верхний уровень маркера (в процентах)
        /// </summary>
        [HelperName("Sigma High, %", Constants.En)]
        [HelperName("Верхний уровень, %", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Верхний уровень маркера (в процентах)")]
        [HelperDescription("High level of this marker (in percents)", Language = Constants.En)]
        [HandlerParameter(true, "50", Min = "0", Max = "10000000", Step = "0.01", NotOptimized = true)]
        public double SigmaHighPct
        {
            get { return m_sigmaHigh * Constants.PctMult; }
            set
            {
                if (value > 0)
                    m_sigmaHigh = value / Constants.PctMult;
            }
        }
        #endregion Parameters

        public IList<Double2> Execute(IList<double> prices)
        {
            List<Double2> res = new List<Double2>();

            if (prices.Count <= 0)
                return res;

            double f = prices[prices.Count - 1];
            res.Add(new Double2(f, m_sigmaLow));
            res.Add(new Double2(f, m_sigmaHigh));

            return res;
        }
    }
}
