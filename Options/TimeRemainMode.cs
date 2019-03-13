using System;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Time to expiry algorythms
    /// \~russian Алгоритмы расчета времени до экспирации
    /// </summary>
    public enum TimeRemainMode
    {
        /// <summary> \~english Plain calendar time \~russian Равномерное календарное время</summary>
        //[LocalizeDisplayName("TimeRemainMode.PlainCalendar")]
        [LocalizeDescription("TimeRemainMode.PlainCalendar")]
        PlainCalendar,

        /// <summary> \~english Plain calendar time except weekends \~russian Равномерное календарное время без выходных дней</summary>
        //[LocalizeDisplayName("TimeRemainMode.PlainCalendarWithoutWeekends")]
        [LocalizeDescription("TimeRemainMode.PlainCalendarWithoutWeekends")]
        PlainCalendarWithoutWeekends,

        /// <summary> \~english Plain calendar time except weekends and holidays \~russian Равномерное календарное время без выходных и праздничных дней</summary>
        //[LocalizeDisplayName("TimeRemainMode.PlainCalendarWithoutHolidays")]
        [LocalizeDescription("TimeRemainMode.PlainCalendarWithoutHolidays")]
        PlainCalendarWithoutHolidays,

        /// <summary> \~english Trading time following RTS schedule \~russian Торговое время в соответствии с расписанием работы РТС</summary>
        //[LocalizeDisplayName("TimeRemainMode.RtsTradingTime")]
        [LocalizeDescription("TimeRemainMode.RtsTradingTime")]
        RtsTradingTime,

        /// <summary> \~english Liquid.Pro trading time following RTS schedule \~russian Взвешенное торговое время по алгоритму Liquid.Pro в соответствии с расписанием работы РТС</summary>
        //[LocalizeDisplayName("TimeRemainMode.LiquidProRtsTradingTime")]
        [LocalizeDescription("TimeRemainMode.LiquidProRtsTradingTime")]
        LiquidProRtsTradingTime,
    }
}
