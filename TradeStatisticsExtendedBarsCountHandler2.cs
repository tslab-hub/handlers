using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Trade Statistics Extended Strings Count 2", Language = Constants.En)]
    [HelperName("Расширенное количество строк торговой статистики 2", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.TRADE_STATISTICS | TemplateTypes.LAST_CONTRACTS_TRADE_STATISTICS)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Блок 'Расширенное количество строк торговой статистики 2' выдает количество строк торговой статистики, соответствующее выбранному фильтру.")]
    [HelperDescription("", Constants.En)]
    public sealed class TradeStatisticsExtendedBarsCountHandler2 : TradeStatisticsExtendedBarsHandler2, ITradeStatisticsExtendedBarsCountHandler2
    {
        protected override double GetResult(IBaseTradeStatisticsWithKind tradeStatistics, IEnumerable<ITradeHistogramBar> bars)
        {
            return bars.Count();
        }
    }
}
