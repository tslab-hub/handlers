using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    // TODO: удачно ли здесь использовать слово 'string' как перевод слова 'строка'?
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Trade Statistics Strings Sum", Language = Constants.En)]
    [HelperName("Сумма строк торговой статистики", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.TRADE_STATISTICS | TemplateTypes.LAST_CONTRACTS_TRADE_STATISTICS)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Блок 'Сумма строк торговой статистики' выдает значение, основанное на сумме значений строк торговой статистики.")]
    [HelperDescription("", Constants.En)]
    public sealed class TradeStatisticsBarsSumHandler : TradeStatisticsBarsHandler, ITradeStatisticsBarsCountHandler
    {
        protected override double GetResult(IBaseTradeStatisticsWithKind tradeStatistics, IEnumerable<ITradeHistogramBar> bars)
        {
            return bars.Sum(item => tradeStatistics.GetValue(item));
        }
    }
}
