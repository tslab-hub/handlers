using System;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("OrderBookTotal", Language = Constants.En)]
    [HelperName("OrderBookTotal", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public class OrderBookTotal : IBar2ValueDoubleHandler
    {
        [HandlerParameter]
        public bool Buy { get; set; }
        [HandlerParameter(Min = "0", Default = "0")]
        public int NumberRows { get; set; }

        public double Execute(ISecurity sec, int barNum)
        {
            if (NumberRows > 0)
            {
                var qds = Buy ? sec.GetBuyQueue(0) : sec.GetSellQueue(0);
                if (qds?.Count > 0)
                {
                    var cnt = qds.Count > NumberRows ? NumberRows : qds.Count;
                    double total = 0d;
                    for (int i = 0; i < cnt; i++)
                    {
                        var qd = qds[i];
                        total += qd.Quantity;
                    }
                    return total;
                }
            }

            return 0d;
        }
    }
}
