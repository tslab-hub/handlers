using System.ComponentModel;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.VolumeAnalysis)]
    [HelperName("Sells", Language = Constants.En)]
    [HelperName("Продажи", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает характеристики (количество или суммарный объем) сделок на продажу")]
    [HelperDescription("A handler calculates statistics (amount or total volume) of a short trades", Constants.En)]
    public sealed class SellsHandler : BuysSellsHandler
    {
        protected override double GetValue(ICachedTradeHistogram histogram)
        {
            return histogram.BidQuantity;
        }

        protected override int GetCount(ICachedTradeHistogram histogram)
        {
            return histogram.BidTradesCount;
        }
    }
}
