using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    // TODO: правильней назвать кубик Bar INDEX или Index of the bar
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Bar number", Language = Constants.En)]
    [HelperName("Номер бара", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.DOUBLE | TemplateTypes.INT | TemplateTypes.BOOL)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индекс элемента в списке баров или числовых значений.")]
    [HelperDescription("An index of an element in a list of bars or numeric values.", Constants.En)]
    public sealed class BarNumber : IOneSourceHandler, IDoubleReturns, IStreamHandler, ISecurityInputs, IDoubleInputs, IIntInputs, IBooleanInputs
    {
        /// <summary>
        /// Рудиментарная реализация интерфейса IList[double], которая для экономии памяти и повышения скорости
        /// обеспечивает только минимальный функционал. В качестве 'элементов' выступают индексы исходного списка баров.
        /// </summary>
        private sealed class IndexList : IList<double>
        {
            public IndexList(int count)
            {
                Count = count;
            }

            public IEnumerator<double> GetEnumerator()
            {
                for (var i = 0; i < Count; i++)
                    yield return i;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Add(double item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public bool Contains(double item)
            {
                return IndexOf(item) >= 0;
            }

            public void CopyTo(double[] array, int arrayIndex)
            {
                throw new NotSupportedException();
            }

            public bool Remove(double item)
            {
                throw new NotSupportedException();
            }

            public int Count { get; }

            public bool IsReadOnly
            {
                get { return true; }
            }

            public int IndexOf(double item)
            {
                var indexOf = (int)item;
                if (item != indexOf)
                    throw new ArgumentException(nameof(item));

                return indexOf >= 0 && indexOf < Count ? indexOf : -1;
            }

            public void Insert(int index, double item)
            {
                throw new NotSupportedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotSupportedException();
            }

            public double this[int index]
            {
                get
                {
                    if (index >= 0 && index < Count)
                        return index;

                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                set { throw new NotSupportedException(); }
            }
        }

        public IList<double> Execute(ISecurity security)
        {
            return new IndexList(security.Bars.Count);
        }

        public IList<double> Execute(IList<double> security)
        {
            return new IndexList(security.Count);
        }

        public IList<double> Execute(IList<int> security)
        {
            return new IndexList(security.Count);
        }

        public IList<double> Execute(IList<bool> security)
        {
            return new IndexList(security.Count);
        }
    }
}
