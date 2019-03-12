using System.ComponentModel;
using TSLab.DataSource;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: английское описание
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("Aligned instrument", Language = Constants.En)]
    [HelperName("Выровненный инструмент", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Блок 'Выровненный инструмент' обеспечивает отображение гистограмм одинаковой ширины вне зависимости от фактического количества свечей в графике.")]
    [HelperDescription("", Constants.En)]
    public sealed class AlignedSecurityHandler : IAlignedSecurityHandler
    {
        /// <summary>
        /// \~english Timeframe (integer value in units of parameter 'Timeframe units')
        /// \~russian Интервал (целое число в единицах параметра 'База интервала')
        /// </summary>
        [HelperName("Timeframe", Constants.En)]
        [HelperName("Интервал", Constants.Ru)]
        [Description("Интервал (целое число в единицах параметра 'База интервала')")]
        [HelperDescription("Timeframe (integer value in units of parameter 'Timeframe units')", Constants.En)]
        [HandlerParameter(true, "1", Min = "1", Max = "365", Step = "1")]
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

        public ISecurity Execute(ISecurity security)
        {
            var timeFrame = TimeFrameFactory.Create(TimeFrame, TimeFrameUnit);
            return new AlignedSecurity(security, timeFrame);
        }
    }
}
