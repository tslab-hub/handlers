using System;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Algorythm to get smile skew at the money
    /// \~russian Режим вычисления наклона улыбки на деньгах
    /// </summary>
    public enum SmileSkewMode
    {
        /// <summary> \~english Raw skew \~russian Размерный наклон</summary>
        //[LocalizeDisplayName("BasePxMode.FixedPx")]
        [LocalizeDescription("SmileSkewMode.RawSkew")]
        RawSkew,

        /// <summary> \~english Exchange skew \~russian Биржевой наклон</summary>
        //[LocalizeDisplayName("BasePxMode.LastTrade")]
        [LocalizeDescription("SmileSkewMode.ExchangeSkew")]
        ExchangeSkew,

        /// <summary> \~english True skew \~russian Истинный наклон</summary>
        //[LocalizeDisplayName("BasePxMode.BidAskMidPoint")]
        [LocalizeDescription("SmileSkewMode.RescaledSkew")]
        RescaledSkew,
    }
}
