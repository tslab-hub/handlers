namespace TSLab.Script.Handlers
{
    public sealed partial class InteractiveLineGen
    {
        private sealed class ConstList : BaseList
        {
            private readonly double m_value;

            public ConstList(double value, int count)
                : base(count)
            {
                m_value = value;
            }

            public ConstList(double value, int count, int minIndex, int maxIndex)
                : base(count, minIndex, maxIndex)
            {
                m_value = value;
            }

            public override int IndexOf(double item)
            {
                return m_value == item ? MinIndex : -1;
            }

            protected override double GetValue(int index)
            {
                return m_value;
            }
        }
    }
}
