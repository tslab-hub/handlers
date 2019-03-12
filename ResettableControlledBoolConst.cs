using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Handlers.Options;
using TSLab.Script.Optimization;
using TSLab.Utils;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Resettable Controlled Boolean Constant", Language = Constants.En)]
    [HelperName("Сбрасываемая управляемая логическая константа", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.BOOL)]
    [Input(1, TemplateTypes.BOOL)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Сбрасываемая управляемая логическая константа (переключатель). " +
        "При поступлении на вход значения 'Истина' данный блок выдает значение из поля 'Значение', при поступлении на вход значения 'Ложь' используется 'Значение по умолчанию'. " +
        "Второй вход определяет чему равно 'Значение'. Если в нем больше истин, то 'Значение' становится равно 'Значению по умолчанию'.")]
    [HelperDescription("Resettable controlled boolean constant (switch). " +
        "If input is TRUE the result is taken from a field 'Value', otherwise the result is taken from a field 'Default value'. " +
        "Second input determines 'Value'. If it contains more TRUE, then 'Value' is set to 'Default value'.", Constants.En)]
    public sealed class ResettableControlledBoolConst : ITwoSourcesHandler, IBooleanReturns, IStreamHandler, IValuesHandlerWithNumber, IBooleanInputs, IContextUses
    {
        public IContext Context { get; set; }

        /// <summary>
        /// \~english A value to return as output of a handler when input is true
        /// \~russian Значение на выходе блока, если на вход подать 'Истина'
        /// </summary>
        [HelperName("Value", Constants.En)]
        [HelperName("Значение", Constants.Ru)]
        [Description("Значение на выходе блока, если на вход подать 'Истина'")]
        [HelperDescription("A value to return as output of a handler when input is true", Constants.En)]
        [HandlerParameter]
        public BoolOptimProperty Value { get; set; }

        /// <summary>
        /// \~english A value to return as output of a handler when input is false
        /// \~russian Значение на выходе блока, если на вход подать 'Ложь'
        /// </summary>
        [HelperName("Default value", Constants.En)]
        [HelperName("Значение по умолчанию", Constants.Ru)]
        [Description("Значение на выходе блока, если на вход подать 'Ложь'")]
        [HelperDescription("A value to return as output of a handler when input is false", Constants.En)]
        [HandlerParameter(NotOptimized = true)]
        public bool DefaultValue { get; set; }

        public IList<bool> Execute(IList<bool> source, IList<bool> resetValues)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (resetValues == null)
                throw new ArgumentNullException(nameof(resetValues));

            var count = Math.Min(source.Count, resetValues.Count);
            if (count == 0)
                return EmptyArrays.Bool;

            if (resetValues[count - 1])
                Value.Value = DefaultValue;

            var firstValue = source[0];
            if (source.Take(count).All(item => item == firstValue))
                return new ConstList<bool>(count, firstValue ? Value : DefaultValue);

            var result = Context?.GetArray<bool>(count) ?? new bool[count];
            for (var i = 0; i < result.Length; i++)
                result[i] = source[i] ? Value : DefaultValue;

            return result;
        }

        public bool Execute(bool source, bool resetValue, int number)
        {
            if (resetValue && number == Context.BarsCount - (Context.IsLastBarUsed ? 1 : 2))
                Value.Value = DefaultValue;

            var result = source ? Value : DefaultValue;
            return result;
        }
    }
}
