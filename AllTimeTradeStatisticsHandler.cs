using System.ComponentModel;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("All Time Trade Statistics", Language = Constants.En)]
    [HelperName("Торговая статистика за всё время", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.TRADE_STATISTICS)]
    [Description("Блок 'Торговая статистика за всё время' создает гистограмму на основе всего доступного временного интервала. " +
        "[br]Гистограмма может быть построена на количестве сделок, объеме торгов, количестве покупок, количестве продаж, разнице количества покупок и продаж и относительной разнице количества покупок и продаж, % (см. параметр 'Вид').")]
    [HelperDescription("", Constants.En)]
    public sealed class AllTimeTradeStatisticsHandler : BaseTradeStatisticsHandler<ITradeStatisticsWithKind>, IAllTimeTradeStatisticsHandler
    {
        /// <summary>
        /// \~english A width of a hystogram relative to a width of a chart pane.
        /// \~russian Ширина гистограммы в процентах относительно ширины панели графика.
        /// </summary>
        [HelperName("Width, %", Constants.En)]
        [HelperName("Ширина, %", Constants.Ru)]
        [Description("Ширина гистограммы в процентах относительно ширины панели графика.")]
        [HelperDescription("A width of a hystogram relative to a width of a chart pane.", Constants.En)]
        [HandlerParameter(true, "10", Min = "1", Max = "100", Step = "1", EditorMin = "1", EditorMax = "100")]
        public override double WidthPercent { get; set; }

        public override ITradeStatisticsWithKind Execute(ISecurity security)
        {
            var runTime = Context.Runtime;
            var id = runTime != null ? string.Join(".", runTime.TradeName, runTime.IsAgentMode, VariableId) : VariableId;
            var stateId = string.Join(".", security.Symbol, security.Interval, security.IsAligned, CombinePricesCount);
            var tradeStatistics = Context.GetTradeStatistics(stateId, () => TradeStatisticsCache.Instance.GetAllTimeTradeStatistics(id, stateId, GetTradeHistogramsCache(security)));
            return new AllTimeTradeStatisticsWithKind(tradeStatistics, Kind, WidthPercent);
        }
    }
}
