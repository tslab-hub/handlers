using System;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Source of price or its kind
    /// \~russian Вид цены или её источник
    /// </summary>
    public enum OptionPxMode
    {
        /// <summary> \~english Bid \~russian Покупка</summary>
        Bid,
        /// <summary> \~english Ask \~russian Продажа</summary>
        Ask,
        /// <summary> \~english Midpoint \~russian Середина спреда</summary>
        Mid,
    }
}
