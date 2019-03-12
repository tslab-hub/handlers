using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // Не стоит навешивать на абстрактные классы атрибуты категорий и описания входов/выходов. Это снижает гибкость управления в перспективе.

    /// <summary>
    /// Базовый класс для торговой стаистистики на барах.
    /// На вход подается Торговая Статистика, на выход идут числа.
    /// </summary>
    public abstract class TradeStatisticsBarsHandler : ITradeStatisticsBarsHandler
    {
        public string VariableId { get; set; }

        public IContext Context { get; set; }

        /// <summary>
        /// \~english Trim value.
        /// \~russian Выбор значения отсечки.
        /// </summary>
        [HelperName("Trim value", Constants.En)]
        [HelperName("Значение отсечки", Constants.Ru)]
        [Description("Выбор значения отсечки.")]
        [HelperDescription("Trim value.", Constants.En)]
        [HandlerParameter(true, Default = "0", Min = "-999999999999999", Max = "999999999999999", Step = "1")]
        public double TrimValue { get; set; }

        /// <summary>
        /// \~english Comparison operator for trimming (greater, greater or equal, less, less or equal, equal, not equal).
        /// \~russian Оператор сравнения для формирования отсечек (больше, больше или равно, меньше, меньше или равно, равно, не равно).
        /// </summary>
        [HelperName("Comparison operator", Constants.En)]
        [HelperName("Режим сравнения отсечки", Constants.Ru)]
        [Description("Оператор сравнения для формирования отсечек (больше, больше или равно, меньше, меньше или равно, равно, не равно).")]
        [HelperDescription("Comparison operator for trimming (greater, greater or equal, less, less or equal, equal, not equal).", Constants.En)]
        [HandlerParameter(true, nameof(ComparisonMode.GreaterOrEqual))]
        public ComparisonMode TrimComparisonMode { get; set; }

        public IList<double> Execute(IBaseTradeStatisticsWithKind tradeStatistics)
        {
            var histograms = tradeStatistics.GetHistograms();
            var tradeHistogramsCache = tradeStatistics.TradeHistogramsCache;
            var barsCount = tradeHistogramsCache.Bars.Count;
            var trimLevel = TrimValue;
            const double DefaultValue = 0;

            if (histograms.Count == 0 || histograms.All(item => item.Bars.Count == 0) || double.IsNaN(trimLevel))
                return new ConstGenBase<double>(barsCount, DefaultValue);

            double[] results = null;
            var runtime = Context?.Runtime;
            var canBeCached = tradeStatistics.HasStaticTimeline && barsCount > 1 && runtime != null;
            string id = null, stateId = null;
            DerivativeTradeStatisticsCacheContext context = null;
            var cachedCount = 0;

            if (canBeCached)
            {
                id = string.Join(".", runtime.TradeName, runtime.IsAgentMode, VariableId);
                stateId = string.Join(".", TrimValue, TrimComparisonMode, tradeStatistics.StateId);
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

            var isInRangeFunc = GetIsInRangeFunc();
            lock (tradeStatistics.Source)
            {
                for (var i = Math.Max(cachedCount, firstBarIndex); i <= lastBarIndex; i++)
                {
                    var bars = tradeStatistics.GetAggregatedHistogramBars(i);
                    results[i] = GetResult(tradeStatistics, bars.Where(item => isInRangeFunc(tradeStatistics, item)));
                }
            }
            for (var i = Math.Max(cachedCount, lastBarIndex + 1); i < barsCount; i++)
                results[i] = DefaultValue;

            if (canBeCached)
                DerivativeTradeStatisticsCache.Instance.SetContext(id, stateId, tradeHistogramsCache, results, context);

            return results;
        }

        private Func<IBaseTradeStatisticsWithKind, ITradeHistogramBar, bool> GetIsInRangeFunc()
        {
            switch (TrimComparisonMode)
            {
                case ComparisonMode.Greater:
                    return (tradeStatistics, bar) => tradeStatistics.GetValue(bar) > TrimValue;
                case ComparisonMode.GreaterOrEqual:
                    return (tradeStatistics, bar) => tradeStatistics.GetValue(bar) >= TrimValue;
                case ComparisonMode.Less:
                    return (tradeStatistics, bar) => tradeStatistics.GetValue(bar) < TrimValue;
                case ComparisonMode.LessOrEqual:
                    return (tradeStatistics, bar) => tradeStatistics.GetValue(bar) <= TrimValue;
                case ComparisonMode.AreEqual:
                    return (tradeStatistics, bar) => tradeStatistics.GetValue(bar) == TrimValue;
                case ComparisonMode.AreNotEqual:
                    return (tradeStatistics, bar) => tradeStatistics.GetValue(bar) != TrimValue;
                default:
                    throw new InvalidEnumArgumentException(nameof(TrimComparisonMode), (int)TrimComparisonMode, TrimComparisonMode.GetType());
            }
        }

        protected abstract double GetResult(IBaseTradeStatisticsWithKind tradeStatistics, IEnumerable<ITradeHistogramBar> bars);
    }
}
