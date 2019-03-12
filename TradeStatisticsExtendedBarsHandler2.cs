using System.Collections.Generic;

namespace TSLab.Script.Handlers
{
    // Не стоит навешивать на абстрактные классы атрибуты категорий и описания входов/выходов
    public abstract class TradeStatisticsExtendedBarsHandler2 : TradeStatisticsBaseExtendedBarsHandler, ITradeStatisticsExtendedBarsHandler2
    {
        [HandlerParameter(true, NotOptimized = true)]
        public bool UseTrimTradesCount { get; set; }

        [HandlerParameter(true, "0", Min = "0", Max = "2147483647", Step = "1", EditorMin = "0")]
        public int TrimTradesCount { get; set; }

        [HandlerParameter(true, nameof(ComparisonMode.GreaterOrEqual))]
        public ComparisonMode TrimTradesCountComparisonMode { get; set; }

        [HandlerParameter(true, NotOptimized = true)]
        public bool UseTrimQuantity { get; set; }

        [HandlerParameter(true, "0", Min = "0", Max = "999999999999999", Step = "1", EditorMin = "0")]
        public double TrimQuantity { get; set; }

        [HandlerParameter(true, nameof(ComparisonMode.GreaterOrEqual))]
        public ComparisonMode TrimQuantityComparisonMode { get; set; }

        [HandlerParameter(true, NotOptimized = true)]
        public bool UseTrimAskQuantity { get; set; }

        [HandlerParameter(true, "0", Min = "0", Max = "999999999999999", Step = "1", EditorMin = "0")]
        public double TrimAskQuantity { get; set; }

        [HandlerParameter(true, nameof(ComparisonMode.GreaterOrEqual))]
        public ComparisonMode TrimAskQuantityComparisonMode { get; set; }

        [HandlerParameter(true, NotOptimized = true)]
        public bool UseTrimBidQuantity { get; set; }

        [HandlerParameter(true, "0", Min = "0", Max = "999999999999999", Step = "1", EditorMin = "0")]
        public double TrimBidQuantity { get; set; }

        [HandlerParameter(true, nameof(ComparisonMode.GreaterOrEqual))]
        public ComparisonMode TrimBidQuantityComparisonMode { get; set; }

        [HandlerParameter(true, NotOptimized = true)]
        public bool UseTrimDeltaAskBidQuantity { get; set; }

        [HandlerParameter(true, "0", Min = "-999999999999999", Max = "999999999999999", Step = "1")]
        public double TrimDeltaAskBidQuantity { get; set; }

        [HandlerParameter(true, nameof(ComparisonMode.GreaterOrEqual))]
        public ComparisonMode TrimDeltaAskBidQuantityComparisonMode { get; set; }

        [HandlerParameter(true, "false", NotOptimized = true)]
        public bool UseTrimRelativeDeltaAskBidQuantityPercent { get; set; }

        [HandlerParameter(true, "0", Min = "-100", Max = "100", Step = "1")]
        public double TrimRelativeDeltaAskBidQuantityPercent { get; set; }

        [HandlerParameter(true, nameof(ComparisonMode.GreaterOrEqual))]
        public ComparisonMode TrimRelativeDeltaAskBidQuantityPercentComparisonMode { get; set; }

        public IList<double> Execute(IBaseTradeStatisticsWithKind tradeStatistics)
        {
            return Execute(
                tradeStatistics,
                new TrimContext(UseTrimTradesCount, TrimTradesCount, TrimTradesCountComparisonMode),
                new TrimContext(UseTrimQuantity, TrimQuantity, TrimQuantityComparisonMode),
                new TrimContext(UseTrimAskQuantity, TrimAskQuantity, TrimAskQuantityComparisonMode),
                new TrimContext(UseTrimBidQuantity, TrimBidQuantity, TrimBidQuantityComparisonMode),
                new TrimContext(UseTrimDeltaAskBidQuantity, TrimDeltaAskBidQuantity, TrimDeltaAskBidQuantityComparisonMode),
                new TrimContext(UseTrimRelativeDeltaAskBidQuantityPercent, TrimRelativeDeltaAskBidQuantityPercent, TrimRelativeDeltaAskBidQuantityPercentComparisonMode));
        }

        protected override string GetParametersStateId()
        {
            return string.Join(
                ".",
                UseTrimTradesCount,
                TrimTradesCount,
                TrimTradesCountComparisonMode,
                UseTrimQuantity,
                TrimQuantity,
                TrimQuantityComparisonMode,
                UseTrimAskQuantity,
                TrimAskQuantity,
                TrimAskQuantityComparisonMode,
                UseTrimBidQuantity,
                TrimBidQuantity,
                TrimBidQuantityComparisonMode,
                UseTrimDeltaAskBidQuantity,
                TrimDeltaAskBidQuantity,
                TrimDeltaAskBidQuantityComparisonMode,
                UseTrimRelativeDeltaAskBidQuantityPercent,
                TrimRelativeDeltaAskBidQuantityPercent,
                TrimRelativeDeltaAskBidQuantityPercentComparisonMode);
        }
    }
}
