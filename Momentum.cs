using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    public abstract class MomentumBase : DoubleStreamAndValuesHandlerWithPeriod
    {
        private ShrinkedList<double> m_source;

        public override bool IsGapTolerant
        {
            get { return true; }
        }

        public override IList<double> Execute(IList<double> source)
        {
            var result = Context?.GetArray<double>(source.Count) ?? new double[source.Count];
            for (var i = 0; i < result.Length; i++)
                result[i] = Calc(source, i);

            return result;
        }

        protected override void InitExecuteContext()
        {
            m_source = new ShrinkedList<double>(Period + 1);
        }

        protected override void ClearExecuteContext()
        {
            m_source = null;
        }

        protected override void InitForGap()
        {
            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - Period);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
                m_source.Add(m_executeContext.GetSourceForGap(i));
        }

        protected override double Execute()
        {
            m_source.Add(m_executeContext.Source);
            var result = Calc(m_source, m_source.Count - 1);
            return result;
        }

        protected abstract double Calc(IList<double> source, int index);
    }

    //[HandlerName("Momentum %")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Momentum %", Language = Constants.En)]
    [HelperName("Моментум %", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индикатор момента (Momentum indicator) нормированный, также известен как Rate Of Change (ROC). Отрицательных значений не принимает, рассчитывается в процентах как MOMENTUM = CLOSE[i] / CLOSE[i - n] * 100.")]
    [HelperDescription("The Momentum indicator, also known as Rate Of Change (ROC). Does not accept any negative values, calculated in percents as MOMENTUM = CLOSE[i] / CLOSE[i - n] * 100)", Constants.En)]
    public sealed class MomentumPct : MomentumBase
    {
        protected override double Calc(IList<double> source, int index)
        {
            var k = Math.Max(0, index - Period);
            var lastSource = source[k];
            var result = lastSource != 0 ? source[index] / lastSource * 100 : 0;
            return result;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Momentum", Language = Constants.En)]
    [HelperName("Моментум", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индикатор момента (Momentum indicator). Рассчитывается как Momentum Simple = C[0] - C[n], где C[0] - цена закрытия текущего периода, а С[n] - цена закрытия N периодов назад.")]
    [HelperDescription("The Momentum indicator. Calculated as Momentum Simple = C[0] - C[n], C[0] - a current period closing price. С[n] - a closing price as it was N periods ago.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Momentum.xml", "Пример по индикатору Momentum", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/Momentum.xml", "Example of Momentum", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class Momentum : MomentumBase
    {
        protected override double Calc(IList<double> source, int index)
        {
            var k = Math.Max(0, index - Period);
            var result = source[index] - source[k];
            return result;
        }
    }

    // TODO: в русской локали название осциллятора все равно дано по-английски)
    //[HandlerName("Chande Momentum Oscillator")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Chande Momentum Oscillator", Language = Constants.En)]
    [HelperName("Chande Momentum Oscillator", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Осциллятор Моментум Тушара Чандэ (Tushar Chande).")]
    [HelperDescription("The Chande Momentum Oscillator.", Constants.En)]
    public sealed class MomentumOsc : DoubleStreamAndValuesHandlerWithPeriod
    {
        private sealed class LocalExecuteContext
        {
            public LocalExecuteContext(int period)
            {
                if (period > 1)
                {
                    M1 = new Queue<double>(period);
                    M2 = new Queue<double>(period);
                }
            }
            public Queue<double> M1 { get; }
            public Queue<double> M2 { get; }
            public double Osc1 { get; set; }
            public double Osc2 { get; set; }
            public double LastSource { get; set; }
        }

        private LocalExecuteContext m_localExecuteContext;

        public override bool IsGapTolerant
        {
            get { return true; }
        }

        public override IList<double> Execute(IList<double> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = Context?.GetArray<double>(source.Count) ?? new double[source.Count];
            if (result.Length > 1)
            {
                var localExecuteContext = new LocalExecuteContext(Period);
                for (var i = 0; i < result.Length; i++)
                    result[i] = Calc(localExecuteContext, source[i], i);
            }
            return result;
        }

        protected override void InitExecuteContext()
        {
            m_localExecuteContext = new LocalExecuteContext(Period);
        }

        protected override void ClearExecuteContext()
        {
            m_localExecuteContext = null;
        }

        protected override void InitForGap()
        {
            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - Period);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                var source = m_executeContext.GetSourceForGap(i);
                var result = Calc(m_localExecuteContext, source, i);
            }
        }

        protected override double Execute()
        {
            var result = Calc(m_localExecuteContext, m_executeContext.Source, m_executeContext.Index);
            return result;
        }

        private double Calc(LocalExecuteContext localExecuteContext, double source, int index)
        {
            if (index == 0)
            {
                localExecuteContext.LastSource = source;
                return 0;
            }
            var m = source - localExecuteContext.LastSource;
            localExecuteContext.LastSource = source;

            var m1 = m > 0 ? m : 0;
            var m2 = m < 0 ? -m : 0;

            if (Period > 1)
            {
                if (localExecuteContext.M1.Count == Period)
                {
                    localExecuteContext.Osc1 -= localExecuteContext.M1.Dequeue();
                    localExecuteContext.Osc2 -= localExecuteContext.M2.Dequeue();
                }
                localExecuteContext.M1.Enqueue(m1);
                localExecuteContext.M2.Enqueue(m2);

                localExecuteContext.Osc1 += m1;
                localExecuteContext.Osc2 += m2;
            }
            else
            {
                localExecuteContext.Osc1 = m1;
                localExecuteContext.Osc2 = m2;
            }
            var oscSum = localExecuteContext.Osc1 + localExecuteContext.Osc2;
            var result = oscSum != 0 ? (localExecuteContext.Osc1 - localExecuteContext.Osc2) / oscSum * 100 : 0;
            return result;
        }
    }
}
