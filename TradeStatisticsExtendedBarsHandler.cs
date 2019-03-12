using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // Не стоит навешивать на абстрактные классы атрибуты категорий и описания входов/выходов
    public abstract class TradeStatisticsExtendedBarsHandler : TradeStatisticsBaseExtendedBarsHandler, ITradeStatisticsExtendedBarsHandler
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
