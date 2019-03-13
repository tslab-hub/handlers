using System;

using TSLab.DataSource;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    public partial class PositionsManager
    {
        /// <summary>
        /// \~english Mock class to store serializable representation of security
        /// \~russian Класс-обертка для хранения сериализуемой информации об инструменте
        /// </summary>
        [Serializable]
        public class SecInfo
        {
            private string m_name;
            private string m_dsName;
            private string m_fullName;
            private bool m_isOption;
            private double m_strike;
            private StrikeType? m_strikeType;
            /// <summary>
            /// Экспирация инструмента (чтобы можно было сверить правильность опционной серии)
            /// </summary>
            private DateTime m_expiry;

            public SecInfo()
            {
                m_name = m_dsName = m_fullName = "";
            }

            public SecInfo(IDataSourceSecurity sec)
            {
                m_name = sec.Name;
                m_dsName = sec.DSName;
                m_fullName = sec.FullName;

                m_strike = sec.Strike;
                m_isOption = sec.IsOption;
                m_expiry = sec.ExpirationDate;

                if (m_isOption)
                {
                    if (sec.ActiveType == ActiveType.OptionPut)
                        m_strikeType = Script.Options.StrikeType.Put;
                    else if (sec.ActiveType == ActiveType.OptionCall)
                        m_strikeType = Script.Options.StrikeType.Call;
                }
            }

            public string Name
            {
                get { return m_name; }
                set { m_name = value; }
            }

            public string DsName
            {
                get { return m_dsName; }
                set { m_dsName = value; }
            }

            public string FullName
            {
                get { return m_fullName; }
                set { m_fullName = value; }
            }

            public bool IsOption
            {
                get { return m_isOption; }
                set { m_isOption = value; }
            }

            public double Strike
            {
                get { return m_strike; }
                set { m_strike = value; }
            }

            public StrikeType? StrikeType
            {
                get { return m_strikeType; }
                set { m_strikeType = value; }
            }

            /// <summary>
            /// Экспирация инструмента (чтобы можно было сверить правильность опционной серии)
            /// </summary>
            public DateTime Expiry
            {
                get { return m_expiry; }
                set { m_expiry = value; }
            }

            public override string ToString()
            {
                string res = "[" + m_dsName + "] " + m_name + " - " + m_fullName;
                return res;
            }

            public bool Equals(SecInfo secInfo)
            {
                if (secInfo == null)
                    return false;

                // проверка специально разбита на 3 части, чтобы легче было дебажить
                bool res = FullName.Equals(secInfo.FullName, StringComparison.InvariantCultureIgnoreCase);
                res &= Name.Equals(secInfo.Name, StringComparison.InvariantCultureIgnoreCase);
                res &= DsName.Equals(secInfo.DsName, StringComparison.InvariantCultureIgnoreCase);
                return res;
            }

            public bool Equals(IDataSourceSecurity secDesc)
            {
                if (secDesc == null)
                    return false;

                // проверка специально разбита на 3 части, чтобы легче было дебажить
                bool res = FullName.Equals(secDesc.FullName, StringComparison.InvariantCultureIgnoreCase);
                res &= Name.Equals(secDesc.Name, StringComparison.InvariantCultureIgnoreCase);
                res &= DsName.Equals(secDesc.DSName, StringComparison.InvariantCultureIgnoreCase);
                return res;
            }

            public bool Equals(IOptionStrike optionStrike)
            {
                bool res = Equals(optionStrike.FinInfo.Security);
                return res;
            }
        }
    }
}
