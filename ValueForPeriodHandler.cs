using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.DataSource;
using TSLab.Script.Handlers.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers
{
    //[HandlerCategory(HandlerCategories.VolumeAnalysis)]
    // Не стоит навешивать на абстрактные классы атрибуты категорий и описания входов/выходов. Это снижает гибкость управления в перспективе.
    public abstract class ValueForPeriodHandler : IValueForPeriodHandler, IContextUses
    {
        public IContext Context { get; set; }

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

        /// <summary>
        /// \~english Processing algo (sum or average)
        /// \~russian Алгоритм вычислений (сумма или среднее)
        /// </summary>
        [HelperName("Processing algo", Constants.En)]
        [HelperName("Алгоритм вычислений", Constants.Ru)]
        [Description("Алгоритм вычислений (сумма или среднее)")]
        [HelperDescription("Processing algo (sum or average)", Constants.En)]
        [HandlerParameter(true, nameof(ValueForPeriodMode.Sum))]
        public ValueForPeriodMode ValueMode { get; set; }

        public IList<double> Execute(ISecurity security)
        {
            if (security == null)
                throw new ArgumentNullException(nameof(security));

            if (TimeFrame <= 0)
                throw new ArgumentOutOfRangeException(nameof(TimeFrame));

            var timeFrame = TimeFrameFactory.Create(TimeFrame, TimeFrameUnit);

            var bars = security.Bars;
            if (bars.Count == 0)
                return EmptyArrays.Double;

            if (bars.Count == 1)
                return new[] { bars[0].Volume };

            DateTime firstDateTime, lastDateTime;
            TimeFrameUtils.GetFirstBounds(timeFrame, bars[0].Date, out firstDateTime, out lastDateTime);
            var results = Context?.GetArray<double>(bars.Count) ?? new double[bars.Count];

            for (int i = 0, firstIndex = 0; i <= results.Length; i++)
            {
                if (i == results.Length || bars[i].Date >= lastDateTime)
                {
                    var count = i - firstIndex;
                    if (count > 0)
                    {
                        var result = 0D;
                        switch (ValueMode)
                        {
                            case ValueForPeriodMode.Sum:
                                for (int j = 0, index = firstIndex; j < count; j++, index++)
                                {
                                    result += GetValue(security, index);
                                    results[index] = result;
                                }
                                break;
                            case ValueForPeriodMode.Average:
                                for (int j = 0, index = firstIndex; j < count; j++, index++)
                                {
                                    result += GetValue(security, index);
                                    results[index] = result / (j + 1);
                                }
                                break;
                            default:
                                throw new InvalidEnumArgumentException(nameof(ValueMode), (int)ValueMode, ValueMode.GetType());
                        }
                    }
                    if (i == results.Length)
                        break;

                    TimeFrameUtils.GetBounds(timeFrame, bars[i].Date, ref firstDateTime, ref lastDateTime);
                    firstIndex = i;
                }
            }
            return results;
        }

        protected abstract double GetValue(ISecurity security, int barIndex);
    }
}
