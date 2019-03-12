namespace TSLab.Script.Handlers
{
    public sealed partial class InteractiveLineGen
    {
        private sealed class LineList : BaseList
        {
            private readonly double m_a;
            private readonly double m_b;

            public LineList(double a, double b, int count, int minIndex, int maxIndex)
                : base(count, minIndex, maxIndex)
            {
                m_a = a;
                m_b = b;
            }

            public override int IndexOf(double item)
            {
                var minValue = GetValue(MinIndex);
                var maxValue = GetValue(MaxIndex);

                if (minValue > maxValue)
                {
                    var value = minValue;
                    minValue = maxValue;
                    maxValue = value;
                }
                if (item >= minValue && item <= maxValue)
                    for (var i = MinIndex; i <= MaxIndex; i++)
                        if (GetValue(i) == item)
                            return i;

                return -1;
            }

            protected override double GetValue(int index)
            {
                return m_a * index + m_b;
            }
        }
    }
}
