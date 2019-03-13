using System;

namespace TSLab.Script.Handlers.Options
{
    public partial class PositionsManager
    {
        /// <summary>
        /// \~english Mock class to store serializable representation of position
        /// \~russian Класс-обертка для хранения сериализуемой информации о виртуальной позиции
        /// </summary>
        [Serializable]
        public class PosInfo
        {
            private bool m_isLong;
            private bool m_isShort;
            private bool m_isVirtual;
            private int m_entryBarNum;
            private double m_shares;
            private double m_entryPrice;
            private string m_entrySignalName;
            private string m_entryNotes;
            private double m_entryCommission;

            private int m_exitBarNum;
            private double m_exitPrice;
            private double m_exitCommission;

            private SecInfo m_secInfo;

            private double m_avgPx;

            public PosInfo()
            {
                m_entrySignalName = "";
                m_entryNotes = "";

                m_secInfo = new SecInfo();
            }

            public PosInfo(IPosition pos)
            {
                m_isLong = pos.IsLong;
                m_isShort = pos.IsShort;
                m_isVirtual = pos.IsVirtual;
                m_entryBarNum = pos.EntryBarNum;
                m_shares = pos.Shares;
                m_entryPrice = pos.AverageEntryPrice;
                m_entrySignalName = pos.EntrySignalName;
                m_entryNotes = pos.EntryNotes;
                m_entryCommission = pos.EntryCommission;

                m_exitBarNum = pos.ExitBarNum;
                m_exitPrice = pos.ExitPrice;
                m_exitCommission = pos.ExitCommission;

                m_secInfo = new SecInfo(pos.Security.SecurityDescription);

                try
                {
                    m_avgPx = pos.GetBalancePrice(pos.Security.Bars.Count - 1);
                }
                catch (Exception)
                {
                    // подавляю все возможные исключения здесь
                    m_avgPx = Double.NaN;
                }
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

            public bool IsVirtual
            {
                get { return m_isVirtual; }
                set { m_isVirtual = value; }
            }

            public int EntryBarNum
            {
                get { return m_entryBarNum; }
                set { m_entryBarNum = value; }
            }

            public double Shares
            {
                get { return m_shares; }
                set { m_shares = value; }
            }

            public double EntryPrice
            {
                get { return m_entryPrice; }
                set { m_entryPrice = value; }
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

            public double EntryCommission
            {
                get { return m_entryCommission; }
                set { m_entryCommission = value; }
            }

            public int ExitBarNum
            {
                get { return m_exitBarNum; }
                set { m_exitBarNum = value; }
            }

            public double ExitPrice
            {
                get { return m_exitPrice; }
                set { m_exitPrice = value; }
            }

            public double ExitCommission
            {
                get { return m_exitCommission; }
                set { m_exitCommission = value; }
            }

            public SecInfo SecInfo
            {
                get { return m_secInfo; }
                set { m_secInfo = value; }
            }

            public double AvgPx
            {
                get { return m_avgPx; }
            }

            public override string ToString()
            {
                string sign = m_isLong ? "+" : "-";
                string res = "[" + m_secInfo.Name + "] " + sign + Math.Abs(m_shares) + " @ " + m_entryPrice;
                return res;
            }
        }
    }
}
