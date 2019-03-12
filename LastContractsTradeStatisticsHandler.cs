using System.ComponentModel;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Last Contracts Trade Statistics", Language = Constants.En)]
    [HelperName("Торговая статистика последних контрактов", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.LAST_CONTRACTS_TRADE_STATISTICS)]
    [Description("Блок 'Торговая статистика последних контрактов' создает гистограмму за указанное количество последних контрактов.")]
    [HelperDescription("", Constants.En)]
    public sealed class LastContractsTradeStatisticsHandler : BaseTradeStatisticsHandler<ILastContractsTradeStatisticsWithKind>, ILastContractsTradeStatisticsHandler
    {
        /// <summary>
        /// \~english A parameter to set contracts count to be used for trade statistics.
        /// \~russian Осуществляет выбор количества контрактов, за которые должна отображаться торговая статистика.
        /// </summary>
        [HelperName("Contracts count", Constants.En)]
        [HelperName("Количество контрактов", Constants.Ru)]
        [Description("Осуществляет выбор количества контрактов, за которые должна отображаться торговая статистика.")]
        [HelperDescription("A parameter to set contracts count to be used for trade statistics.", Constants.En)]
        [HandlerParameter(true, "10000", Min = "10000", Max = "1000000", Step = "10000", EditorMin = "1")]
        public int ContractsCount { get; set; }

        public override ILastContractsTradeStatisticsWithKind Execute(ISecurity security)
        {
            var runTime = Context.Runtime;
            var id = runTime != null ? string.Join(".", runTime.TradeName, runTime.IsAgentMode, VariableId) : VariableId;
            var stateId = string.Join(".", security.Symbol, security.Interval, security.IsAligned, CombinePricesCount, ContractsCount);
            var tradeStatistics = Context.GetLastContractsTradeStatistics(stateId, () => new LastContractsTradeStatistics(id, stateId, GetTradeHistogramsCache(security), ContractsCount));
            return new LastContractsTradeStatisticsWithKind(tradeStatistics, Kind, WidthPercent);
        }
    }
}
