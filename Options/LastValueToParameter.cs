using System;
using System.ComponentModel;

using TSLab.Script.Optimization;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Convert last value in series to parameter
    /// \~russian Преобразовать последнее значение индикатора в параметр
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsIndicators)]
    [HelperName("Last Value", Language = Constants.En)]
    [HelperName("Последнее значение", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Преобразовать последнее значение индикатора в параметр")]
    [HelperDescription("Convert last value in series to parameter", Constants.En)]
    public class LastValueToParameter : BaseContextHandler, IValuesHandlerWithNumber
    {
        private OptimProperty m_result = new OptimProperty(0, true, double.MinValue, double.MaxValue, 1.0, 4);

        #region Parameters
        /// <summary>
        /// Свойство используется только для отображения на UI
        /// </summary>
        [HelperName("Display value", Constants.En)]
        [HelperName("Показываемое значение", Constants.Ru)]
        [Description("Показываемое значение (для отображения в интерфейсе агента)")]
        [HelperDescription("Display value (just to show it on ControlPane)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0", IsCalculable = true)]
        public OptimProperty Result
        {
            get { return m_result; }
            set { m_result = value; }
        }

        /// <summary>
        /// Свойство используется только для связывания с другими блоком LinkedParameters
        /// </summary>
        //[ReadOnly(true)]
        [HelperName("Last value", Constants.En)]
        [HelperName("Последнее значение", Constants.Ru)]
        [Description("Последнее значение")]
        [HelperDescription("Last value", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "0", IsCalculable = false)]
        public double LastValue
        {
            get
            {
                if (m_result != null)
                    return m_result.Value;
                else
                    return Constants.NaN;
            }
            set
            {
            }
        }

        ///// <summary>
        ///// \~english Display units (hundreds, thousands, as is)
        ///// \~russian Единицы отображения (сотни, тысячи, как есть)
        ///// </summary>
        //[HelperName("Display Units", Constants.En)]
        //[HelperName("Единицы отображения", Constants.Ru)]
        //[Description("Единицы отображения (сотни, тысячи, как есть)")]
        //[HelperDescription("Display units (hundreds, thousands, as is)", Constants.En)]
        //[HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "AsIs")]
        //public FixedValueMode DisplayUnits
        //{
        //    get { return m_valueMode; }
        //    set { m_valueMode = value; }
        //}
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.DOUBLE, чтобы подключаться к источнику
        /// </summary>
        public void Execute(double source, int barNum)
        {
            int len = ContextBarsCount;
            if (len - 1 <= barNum)
            {
                m_result.Value = source;
            }
        }
    }
}
