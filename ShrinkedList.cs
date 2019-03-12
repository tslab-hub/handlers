using System;
using System.Collections;
using System.Collections.Generic;

namespace TSLab.Script.Handlers
{
    public sealed class ShrinkedList<T> : IList<T>
    {
        private readonly List<T> m_list;

        public ShrinkedList(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            m_list = new List<T>(capacity);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_list.GetEnumerator();
        }

        public void Add(T item)
        {
            if (m_list.Count == m_list.Capacity)
                m_list.RemoveAt(0);

            m_list.Add(item);
        }

        public void Clear()
        {
            m_list.Clear();
        }

        public bool Contains(T item)
        {
            return m_list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            m_list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            return m_list.Remove(item);
        }

        public int Count
        {
            get { return m_list.Count; }
        }

        public bool IsReadOnly
        {
            get { return ((IList<T>)m_list).IsReadOnly; }
        }

        public int IndexOf(T item)
        {
            return m_list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            m_list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            m_list.RemoveAt(index);
        }

        public T this[int index]
        {
            get { return m_list[index]; }
            set { m_list[index] = value; }
        }
    }
}
