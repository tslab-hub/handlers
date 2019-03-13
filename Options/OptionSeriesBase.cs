using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    [HandlerCategory(HandlerCategories.Options)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION_STRIKE)]
    [OutputType(TemplateTypes.DOUBLE2)]
    public abstract class OptionSeriesBase : IStreamHandler
    {
        /// <summary>
        /// \~english Type of option to be used in handler (put, call, any)
        /// \~russian Тип опционов, которые будут использованы в обработчике (пут, колл, любой)
        /// </summary>
        [HelperName("Option Type", Constants.En)]
        [HelperName("Вид опционов", Constants.Ru)]
        [Description("Тип опционов, которые будут использованы в обработчике (пут, колл, любой)")]
        [HelperDescription("Type of option to be used in handler (put, call, any)", Constants.En)]
        //[HandlerParameter(NotOptimized = true)]
        [HandlerParameter(true, "Any")]
        public StrikeType StrikeType { get; set; }

        public IList<Double2> Execute(IOption source)
        {
            var strikes = source.CurrentSeries.GetStrikes();
            return CalculateInternal(strikes);
        }

        public IList<Double2> Execute(IOptionSeries source)
        {
            var strikes = source != null ? source.GetStrikes() : new IOptionStrike[0];
            return CalculateInternal(strikes);
        }

        public IList<Double2> Execute(IOptionStrike source)
        {
            var strikes = source != null ? new[] { source } : new IOptionStrike[0];
            return CalculateInternal(strikes);
        }

        private IList<Double2> CalculateInternal(IEnumerable<IOptionStrike> strikes)
        {
            if (StrikeType == StrikeType.Call)
                strikes = strikes.Where(s => s.StrikeType == StrikeType.Call);
            else if (StrikeType == StrikeType.Put)
                strikes = strikes.Where(s => s.StrikeType == StrikeType.Put);
            var res = Calculate(strikes.OrderBy(st => st.Strike).ToArray()).ToArray();
            return res;
        }

        protected abstract IEnumerable<Double2> Calculate(IOptionStrike[] strikes);
    }

    [HandlerCategory(HandlerCategories.Options)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.OPTION | TemplateTypes.OPTION_SERIES)]
    [Input(1, TemplateTypes.DOUBLE2 | TemplateTypes.DOUBLE2N | TemplateTypes.INTERACTIVESPLINE)]
    [OutputType(TemplateTypes.DOUBLE2)]
    public abstract class OptionSeriesBase2 : IStreamHandler
    {
        public IList<Double2> Execute(IOption source, InteractiveSeries data)
        {
            var strikes = source.GetStrikes().ToArray();
            return Calculate(strikes, data.ControlPoints);
        }

        public IList<Double2> Execute(IOption source, IReadOnlyList<InteractiveObject> data)
        {
            var strikes = source.GetStrikes().ToArray();
            return Calculate(strikes, data);
        }

        public IList<Double2> Execute(IOptionSeries source, InteractiveSeries data)
        {
            var strikes = source.GetStrikes().ToArray();
            return Calculate(strikes, data.ControlPoints);
        }

        public IList<Double2> Execute(IOptionSeries source, IReadOnlyList<InteractiveObject> data)
        {
            var strikes = source.GetStrikes().ToArray();
            return Calculate(strikes, data);
        }

        protected abstract IList<Double2> Calculate(IOptionStrike[] strikes, IReadOnlyList<InteractiveObject> data);
    }
}
