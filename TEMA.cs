using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;

// ReSharper disable InconsistentNaming
namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("TEMA", Language = Constants.En)]
    [HelperName("TEMA", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Тройное экспоненциальное сглаженное скользящее среднее.")]
    [HelperDescription("The Triple Exponential Moving Average.", Constants.En)]
    public sealed class TEMA : DoubleStreamAndValuesHandlerWithPeriod
    {
        private EMA m_ema1;
        private EMA m_ema2;
        private EMA m_ema3;

        public override bool IsGapTolerant
        {
            get { return IsSimple; }
        }

        public override IList<double> Execute(IList<double> source)
        {
            return Calc(source, Period, Context);
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                return;

            m_ema1 = new EMA { Context = Context, Period = Period };
            m_ema2 = new EMA { Context = Context, Period = Period };
            m_ema3 = new EMA { Context = Context, Period = Period };
        }

        protected override void ClearExecuteContext()
        {
            m_ema1 = null;
            m_ema2 = null;
            m_ema3 = null;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                return;

            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - GapTolerancePeriodMultiplier * Period);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                var source = m_executeContext.GetSourceForGap(i);
                var ema1 = m_ema1.Execute(source, i);
                var ema2 = m_ema2.Execute(ema1, i);
                var ema3 = m_ema3.Execute(ema2, i);
            }
        }

        protected override double Execute()
        {
            if (IsSimple)
                return m_executeContext.Source;

            var ema1 = m_ema1.Execute(m_executeContext.Source, m_executeContext.Index);
            var ema2 = m_ema2.Execute(ema1, m_executeContext.Index);
            var ema3 = m_ema3.Execute(ema2, m_executeContext.Index);
            var result = 3 * ema1 - 3 * ema2 + ema3;
            return result;
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
                if (period == 1 || result.Length == 1)
                    source.CopyTo(result, 0);
                else
                {
                    var ema1 = Series.EMA(source, period, context);
                    var ema2 = Series.EMA(ema1, period, context);
                    var ema3 = Series.EMA(ema2, period, context);

                    for (var i = 0; i < result.Length; i++)
                        result[i] = 3 * ema1[i] - 3 * ema2[i] + ema3[i];

                    context?.ReleaseArray((Array)ema1);
                    context?.ReleaseArray((Array)ema2);
                    context?.ReleaseArray((Array)ema3);
                }
            }
            return result;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("DEMA", Language = Constants.En)]
    [HelperName("DEMA", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Двойное экспоненциальное сглаженное скользящее среднее.")]
    [HelperDescription("The Double Exponential Moving Average.", Constants.En)]
    public sealed class DEMA : DoubleStreamAndValuesHandlerWithPeriod
    {
        private EMA m_ema1;
        private EMA m_ema2;

        public override bool IsGapTolerant
        {
            get { return IsSimple; }
        }

        public override IList<double> Execute(IList<double> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = Context?.GetArray<double>(source.Count) ?? new double[source.Count];
            if (result.Length > 0)
            {
                if (Period == 1 || result.Length == 1)
                    source.CopyTo(result, 0);
                else
                {
                    var ema1 = Series.EMA(source, Period, Context);
                    var ema2 = Series.EMA(ema1, Period, Context);

                    for (var i = 0; i < result.Length; i++)
                        result[i] = 2 * ema1[i] - ema2[i];

                    Context?.ReleaseArray((Array)ema1);
                    Context?.ReleaseArray((Array)ema2);
                }
            }
            return result;
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                return;

            m_ema1 = new EMA { Context = Context, Period = Period };
            m_ema2 = new EMA { Context = Context, Period = Period };
        }

        protected override void ClearExecuteContext()
        {
            m_ema1 = null;
            m_ema2 = null;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                return;

            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - GapTolerancePeriodMultiplier * Period);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                var source = m_executeContext.GetSourceForGap(i);
                var ema1 = m_ema1.Execute(source, i);
                var ema2 = m_ema2.Execute(ema1, i);
            }
        }

        protected override double Execute()
        {
            if (IsSimple)
                return m_executeContext.Source;

            var ema1 = m_ema1.Execute(m_executeContext.Source, m_executeContext.Index);
            var ema2 = m_ema2.Execute(ema1, m_executeContext.Index);
            var result = 2 * ema1 - ema2;
            return result;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    //[HandlerName("TEMA (Zero Lag)")]
    //[HandlerName("TEMA (без задержки)", Language = "ru-RU")]
    [HelperName("TEMA (Zero Lag)", Language = Constants.En)]
    [HelperName("TEMA (без задержки)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Тройное экспоненциальное сглаженное скользящее среднее с ограниченной задержкой.")]
    [HelperDescription("The Triple Exponential Moving Average with a limited lag. ", Constants.En)]
    [SuppressMessage("StyleCopPlus.StyleCopPlusRules", "SP0100:AdvancedNamingRules", Justification = "Reviewed. Suppression is OK here.")]
    public sealed class ZeroLagTEMA : DoubleStreamAndValuesHandlerWithPeriod
    {
        private TEMA m_tema1;
        private TEMA m_tema2;

        public override bool IsGapTolerant
        {
            get { return IsSimple; }
        }

        public override IList<double> Execute(IList<double> source)
        {
            return Calc(source, Period, Context);
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                return;

            m_tema1 = new TEMA { Context = Context, Period = Period };
            m_tema2 = new TEMA { Context = Context, Period = Period };
        }

        protected override void ClearExecuteContext()
        {
            m_tema1 = null;
            m_tema2 = null;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                return;

            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - GapTolerancePeriodMultiplier * Period);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                var source = m_executeContext.GetSourceForGap(i);
                var tema1 = m_tema1.Execute(source, i);
                var tema2 = m_tema2.Execute(tema1, i);
            }
        }

        protected override double Execute()
        {
            if (IsSimple)
                return m_executeContext.Source;

            var tema1 = m_tema1.Execute(m_executeContext.Source, m_executeContext.Index);
            var tema2 = m_tema2.Execute(tema1, m_executeContext.Index);
            var result = 2 * tema1 - tema2;
            return result;
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
                if (period == 1 || result.Length == 1)
                    source.CopyTo(result, 0);
                else
                {
                    var tema1 = TEMA.Calc(source, period, context);
                    var tema2 = TEMA.Calc(tema1, period, context);

                    for (var i = 0; i < source.Count; i++)
                        result[i] = 2 * tema1[i] - tema2[i];

                    context?.ReleaseArray((Array)tema1);
                    context?.ReleaseArray((Array)tema2);
                }
            }
            return result;
        }
    }
}
