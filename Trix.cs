using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;

// ReSharper disable InconsistentNaming
namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("TRIX", Language = Constants.En)]
    [HelperName("TRIX", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Тройное экспоненциальное скользящее.")]
    [HelperDescription("The Triple Exponential Average.", Constants.En)]
    public sealed class TRIX : DoubleStreamAndValuesHandlerWithPeriod
    {
        private double m_lastEma3;
        private EMA m_ema1;
        private EMA m_ema2;
        private EMA m_ema3;

        public override bool IsGapTolerant
        {
            get { return IsSimple; }
        }

        public override IList<double> Execute(IList<double> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            IList<double> ema3;
            if (IsSimple)
                ema3 = source;
            else
            {
                var ema1 = Series.EMA(source, Period, Context);
                var ema2 = Series.EMA(ema1, Period, Context);
                Context?.ReleaseArray((Array)ema1);
                ema3 = Series.EMA(ema2, Period, Context);
                Context?.ReleaseArray((Array)ema2);
            }
            var result = Context?.GetArray<double>(source.Count) ?? new double[source.Count];
            for (var i = 1; i < result.Length; i++)
            {
                var lastEma3 = ema3[i - 1];
                result[i] = lastEma3 != 0 ? (ema3[i] - lastEma3) / lastEma3 : 0;
            }
            Context?.ReleaseArray((Array)ema3);
            return result;
        }

        protected override void InitExecuteContext()
        {
            m_lastEma3 = 0;
            if (IsSimple)
                return;

            m_ema1 = new EMA { Context = Context, Period = Period };
            m_ema2 = new EMA { Context = Context, Period = Period };
            m_ema3 = new EMA { Context = Context, Period = Period };
        }

        protected override void ClearExecuteContext()
        {
            m_lastEma3 = 0;
            m_ema1 = null;
            m_ema2 = null;
            m_ema3 = null;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                m_lastEma3 = m_executeContext.GetSourceForGap(m_executeContext.Index - 1);
            else
            {
                var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - 4 * Period);
                for (var i = firstIndex; i < m_executeContext.Index; i++)
                {
                    var source = m_executeContext.GetSourceForGap(i);
                    var ema1 = m_ema1.Execute(source, i);
                    var ema2 = m_ema2.Execute(ema1, i);
                    var ema3 = m_ema3.Execute(ema2, i);
                    m_lastEma3 = ema3;
                }
            }
        }

        protected override double Execute()
        {
            double ema3;
            if (IsSimple)
                ema3 = m_executeContext.Source;
            else
            {
                var ema1 = m_ema1.Execute(m_executeContext.Source, m_executeContext.Index);
                var ema2 = m_ema2.Execute(ema1, m_executeContext.Index);
                ema3 = m_ema3.Execute(ema2, m_executeContext.Index);
            }
            var result = m_lastEma3 != 0 ? (ema3 - m_lastEma3) / m_lastEma3 : 0;
            m_lastEma3 = ema3;
            return result;
        }
    }
}
