using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    public abstract class DoubleStreamAndValuesHandler : IDoubleStreamAndValuesHandler
    {
        protected sealed class ExecuteContext
        {
            private double m_a;
            private double m_b;

            public double Source { get; set; }
            public int Index { get; set; }
            public double LastResult { get; set; }
            public double LastSource { get; set; }
            public int LastIndex { get; set; }

            public void InitForGap()
            {
                if (LastIndex < 0)
                {
                    m_a = 0;
                    m_b = Source;
                }
                else
                {
                    m_a = (LastSource - Source) / (LastIndex - Index);
                    m_b = LastSource - m_a * LastIndex;
                }
            }

            public double GetSourceForGap(int i)
            {
                var source = m_a * i + m_b;
                return source;
            }
        }

        protected ExecuteContext m_executeContext;

        public IContext Context { get; set; }

        public abstract bool IsGapTolerant { get; }

        public abstract IList<double> Execute(IList<double> source);

        public double Execute(double source, int index)
        {
            if (index < 0 || index >= Context.BarsCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (m_executeContext != null)
            {
                if (index < m_executeContext.LastIndex)
                    throw new ArgumentException(nameof(index));

                if (index == m_executeContext.LastIndex)
                    return m_executeContext.LastResult;

                m_executeContext.Source = source;
                m_executeContext.Index = index;
            }
            else
            {
                m_executeContext = new ExecuteContext { Source = source, Index = index, LastIndex = -1 };
                InitExecuteContext();
            }
            if (index - m_executeContext.LastIndex > 1)
            {
                m_executeContext.InitForGap();
                InitForGap();
            }
            m_executeContext.LastResult = Execute();
            m_executeContext.LastSource = source;
            m_executeContext.LastIndex = index;

            if (index == Context.BarsCount - 1)
                ClearExecuteContext();

            return m_executeContext.LastResult;
        }

        protected abstract void InitExecuteContext();

        protected abstract void ClearExecuteContext();

        protected abstract void InitForGap();

        protected abstract double Execute();
    }

    public abstract class DoubleStreamAndValuesHandlerWithPeriod : DoubleStreamAndValuesHandler, IDoubleStreamAndValuesHandlerWithPeriod
    {
        protected const int GapTolerancePeriodMultiplier = 2;
        private int m_period = 1;

        /// <summary>
        /// \~english Indicator period (processing window)
        /// \~russian Период индикатора (окно расчетов)
        /// </summary>
        [HelperName("Period", Constants.En)]
        [HelperName("Период", Constants.Ru)]
        [Description("Период индикатора (окно расчетов)")]
        [HelperDescription("Indicator period (processing window)", Constants.En)]
        [HandlerParameter(true, "20", Min = "10", Max = "100", Step = "5", EditorMin = "1")]
        public int Period
        {
            get { return m_period; }
            set { m_period = Math.Max(value, 1); }
        }

        protected virtual bool IsSimple
        {
            get { return Period == 1 || Context.BarsCount == 1; }
        }
    }
}
