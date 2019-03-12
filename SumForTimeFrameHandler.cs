using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TSLab.DataSource;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: у этого кубика нет описания. Судя по коду что-то вроде "сумма значений в пределах указанного таймфрейма"
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Sum for time frame", Language = Constants.En)]
    [HelperName("Сумма за таймфрейм", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Сумма за таймфрейм")]
    [HelperDescription("Sum for time frame", Constants.En)]
    public sealed class SumForTimeFrameHandler : IDoubleStreamAndValuesHandler
    {
        public IContext Context { get; set; }

        public bool IsGapTolerant => true;

        /// <summary>
        /// \~english Timeframe (format D.HH:MM:SS)
        /// \~russian Интервал (формат Д.ЧЧ:ММ:СС)
        /// </summary>
        [HelperName("Timeframe", Constants.En)]
        [HelperName("Интервал", Constants.Ru)]
        [Description("Интервал (формат Д.ЧЧ:ММ:СС)")]
        [HelperDescription("Timeframe (format D.HH:MM:SS)", Constants.En)]
        [HandlerParameter(true, "1:0:0", Min = "0:0:0", Max = "365.0:0:0", Step = "0:1:0", EditorMin = "0:0:0", EditorMax = "365.0:0:0")]
        public TimeSpan TimeFrame { get; set; }

        private DateTime m_firstDateTime;
        private DateTime m_lastDateTime;
        private double m_sum;
        private int m_index = -1;

        public IList<double> Execute(IList<double> values)
        {
            var security = Context.Runtime.Securities.First();
            var bars = security.Bars;
            var count = Math.Min(bars.Count, values.Count);

            if (count == 0)
                return new ConstList<double>(values.Count, 0);

            var results = Context?.GetArray<double>(values.Count) ?? new double[values.Count];
            TimeFrameUtils.GetFirstBounds(TimeFrame, bars[0].Date, out var firstDateTime, out var lastDateTime);
            var sum = 0D;

            for (var i = 0; i < count; i++)
            {
                var barDate = bars[i].Date;
                if (barDate >= lastDateTime)
                {
                    TimeFrameUtils.GetBounds(TimeFrame, barDate, ref firstDateTime, ref lastDateTime);
                    sum = 0;
                }
                results[i] = sum += values[i];
            }
            for (var i = count; i < results.Length; i++)
                results[i] = sum;

            return results;
        }

        public double Execute(double value, int index)
        {
            if (index < m_index)
                throw new InvalidOperationException();

            if (index == m_index)
                return m_sum;

            var security = Context.Runtime.Securities.First();
            var bars = security.Bars;
            var count = Math.Min(bars.Count, index + 1);

            if (count == 0)
                return 0;

            if (m_firstDateTime == default(DateTime))
                TimeFrameUtils.GetFirstBounds(TimeFrame, bars[index].Date, out m_firstDateTime, out m_lastDateTime);

            for (var i = m_index + 1; i < count; i++)
            {
                var barDate = bars[i].Date;
                if (barDate >= m_lastDateTime)
                {
                    TimeFrameUtils.GetBounds(TimeFrame, barDate, ref m_firstDateTime, ref m_lastDateTime);
                    m_sum = 0;
                }
                m_sum += value;
            }
            m_index = index;
            return m_sum;
        }
    }
}
