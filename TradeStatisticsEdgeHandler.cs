using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // Не стоит навешивать на абстрактные классы атрибуты категорий и описания входов/выходов. Это снижает гибкость управления в перспективе.
    public abstract class TradeStatisticsEdgeHandler : ITradeStatisticsEdgeHandler
    {
        public string VariableId { get; set; }

        public IContext Context { get; set; }

        /// <summary>
        /// \~english Trim level (as percents).
        /// \~russian Позволяет установить значение отсечки в процентах.
        /// </summary>
        [HelperName("Trim level, %", Constants.En)]
        [HelperName("Уровень отсечки, %", Constants.Ru)]
        [Description("Позволяет установить значение отсечки в процентах.")]
        [HelperDescription("Trim level (as percents).", Constants.En)]
        [HandlerParameter(true, "0", Min = "0", Max = "100", Step = "1", EditorMin = "0", EditorMax = "100")]
        public double TrimLevelPercent { get; set; }

        public IList<double> Execute(IBaseTradeStatisticsWithKind tradeStatistics)
        {
            var histograms = tradeStatistics.GetHistograms();
            var tradeHistogramsCache = tradeStatistics.TradeHistogramsCache;
            var barsCount = tradeHistogramsCache.Bars.Count;
            var trimLevelPercent = TrimLevelPercent;
            const double DefaultValue = double.NaN;

            if (histograms.Count == 0 || histograms.All(item => item.Bars.Count == 0) || trimLevelPercent < 0 || trimLevelPercent > 100 || double.IsNaN(trimLevelPercent))
                return new ConstGenBase<double>(barsCount, DefaultValue);

            double[] results = null;
            var runtime = Context?.Runtime;
            var canBeCached = tradeStatistics.HasStaticTimeline && barsCount > 1 && runtime != null;
            string id = null, stateId = null;
            DerivativeTradeStatisticsCacheContext context = null;
            var cachedCount = 0;
            var lastResult = DefaultValue;

            if (canBeCached)
            {
                id = string.Join(".", runtime.TradeName, runtime.IsAgentMode, VariableId);
                stateId = TrimLevelPercent + "." + tradeStatistics.StateId;
                context = DerivativeTradeStatisticsCache.Instance.GetContext(id, stateId, tradeHistogramsCache);

                if (context != null)
                {
                    var cachedResults = context.Values;
                    cachedCount = Math.Min(cachedResults.Length, barsCount) - 1;

                    if (cachedResults.Length == barsCount)
                        results = cachedResults;
                    else
                        Buffer.BlockCopy(cachedResults, 0, results = new double[barsCount], 0, cachedCount * sizeof(double));

                    lastResult = results[cachedCount - 1];
                }
                else
                    results = new double[barsCount];
            }
            else
                results = Context?.GetArray<double>(barsCount) ?? new double[barsCount];

            tradeStatistics.GetHistogramsBarIndexes(out var firstBarIndex, out var lastBarIndex);
            for (var i = cachedCount; i < firstBarIndex; i++)
                results[i] = lastResult;

            lock (tradeStatistics.Source)
                for (var i = Math.Max(cachedCount, firstBarIndex); i <= lastBarIndex; i++)
                    results[i] = lastResult = GetPrice(tradeStatistics, i, lastResult);

            for (var i = Math.Max(cachedCount, lastBarIndex + 1); i < barsCount; i++)
                results[i] = lastResult;

            if (canBeCached)
                DerivativeTradeStatisticsCache.Instance.SetContext(id, stateId, tradeHistogramsCache, results, context);

            return results;
        }

        private double GetPrice(IBaseTradeStatisticsWithKind tradeStatistics, int barIndex, double lastPrice)
        {
            var bars = tradeStatistics.GetAggregatedHistogramBars(barIndex);
            if (bars.Count == 0)
                return lastPrice;

            var allValuesSum = bars.Sum(item => Math.Abs(tradeStatistics.GetValue(item)));
            if (allValuesSum == 0)
                return lastPrice;

            var trimLevelPercent = TrimLevelPercent;
            if (trimLevelPercent == 0)
                return GetFirstPrice(bars);

            if (trimLevelPercent == 100)
                return GetLastPrice(bars);

            var edgeValuesSum = allValuesSum * trimLevelPercent / 100;
            foreach (var bar in GetOrderedBars(bars))
            {
                var value = Math.Abs(tradeStatistics.GetValue(bar));
                if (edgeValuesSum <= value)
                {
                    var result = GetPrice(bar, edgeValuesSum / value);
                    return result;
                }
                edgeValuesSum -= value;
            }
            return GetLastPrice(bars);
        }

        protected abstract double GetFirstPrice(IReadOnlyList<ITradeHistogramBar> bars);

        protected abstract double GetLastPrice(IReadOnlyList<ITradeHistogramBar> bars);

        protected abstract IEnumerable<ITradeHistogramBar> GetOrderedBars(IEnumerable<ITradeHistogramBar> bar);

        protected abstract double GetPrice(ITradeHistogramBar bar, double coefficient);
    }
}
