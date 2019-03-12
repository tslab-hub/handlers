using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;

// ReSharper disable InconsistentNaming
namespace TSLab.Script.Handlers
{
    public abstract class BaseAMA : DoubleStreamAndValuesHandler
    {
        private sealed class LocalExecuteContext : IDisposable
        {
            private readonly IMemoryContext m_context;

            public LocalExecuteContext(IList<double> source, IMemoryContext context)
            {
                Source = source;
                Smooth = context.GetArray<double>(7);
                Detrender = context.GetArray<double>(7);
                I1 = context.GetArray<double>(7);
                Q1 = context.GetArray<double>(7);
                m_context = context;
            }

            public void Dispose()
            {
                m_context.ReleaseArray(Smooth);
                m_context.ReleaseArray(Detrender);
                m_context.ReleaseArray(I1);
                m_context.ReleaseArray(Q1);
            }

            public IList<double> Source { get; }
            public double[] Smooth { get; }
            public double[] Detrender { get; }
            public double[] I1 { get; }
            public double[] Q1 { get; }
            public double LastI2 { get; set; }
            public double LastQ2 { get; set; }
            public double LastPeriod { get; set; }
            public double LastPhase { get; set; }
            public double LastRe { get; set; }
            public double LastIm { get; set; }
            public double LastMama { get; set; }
            public double LastFama { get; set; }
        }

        private readonly bool m_isMama;
        private LocalExecuteContext m_localExecuteContext;

        protected BaseAMA(bool isMama)
        {
            m_isMama = isMama;
        }

        public override bool IsGapTolerant
        {
            get { return false; }
        }

        /// <summary>
        /// \~english Fast limit
        /// \~russian Быстрый параметр
        /// </summary>
        [HelperName("Fast limit", Constants.En)]
        [HelperName("Быстрый лимит", Constants.Ru)]
        [Description("Быстрый параметр")]
        [HelperDescription("Fast limit parameter", Constants.En)]
        [HandlerParameter(true, "0.5", Min = "0.1", Max = "1.0", Step = "0.1")]
        public double FastLimit { get; set; }

        /// <summary>
        /// \~english Slow limit
        /// \~russian Медленный параметр
        /// </summary>
        [HelperName("Slow limit", Constants.En)]
        [HelperName("Медленный лимит", Constants.Ru)]
        [Description("Медленный параметр")]
        [HelperDescription("Slow limit parameter", Constants.En)]
        [HandlerParameter(true, "0.05", Min = "0.01", Max = "0.1", Step = "0.01")]
        public double SlowLimit { get; set; }

        public override IList<double> Execute(IList<double> source)
        {
            var result = Context.GetArray<double>(source.Count);
            if (result.Length > 0)
            {
                if (result.Length <= 6)
                    source.CopyTo(result, 0);
                else
                    using (var executeContext = new LocalExecuteContext(source, Context))
                        for (var i = 0; i < result.Length; i++)
                            result[i] = Execute(executeContext, i);
            }
            return result;
        }

        protected override void InitExecuteContext()
        {
            m_localExecuteContext = new LocalExecuteContext(new ShrinkedList<double>(7), Context);
        }

        protected override void ClearExecuteContext()
        {
            m_localExecuteContext = null;
        }

        protected override void InitForGap()
        {
            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - 5);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                m_localExecuteContext.Source.Add(m_executeContext.GetSourceForGap(i));
                var result = Execute(m_localExecuteContext, m_localExecuteContext.Source.Count - 1);
            }
        }

        protected override double Execute()
        {
            m_localExecuteContext.Source.Add(m_executeContext.Source);
            var result = Execute(m_localExecuteContext, m_localExecuteContext.Source.Count - 1);
            return result;
        }

        private double Execute(LocalExecuteContext localExecuteContext, int index)
        {
            var source = localExecuteContext.Source;
            if (index < 6)
                return localExecuteContext.LastMama = localExecuteContext.LastFama = source[index];

            var smooth = localExecuteContext.Smooth;
            var detrender = localExecuteContext.Detrender;
            var i1 = localExecuteContext.I1;
            var q1 = localExecuteContext.Q1;
            var lastI2 = localExecuteContext.LastI2;
            var lastQ2 = localExecuteContext.LastQ2;
            var lastPeriod = localExecuteContext.LastPeriod;
            var lastPhase = localExecuteContext.LastPhase;
            var lastRe = localExecuteContext.LastRe;
            var lastIm = localExecuteContext.LastIm;
            var lastMama = localExecuteContext.LastMama;
            var lastFama = localExecuteContext.LastFama;

            ShiftBuffer(smooth);
            ShiftBuffer(detrender);
            ShiftBuffer(i1);
            ShiftBuffer(q1);

            smooth[6] = (4 * source[index] + 3 * source[index - 1] + 2 * source[index - 2] + source[index - 3]) / 10;
            detrender[6] = (.0962 * smooth[6] + .5769 * smooth[4] - .5769 * smooth[2] - .0962 * smooth[0]) *
                           (.075 * lastPeriod + .54);

            //Compute InPhase and Quadrature components
            q1[6] = (.0962 * detrender[6] + .5769 * detrender[4] - .5769 * detrender[2] - .0962 * detrender[0]) *
                    (.075 * lastPeriod + .54);
            i1[6] = detrender[3];

            //Advance the phase of I1 and Q1 by 90 degrees
            var jI = (.0962 * i1[6] + .5769 * i1[4] - .5769 * i1[2] - .0962 * i1[0]) * (.075 * lastPeriod + .54);
            var jQ = (.0962 * q1[6] + .5769 * q1[4] - .5769 * q1[2] - .0962 * q1[0]) * (.075 * lastPeriod + .54);

            //Phasor addition for 3 bar averaging)
            var i2 = i1[6] - jQ;
            var q2 = q1[6] + jI;

            //Smooth the I and Q components before applying the discriminator
            i2 = .2 * i2 + .8 * lastI2;
            q2 = .2 * q2 + .8 * lastQ2;

            //Homodyne Discriminator
            var re = i2 * lastI2 + q2 * lastQ2;
            var im = i2 * lastQ2 - q2 * lastI2;
            re = .2 * re + .8 * lastRe;
            im = .2 * im + .8 * lastIm;

            var period = 0D;
            if (im != 0 && re != 0)
                period = 2 * Math.PI / Math.Atan2(im, re);

            if (period > 1.5 * lastPeriod)
                period = 1.5 * lastPeriod;
            else if (period < .67 * lastPeriod)
                period = .67 * lastPeriod;

            if (period < 6)
                period = 6;
            else if (period > 50)
                period = 50;

            period = .2 * period + .8 * lastPeriod;
            //smoothPeriod = .33 * period + .67 * smoothPeriod[index - 1];

            var phase = 0D;
            if (i1[6] != 0)
                phase = Math.Atan2(q1[6], i1[6]) * 180 / Math.PI;

            var deltaPhase = Math.Max(1, lastPhase - phase);
            var alpha = Math.Max(SlowLimit, FastLimit / deltaPhase);
            var mama = alpha * source[index] + (1 - alpha) * lastMama;
            var result = m_isMama ? mama : (localExecuteContext.LastFama = .5 * alpha * mama + (1 - .5 * alpha) * lastFama);

            localExecuteContext.LastI2 = i2;
            localExecuteContext.LastQ2 = q2;
            localExecuteContext.LastPeriod = period;
            localExecuteContext.LastPhase = phase;
            localExecuteContext.LastRe = re;
            localExecuteContext.LastIm = im;
            localExecuteContext.LastMama = mama;
            return result;
        }

        private static void ShiftBuffer(double[] buffer)
        {
            Array.Copy(buffer, 1, buffer, 0, buffer.Length - 1);
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("MAMA", Language = Constants.En)]
    [HelperName("MAMA", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Мезоадаптивное сглаженное скользящее среднее.")]
    [HelperDescription("The Mesa Adaptive Moving Average.", Constants.En)]
    public sealed class MAMA : BaseAMA
    {
        public MAMA()
            : base(true)
        {
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("FAMA", Language = Constants.En)]
    [HelperName("FAMA", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Фрактальное сглаженное скользящее среднее.")]
    [HelperDescription("The Fractal Adaptive Moving Average.", Constants.En)]
    public sealed class FAMA : BaseAMA
    {
        public FAMA()
            : base(false)
        {
        }
    }
}
