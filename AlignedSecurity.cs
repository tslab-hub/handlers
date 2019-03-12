using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using TSLab.DataSource;

namespace TSLab.Script.Handlers
{
    public sealed partial class AlignedSecurity : DisposeHelper, ISecurity
    {
        private sealed class AlignedDataBar : IDataBar
        {
            private readonly IDataBar m_bar;

            public AlignedDataBar(IDataBar bar, int originalIndex)
            {
                m_bar = bar;
                OriginalIndex = originalIndex;
            }

            public int OriginalIndex { get; }

            public object Clone()
            {
                throw new NotSupportedException();
            }

            public void Store(BinaryWriter stream)
            {
                throw new NotSupportedException();
            }

            public void Restore(BinaryReader stream, int version)
            {
                throw new NotSupportedException();
            }

            public DateTime Date
            {
                get { return m_bar.Date; }
                set { m_bar.Date = value; }
            }

            public long Ticks
            {
                get { return m_bar.Ticks; }
                set { m_bar.Ticks = value; }
            }

            public double PotensialOpen
            {
                get { return m_bar.PotensialOpen; }
            }

            public double Open => m_bar.Open;

            public double Low => m_bar.Low;

            public double High => m_bar.High;

            public double Close => m_bar.Close;

            public bool IsAdditional => m_bar.IsAdditional;

            public bool IsReadonly => m_bar.IsReadonly;

            public int TicksCount => m_bar.TicksCount;

            public void Add(IBaseBar b2)
            {
                m_bar.Add(b2);
            }

            public double Volume => m_bar.Volume;

            public double Interest => m_bar.Interest;

            public TradeNumber FirstTradeId => m_bar.FirstTradeId;

            public IDataBar MakeAdditional(DateTime newTime, bool byOpen)
            {
                throw new NotSupportedException();
            }
        }
        private static readonly IReadOnlyList<AlignedDataBar> s_emptyBars = new AlignedDataBar[0];
        private static readonly IReadOnlyList<Trade> s_emptyTrades = new Trade[0];
        private readonly ISecurity m_security;
        private readonly TimeSpan m_timeFrame;
        private IReadOnlyList<AlignedDataBar> m_bars;

        public AlignedSecurity(ISecurity security, TimeSpan timeFrame)
        {
            if (timeFrame <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeFrame));

            m_security = security ?? throw new ArgumentNullException(nameof(security));
            m_timeFrame = timeFrame;
            SecurityDescription = new DataSourceSecurity(security.SecurityDescription);
        }

        protected override void Dispose(bool disposing)
        {
        }

        public string Symbol => m_security.Symbol;

        public IDataSourceSecurity SecurityDescription { get; }

        public FinInfo FinInfo
        {
            get { throw new NotSupportedException(); }
        }

        public IReadOnlyList<IDataBar> Bars
        {
            get
            {
                if (m_bars != null)
                    return m_bars;

                lock (this)
                {
                    if (m_bars != null)
                        return m_bars;

                    var originalBars = m_security.Bars;
                    if (originalBars.Count == 0)
                        return m_bars = s_emptyBars;

                    if (originalBars.Count == 1)
                        return m_bars = new[] { new AlignedDataBar(originalBars[0], 0) };

                    for (var i = 1; i < originalBars.Count; i++)
                        if (ReferenceEquals(originalBars[i - 1], originalBars[i]))
                            throw new InvalidOperationException(string.Format("There are the same bars at {0} and {1} indexes.", i - 1, i));

                    DateTime firstDateTime, lastDateTime;
                    TimeFrameUtils.GetFirstBounds(m_timeFrame, originalBars[0].Date, out firstDateTime, out lastDateTime);
                    var interval = GetInterval();
                    var lastClose = double.NaN;
                    var alignedBars = new List<AlignedDataBar>(originalBars.Count);

                    for (int i = 0, firstIndex = 0; i <= originalBars.Count; i++)
                    {
                        if (i == originalBars.Count || originalBars[i].Date >= lastDateTime)
                        {
                            var dateTime = firstDateTime;
                            for (int j = firstIndex, jMax = i - 1; j <= jMax; j++)
                            {
                                var originalBar = originalBars[j];
                                while (dateTime < originalBar.Date)
                                {
                                    alignedBars.Add(new AlignedDataBar(new DataBar(dateTime, lastClose, lastClose, lastClose, lastClose), -1));
                                    dateTime += interval;
                                }
                                alignedBars.Add(new AlignedDataBar(originalBar, j));
                                dateTime = originalBar.Date + interval;
                                lastClose = originalBar.Close;

                                if (j == jMax)
                                {
                                    while (dateTime < lastDateTime)
                                    {
                                        alignedBars.Add(new AlignedDataBar(new DataBar(dateTime, lastClose, lastClose, lastClose, lastClose), -1));
                                        dateTime += interval;
                                    }
                                }
                            }
                            if (i == originalBars.Count)
                                break;

                            TimeFrameUtils.GetBounds(m_timeFrame, originalBars[i].Date, ref firstDateTime, ref lastDateTime);
                            firstIndex = i;
                        }
                    }
                    return m_bars = alignedBars;
                }
            }
        }

        public bool IsBarsLoaded => m_bars != null;

        private TimeSpan GetInterval()
        {
            switch (IntervalBase)
            {
                case DataIntervals.DAYS:
                    return TimeSpan.FromDays(Interval);
                case DataIntervals.MINUTE:
                    return TimeSpan.FromMinutes(Interval);
                case DataIntervals.SECONDS:
                    return TimeSpan.FromSeconds(Interval);
                default:
                    throw new InvalidEnumArgumentException(nameof(IntervalBase), (int)IntervalBase, IntervalBase.GetType());
            }
        }

        public IReadOnlyList<IQueueData> GetBuyQueue(int barNum)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<IQueueData> GetSellQueue(int barNum)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<ITrade> GetTrades(int barNum)
        {
            var bars = Bars;
            var bar = (AlignedDataBar)bars[barNum];
            return bar.OriginalIndex >= 0 ? m_security.GetTrades(bar.OriginalIndex) : s_emptyTrades;
        }

        public IReadOnlyList<ITrade> GetTrades(int firstBarIndex, int lastBarIndex)
        {
            var bars = Bars.Skip(firstBarIndex).Take(lastBarIndex - firstBarIndex + 1).Cast<AlignedDataBar>().Where(item => item.OriginalIndex >= 0);
            if (!bars.Any())
                return s_emptyTrades;

            var firstBar = bars.First();
            var lastBar = bars.Last();
            return m_security.GetTrades(firstBar.OriginalIndex, lastBar.OriginalIndex);
        }

        public int GetTradesCount(int firstBarIndex, int lastBarIndex)
        {
            var bars = Bars.Skip(firstBarIndex).Take(lastBarIndex - firstBarIndex + 1).Cast<AlignedDataBar>().Where(item => item.OriginalIndex >= 0);
            if (!bars.Any())
                return 0;

            var firstBar = bars.First();
            var lastBar = bars.Last();
            return m_security.GetTradesCount(firstBar.OriginalIndex, lastBar.OriginalIndex);
        }

        public IReadOnlyList<IReadOnlyList<ITrade>> GetTradesPerBar(int firstBarIndex, int lastBarIndex)
        {
            var count = lastBarIndex - firstBarIndex + 1;
            var bars = Bars.Skip(firstBarIndex).Take(count).Cast<AlignedDataBar>().Where(item => item.OriginalIndex >= 0);

            if (!bars.Any())
            {
                var emptyTradesPerBar = new IReadOnlyList<Trade>[count];
                for (var i = 0; i < count; i++)
                    emptyTradesPerBar[i] = s_emptyTrades;

                return emptyTradesPerBar;
            }
            var firstBar = bars.First();
            var lastBar = bars.Last();
            var originalTradesPerBar = m_security.GetTradesPerBar(firstBar.OriginalIndex, lastBar.OriginalIndex);

            if (firstBarIndex == firstBar.OriginalIndex && lastBarIndex == lastBar.OriginalIndex)
                return originalTradesPerBar;

            var tradesPerBar = new IReadOnlyList<ITrade>[count];
            for (var i = firstBarIndex; i <= lastBarIndex; i++)
            {
                var barOriginalIndex = m_bars[i].OriginalIndex;
                tradesPerBar[i - firstBarIndex] = barOriginalIndex >= 0 ? originalTradesPerBar[barOriginalIndex - firstBar.OriginalIndex] : s_emptyTrades;
            }
            return tradesPerBar;
        }

        public IList<double> OpenPrices
        {
            get { throw new NotSupportedException(); }
        }

        public IList<double> ClosePrices
        {
            get { throw new NotSupportedException(); }
        }

        public IList<double> HighPrices
        {
            get { throw new NotSupportedException(); }
        }

        public IList<double> LowPrices
        {
            get { throw new NotSupportedException(); }
        }

        public IList<double> Volumes
        {
            get { throw new NotSupportedException(); }
        }

        public Interval IntervalInstance => m_security.IntervalInstance;

        public int Interval => m_security.Interval;

        public DataIntervals IntervalBase => m_security.IntervalBase;

        public int LotSize
        {
            get { throw new NotSupportedException(); }
        }

        public double LotTick
        {
            get { throw new NotSupportedException(); }
        }

        public double Margin
        {
            get { throw new NotSupportedException(); }
        }

        public double Tick => m_security.Tick;

        public int Decimals => m_security.Decimals;

        public IPositionsList Positions
        {
            get { throw new NotSupportedException(); }
        }

        public ISecurity CompressTo(int interval)
        {
            throw new NotSupportedException();
        }

        public ISecurity CompressTo(Interval interval)
        {
            throw new NotSupportedException();
        }

        public ISecurity CompressTo(Interval interval, int shift)
        {
            throw new NotSupportedException();
        }

        public ISecurity CompressTo(Interval interval, int shift, int adjustment, int adjShift)
        {
            throw new NotSupportedException();
        }

        public ISecurity CompressToVolume(Interval interval)
        {
            throw new NotSupportedException();
        }

        public ISecurity CompressToPriceRange(Interval interval)
        {
            throw new NotSupportedException();
        }

        public IList<double> Decompress(IList<double> candles)
        {
            throw new NotSupportedException();
        }

        public IList<TK> Decompress<TK>(IList<TK> candles, DecompressMethodWithDef method)
            where TK : struct
        {
            throw new NotSupportedException();
        }

        public void ConnectSecurityList(IGraphListBase list)
        {
            throw new NotSupportedException();
        }

        public void ConnectDoubleList(IGraphListBase list, IDoubleHandlerWithUpdate handler)
        {
            m_security.ConnectDoubleList(list, handler);
        }

        public double RoundPrice(double price)
        {
            throw new NotSupportedException();
        }

        public double RoundShares(double shares)
        {
            throw new NotSupportedException();
        }

        public CommissionDelegate Commission
        {
            set { throw new NotSupportedException(); }
            get { throw new NotSupportedException(); }
        }

        public ISecurity CloneAndReplaceBars(IEnumerable<IDataBar> newcandles)
        {
            throw new NotSupportedException();
        }

        public string CacheName
        {
            get { throw new NotSupportedException(); }
        }

        public void UpdateQueueData()
        {
            throw new NotSupportedException();
        }

        public double InitDeposit
        {
            set { throw new NotSupportedException(); }
            get { throw new NotSupportedException(); }
        }

        public bool IsRealtime
        {
            get { throw new NotSupportedException(); }
        }

        public bool IsPortfolioReady
        {
            get { throw new NotSupportedException(); }
        }

        public bool SimulatePositionOrdering
        {
            get { throw new NotSupportedException(); }
        }

        public bool IsAligned => true;
    }
}
