using System;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Units to be used for presentation
    /// \~russian В какой размерности показывать хранимое значение
    /// </summary>
    public enum FixedValueMode
    {
        /// <summary> \~english As is \~russian Как есть</summary>
        //[LocalizeDisplayName("FixedValueMode.AsIs")]
        [LocalizeDescription("FixedValueMode.AsIs")]
        AsIs,

        /// <summary> \~english As percent (multiply by 100) \~russian В процентах (умножить на 100)</summary>
        //[LocalizeDisplayName("FixedValueMode.Percent")]
        [LocalizeDescription("FixedValueMode.Percent")]
        Percent,

        /// <summary> \~english As per mille (multiply by 1000) \~russian В промилле (умножить на 1000)</summary>
        //[LocalizeDisplayName("FixedValueMode.Promille")]
        [LocalizeDescription("FixedValueMode.Promille")]
        Promille,

        /// <summary> \~english As thousand (divide by 1000) \~russian В тысячах (разделить на 1000)</summary>
        //[LocalizeDisplayName("FixedValueMode.Thousand")]
        [LocalizeDescription("FixedValueMode.Thousand")]
        Thousand,

        /// <summary> \~english Convert to days (multiply by 365) \~russian Перевести в дни (умножить на 365)</summary>
        //[LocalizeDisplayName("FixedValueMode.YearsAsDays")]
        [LocalizeDescription("FixedValueMode.YearsAsDays")]
        YearsAsDays,

        /// <summary> \~english Convert to years (divide by 365) \~russian Перевести в годы (разделить на 365)</summary>
        //[LocalizeDisplayName("FixedValueMode.DaysAsYears")]
        [LocalizeDescription("FixedValueMode.DaysAsYears")]
        DaysAsYears,

        /// <summary> \~english As Parts-per-Million (multiply by 1000000) \~russian В миллионных долях (умножить на 1000000)</summary>
        //[LocalizeDisplayName("FixedValueMode.PartsPerMillion")]
        [LocalizeDescription("FixedValueMode.PartsPerMillion")]
        PartsPerMillion,
    }
}
