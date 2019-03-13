using System;
using System.ComponentModel;

using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Base asset (bar handler)
    /// \~russian Базовый актив (побарный обработчик)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Base asset", Language = Constants.En)]
    [HelperName("Базовый актив", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION | TemplateTypes.OPTION_SERIES)]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Базовый актив (побарный обработчик)")]
    [HelperDescription("Base asset (bar handler)", Constants.En)]
    public class OptionBase2 : IValuesHandlerWithNumber, ISecurityReturns
    {
        public ISecurity Execute(IOption opt, int barNum)
        {
            return opt.UnderlyingAsset;
        }

        public ISecurity Execute(IOptionSeries opt, int barNum)
        {
            return opt.UnderlyingAsset;
        }
    }
}
