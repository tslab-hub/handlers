using System;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Algorythm to get current date
    /// \~russian Способ вычисления текущей псевдо-даты
    /// </summary>
    public enum CurrentDateMode
    {
        /// <summary> \~english Fixed date \~russian Фиксированная дата</summary>
        //[LocalizeDisplayName("CurrentDateMode.FixedDate")]
        [LocalizeDescription("CurrentDateMode.FixedDate")]
        FixedDate,

        /// <summary> \~english Current date \~russian Текущая дата</summary>
        //[LocalizeDisplayName("CurrentDateMode.CurrentDate")]
        [LocalizeDescription("CurrentDateMode.CurrentDate")]
        CurrentDate,

        /// <summary> \~english Tomorrow \~russian Завтра</summary>
        //[LocalizeDisplayName("CurrentDateMode.Tomorrow")]
        [LocalizeDescription("CurrentDateMode.Tomorrow")]
        Tomorrow,

        /// <summary> \~english Next working day \~russian На следующий рабочий день</summary>
        //[LocalizeDisplayName("CurrentDateMode.NextWorkingDay")]
        [LocalizeDescription("CurrentDateMode.NextWorkingDay")]
        NextWorkingDay,

        /// <summary> \~english Next week \~russian Через неделю</summary>
        //[LocalizeDisplayName("CurrentDateMode.NextWeek")]
        [LocalizeDescription("CurrentDateMode.NextWeek")]
        NextWeek,

        ///// <summary> \~english Next day of week \~russian На ближайший день недели</summary>
        //NextDayOfWeek,
    }
}
