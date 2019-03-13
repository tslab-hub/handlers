using System;
using System.ComponentModel;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Linear transform (a*x+b)
    /// \~russian Линейное преобразование (a*x+b)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Linear transform (a*x+b)", Language = Constants.En)]
    [HelperName("Линейное преобразование (a*x+b)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Линейное преобразование (a*x+b)")]
    [HelperDescription("Linear transform (a*x+b)", Constants.En)]
    public class LinearTransform : IValuesHandlerWithNumber
    {
        private double m_add = 0;
        private double m_multiplier = 1;

        #region Parameters
        /// <summary>
        /// \~english Summand
        /// \~russian Слагаемое
        /// </summary>
        [HelperName("Summand", Constants.En)]
        [HelperName("Слагаемое", Constants.Ru)]
        [Description("Слагаемое")]
        [HelperDescription("Summand", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0.0", Min = "-5000000.0", Max = "5000000.0", Step = "1")]
        public double Add
        {
            get { return m_add; }
            set { m_add = value; }
        }

        /// <summary>
        /// \~english Multiplier
        /// \~russian Множитель
        /// </summary>
        [HelperName("Multiplier", Constants.En)]
        [HelperName("Множитель", Constants.Ru)]
        [Description("Множитель")]
        [HelperDescription("Multiplier", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "1.0", Min = "-5000000.0", Max = "5000000.0", Step = "1")]
        public double Mult
        {
            get { return m_multiplier; }
            set { m_multiplier = value; }
        }
        #endregion Parameters

        public double Execute(double val, int barNum)
        {
            double res = m_multiplier * val + m_add;
            return res;
        }
    }
}
