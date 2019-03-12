using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Trade Statistics Strings Count", Language = Constants.En)]
    [HelperName("Количество строк торговой статистики", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.TRADE_STATISTICS | TemplateTypes.LAST_CONTRACTS_TRADE_STATISTICS)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Блок 'Количество строк торговой статистики' выдает количество строк торговой статистики, соответствующее выбранному фильтру.")]
    [HelperDescription("", Constants.En)]
    public sealed class TradeStatisticsBarsCountHandler : TradeStatisticsBarsHandler, ITradeStatisticsBarsCountHandler
    {
        protected override double GetResult(IBaseTradeStatisticsWithKind tradeStatistics, IEnumerable<ITradeHistogramBar> bars)
        {
            return bars.Count();
        }
    }
}
