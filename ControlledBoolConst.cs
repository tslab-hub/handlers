using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Handlers.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Controlled Boolean Constant", Language = Constants.En)]
    [HelperName("Управляемая логическая константа", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.BOOL)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Управляемая логическая константа (переключатель). При поступлении на вход значения 'Истина' данный блок выдает значение из поля 'Значение', при поступлении на вход значения 'Ложь' используется 'Значение по умолчанию'.")]
    [HelperDescription("Controlled boolean constant (switch). If input is TRUE the result is taken from a field 'Value', otherwise the result is taken from a field 'Default value'.", Constants.En)]
    public sealed class ControlledBoolConst : IOneSourceHandler, IBooleanReturns, IStreamHandler, IValuesHandler, IBooleanInputs, IContextUses
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
        public bool Value { get; set; }

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

        public IList<bool> Execute(IList<bool> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (source.Count == 0)
                return EmptyArrays.Bool;

            var firstValue = source[0];
            if (source.All(item => item == firstValue))
                return new ConstList<bool>(source.Count, firstValue ? Value : DefaultValue);

            var result = Context?.GetArray<bool>(source.Count) ?? new bool[source.Count];
            for (var i = 0; i < result.Length; i++)
                result[i] = source[i] ? Value : DefaultValue;

            return result;
        }

        public bool Execute(bool value, int index)
        {
            return value ? Value : DefaultValue;
        }
    }
}
