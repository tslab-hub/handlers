using System;

using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Information about tradable smile node
    /// \~russian Информация о торгуемом узле улыбки
    /// </summary>
    public class SmileNodeInfo
    {
        public double F;
        public double dT;
        /// <summary>Безрисковая ставка В ПРОЦЕНТАХ</summary>
        public double RiskFreeRate;
        public double Sigma;
        /// <summary>Цена опциона ИЛИ волатильность задачи котирования</summary>
        public double OptPx;
        /// <summary>Сдвиг котировки опциона (в шагах цены) при использовании в качестве задачи котирования</summary>
        public int ShiftOptPx;
        public bool Expired;
        public double Strike;
        public string Symbol;
        public string DSName;
        public string FullName;
        public ISecurity Security;
        public OptionPxMode PxMode;
        public StrikeType OptionType;
        public IOptionStrikePair Pair;

        public DateTime ScriptTime;
        public DateTime CalendarTime;

        public double Qty = 0;
        public DateTime? ClickTime;
    }
}
