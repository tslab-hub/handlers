using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Trade Statistics Extended Strings Sum 2", Language = Constants.En)]
    [HelperName("Расширенная сумма строк торговой статистики 2", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.TRADE_STATISTICS | TemplateTypes.LAST_CONTRACTS_TRADE_STATISTICS)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Блок 'Расширенная сумма строк торговой статистики 2', выдает значение, основанное на сумме значений строк торговой статистики.")]
    [HelperDescription("", Constants.En)]
    public sealed class TradeStatisticsExtendedBarsSumHandler2 : TradeStatisticsExtendedBarsHandler2, ITradeStatisticsExtendedBarsSumHandler2
    {
        /// <summary>
        /// \~english Kind of a trade statistics (trades count, trade volume, buy count, sell count, buy and sell difference, relative buy and sell difference).
        /// \~russian Вид торговой статистики (количество сделок, объем торгов, количество покупок, количество продаж, разница количества покупок и продаж, относительная разница количества покупок и продаж).
        /// </summary>
        [HelperName("Kind", Constants.En)]
        [HelperName("Вид", Constants.Ru)]
        [Description("Вид торговой статистики (количество сделок, объем торгов, количество покупок, количество продаж, разница количества покупок и продаж, относительная разница количества покупок и продаж).")]
        [HelperDescription("Kind of a trade statistics (trades count, trade volume, buy count, sell count, buy and sell difference, relative buy and sell difference).", Constants.En)]
        [HandlerParameter(true, nameof(TradeStatisticsKind.TradesCount))]
        public TradeStatisticsKind Kind { get; set; }

        protected override double GetResult(IBaseTradeStatisticsWithKind tradeStatistics, IEnumerable<ITradeHistogramBar> bars)
        {
            return bars.Sum(item => tradeStatistics.GetValue(item, Kind));
        }

        protected override string GetParametersStateId()
        {
            return base.GetParametersStateId() + "." + Kind;
        }
    }
}
