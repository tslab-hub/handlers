using System;
using System.Collections;
using System.Collections.Generic;

namespace TSLab.Script.Handlers
{
    internal sealed class ConstList<T> : IList<T>, IReadOnlyList<T>
    {
        private static readonly IEqualityComparer<T> s_equalityComparer = EqualityComparer<T>.Default;
        private readonly T m_value;

        public ConstList(int count, T value)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            Count = count;
            m_value = value;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
                yield return m_value;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T item)
        {
            return Count > 0 && s_equalityComparer.Equals(m_value, item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotSupportedException();
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        public int Count { get; }

        public bool IsReadOnly => true;

        public int IndexOf(T item)
        {
            return Contains(item) ? 0 : -1;
        }

        public void Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return m_value;
            }
            set { throw new NotSupportedException(); }
        }
    }
}
