using System;

namespace TSLab.Script.Handlers.Options
{
    public partial class PositionsManager
    {
        /// <summary>
        /// \~english Mock class to store serializable representation of volatility target
        /// \~russian Класс-обертка для хранения сериализуемой информации о лимитной заявке в терминах волатильности
        /// </summary>
        [Serializable]
        public class IvTargetInfo
        {
            private bool m_isLong;
            private bool m_isShort;
            private double m_targetShares;
            private string m_entrySignalName;
            private string m_entryNotes;

            private double m_entryIv;
            private int m_entryShiftPrice;
            private QuoteIvMode m_quoteMode;
            /// <summary>
            /// Дата начала задачи котирования (до этого момента времени заявки в рынок выставляться не будут)
            /// </summary>
            private DateTime m_startDate;
            /// <summary>
            /// Дата истечения задачи котирования (после этого времени она будет автоматически отменена)
            /// </summary>
            private DateTime m_expirationDate;

            private SecInfo m_secInfo;

            public IvTargetInfo()
            {
                m_entrySignalName = "";
                m_entryNotes = "";

                m_entryIv = 0;
                m_entryShiftPrice = 0;
                m_quoteMode = QuoteIvMode.Relative;

                m_startDate = DateTime.MinValue;
                m_expirationDate = DateTime.MaxValue;

                m_secInfo = new SecInfo();
            }

            public IvTargetInfo(bool isLong, double targetQty,
                QuoteIvMode quoteMode, double iv, string signalName, string notes)
                : this(isLong, targetQty, quoteMode, iv, 0, signalName, notes)
            {
            }

            public IvTargetInfo(bool isLong, double targetQty,
                QuoteIvMode quoteMode, double iv, int shiftPrice, string signalName, string notes)
            {
                m_isLong = isLong;
                m_isShort = !isLong;

                m_entryIv = iv;
                m_entryShiftPrice = shiftPrice;
                m_quoteMode = quoteMode;
                m_targetShares = targetQty;

                m_entrySignalName = signalName;
                m_entryNotes = notes;

                m_startDate = DateTime.MinValue;
                m_expirationDate = DateTime.MaxValue;

                m_secInfo = new SecInfo();
                //m_secInfo = new SecInfo(sec.FinInfo.Security);
            }

            public bool IsLong
            {
                get { return m_isLong; }
                set { m_isLong = value; }
            }

            public bool IsShort
            {
                get { return m_isShort; }
                set { m_isShort = value; }
            }

            public double TargetShares
            {
                get { return m_targetShares; }
                set { m_targetShares = value; }
            }

            public QuoteIvMode QuoteMode
            {
                get { return m_quoteMode; }
                set { m_quoteMode = value; }
            }

            public double EntryIv
            {
                get { return m_entryIv; }
                set { m_entryIv = value; }
            }

            /// <summary>
            /// Сдвиг цены входа в шагах цены
            /// </summary>
            public int EntryShiftPrice
            {
                get { return m_entryShiftPrice; }
                set { m_entryShiftPrice = value; }
            }

            /// <summary>
            /// Дата начала задачи котирования (до этого момента времени заявки в рынок выставляться не будут)
            /// </summary>
            public DateTime StartDate
            {
                get { return m_startDate; }
                set { m_startDate = value; }
            }

            /// <summary>
            /// Дата истечения задачи котирования (после этого времени она будет автоматически отменена)
            /// </summary>
            public DateTime ExpirationDate
            {
                get { return m_expirationDate; }
                set { m_expirationDate = value; }
            }

            public string EntrySignalName
            {
                get { return m_entrySignalName; }
                set { m_entrySignalName = value; }
            }

            public string EntryNotes
            {
                get { return m_entryNotes; }
                set { m_entryNotes = value; }
            }

            public SecInfo SecInfo
            {
                get { return m_secInfo; }
                set { m_secInfo = value; }
            }

            public override string ToString()
            {
                string sign = m_isLong ? "+" : "-";
                string res = "[" + m_secInfo.Name + "] " + sign + Math.Abs(m_targetShares) + " @ " + m_entryIv;
                return res;
            }
        }
    }
}
