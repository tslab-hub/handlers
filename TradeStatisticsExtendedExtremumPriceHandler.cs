using System.Collections.Generic;
using System.ComponentModel;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Trade Statistics Extended Extremum Price 1", Language = Constants.En)]
    [HelperName("Расширенная экстремальная цена торговой статистики 1", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.TRADE_STATISTICS | TemplateTypes.LAST_CONTRACTS_TRADE_STATISTICS)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Блок 'Расширенная экстремальная цена торговой статистики 1' показывает максимальное значение торговой статистики при экстремальной цене.")]
    [HelperDescription("", Constants.En)]
    public sealed class TradeStatisticsExtendedExtremumPriceHandler : TradeStatisticsBaseExtendedExtremumPriceHandler, ITradeStatisticsExtendedExtremumPriceHandler
    {
        [HandlerParameter(true, NotOptimized = true)]
        public bool UseTrimTradesCount { get; set; }

        [HandlerParameter(true, "0", Min = "0", Max = "2147483647", Step = "1", EditorMin = "0")]
        public int TrimTradesCount { get; set; }

        [HandlerParameter(true, NotOptimized = true)]
        public bool UseTrimQuantity { get; set; }

        [HandlerParameter(true, "0", Min = "0", Max = "999999999999999", Step = "1", EditorMin = "0")]
        public double TrimQuantity { get; set; }

        [HandlerParameter(true, NotOptimized = true)]
        public bool UseTrimAskQuantity { get; set; }

        [HandlerParameter(true, "0", Min = "0", Max = "999999999999999", Step = "1", EditorMin = "0")]
        public double TrimAskQuantity { get; set; }

        [HandlerParameter(true, NotOptimized = true)]
        public bool UseTrimBidQuantity { get; set; }

        [HandlerParameter(true, "0", Min = "0", Max = "999999999999999", Step = "1", EditorMin = "0")]
        public double TrimBidQuantity { get; set; }

        [HandlerParameter(true, NotOptimized = true)]
        public bool UseTrimDeltaAskBidQuantity { get; set; }

        [HandlerParameter(true, "0", Min = "-999999999999999", Max = "999999999999999", Step = "1")]
        public double TrimDeltaAskBidQuantity { get; set; }

        [HandlerParameter(true, "false", NotOptimized = true)]
        public bool UseTrimRelativeDeltaAskBidQuantityPercent { get; set; }

        [HandlerParameter(true, "0", Min = "-100", Max = "100", Step = "1")]
        public double TrimRelativeDeltaAskBidQuantityPercent { get; set; }

        [HandlerParameter(true, nameof(ComparisonMode.GreaterOrEqual))]
        public ComparisonMode TrimComparisonMode { get; set; }

        public override IList<double> Execute(IBaseTradeStatisticsWithKind tradeStatistics)
        {
            return Execute(
                tradeStatistics,
                new TrimContext(UseTrimTradesCount, TrimTradesCount, TrimComparisonMode),
                new TrimContext(UseTrimQuantity, TrimQuantity, TrimComparisonMode),
                new TrimContext(UseTrimAskQuantity, TrimAskQuantity, TrimComparisonMode),
                new TrimContext(UseTrimBidQuantity, TrimBidQuantity, TrimComparisonMode),
                new TrimContext(UseTrimDeltaAskBidQuantity, TrimDeltaAskBidQuantity, TrimComparisonMode),
                new TrimContext(UseTrimRelativeDeltaAskBidQuantityPercent, TrimRelativeDeltaAskBidQuantityPercent, TrimComparisonMode));
        }

        protected override string GetParametersStateId()
        {
            return string.Join(
                ".",
                base.GetParametersStateId(),
                UseTrimTradesCount,
                TrimTradesCount,
                UseTrimQuantity,
                TrimQuantity,
                UseTrimAskQuantity,
                TrimAskQuantity,
                UseTrimBidQuantity,
                TrimBidQuantity,
                UseTrimDeltaAskBidQuantity,
                TrimDeltaAskBidQuantity,
                UseTrimRelativeDeltaAskBidQuantityPercent,
                TrimRelativeDeltaAskBidQuantityPercent,
                TrimComparisonMode);
        }
    }
}
