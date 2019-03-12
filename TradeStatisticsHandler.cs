using System;
using System.ComponentModel;
using TSLab.DataSource;
using TSLab.Script.Handlers.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Trade Statistics", Language = Constants.En)]
    [HelperName("Торговая статистика", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.TRADE_STATISTICS)]
    [Description("Блок 'Торговая статистика' создает гистограмму за выбранный таймфрейм (временной интервал). " +
        "[br]Гистограмма может быть построена на количестве сделок, объеме торгов, количестве покупок, количестве продаж, разнице количества покупок и продаж и относительной разнице количества покупок и продаж, % (см. параметр 'Вид').")]
    [HelperDescription("", Constants.En)]
    public sealed class TradeStatisticsHandler : BaseTradeStatisticsHandler<ITradeStatisticsWithKind>, ITradeStatisticsHandler
    {
        /// <summary>
        /// \~english Time frame kind (from now to past, from midnight to now).
        /// \~russian Вид таймфрейма (из настоящего в прошлое, от полуночи в настоящее).
        /// </summary>
        [HelperName("Time frame kind", Constants.En)]
        [HelperName("Вид таймфрейма", Constants.Ru)]
        [Description("Вид таймфрейма (из настоящего в прошлое, от полуночи в настоящее).")]
        [HelperDescription("Time frame kind (from now to past, from midnight to now).", Constants.En)]
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

        [HandlerParameter(true, "false", NotOptimized = true)]
        public bool UseTopTimeFrame { get; set; }

        [HandlerParameter(true, "20", Min = "1", Max = "2000000", Step = "1", EditorMin = "1")]
        public int TopTimeFrame { get; set; }

        [HandlerParameter(true, nameof(TimeFrameUnit.Day))]
        public TimeFrameUnit TopTimeFrameUnit { get; set; }

        public override ITradeStatisticsWithKind Execute(ISecurity security)
        {
            var timeFrame = TimeFrameFactory.Create(TimeFrame, TimeFrameUnit);
            int topTimeFrameNumber;
            TimeFrameUnit topTimeFrameUnit;
            TimeSpan topTimeFrame;

            if (UseTopTimeFrame)
            {
                topTimeFrameNumber = TopTimeFrame;
                topTimeFrameUnit = TopTimeFrameUnit;
                topTimeFrame = TimeFrameFactory.Create(topTimeFrameNumber, topTimeFrameUnit);

                if (topTimeFrame.Ticks % timeFrame.Ticks != 0)
                    throw new InvalidOperationException(string.Format(RM.GetString("TopTimeFrameMustBeDivisableByTimeFrame"), ToString(TopTimeFrame, topTimeFrameUnit), ToString(TimeFrame, TimeFrameUnit)));
            }
            else
            {
                var maxTimeSpan = TimeSpan.FromSeconds(int.MaxValue);
                switch (TimeFrameUnit)
                {
                    case TimeFrameUnit.Second:
                        topTimeFrameNumber = (int)maxTimeSpan.TotalSeconds;
                        break;
                    case TimeFrameUnit.Minute:
                        topTimeFrameNumber = (int)maxTimeSpan.TotalMinutes;
                        break;
                    case TimeFrameUnit.Hour:
                        topTimeFrameNumber = (int)maxTimeSpan.TotalHours;
                        break;
                    case TimeFrameUnit.Day:
                        topTimeFrameNumber = (int)maxTimeSpan.TotalDays;
                        break;
                    default:
                        throw new InvalidEnumArgumentException(nameof(TimeFrameUnit), (int)TimeFrameUnit, TimeFrameUnit.GetType());
                }
                topTimeFrameNumber = topTimeFrameNumber / TimeFrame * TimeFrame;
                topTimeFrameUnit = TimeFrameUnit;
                topTimeFrame = TimeFrameFactory.Create(topTimeFrameNumber, topTimeFrameUnit);
            }
            var runTime = Context.Runtime;
            var id = runTime != null ? string.Join(".", runTime.TradeName, runTime.IsAgentMode, VariableId) : VariableId;
            var stateId = string.Join(".", security.Symbol, security.Interval, security.IsAligned, CombinePricesCount, TimeFrameKind, TimeFrame, TimeFrameUnit, topTimeFrameNumber, topTimeFrameUnit);
            var tradeStatistics = Context.GetTradeStatistics(stateId, () => new TradeStatistics(id, stateId, GetTradeHistogramsCache(security), TimeFrameKind, timeFrame, TimeFrameUnit, topTimeFrame));
            return new TradeStatisticsWithKind(tradeStatistics, Kind, WidthPercent);
        }

        private static string ToString(int timeFrame, TimeFrameUnit timeFrameUnit)
        {
            return timeFrame + ":" + timeFrameUnit.GetDescription();
        }
    }
}
