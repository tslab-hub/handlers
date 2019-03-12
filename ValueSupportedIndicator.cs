using System;
using System.Collections.Generic;

namespace TSLab.Script.Handlers
{
    [Obsolete]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE, Name = "ItemConnector.Src")]
    public abstract class ValueSupportedIndicator : IValuesHandlerWithNumber, IDoubleReturns, IContextUses
    {
        private double[] m_result, m_source;

        public double Execute(double source, int i)
        {
            if (i < 0 || i >= Context.BarsCount)
                throw new ArgumentOutOfRangeException(nameof(i));

            if (m_result == null)
            {
                m_result = Context.GetArray<double>(Context.BarsCount);
                m_source = Context.GetArray<double>(Context.BarsCount);
            }
            m_source[i] = source;
            return Execute(m_source, m_result, i);
        }

        protected abstract double Execute(IList<double> source, IList<double> result, int num);

        public IContext Context { get; set; }
    }

    [Obsolete]
    public abstract class ValueSupportedIndicatorWithPeriod : ValueSupportedIndicator
    {
        private int m_period = 1;

        [HandlerParameter(true, "20", Min = "10", Max = "100", Step = "5", EditorMin = "1")]
        public int Period
        {
            get { return m_period; }
            set { m_period = Math.Max(value, 1); }
        }
    }

    [InputsCount(2)]
    [Input(0, TemplateTypes.DOUBLE)]
    [Input(1, TemplateTypes.DOUBLE)]
    public abstract class ValueSupportedComparer : IValuesHandlerWithNumber, IBooleanReturns, IContextUses
    {
        private bool[] m_data;
        private double[] m_source1, m_source2;

        public bool Execute(double source1, double source2, int i)
        {
            if (m_data == null)
            {
                m_data = Context.GetArray<bool>(Context.BarsCount);
                m_source1 = Context.GetArray<double>(Context.BarsCount);
                m_source2 = Context.GetArray<double>(Context.BarsCount);
            }
            m_source1[i] = source1;
            m_source2[i] = source2;
            return Execute(m_source1, m_source2, m_data, i);
        }

        protected abstract bool Execute(IList<double> source1, IList<double> source2, IList<bool> data, int num);

        public IContext Context { get; set; }
    }
}
