using System;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Algorythms to get expiration date
    /// \~russian Алгоритмы расчета даты экспирации
    /// </summary>
    public enum ExpiryMode
    {
        /// <summary> \~english Fixed date \~russian Фиксированная дата</summary>
        //[LocalizeDisplayName("ExpiryMode.FixedExpiry")]
        [LocalizeDescription("ExpiryMode.FixedExpiry")]
        FixedExpiry,

        /// <summary> \~english First expiry \~russian Ближайшая экспирация</summary>
        //[LocalizeDisplayName("ExpiryMode.FirstExpiry")]
        [LocalizeDescription("ExpiryMode.FirstExpiry")]
        FirstExpiry,

        /// <summary> \~english Last expiry \~russian Последняя экспирация</summary>
        //[LocalizeDisplayName("ExpiryMode.LastExpiry")]
        [LocalizeDescription("ExpiryMode.LastExpiry")]
        LastExpiry,

        /// <summary> \~english Expiry by number \~russian Экспирация по порядковому номеру</summary>
        //[LocalizeDisplayName("ExpiryMode.ExpiryByNumber")]
        [LocalizeDescription("ExpiryMode.ExpiryByNumber")]
        ExpiryByNumber,
    }
}
