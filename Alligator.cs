using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;

// ReSharper disable InconsistentNaming
namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("SMMA", Language = Constants.En)]
    [HelperName("SMMA", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Сглаженное скользящее среднее (Smoothed Moving Average, SMMA).")]
    [HelperDescription("The Smoothed Moving Average.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Alligator_tradable.xml", "Пример по индикатору Alligator", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/Alligator_tradable.xml", "Example of Alligator", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class SMMA : DoubleStreamAndValuesHandlerWithPeriod
    {
        private SMA m_sma;
        private Queue<double> m_queue;

        public override bool IsGapTolerant
        {
            get { return true; }
        }

        /// <summary>
        /// \~english Shift
        /// \~russian Сдвиг
        /// </summary>
        [HelperName("Shift", Constants.En)]
        [HelperName("Сдвиг", Constants.Ru)]
        [Description("Сдвиг")]
        [HelperDescription("Shift", Constants.En)]
        [HandlerParameter(true, "5", Min = "1", Max = "10", Step = "1")]
        public int Shift { get; set; }

        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.SMMA(source, Period, Shift, Context);
            return result;
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                return;

            m_sma = new SMA { Context = Context, Period = Period };
            if (Shift > 0)
            {
                m_queue = new Queue<double>(Shift + 1);
                for (var i = 0; i < Shift; i++)
                    m_queue.Enqueue(m_executeContext.Source);
            }
        }

        protected override void ClearExecuteContext()
        {
            m_sma = null;
            m_queue = null;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                return;

            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - Period + 1 - Shift);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                var source = m_executeContext.GetSourceForGap(i);
                Calc(source, i);
            }
        }

        protected override double Execute()
        {
            if (IsSimple)
                return m_executeContext.Source;

            var result = Calc(m_executeContext.Source, m_executeContext.Index);
            return result;
        }

        protected override bool IsSimple
        {
            get { return (Period == 1 && Shift == 0) || Context.BarsCount == 1; }
        }

        private double Calc(double source, int index)
        {
            var result = m_sma.Execute(source, index);
            if (m_queue != null)
            {
                m_queue.Enqueue(result);
                result = m_queue.Dequeue();
            }
            return result;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("LWMA", Language = Constants.En)]
    [HelperName("LWMA", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Линейно-взвешенное сглаженное скользящее среднее.")]
    [HelperDescription("The Linear-Weighted Moving Average", Constants.En)]
    public sealed class LWMA : DoubleStreamAndValuesHandlerWithPeriod
    {
        private ShrinkedList<double> m_source;
        private int m_iSum;

        public override bool IsGapTolerant
        {
            get { return true; }
        }

        public override IList<double> Execute(IList<double> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = Context?.GetArray<double>(source.Count) ?? new double[source.Count];
            if (result.Length >= Period)
            {
                if (Period == 1)
                    source.CopyTo(result, 0);
                else
                {
                    var iSum = GetISum();
                    for (var i = Period - 1; i < result.Length; i++)
                        result[i] = Calc(source, i, iSum);
                }
            }
            return result;
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                return;

            m_source = new ShrinkedList<double>(Period);
            m_iSum = GetISum();
        }

        protected override void ClearExecuteContext()
        {
            m_source = null;
            m_iSum = 0;
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
                return m_executeContext.Index >= Period - 1 ? m_executeContext.Source : 0;

            m_source.Add(m_executeContext.Source);
            var result = m_source.Count == Period ? Calc(m_source, m_source.Count - 1, m_iSum) : 0;
            return result;
        }

        protected override bool IsSimple
        {
            get { return Period == 1 || Context.BarsCount < Period; }
        }

        // TODO: сумма арифметической прогрессии имеет формулу в замкнутом виде.
        private int GetISum()
        {
            var iSum = 0;
            for (var i = 1; i <= Period; i++)
                iSum += i;

            return iSum;
        }

        private double Calc(IList<double> source, int index, int iSum)
        {
            var sum = 0D;
            for (var i = 1; i <= Period; i++)
                sum += source[index - Period + i] * i;

            var result = sum / iSum;
            return result;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Median Price", Language = Constants.En)]
    [HelperName("Медианная цена", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Функция суммирует значения цен High и Low для бара, а затем делит эту сумму на 2. Это значение является серединой бара.")]
    [HelperDescription("This function sums up values of High and Low in the bar, and then divides this sum by 2. This value is the middle of the bar.", Constants.En)]
    public sealed class MedianPrice : IBar2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        public IList<double> Execute(ISecurity source)
        {
            return Series.MedianPrice(source.Bars, Context);
        }
    }
}
