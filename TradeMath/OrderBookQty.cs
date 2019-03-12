using System;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("OrderBookQuantity", Language = Constants.En)]
    [HelperName("OrderBookQuantity", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public sealed class OrderBookQty : IBar2ValueDoubleHandler
    {
        [HandlerParameter]
        public bool Buy { get; set; }
        [HandlerParameter(Min = "0", Default= "0")]
        public int Index { get; set; }

        public double Execute(ISecurity sec, int barNum)
        {
            var qds = Buy ? sec.GetBuyQueue(0) : sec.GetSellQueue(0);
            if (qds?.Count > 0 && Index >= 0 && qds.Count > Index)
            {
                var qd = qds[Index];
                return qd?.Quantity ?? 0.0;
            }

            return 0d;
        }
    }
}
