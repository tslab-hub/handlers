using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Trade Statistics Upper Edge", Language = Constants.En)]
    [HelperName("Верхний уровень торговой статистики", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.TRADE_STATISTICS | TemplateTypes.LAST_CONTRACTS_TRADE_STATISTICS)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Блок 'Верхний уровень торговой статистики' позволяет установить в процентном значении отсечки данных по верхнему уровню. Данный блок соединяется с блоком 'Торговая статистика'.")]
    [HelperDescription("", Constants.En)]
    public sealed class TradeStatisticsUpperEdgeHandler : TradeStatisticsEdgeHandler
    {
        protected override double GetFirstPrice(IReadOnlyList<ITradeHistogramBar> bars)
        {
            return bars.Last().MaxPrice;
        }

        protected override double GetLastPrice(IReadOnlyList<ITradeHistogramBar> bars)
        {
            return bars.First().MinPrice;
        }

        protected override IEnumerable<ITradeHistogramBar> GetOrderedBars(IEnumerable<ITradeHistogramBar> bars)
        {
            return bars.Reverse();
        }

        protected override double GetPrice(ITradeHistogramBar bar, double coefficient)
        {
            double result;
            if (coefficient < 0.5)
                result = bar.MaxPrice + (bar.AveragePrice - bar.MaxPrice) * coefficient * 2;
            else if (coefficient > 0.5)
                result = bar.AveragePrice + (bar.MinPrice - bar.AveragePrice) * (coefficient - 0.5) * 2;
            else
                result = bar.AveragePrice;

            return result;
        }
    }
}
