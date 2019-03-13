using System;
using System.Diagnostics.CodeAnalysis;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Strike selection algorythm
    /// \~russian Режим выбора страйка
    /// </summary>
    public enum StrikeSelectionMode
    {
        /// <summary>
        /// \~english Fixed strike given in handler's settings
        /// \~russian Фиксированный страйк, указанный в настройках блока
        /// </summary>
        //[LocalizeDisplayName("StrikeSelectionMode.FixedStrike")]
        [LocalizeDescription("StrikeSelectionMode.FixedStrike")]
        FixedStrike,

        /// <summary> \~english Nearest strike to ATM \~russian Ближайший к деньгам</summary>
        //[LocalizeDisplayName("StrikeSelectionMode.NearestATM")]
        [LocalizeDescription("StrikeSelectionMode.NearestATM")]
        [SuppressMessage("StyleCopPlus.StyleCopPlusRules", "SP0100:AdvancedNamingRules", Justification = "Reviewed. Suppression is OK here.")]
        NearestATM,

        /// <summary> \~english Smallest strike \~russian Минимальный страйк</summary>
        //[LocalizeDisplayName("StrikeSelectionMode.MinStrike")]
        [LocalizeDescription("StrikeSelectionMode.MinStrike")]
        MinStrike,

        /// <summary> \~english Biggest strike \~russian Максимальный страйк</summary>
        //[LocalizeDisplayName("StrikeSelectionMode.MaxStrike")]
        [LocalizeDescription("StrikeSelectionMode.MaxStrike")]
        MaxStrike,
    }
}
