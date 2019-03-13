using System;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Algorythm to get base asset price
    /// \~russian Алгоритм определения цены БА
    /// </summary>
    public enum BasePxMode
    {
        /// <summary> \~english Fixed price \~russian Фиксированная цена</summary>
        //[LocalizeDisplayName("BasePxMode.FixedPx")]
        [LocalizeDescription("BasePxMode.FixedPx")]
        FixedPx,

        /// <summary> \~english Last trade \~russian Последний трейд в БА</summary>
        //[LocalizeDisplayName("BasePxMode.LastTrade")]
        [LocalizeDescription("BasePxMode.LastTrade")]
        LastTrade,

        /// <summary> \~english L1 midpoint \~russian Между заявками</summary>
        //[LocalizeDisplayName("BasePxMode.BidAskMidPoint")]
        [LocalizeDescription("BasePxMode.BidAskMidPoint")]
        BidAskMidPoint,

        /// <summary> \~english Using theoretical option prices \~russian На основании теоретических цен опционов</summary>
        //[LocalizeDisplayName("BasePxMode.TheorPxBased")]
        [LocalizeDescription("BasePxMode.TheorPxBased")]
        TheorPxBased,

        // [2015-09-22] Пока неясен алгоритм и значение ни разу не использовалось, отключаю.
        ///// <summary> \~english Using option quotes L1 \~russian На основании опционных котировок</summary>
        //OptionBased,
    }
}
