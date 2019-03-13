using System;

using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Static class with options utilities: time to expiry, theor price, IV etc.
    /// \~russian Статический класс с опционными утилитами: расчет времени до экспирации, теоретическая цена, волатильность и т.п.
    /// </summary>
    public static class OptionUtils
    {
        /// <summary> \~english Minutes in day (1440) \~russian Минут в сутках (1440)</summary>
        public const double MinutesInDay = 1440.0;
        /// <summary> \~english 12*60 + 92 = 812 trading minutes in FORTS day \~russian 12*60 + 92 = 812 торговых минут в сутках ФОРТС</summary>
        public const int TradingMinutesInDayRts = (8 * 60 + 42) + (4 * 60 + 50);

        /// <summary> \~english Days in year (365.242) \~russian Дней в году (365.242)</summary>
        public const double DaysInYear = MinutesInYear / MinutesInDay;
        /// <summary>
        /// \~english Minutes in year (365 days 5 hours 48 minutes and 46 seconds == 525948.77 minutes)
        /// \~russian Минут в году (365 дней 5 часов 48 минут и 46 секунд == 525948.77 минут)
        /// </summary>
        public const double MinutesInYear = 365.0 * MinutesInDay + 5.0 * 60.0 + 48.0 + 46.0 / 60.0;

        /// <summary> \~english Ticks in day (864000000000) \~russian Тиков в сутках (864000000000)</summary>
        public static readonly long TradingTicksInDay = new TimeSpan(0, (int)MinutesInDay, 0).Ticks;

        /// <summary>
        /// \~english 12*60 + 92 = 812 trading ticks in FORTS day
        /// \~russian 12*60 + 92 = 812 торговых тиков в сутках ФОРТС
        /// </summary>
        public static readonly long TradingTicksInDayRts = new TimeSpan(0, TradingMinutesInDayRts, 0).Ticks;

        public static readonly long RtsMorningTicks = new TimeSpan(10, 0, 0).Ticks;
        public static readonly long RtsDayClearingTicks = new TimeSpan(14, 0, 0).Ticks;
        public static readonly long RtsDayClearingEndTicks = new TimeSpan(14, 3, 0).Ticks;
        public static readonly long RtsClearingTicks = new TimeSpan(18, 45, 0).Ticks;
        public static readonly long RtsEveningTicks = new TimeSpan(19, 0, 0).Ticks;
        public static readonly long RtsEodTicks = new TimeSpan(23, 50, 0).Ticks;

        /// <summary>
        /// \~english 
        /// \~russian Расширение для класса TimeSpan, которое позволяет вычислять время между двумя датами в долях года
        /// </summary>
        public static double TotalYears(this TimeSpan ts)
        {
            double res = ts.TotalMinutes / MinutesInYear;
            return res;
        }

        /// <summary>
        /// \~russian Вычисление времени между двумя датами в долях года
        /// </summary>
        /// <param name="end">конечная дата</param>
        /// <param name="beg">начальная дата</param>
        public static double YearsBetweenDates(DateTime end, DateTime beg)
        {
            double res = (end - beg).TotalYears();
            return res;
        }

        /// <summary>
        /// Вычисление времени между двумя датами. Суббота и воскресенье игнорируются.
        /// </summary>
        /// <param name="end">конечная дата</param>
        /// <param name="beg">начальная дата</param>
        //[Obsolete("Рекомендуется использовать быстрый метод GetDtWithoutWeekends")]
        public static TimeSpan GetDtWithoutWeekendsSlow(DateTime end, DateTime beg)
        {
            if (end == beg)
                return new TimeSpan(); // Совпадающие даты всегда имеют нулевое расстояние
            else if (end < beg)
                return -GetDtWithoutWeekendsSlow(beg, end); // При "неправильном" порядке дат делаю рекурсивный вызов

            long res = 0L;
            // 1. Гарантирую, что начало периода будет в следующий рабочий понедельник
            // (точнее, в полночь с воскресенья на пнд).
            if (beg.DayOfWeek == System.DayOfWeek.Saturday)
            {
                beg = beg.Date.AddDays(2); // выставляюсь на понедельник
                // Если теперь начало периода превышает его окончание, то возвращаю 0
                // поскольку это означает, что обе даты сидят в одной паре выходных
                if (beg >= end)
                    return new TimeSpan();
            }
            else if (beg.DayOfWeek == System.DayOfWeek.Sunday)
            {
                beg = beg.Date.AddDays(1); // выставляюсь на понедельник
                // Если теперь начало периода превышает его окончание, то возвращаю 0
                // поскольку это означает, что обе даты сидят в одной паре выходных
                if (beg >= end)
                    return new TimeSpan();
            }

            // 2. Гарантирую, что конец периода будет в следующий рабочий понедельник.
            if (end.DayOfWeek == System.DayOfWeek.Saturday)
                end = end.Date.AddDays(2); // выставляюсь на понедельник
            else if (end.DayOfWeek == System.DayOfWeek.Sunday)
                end = end.Date.AddDays(1); // выставляюсь на понедельник

            // 3. Медленный, но надёжный алгоритм -- итерации в цикле
            res = -beg.TimeOfDay.Ticks;
            beg = beg.Date;
            res += end.TimeOfDay.Ticks;
            end = end.Date;

            int dayCounter = 0;
            while (beg < end)
            {
                dayCounter++;
                beg = beg.AddDays(1);
                if (beg.DayOfWeek == System.DayOfWeek.Saturday)
                    beg = beg.AddDays(2);
                else if (beg.DayOfWeek == System.DayOfWeek.Sunday)
                    beg = beg.AddDays(1);
            }

            res += (dayCounter * TradingTicksInDay);

            return new TimeSpan(res);
        }

        /// <summary>
        /// Вычисление времени между двумя датами. Суббота и воскресенье игнорируются.
        /// </summary>
        /// <param name="end">конечная дата</param>
        /// <param name="beg">начальная дата</param>
        /// <param name="calendar">календарь. По умолчанию используется расписание торгов Московской биржи</param>
        public static TimeSpan GetDtWithoutHolidaysSlow(DateTime end, DateTime beg, ICalendar calendar = null)
        {
            if (end == beg)
                return new TimeSpan(); // Совпадающие даты всегда имеют нулевое расстояние
            else if (end < beg)
                return -GetDtWithoutWeekendsSlow(beg, end); // При "неправильном" порядке дат делаю рекурсивный вызов

            if (calendar == null)
                calendar = CalendarWithoutHolidays.Russia;

            long res = 0L;
            // 1. Гарантирую, что начало периода будет в следующий рабочий ДЕНЬ
            // (точнее, в полночь с воскресенья на пнд).
            //if (beg.DayOfWeek == System.DayOfWeek.Saturday)
            while (!calendar.IsWorkingDay(beg))
            {
                beg = beg.Date.AddDays(1); // выставляюсь на следующий календарный день
            }
            // Если теперь начало периода превышает его окончание, то возвращаю 0
            // поскольку это означает, что обе даты сидят в одном блоке праздников
            if (beg >= end)
                return new TimeSpan();

            // 2. Гарантирую, что конец периода будет в следующий рабочий ДЕНЬ
            //if (end.DayOfWeek == System.DayOfWeek.Saturday)
            while (!calendar.IsWorkingDay(end))
            {
                end = end.Date.AddDays(1); // выставляюсь на следующий календарный день
            }

            // 3. Медленный, но надёжный алгоритм -- итерации в цикле
            res = -beg.TimeOfDay.Ticks;
            beg = beg.Date;
            res += end.TimeOfDay.Ticks;
            end = end.Date;

            int dayCounter = 0;
            while (beg < end)
            {
                dayCounter++;
                beg = beg.AddDays(1);
                //if (beg.DayOfWeek == System.DayOfWeek.Saturday)
                while (!calendar.IsWorkingDay(beg))
                {
                    beg = beg.AddDays(1);
                }
            }

            res += (dayCounter * TradingTicksInDay);

            return new TimeSpan(res);
        }

        /// <summary>
        /// Вычисление фактического торгового времени между двумя датами по расписанию ФОРТС на 14.11.2014.
        /// </summary>
        /// <param name="end">конечная дата</param>
        /// <param name="beg">начальная дата</param>
        public static TimeSpan GetDtRtsTradingTime(DateTime end, DateTime beg, ICalendar calendar = null)
        {
            if (end == beg)
                return new TimeSpan(); // Совпадающие даты всегда имеют нулевое расстояние
            else if (end < beg)
                return -GetDtRtsTradingTime(beg, end, calendar); // При "неправильном" порядке дат делаю рекурсивный вызов

            if (calendar == null)
                calendar = CalendarWithoutHolidays.Russia;

            long res = 0L;
            // 1. Гарантирую, что начало периода будет в следующий рабочий ДЕНЬ
            // (точнее, в полночь с воскресенья на пнд).
            //if (beg.DayOfWeek == System.DayOfWeek.Saturday)
            while (!calendar.IsWorkingDay(beg))
            {
                beg = beg.Date.AddDays(1); // выставляюсь на следующий календарный день
            }

            // 2. Гарантирую, что конец периода будет в следующий рабочий ДЕНЬ
            //if (end.DayOfWeek == System.DayOfWeek.Saturday)
            while (!calendar.IsWorkingDay(end))
            {
                end = end.Date.AddDays(1); // выставляюсь на следующий календарный день
            }

            // 3. Гарантирую, что если начало периода лежит до 10:00, оно будет сдвинуто на 10:00
            //long rtsMorningTicks = (new TimeSpan(10, 0, 0)).Ticks;
            //long rtsDayClearingTicks = (new TimeSpan(14, 0, 0)).Ticks;
            //long rtsDayClearingEndTicks = (new TimeSpan(14, 3, 0)).Ticks;
            //long rtsClearingTicks = (new TimeSpan(18, 45, 0)).Ticks;
            //long rtsEveningTicks = (new TimeSpan(19, 0, 0)).Ticks;
            //long rtsEodTicks = (new TimeSpan(23, 50, 0)).Ticks;

            // 4. Медленный, но надёжный алгоритм -- итерации в цикле.
            #region 4.1. Сначала надо разобраться с левой границей
            {
                long begTimeOfDayTicks = beg.TimeOfDay.Ticks;
                if (begTimeOfDayTicks < RtsMorningTicks) // Если раннее утро -- не считаю вообще!
                {
                }
                else if (begTimeOfDayTicks <= RtsDayClearingTicks) // Кусочек времени от начала торгов
                {
                    res -= (begTimeOfDayTicks - RtsMorningTicks);
                }
                else if (begTimeOfDayTicks <= RtsDayClearingTicks)
                    // Внутри дневного клиринга просто беру 4 часа утренней торговли
                {
                    res -= (RtsDayClearingTicks - RtsMorningTicks);
                }
                else if (begTimeOfDayTicks <= RtsClearingTicks)
                    // Между дневным и вечерним клирингами беру 4 часа утренней торговли + хвостик от окончания дневного клиринга 
                {
                    res -= (RtsDayClearingTicks - RtsMorningTicks);
                    res -= (begTimeOfDayTicks - RtsDayClearingEndTicks);
                }
                else if (begTimeOfDayTicks <= RtsEveningTicks)
                    // Внутри вечернего клиринга просто беру 4 часа утренней торговли + 4ч 42м дневной
                {
                    res -= (RtsDayClearingTicks - RtsMorningTicks);
                    res -= (RtsClearingTicks - RtsDayClearingEndTicks);
                }
                else if (begTimeOfDayTicks <= RtsEodTicks)
                    // Внутри вечернего клиринга беру 4 часа утренней торговли + 4ч 42м дневной + хвостик от начала вечерки
                {
                    res -= (RtsDayClearingTicks - RtsMorningTicks);
                    res -= (RtsClearingTicks - RtsDayClearingEndTicks);
                    res -= (begTimeOfDayTicks - RtsEveningTicks);
                }
                else
                    // Если торги уже закончились, то нужно взять полный день: 4 часа утренней торговли + 4ч 42м дневной + 4ч 50мин вечерки
                {
                    res -= (RtsDayClearingTicks - RtsMorningTicks);
                    res -= (RtsClearingTicks - RtsDayClearingEndTicks);
                    res -= (RtsEodTicks - RtsEveningTicks);
                }
            }
            #endregion 4.1. Сначала надо разобраться с левой границей
            beg = beg.Date;

            #region 4.2. Теперь надо разобраться с правой границей
            {
                long tmpRes = 0L;
                long endgTimeOfDayTicks = end.TimeOfDay.Ticks;
                if (endgTimeOfDayTicks < RtsMorningTicks) // Если раннее утро -- не считаю вообще!
                {
                }
                else if (endgTimeOfDayTicks <= RtsDayClearingTicks) // Кусочек времени от начала торгов
                {
                    tmpRes += (endgTimeOfDayTicks - RtsMorningTicks);
                }
                else if (endgTimeOfDayTicks <= RtsDayClearingTicks)
                    // Внутри дневного клиринга просто беру 4 часа утренней торговли
                {
                    tmpRes += (RtsDayClearingTicks - RtsMorningTicks);
                }
                else if (endgTimeOfDayTicks <= RtsClearingTicks)
                    // Между дневным и вечерним клирингами беру 4 часа утренней торговли + хвостик от окончания дневного клиринга 
                {
                    tmpRes += (RtsDayClearingTicks - RtsMorningTicks);
                    tmpRes += (endgTimeOfDayTicks - RtsDayClearingEndTicks);
                }
                else if (endgTimeOfDayTicks <= RtsEveningTicks)
                    // Внутри вечернего клиринга просто беру 4 часа утренней торговли + 4ч 42м дневной
                {
                    tmpRes += (RtsDayClearingTicks - RtsMorningTicks);
                    tmpRes += (RtsClearingTicks - RtsDayClearingEndTicks);
                }
                else if (endgTimeOfDayTicks <= RtsEodTicks)
                    // Внутри вечернего клиринга беру 4 часа утренней торговли + 4ч 42м дневной + хвостик от начала вечерки
                {
                    tmpRes += (RtsDayClearingTicks - RtsMorningTicks);
                    tmpRes += (RtsClearingTicks - RtsDayClearingEndTicks);
                    tmpRes += (endgTimeOfDayTicks - RtsEveningTicks);
                }
                else
                    // Если торги уже закончились, то нужно взять полный день: 4 часа утренней торговли + 4ч 42м дневной + 4ч 50мин вечерки
                {
                    tmpRes += (RtsDayClearingTicks - RtsMorningTicks);
                    tmpRes += (RtsClearingTicks - RtsDayClearingEndTicks);
                    tmpRes += (RtsEodTicks - RtsEveningTicks);
                }

                res += tmpRes;
            }
            #endregion 4.2. Теперь надо разобраться с правой границей
            end = end.Date;

            int dayCounter = 0;
            while (beg < end)
            {
                dayCounter++;
                beg = beg.AddDays(1);
                //if (beg.DayOfWeek == System.DayOfWeek.Saturday)
                while (!calendar.IsWorkingDay(beg))
                {
                    beg = beg.AddDays(1);
                }
            }

            //res += new TimeSpan(0, dayCounter * TradingMinutesInDayRts, 0);
            res += dayCounter * TradingTicksInDayRts;

            return new TimeSpan(res);
        }

        /// <summary>
        /// Получить полное количество торговых дней в году с учетом их весов.
        /// (Результат кешируется в статической коллекции для ускорения последующих обращений)
        /// </summary>
        /// <param name="year">год, который нас интересует</param>
        /// <returns>полное количество торговых дней в году с учетом их весов</returns>
        public static double GetLiquidProRtsTradingDaysInYear(int year)
        {
            double daysInYear = LiquidProTimeModelRepository.GetDaysInYear(year);
            return daysInYear;
        }

        /// <summary>
        /// Вычисление фактического взвешенного торгового времени (по алгоритму Liquid.Pro)
        /// между двумя датами по расписанию ФОРТС на 19.09.2017.
        /// </summary>
        /// <param name="end">конечная дата</param>
        /// <param name="beg">начальная дата</param>
        /// <param name="daysinEndYear">количество дней в году, который указан в поздней из двух дат</param>
        public static double GetDtLiquidProRtsTradingTime(DateTime end, DateTime beg, out double daysInEndYear)
        {
            daysInEndYear = LiquidProTimeModelRepository.GetDaysInYear(end.Year);

            if (end == beg)
                return 0; // Совпадающие даты всегда имеют нулевое расстояние
            else if (end < beg)
                return -GetDtLiquidProRtsTradingTime(beg, end, out daysInEndYear); // При "неправильном" порядке дат делаю рекурсивный вызов

            double dT = LiquidProTimeModelRepository.GetYearPartBetween(beg, end);
            return dT;
        }
    }
}
