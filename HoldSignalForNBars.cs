using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.Position)]
    [HelperName("Hold signal for N bars", Language = Constants.En)]
    [HelperName("Удерживать сигнал N баров", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.BOOL)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Удерживает сигнал 'Истина' в течение заданного количества баров после его появления.")]
    [HelperDescription("Holds a signal TRUE for some number of bars.", Constants.En)]
    public sealed class HoldSignalForNBars : IOneSourceHandler, IBooleanReturns, IStreamHandler, IBooleanInputs
    {
        /// <summary>
        /// \~english Hold signal for N bars
        /// \~russian Удерживать сигнал в течение N баров
        /// </summary>
        [HelperName("Bars count", Constants.En)]
        [HelperName("Количество баров", Constants.Ru)]
        [Description("Количество баров для продления сигнала")]
        [HelperDescription("Bars count to hold a signal", Constants.En)]
        [HandlerParameter(true, "0", Min = "0", Max = "10", Step = "1", EditorMin = "0")]
        public int NBars { get; set; }

        public IList<bool> Execute(IList<bool> source)
        {
            if (NBars <= 0 || source.Count <= 1 || source.All(item => item) || source.All(item => !item))
                return source;

            var result = new List<bool>(source);
            for (var i = 0; i < source.Count; i++)
            {
                if (source[i])
                {
                    var jMax = Math.Min(i + NBars, source.Count - 1);
                    for (var j = i + 1; j <= jMax; j++)
                        result[j] = true;
                }
            }
            return result;
        }
    }
}
