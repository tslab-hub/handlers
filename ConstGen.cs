using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace TSLab.Script.Handlers
{
    //[HandlerName("Constant")]
    [HandlerCategory(HandlerCategories.TradeMath, "Const", true)]
    [HelperName("Constant", Language = Constants.En)]
    [HelperName("Константа", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [Description("Постоянное значение.")]
    [HelperDescription("A constant value.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/StochK.xml", "Пример по индикатору Stochastic K", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/StochK.xml", "Example of Stochastic K", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/2MA_customStop.xml", "Пример стратегии 2МА с нестандартным стопом", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/2MA_customStop.xml", "Example of 2МА (with nonstandard stop)", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class ConstGen : ConstGenBase<double>, IBar2DoubleHandler, IDouble1CalculatorHandler, IDouble2DoubleHandler
    {
        /// <summary>
        /// \~english A value to return as output of a handler
        /// \~russian Значение на выходе блока
        /// </summary>
        [HelperName("Value", Constants.En)]
        [HelperName("Значение", Constants.Ru)]
        [Description("Значение на выходе блока")]
        [HelperDescription("A value to return as output of a handler", Constants.En)]
        [HandlerParameter]
        public double Value { get; set; }

        public IList<double> Execute(IContext context)
        {
            MakeList(context.BarsCount, Value);
            return this;
        }

        public IList<double> Execute(ISecurity source)
        {
            MakeList(source.Bars.Count, Value);
            return this;
        }

        public double Execute(double source1)
        {
            return Value;
        }

        public IList<double> Execute(IList<double> source)
        {
            MakeList(source.Count, Value);
            return this;
        }
    }

    //[HandlerName("Bool Constant")]
    [HandlerCategory(HandlerCategories.TradeMath, "BoolConst", true)]
    [HelperName("Boolean Constant", Language = Constants.En)]
    [HelperName("Логическая константа", Language = Constants.Ru)]
    [Description("По аналогии с константой выдает фиксированное логическое значение на каждый бар.")]
    [HelperDescription("This block sets a fixed boolean value for every bar.", Constants.En)]
    public class BoolConst : ConstGenBase<bool>, IBar2BoolsHandler
    {
        /// <summary>
        /// \~english A value to return as output of a handler
        /// \~russian Значение на выходе блока
        /// </summary>
        [HelperName("Value", Constants.En)]
        [HelperName("Значение", Constants.Ru)]
        [Description("Значение на выходе блока")]
        [HelperDescription("A value to return as output of a handler", Constants.En)]
        [HandlerParameter]
        public bool Value { get; set; }

        public IList<bool> Execute(IContext context)
        {
            MakeList(context.BarsCount, Value);
            return this;
        }

        public IList<bool> Execute(ISecurity source)
        {
            MakeList(source.Bars.Count, Value);
            return this;
        }
    }

    // useless test class

    //[HandlerName("Bool Breaker")]
    [HandlerCategory(HandlerCategories.TradeMath, "BoolConst", true)]
    [HelperName("Boolean Breaker", Language = Constants.En)]
    [HelperName("Логический разделитель", Language = Constants.Ru)]
    [Description("Используется совместно с блоком 'Контрольная панель' для осуществления ручного управления кнопками на ней (режим полуавтоматической торговли). При нажатии на кнопку будет выдан true только для текущей свечи, что гарантирует, что сигнал будет выдан всегда на текущей свече.")]
    [HelperDescription("Sets a FALSE value at every candle except the last one. For the last value sets a selected TRUE/FALSE value. This block is used together with the control pane buttons.", Constants.En)]
    public sealed class BoolBreaker : BoolConst
    {
        public override IEnumerator<bool> GetEnumerator()
        {
            return new BreakerEnumerator(m_count, m_value);
        }

        public override bool this[int index]
        {
            get { return index == m_count - 1 && m_value; }
            set { throw new InvalidOperationException(); }
        }

        private sealed class BreakerEnumerator : Enumerator
        {
            public BreakerEnumerator(int size, bool value)
                : base(size, value)
            {
            }

            public override bool Current
            {
                get { return m_cur >= m_size - 1 && m_value; }
            }
        }
    }

    /// <summary>
    /// Рудиментарная реализация интерфейса IList[T], которая для экономии памяти и повышения скорости обеспечивает только минимальный функционал.
    /// В качестве всех 'элементов списка' выступает одно значение, переданное в конструкторе.
    /// </summary>
    public class ConstGenBase<T> : IList<T>
        where T : struct, IComparable
    {
        protected int m_count = int.MaxValue;

        protected T m_value;

        protected ConstGenBase()
        {
        }

        public ConstGenBase(int count, T value)
        {
            m_count = count;
            m_value = value;
        }

        protected void MakeList(int count, T value)
        {
            m_count = count;
            m_value = value;
        }

        #region Implementation of IEnumerable

        public virtual IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(m_count, m_value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Implementation of ICollection<double>

        public void Add(T item)
        {
            throw new InvalidOperationException();
        }

        public void Clear()
        {
            throw new InvalidOperationException();
        }

        public bool Contains(T item)
        {
            return item.Equals(m_value);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            for (var i = arrayIndex; i < array.Length; i++)
                array[i] = m_value;
        }

        public bool Remove(T item)
        {
            throw new InvalidOperationException();
        }

        public int Count
        {
            get { return m_count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        #endregion

        #region Implementation of IList<T>

        public int IndexOf(T item)
        {
            return 0;
        }

        public void Insert(int index, T item)
        {
            throw new InvalidOperationException();
        }

        public void RemoveAt(int index)
        {
            throw new InvalidOperationException();
        }

        public virtual T this[int index]
        {
            get { return m_value; }
            set { throw new InvalidOperationException(); }
        }

        #endregion

        protected class Enumerator : IEnumerator<T>
        {
            protected readonly int m_size;
            protected readonly T m_value;
            protected int m_cur;

            public Enumerator(int size, T value)
            {
                m_size = size;
                m_cur = 0;
                m_value = value;
            }

            #region Implementation of IDisposable

            public void Dispose()
            {
            }

            #endregion

            #region Implementation of IEnumerator

            public bool MoveNext()
            {
                return ++m_cur <= m_size;
            }

            public void Reset()
            {
                m_cur = 0;
            }

            public virtual T Current
            {
                get { return m_value; }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            #endregion
        }
    }
}