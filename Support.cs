using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable InconsistentNaming
namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Flip", Language = Constants.En)]
    [HelperName("Перевернуть", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.BOOL)]
    [Input(1, TemplateTypes.BOOL)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Имитирует работу триггера с двумя входами, при появлении 'Истина' на первом входе, выходное значение становится 'Истина' до появления 'Истина' на втором входе. При появлении 'Истина' на втором входе, значение индикатора становится 'Ложь', до появления значения 'Истина' на первом входе. Если 'Истина' появляется одновременно на двух входах, то значение индикатора - 'Ложь', т.е. первый вход игнорируется.")]
    [HelperDescription("Imitates a trigger with 2 entries, when True appears at the first entry, an outgoing value becomes True, until True appears at the second entry. When True appears at the second entry, an indicator value becomes False, until True appears at the first entry. If True appears at two entries simultaneously, then an indicator valye is False, so it means that the first entry is ignored.", Constants.En)]
    public class Flip : IBoolAccum
    {
        public IContext Context { get; set; }

        public IList<bool> Execute(IList<bool> source1, IList<bool> source2)
        {
            return Calc(source1, source2, Context);
        }

        public bool m_last;

        public bool Execute(bool source1, bool source2)
        {
            m_last = Calc(source1, source2, m_last);
            return m_last;
        }

        public static IList<bool> Calc(IList<bool> source1, IList<bool> source2, IMemoryContext context = null)
        {
            var res = context?.GetArray<bool>(source1.Count) ?? new bool[source1.Count];
            for (int i = 1; i < source1.Count; i++)
            {
                // TODO: в этой проверке i всегда больше 0, потому что мы внутри фор от 1.
                var last = i > 0 && res[i - 1];
                res[i] = Calc(source1[i], source2[i], last);
            }
            return res;
        }

        public static bool Calc(bool source1, bool source2, bool last)
        {
            var res = last;
// ReSharper disable ConvertIfToOrExpression
            if (!res & source1)
// ReSharper restore ConvertIfToOrExpression
            {
                res = true;
            }
            if (res & source2)
            {
                res = false;
            }
            return res;
        }
    }

    // TODO: неточное описание. Сигнал не задерживается, а запоминается на Period баров.
    // Таким образом можно к примеру продлевать область действия точечного события типа Пересечение.
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Hold", Language = Constants.En)]
    [HelperName("Задержать", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.BOOL)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Задерживает входящий логический сигнал на N свечей (параметр 'Период'). Т.е. если входящее значение на определенной свече становится 'Истина', то оно будет продублировано на N свечей.")]
    [HelperDescription("Holds an incoming Boolean signal during N candles (the Period parameter). So, if an incoming candle value becomes True, it will be duplicated for N candles.", Constants.En)]
    public class Hold : BasePeriodIndicatorHandler, IBoolConvertor, IContextUses
    {
        public IContext Context { get; set; }

        public IList<bool> Execute(IList<bool> source)
        {
            return Calc(source, Period, Context);
        }

        private int m_savedNum = -1;

        public bool Execute(bool source, int num)
        {
            return Calc(source, Period, num, ref m_savedNum);
        }

        public static IList<bool> Calc(IList<bool> source, int period, IMemoryContext context = null)
        {
            var res = context?.GetArray<bool>(source.Count) ?? new bool[source.Count];
            int savedNum = -1;
            for (int i = 1; i < source.Count; i++)
            {
                res[i] = Calc(source[i], period, i, ref savedNum);
            }
            return res;
        }

        public static bool Calc(bool source, int period, int num, ref int savedNum)
        {
            if (source)
            {
                savedNum = num;
            }
            if (savedNum >= 0 && (num - savedNum) <= period)
            {
                source = true;
            }
            return source;
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Shift", Language = Constants.En)]
    [HelperName("Сдвиг", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Сдвиг значений на N свечей вправо.")]
    [HelperDescription("Moves values for N candles to the right.", Constants.En)]
    public sealed class Shift : DoubleStreamAndValuesHandlerWithPeriod
    {
        private double m_firstValue;
        private Queue<double> m_queue;

        public override bool IsGapTolerant
        {
            get { return true; }
        }

        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.Shift(source, Period, Context);
            return result;
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                m_firstValue = m_executeContext.Source;
            else
            {
                m_queue = new Queue<double>(Period);
                for (var i = 0; i < Period; i++)
                    m_queue.Enqueue(m_executeContext.Source);
            }
        }

        protected override void ClearExecuteContext()
        {
            m_firstValue = 0;
            m_queue = null;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                return;

            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - Period);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                var result = m_queue.Dequeue();
                var source = m_executeContext.GetSourceForGap(i);
                m_queue.Enqueue(source);
            }
        }

        protected override double Execute()
        {
            if (IsSimple)
                return m_firstValue;

            var result = m_queue.Dequeue();
            m_queue.Enqueue(m_executeContext.Source);
            return result;
        }

        protected override bool IsSimple
        {
            get { return Context.BarsCount <= Period; }
        }
    }
}
