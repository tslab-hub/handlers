using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    public abstract class AroonBase : DoubleStreamAndValuesHandlerWithPeriod
    {
        private ShrinkedList<double> m_source;

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
                if (Period == 1)
                    for (var i = 1; i < result.Length; i++)
                        result[i] = 100;
                else
                {
                    for (var i = 1; i < result.Length; i++)
                    {
                        var value = Calc(source, i);
                        result[i] = 100 * (Period - value) / Period;
                    }
                }
            }
            return result;
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                return;

            m_source = new ShrinkedList<double>(Period);
        }

        protected override void ClearExecuteContext()
        {
            m_source = null;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                return;

            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - Period + 1);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
                m_source.Add(m_executeContext.GetSourceForGap(i));
        }

        protected override double Execute()
        {
            if (IsSimple)
                return m_executeContext.Index == 0 ? 0 : 100;

            m_source.Add(m_executeContext.Source);
            if (m_executeContext.Index == 0)
                return 0;

            var value = Calc(m_source, m_source.Count - 1);
            var result = 100 * (Period - value) / Period;
            return result;
        }

        protected abstract double Calc(IList<double> source, int index);
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    //[HandlerName("Aroon+")]
    [HelperName("Aroon+", Language = Constants.En)]
    [HelperName("Aroon+", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индиатор Арун+.")]
    [HelperDescription("The Aroon+ indicator.", Constants.En)]
    public sealed class AroonUp : AroonBase
    {
        protected override double Calc(IList<double> source, int index)
        {
            return Highest(source, index, Period);
        }

        public static double Highest(IList<double> source, int index, int period)
        {
            var extremeIndex = index;
            var extremeValue = source[index];
            var firstIndex = Math.Max(index - period + 1, 0);

            for (var i = index - 1; i >= firstIndex; i--)
            {
                if (extremeValue < source[i])
                {
                    extremeIndex = i;
                    extremeValue = source[i];
                }
            }
            return index - extremeIndex;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    //[HandlerName("Aroon-")]
    [HelperName("Aroon-", Language = Constants.En)]
    [HelperName("Aroon-", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индикатор Арун-.")]
    [HelperDescription("The Aroon- indicator.", Constants.En)]
    public sealed class AroonDown : AroonBase
    {
        protected override double Calc(IList<double> source, int index)
        {
            return Lowest(source, index, Period);
        }

        public static double Lowest(IList<double> source, int index, int period)
        {
            var extremeIndex = index;
            var extremeValue = source[index];
            var firstIndex = Math.Max(index - period + 1, 0);

            for (var i = index - 1; i >= firstIndex; i--)
            {
                if (extremeValue > source[i])
                {
                    extremeIndex = i;
                    extremeValue = source[i];
                }
            }
            return index - extremeIndex;
        }
    }
}
