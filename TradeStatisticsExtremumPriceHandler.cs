using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Trade Statistics Extremum Price", Language = Constants.En)]
    [HelperName("Экстремальная цена торговой статистики", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.TRADE_STATISTICS | TemplateTypes.LAST_CONTRACTS_TRADE_STATISTICS)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Блок 'Экстремальная цена торговой статистики' показывает цену, при которой было достигнуто максимальное значение гистограммы.")]
    [HelperDescription("", Constants.En)]
    public sealed class TradeStatisticsExtremumPriceHandler : TradeStatisticsBaseExtremumPriceHandler, ITradeStatisticsExtremumPriceHandler
    {
        /// <summary>
        /// \~english Trim value mode (none, relative, absolute).
        /// \~russian Режим значения отсечки (никакая, относительная, абсолютная).
        /// </summary>
        [HelperName("Trim value mode", Constants.En)]
        [HelperName("Режим значения отсечки", Constants.Ru)]
        [Description("Режим значения отсечки (никакая, относительная, абсолютная).")]
        [HelperDescription("", Constants.En)]
        [HandlerParameter(true, nameof(TrimValueMode.None))]
        public TrimValueMode TrimValueMode { get; set; }

        /// <summary>
        /// \~english Trim value.
        /// \~russian Выбор значения отсечки.
        /// </summary>
        [HelperName("Trim value", Constants.En)]
        [HelperName("Значение отсечки", Constants.Ru)]
        [Description("Выбор значения отсечки.")]
        [HelperDescription("Trim value.", Constants.En)]
        [HandlerParameter(true, "0", Min = "0", Max = "100", Step = "1", EditorMin = "0")]
        public double TrimValue { get; set; }

        public override IList<double> Execute(IBaseTradeStatisticsWithKind tradeStatistics)
        {
            var histograms = tradeStatistics.GetHistograms();
            var tradeHistogramsCache = tradeStatistics.TradeHistogramsCache;
            var barsCount = tradeHistogramsCache.Bars.Count;
            const double DefaultValue = double.NaN;

            var trimValue = TrimValue;
            if (histograms.Count == 0 ||
                histograms.All(item => item.Bars.Count == 0) ||
                (TrimValueMode == TrimValueMode.Relative && (double.IsNaN(trimValue) || trimValue < 0 || trimValue > 100)) ||
                (TrimValueMode == TrimValueMode.Absolute && (double.IsNaN(trimValue) || trimValue < 0)))
            {
                return new ConstGenBase<double>(barsCount, DefaultValue);
            }
            tradeStatistics.GetHistogramsBarIndexes(out var firstBarIndex, out var lastBarIndex);

            switch (TrimValueMode)
            {
                case TrimValueMode.None:
                    trimValue = 0;
                    break;
                case TrimValueMode.Relative:
                    if (trimValue > 0)
                    {
                        var lastPrice = DefaultValue;
                        var maxValue = double.NegativeInfinity;

                        lock (tradeStatistics.Source)
                        {
                            for (var i = firstBarIndex; i <= lastBarIndex; i++)
                            {
                                var extremum = GetExtremum(tradeStatistics, i, ref lastPrice);
                                if (extremum.Bar != null && maxValue < extremum.Value)
                                    maxValue = extremum.Value;
                            }
                        }
                        trimValue = maxValue * trimValue / 100;
                    }
                    else
                        trimValue = 0;

                    break;
                case TrimValueMode.Absolute:
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(TrimValueMode), (int)TrimValueMode, TrimValueMode.GetType());
            }
            double[] results = null;
            var runtime = Context?.Runtime;
            var canBeCached = tradeStatistics.HasStaticTimeline && barsCount > 1 && runtime != null;
            string id = null, stateId = null;
            DerivativeTradeStatisticsCacheContext context = null;
            var cachedCount = 0;
            double lastResult1 = DefaultValue, lastResult2 = DefaultValue;

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

                    lastResult1 = lastResult2 = results[cachedCount - 1];
                }
                else
                    results = new double[barsCount];
            }
            else
                results = Context?.GetArray<double>(barsCount) ?? new double[barsCount];

            for (var i = cachedCount; i < firstBarIndex; i++)
                results[i] = lastResult2;

            lock (tradeStatistics.Source)
            {
                for (var i = Math.Max(cachedCount, firstBarIndex); i <= lastBarIndex; i++)
                {
                    var extremum = GetExtremum(tradeStatistics, i, ref lastResult1);
                    if (extremum.Bar != null && extremum.Value >= trimValue)
                        lastResult2 = lastResult1;

                    results[i] = lastResult2;
                }
            }
            for (var i = Math.Max(cachedCount, lastBarIndex + 1); i < barsCount; i++)
                results[i] = lastResult2;

            if (canBeCached)
                DerivativeTradeStatisticsCache.Instance.SetContext(id, stateId, tradeHistogramsCache, results, context);

            return results;
        }

        protected override string GetParametersStateId()
        {
            return string.Join(".", base.GetParametersStateId(), TrimValueMode, TrimValue);
        }
    }
}
