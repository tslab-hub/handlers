using System;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.VolumeAnalysis)]
    [HelperName("Volume for period", Language = Constants.En)]
    [HelperName("Объём за период", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Объём за период (суммарный или средний)")]
    [HelperDescription("Volume for period (total or average)", Constants.En)]
    public sealed class VolumeForPeriodHandler : ValueForPeriodHandler
    {
        protected override double GetValue(ISecurity security, int barIndex)
        {
            return security.Bars[barIndex].Volume;
        }
    }
}
