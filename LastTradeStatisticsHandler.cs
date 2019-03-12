using System.ComponentModel;
using TSLab.DataSource;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Last Trade Statistics", Language = Constants.En)]
    [HelperName("Последняя торговая статистика", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.TRADE_STATISTICS)]
    [Description("Блок 'Последняя торговая статистика' создает гистограмму за указанный временной интервал.")]
    [HelperDescription("", Constants.En)]
    public sealed class LastTradeStatisticsHandler : BaseTradeStatisticsHandler<ITradeStatisticsWithKind>, ITradeStatisticsHandler
    {
        [HandlerParameter(true, nameof(TimeFrameKind.FromMidnightToNow))]
        public TimeFrameKind TimeFrameKind { get; set; }

        /// <summary>
        /// \~english Timeframe (integer value in units of parameter 'Timeframe units')
        /// \~russian Интервал (целое число в единицах параметра 'База интервала')
        /// </summary>
        [HelperName("Timeframe", Constants.En)]
        [HelperName("Интервал", Constants.Ru)]
        [Description("Интервал (целое число в единицах параметра 'База интервала')")]
        [HelperDescription("Timeframe (integer value in units of parameter 'Timeframe units')", Constants.En)]
        [HandlerParameter(true, "1", Min = "1", Max = "365", Step = "1", EditorMin = "1")]
        public int TimeFrame { get; set; }

        /// <summary>
        /// \~english Timeframe units (second, minute, hour, day)
        /// \~russian База интервала (секунды, минуты, часы, дни)
        /// </summary>
        [HelperName("Timeframe units", Constants.En)]
        [HelperName("База интервала", Constants.Ru)]
        [Description("База интервала (секунды, минуты, часы, дни)")]
        [HelperDescription("Timeframe units (second, minute, hour, day)", Constants.En)]
        [HandlerParameter(true, nameof(TimeFrameUnit.Hour))]
        public TimeFrameUnit TimeFrameUnit { get; set; }

        [HandlerParameter(true, "0", Min = "0", Max = "365", Step = "1", EditorMin = "0")]
        public int TimeFrameShift { get; set; }

        [HandlerParameter(true, nameof(TimeFrameUnit.Hour))]
        public TimeFrameUnit TimeFrameShiftUnit { get; set; }

        public override ITradeStatisticsWithKind Execute(ISecurity security)
        {
            var timeFrame = TimeFrameFactory.Create(TimeFrame, TimeFrameUnit);
            var timeFrameShift = TimeFrameFactory.Create(TimeFrameShift, TimeFrameShiftUnit);
            var runTime = Context.Runtime;
            var id = runTime != null ? string.Join(".", runTime.TradeName, runTime.IsAgentMode, VariableId) : VariableId;
            var stateId = string.Join(".", security.Symbol, security.Interval, security.IsAligned, CombinePricesCount, TimeFrameKind, TimeFrame, TimeFrameUnit, TimeFrameShift, TimeFrameShiftUnit);
            var tradeStatistics = Context.GetTradeStatistics(stateId, () => new LastTradeStatistics(id, stateId, GetTradeHistogramsCache(security), TimeFrameKind, timeFrame, TimeFrameUnit, timeFrameShift, TimeFrameShiftUnit));
            return new TradeStatisticsWithKind(tradeStatistics, Kind, WidthPercent);
        }
    }
}
