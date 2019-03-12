using System;
using System.Collections.Generic;
using System.ComponentModel;
using TSLab.Script.Handlers.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Bars count for values sum", Language = Constants.En)]
    [HelperName("Количество баров для суммы значений", Language = Constants.Ru)]
    [Description("Количество баров для суммы значений")]
    [HelperDescription("Bars count for values sum", Constants.En)]
    public sealed class BarsCountForValuesSumHandler : DoubleStreamAndValuesHandler
    {
        private double[] m_source;

        public override bool IsGapTolerant => false;

        /// <summary>
        /// \~english Indicator values sum
        /// \~russian Сумма значений индикатора
        /// </summary>
        [HelperName("Values sum", Constants.En)]
        [HelperName("Сумма значений", Constants.Ru)]
        [Description("Сумма значений индикатора")]
        [HelperDescription("Indicator values sum", Constants.En)]
        [HandlerParameter(true, "1", Min = "0", Max = "2147483647", Step = "1", EditorMin = "1")]
        public double ValuesSum { get; set; }

        public override IList<double> Execute(IList<double> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (source.Count == 0)
                return EmptyArrays.Double;

            var results = Context.GetArray<double>(source.Count);
            for (var i = 0; i < source.Count; i++)
                results[i] = Calculate(source, i);

            return results;
        }

        protected override void InitExecuteContext()
        {
            m_source = Context.GetArray<double>(Context.BarsCount);
        }

        protected override void ClearExecuteContext()
        {
            Context.ReleaseArray(m_source);
            m_source = null;
        }

        protected override void InitForGap()
        {
            for (var i = m_executeContext.LastIndex; i < m_executeContext.Index; i++)
                m_source[i] = m_executeContext.GetSourceForGap(i);
        }

        protected override double Execute()
        {
            var index = m_executeContext.Index;
            m_source[index] = m_executeContext.Source;
            return Calculate(m_source, index);
        }

        private double Calculate(IList<double> source, int index)
        {
            var valuesSum = 0D;
            for (var j = index; j >= 0; j--)
            {
                valuesSum += source[j];
                if (valuesSum >= ValuesSum)
                    return index - j + 1;
            }
            return 0;
        }
    }
}
