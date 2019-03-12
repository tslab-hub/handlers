using System;
using System.Collections;
using System.Collections.Generic;

namespace TSLab.Script.Handlers
{
    public sealed partial class InteractiveLineGen
    {
        private abstract class BaseList : IList<double>
        {
            protected BaseList(int count)
                : this(count, 0, count - 1)
            {
            }

            protected BaseList(int count, int minIndex, int maxIndex)
            {
                if (count == 0)
                {
                    if (minIndex != -1)
                        throw new ArgumentOutOfRangeException(nameof(minIndex));

                    if (maxIndex != -1)
                        throw new ArgumentOutOfRangeException(nameof(maxIndex));
                }
                else if (count > 0)
                {
                    if (minIndex < 0)
                        throw new ArgumentOutOfRangeException(nameof(minIndex));

                    if (maxIndex >= count)
                        throw new ArgumentOutOfRangeException(nameof(maxIndex));

                    if (minIndex > maxIndex)
                        throw new ArgumentOutOfRangeException(nameof(maxIndex));
                }
                else
                    throw new ArgumentOutOfRangeException(nameof(count));

                Count = count;
                MinIndex = minIndex;
                MaxIndex = maxIndex;
            }

            public IEnumerator<double> GetEnumerator()
            {
                if (Count > 0)
                {
                    for (var i = 0; i < MinIndex; i++)
                        yield return double.NaN;

                    for (var i = MinIndex; i <= MaxIndex; i++)
                        yield return GetValue(i);

                    for (var i = MaxIndex + 1; i < Count; i++)
                        yield return double.NaN;
                }
                else
                    yield return double.NaN;
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

            public abstract int IndexOf(double item);

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
                get { return index >= MinIndex && index <= MaxIndex ? GetValue(index) : double.NaN; }
                set { throw new NotSupportedException(); }
            }

            protected int MinIndex { get; }

            protected int MaxIndex { get; }

            protected abstract double GetValue(int index);
        }
    }
}
