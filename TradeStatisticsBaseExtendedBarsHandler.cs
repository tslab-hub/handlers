using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace TSLab.Script.Handlers
{
    //[HandlerCategory(HandlerCategories.ClusterAnalysis)]
    //[Input(0, TemplateTypes.TRADE_STATISTICS | TemplateTypes.LAST_CONTRACTS_TRADE_STATISTICS)]
    // Не стоит навешивать на абстрактные классы атрибуты категорий и описания входов/выходов. Это снижает гибкость управления в перспективе.
    public abstract class TradeStatisticsBaseExtendedBarsHandler : INeedVariableId, IContextUses
    {
        protected struct TrimContext
        {
            public TrimContext(bool useTrimValue, double trimValue, ComparisonMode trimComparisonMode)
            {
                UseTrimValue = useTrimValue;
                TrimValue = trimValue;
                TrimComparisonMode = trimComparisonMode;
            }

            public bool UseTrimValue { get; }

            public double TrimValue { get; }

            public ComparisonMode TrimComparisonMode { get; }
        }

        public string VariableId { get; set; }

        public IContext Context { get; set; }

        protected IList<double> Execute(
            IBaseTradeStatisticsWithKind tradeStatistics,
            TrimContext tradesCountTrimContext,
            TrimContext quantityTrimContext,
            TrimContext askQuantityTrimContext,
            TrimContext bidQuantityTrimContext,
            TrimContext deltaAskBidQuantityTrimContext,
            TrimContext relativeDeltaAskBidQuantityPercentTrimContext)
        {
            var histograms = tradeStatistics.GetHistograms();
            var tradeHistogramsCache = tradeStatistics.TradeHistogramsCache;
            var barsCount = tradeHistogramsCache.Bars.Count;
            const double DefaultValue = double.NaN;

            if (histograms.Count == 0 ||
                histograms.All(item => item.Bars.Count == 0) ||
                IsInvalid(tradesCountTrimContext) ||
                IsInvalid(quantityTrimContext) ||
                IsInvalid(askQuantityTrimContext) ||
                IsInvalid(bidQuantityTrimContext) ||
                IsInvalid(deltaAskBidQuantityTrimContext) ||
                IsInvalid(relativeDeltaAskBidQuantityPercentTrimContext))
                {
                    return new ConstGenBase<double>(barsCount, DefaultValue);
            }
            var isInRangeFuncs = new List<Func<ITradeHistogramBar, bool>>();
            if (tradesCountTrimContext.UseTrimValue)
                isInRangeFuncs.Add(GetIsInRangeFunc(tradeStatistics, TradeStatisticsKind.TradesCount, tradesCountTrimContext));

            if (quantityTrimContext.UseTrimValue)
                isInRangeFuncs.Add(GetIsInRangeFunc(tradeStatistics, TradeStatisticsKind.Quantity, quantityTrimContext));

            if (askQuantityTrimContext.UseTrimValue)
                isInRangeFuncs.Add(GetIsInRangeFunc(tradeStatistics, TradeStatisticsKind.AskQuantity, askQuantityTrimContext));

            if (bidQuantityTrimContext.UseTrimValue)
                isInRangeFuncs.Add(GetIsInRangeFunc(tradeStatistics, TradeStatisticsKind.BidQuantity, bidQuantityTrimContext));

            if (deltaAskBidQuantityTrimContext.UseTrimValue)
                isInRangeFuncs.Add(GetIsInRangeFunc(tradeStatistics, TradeStatisticsKind.DeltaAskBidQuantity, deltaAskBidQuantityTrimContext));

            if (relativeDeltaAskBidQuantityPercentTrimContext.UseTrimValue)
                isInRangeFuncs.Add(GetIsInRangeFunc(tradeStatistics, TradeStatisticsKind.RelativeDeltaAskBidQuantityPercent, relativeDeltaAskBidQuantityPercentTrimContext));

            double[] results = null;
            var runtime = Context?.Runtime;
            var canBeCached = tradeStatistics.HasStaticTimeline && barsCount > 1 && runtime != null;
            string id = null, stateId = null;
            DerivativeTradeStatisticsCacheContext context = null;
            var cachedCount = 0;

            if (canBeCached)
            {
                id = string.Join(".", runtime.TradeName, runtime.IsAgentMode, VariableId);
                stateId = GetParametersStateId() + "." + tradeStatistics.StateId;
                context = DerivativeTradeStatisticsCache.Instance.GetContext(id, stateId, tradeHistogramsCache);

                if (context != null)
                {
                    var cachedResults = context.Values;
                    cachedCount = Math.Min(cachedResults.Length, barsCount) - 1;

                    if (cachedResults.Length == barsCount)
                        results = cachedResults;
                    else
                        Buffer.BlockCopy(cachedResults, 0, results = new double[barsCount], 0, cachedCount * sizeof(double));
                }
                else
                    results = new double[barsCount];
            }
            else
                results = Context?.GetArray<double>(barsCount) ?? new double[barsCount];

            tradeStatistics.GetHistogramsBarIndexes(out var firstBarIndex, out var lastBarIndex);
            for (var i = cachedCount; i < firstBarIndex; i++)
                results[i] = DefaultValue;

            lock (tradeStatistics.Source)
            {
                for (var i = Math.Max(cachedCount, firstBarIndex); i <= lastBarIndex; i++)
                {
                    var bars = tradeStatistics.GetAggregatedHistogramBars(i);
                    var selectedBars = isInRangeFuncs.Count > 0
                        ? bars.Where(bar => isInRangeFuncs.All(item => item(bar)))
                        : bars;

                    results[i] = GetResult(tradeStatistics, selectedBars);
                }
            }
            for (var i = Math.Max(cachedCount, lastBarIndex + 1); i < barsCount; i++)
                results[i] = DefaultValue;

            if (canBeCached)
                DerivativeTradeStatisticsCache.Instance.SetContext(id, stateId, tradeHistogramsCache, results, context);

            return results;
        }

        private static bool IsInvalid(TrimContext trimContext)
        {
            return trimContext.UseTrimValue && double.IsNaN(trimContext.TrimValue);
        }

        private static Func<ITradeHistogramBar, bool> GetIsInRangeFunc(IBaseTradeStatisticsWithKind tradeStatistics, TradeStatisticsKind tradeStatisticsKind, TrimContext trimContext)
        {
            var trimComparisonMode = trimContext.TrimComparisonMode;
            switch (trimComparisonMode)
            {
                case ComparisonMode.Greater:
                    return bar => tradeStatistics.GetValue(bar, tradeStatisticsKind) > trimContext.TrimValue;
                case ComparisonMode.GreaterOrEqual:
                    return bar => tradeStatistics.GetValue(bar, tradeStatisticsKind) >= trimContext.TrimValue;
                case ComparisonMode.Less:
                    return bar => tradeStatistics.GetValue(bar, tradeStatisticsKind) < trimContext.TrimValue;
                case ComparisonMode.LessOrEqual:
                    return bar => tradeStatistics.GetValue(bar, tradeStatisticsKind) <= trimContext.TrimValue;
                case ComparisonMode.AreEqual:
                    return bar => tradeStatistics.GetValue(bar, tradeStatisticsKind) == trimContext.TrimValue;
                case ComparisonMode.AreNotEqual:
                    return bar => tradeStatistics.GetValue(bar, tradeStatisticsKind) != trimContext.TrimValue;
                default:
                    throw new InvalidEnumArgumentException(nameof(trimComparisonMode), (int)trimComparisonMode, trimComparisonMode.GetType());
            }
        }

        protected abstract string GetParametersStateId();

        protected abstract double GetResult(IBaseTradeStatisticsWithKind tradeStatistics, IEnumerable<ITradeHistogramBar> bars);
    }
}
