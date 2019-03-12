using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Trade Statistics Price Value", Language = Constants.En)]
    [HelperName("Значение цены торговой статистики", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.TRADE_STATISTICS | TemplateTypes.LAST_CONTRACTS_TRADE_STATISTICS)]
    [Input(1, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Блок 'Значение цены торговой статистики' создает гистограмму на основе выбранной цены. Используется совместно с блоком 'Торговая статистика'.")]
    [HelperDescription("", Constants.En)]
    public sealed class TradeStatisticsPriceValueHandler : ITradeStatisticsPriceValueHandler
    {
        public string VariableId { get; set; }

        public IContext Context { get; set; }

        public IList<double> Execute(IBaseTradeStatisticsWithKind tradeStatistics, IList<double> prices)
        {
            var histograms = tradeStatistics.GetHistograms();
            var tradeHistogramsCache = tradeStatistics.TradeHistogramsCache;
            const double DefaultValue = 0;

            var pricesCount = prices.Count;
            if (histograms.Count == 0 || histograms.All(item => item.Bars.Count == 0) || pricesCount == 0)
                return new ConstGenBase<double>(prices.Count, DefaultValue);

            double[] results = null;
            var runtime = Context?.Runtime;
            var canBeCached = tradeStatistics.HasStaticTimeline && tradeHistogramsCache.Bars.Count > 1 && pricesCount > 1 && runtime != null;
            string id = null, stateId = null;
            DerivativeTradeStatisticsCacheContext context = null;
            var cachedCount = 0;

            if (canBeCached)
            {
                id = string.Join(".", runtime.TradeName, runtime.IsAgentMode, VariableId);
                stateId = tradeStatistics.StateId;
                context = DerivativeTradeStatisticsCache.Instance.GetContext(id, stateId, tradeHistogramsCache);

                if (context != null)
                {
                    var cachedResults = context.Values;
                    cachedCount = Math.Min(cachedResults.Length, pricesCount) - 1;

                    if (cachedResults.Length == pricesCount)
                        results = cachedResults;
                    else
                        Buffer.BlockCopy(cachedResults, 0, results = new double[pricesCount], 0, cachedCount * sizeof(double));
                }
                else
                    results = new double[pricesCount];
            }
            else
                results = Context?.GetArray<double>(pricesCount) ?? new double[pricesCount];

            tradeStatistics.GetHistogramsBarIndexes(out var firstBarIndex, out var lastBarIndex);
            var iMax = Math.Min(firstBarIndex, pricesCount);

            for (var i = cachedCount; i < iMax; i++)
                results[i] = DefaultValue;

            lock (tradeStatistics.Source)
            {
                iMax = Math.Min(lastBarIndex, pricesCount - 1);
                for (var i = Math.Max(cachedCount, firstBarIndex); i <= iMax; i++)
                {
                    var histogramBars = tradeStatistics.GetAggregatedHistogramBars(i);
                    if (histogramBars.Count > 1)
                    {
                        var price = prices[i];
                        var lowPrice = histogramBars[0].LowPrice;
                        var index = (int)((price - lowPrice) / tradeStatistics.PriceStep);

                        if (price >= lowPrice + tradeStatistics.PriceStep * (index + 1)) // PROD-5600
                            index++; // имеем погрешность примерно в 1e13  при делении, лечим проверкой

                        results[i] = index >= 0 && index < histogramBars.Count ? tradeStatistics.GetValue(histogramBars[index]) : DefaultValue;
                    }
                    else if (histogramBars.Count == 1)
                    {
                        var price = prices[i];
                        var histogramBar = histogramBars[0];
                        results[i] = price >= histogramBar.LowPrice && price < histogramBar.HighPrice ? tradeStatistics.GetValue(histogramBar) : DefaultValue;
                    }
                    else
                        results[i] = DefaultValue;
                }
            }
            for (var i = Math.Max(cachedCount, lastBarIndex + 1); i < pricesCount; i++)
                results[i] = DefaultValue;

            if (canBeCached)
                DerivativeTradeStatisticsCache.Instance.SetContext(id, stateId, tradeHistogramsCache, results, context);

            return results;
        }
    }
}
