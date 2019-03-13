using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    [HandlerCategory(HandlerCategories.Options)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION_STRIKE)]
    [OutputType(TemplateTypes.DOUBLE2N)]
    public abstract class OptionSeriesBaseN : BaseContextHandler, IStreamHandler
    {
        /// <summary>
        /// \~english Type of option to be used in handler (put, call, any)
        /// \~russian Тип опционов, которые будут использованы в обработчике (пут, колл, любой)
        /// </summary>
        [HelperName("Option Type", Constants.En)]
        [HelperName("Вид опционов", Constants.Ru)]
        [Description("Тип опционов, которые будут использованы в обработчике (пут, колл, любой)")]
        [HelperDescription("Type of option to be used in handler (put, call, any)", Constants.En)]
        [HandlerParameter(NotOptimized = true)]
        public StrikeType StrikeType { get; set; }

        /// <summary>
        /// \~english Shift calculations back in time
        /// \~russian Перевод времени в прошлое на заданное число баров
        /// </summary>
        [HelperName("Shift Time", Constants.En)]
        [HelperName("Сдвиг времени", Constants.Ru)]
        [Description("Перевод времени в прошлое на заданное число баров")]
        [HelperDescription("Shift calculations back in time", Constants.En)]
        [HandlerParameter(true, "0", Min = "0", Max = "1000", Step = "1")]
        public int Shift { get; set; }

        public Double2N Execute(IOption source)
        {
            var strikes = source.CurrentSeries.GetStrikes();
            return CalculateInternal(strikes);
        }

        public Double2N Execute(IOptionSeries source)
        {
            var strikes = source != null ? source.GetStrikes() : new IOptionStrike[0];
            return CalculateInternal(strikes);
        }

        public Double2N Execute(IOptionStrike source)
        {
            var strikes = source != null ? new[] { source } : new IOptionStrike[0];
            return CalculateInternal(strikes);
        }

        private Double2N CalculateInternal(IEnumerable<IOptionStrike> strikes)
        {
            if (StrikeType == StrikeType.Call)
                strikes = strikes.Where(s => s.StrikeType == StrikeType.Call);
            else if (StrikeType == StrikeType.Put)
                strikes = strikes.Where(s => s.StrikeType == StrikeType.Put);
            var array = strikes.OrderBy(st => st.Strike).ToArray();
            var maxBar = Context.BarsCount - 1;
            var initialbarNumber = maxBar - Shift; // по умолчанию выдаём данные для последней свечи
            initialbarNumber = Math.Min(maxBar, Math.Max(0, initialbarNumber));
            return new Double2N(Context, initialbarNumber,
                barNum => Context.GetData(
                    VariableId,
                    new[]
                    {
                        barNum.ToString(CultureInfo.InvariantCulture)
                    },
                    () => Calculate(array, barNum).ToArray()));
        }

        protected abstract IEnumerable<Double2> Calculate(IOptionStrike[] strikes, int barNum);
    }
}