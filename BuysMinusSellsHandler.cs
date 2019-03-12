using System.ComponentModel;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.VolumeAnalysis)]
    [HelperName("Buys Minus Sells", Language = Constants.En)]
    [HelperName("Покупки минус продажи", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает разницу характеристик (количество или суммарный объем) сделок на покупку и на продажу")]
    [HelperDescription("A handler calculates statistics difference (amount or total volume) of a long and short trades", Constants.En)]
    public sealed class BuysMinusSellsHandler : BuysSellsHandler
    {
        protected override double GetValue(ICachedTradeHistogram histogram)
        {
            return histogram.DeltaAskBidQuantity;
        }

        protected override int GetCount(ICachedTradeHistogram histogram)
        {
            return histogram.AskTradesCount - histogram.BidTradesCount;
        }
    }
}
