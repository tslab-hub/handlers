using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TSLab.DataSource;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    public sealed class SpecifiedQuantityPriceAtTradeHandler : ISpecifiedQuantityPriceAtTradeHandler
    {
        public IContext Context { get; set; }

        [HandlerParameter(true, "0", Min = "0", Max = "2147483647", Step = "1", EditorMin = "0", EditorMax = "2147483647")]
        public double Quantity { get; set; }

        [HandlerParameter(true, nameof(SpecifiedTradeDirection.Any))]
        public SpecifiedTradeDirection Direction { get; set; }

        public IList<double> Execute(ISecurity security)
        {
            Func<ITrade, bool> isValidTradeFunc;
            if (Direction == SpecifiedTradeDirection.Any)
                isValidTradeFunc = IsValidAnyTrade;
            else if (Direction == SpecifiedTradeDirection.Buy)
                isValidTradeFunc = IsValidBuyTrade;
            else if (Direction == SpecifiedTradeDirection.Sell)
                isValidTradeFunc = IsValidSellTrade;
            else
                throw new InvalidEnumArgumentException(nameof(Direction), (int)Direction, typeof(SpecifiedTradeDirection));

            var barsCount = Context.BarsCount;
            var results = Context.GetArray<double>(barsCount);
            var tradesCache = Context.GetTradesCache(security);
            var price = 0D;

            for (var i = 0; i < barsCount; i++)
            {
                var trade = tradesCache.GetTrades(i).LastOrDefault(isValidTradeFunc);
                if (trade != null)
                    price = trade.Price;

                results[i] = price;
            }
            return results;
        }

        private bool IsValidAnyTrade(ITrade trade)
        {
            return trade.Quantity > Quantity;
        }

        private bool IsValidBuyTrade(ITrade trade)
        {
            return trade.Direction == TradeDirection.Buy && trade.Quantity > Quantity;
        }

        private bool IsValidSellTrade(ITrade trade)
        {
            return trade.Direction == TradeDirection.Sell && trade.Quantity > Quantity;
        }
    }
}
