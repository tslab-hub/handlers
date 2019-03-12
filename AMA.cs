using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;

// ReSharper disable InconsistentNaming
namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("AMA", Language = Constants.En)]
    [HelperName("AMA", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Адаптивное сглаженное скользящее среднее.")]
    [HelperDescription("The Adaptive Moving Average.", Constants.En)]
    public sealed class AMA : DoubleStreamAndValuesHandlerWithPeriod
    {
        private ShrinkedList<double> m_source;
        private double m_lastResult;

        public override bool IsGapTolerant
        {
            get { return false; }
        }

        public override IList<double> Execute(IList<double> source)
        {
            return Calc(source, Period, Context);
        }

        protected override void InitExecuteContext()
        {
            m_source = new ShrinkedList<double>(Period + 2);
            m_lastResult = 0;
        }

        protected override void ClearExecuteContext()
        {
            m_source = null;
            m_lastResult = 0;
        }

        protected override void InitForGap()
        {
            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - 7 * Period);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                m_source.Add(m_executeContext.GetSourceForGap(i));
                m_lastResult = Calc(m_source, m_lastResult, m_source.Count - 1, Period);
            }
        }

        protected override double Execute()
        {
            m_source.Add(m_executeContext.Source);
            m_lastResult = Calc(m_source, m_lastResult, m_source.Count - 1, Period);
            return m_lastResult;
        }

        public static IList<double> Calc(IList<double> source, int period, IMemoryContext context = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (period <= 0)
                throw new ArgumentOutOfRangeException(nameof(period));

            var result = context?.GetArray<double>(source.Count) ?? new double[source.Count];
            if (result.Length > 0)
            {
                result[0] = Calc(source, 0, 0, period);
                for (var i = 1; i < result.Length; i++)
                    result[i] = Calc(source, result[i - 1], i, period);
            }
            return result;
        }

        private static double Calc(IList<double> source, double lastResult, int index, int period)
        {
            if (index <= period)
                return source[index];

            var signal = Math.Abs(source[index] - source[index - period]);
            var noise = 0.000000001;

            for (var i = 0; i < period; i++)
                noise = noise + Math.Abs(source[index - i] - source[index - i - 1]);

            var er = signal / noise;
            var ssc = (er * 0.60215) + 0.06452;
            var cst = Math.Pow(ssc, 2);
            var result = lastResult + cst * (source[index] - lastResult);
            return result;
        }
    }
}
