using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.Optimization;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Time to expiry as a fraction of year. Different algorythms are available.
    /// \~russian Время до экспирации в долях года. Заложены различные алгоритмы (фиксированное время, плоское календарное время, плоское календарное время с учетом выходных, и т.п.).
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsIndicators)]
    [HelperName("Time to expiry", Language = Constants.En)]
    [HelperName("Время до экспирации", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION | TemplateTypes.OPTION_SERIES, Name = Constants.AnyOption)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Время до экспирации в долях года. Заложены различные алгоритмы (фиксированное время, плоское календарное время, плоское календарное время с учетом выходных, и т.п.).")]
    [HelperDescription("Time to expiry in year fractions. Various algorithms are applied (fixed time, plain calendar time, plain calendar time including days off and so on).", Constants.En)]
    public class TimeToExpiry : BaseContextHandler, IStreamHandler, ICustomListValues
    {
        private const string DefaultExpiration = "17-11-2014 " + Constants.DefaultFortsExpiryTimeStr;

        // GLSP-435 - Проверяю другие варианты названий
        private const string VisibleExpiryTimeNameEn = "Expiry time";
        private const string VisibleExpiryTimeNameRu = "Время экспирации";

        public static readonly string TimeFormat = "g";
        /// <summary>dd-MM-yyyy HH:mm</summary>
        public static readonly string DateTimeFormat = "dd-MM-yyyy HH:mm";

        private static readonly DateTime s_yearBeg = new DateTime(2017, 1, 1);
        private static readonly DateTime s_yearEnd = new DateTime(2018, 1, 1);

        internal static readonly double DaysInYearPlainCalendar = (s_yearEnd - s_yearBeg).TotalDays;
        internal static readonly double DaysInYearPlainCalendarWithoutWeekends =
            OptionUtils.GetDtWithoutWeekendsSlow(s_yearEnd, s_yearBeg).TotalDays;
        internal static readonly double DaysInYearPlainCalendarWithoutHolidays =
            OptionUtils.GetDtWithoutHolidaysSlow(s_yearEnd, s_yearBeg).TotalDays;
        internal static readonly double DaysInYearRts =
            OptionUtils.GetDtRtsTradingTime(s_yearEnd, s_yearBeg).TotalDays;
        internal static readonly double DaysInYearLiquidProRts =
            OptionUtils.GetLiquidProRtsTradingDaysInYear(s_yearBeg.Year);

        private ExpiryMode m_expiryMode = ExpiryMode.FixedExpiry;
        private CurrentDateMode m_dateMode = CurrentDateMode.CurrentDate;
        private TimeRemainMode m_tRemainMode = TimeRemainMode.PlainCalendar;

        private TimeSpan m_dateShift;
        private bool m_useDays = false;
        private int m_seriesIndex = 1;
        private string m_expDateStr = DefaultExpiration;
        private string m_fixedDateStr = DefaultExpiration;
        private string m_expTimeStr = Constants.DefaultFortsExpiryTimeStr;
        private OptimProperty m_dT = new OptimProperty(0.08, false, double.MinValue, double.MaxValue, 1.0, 3);
        private DateTime m_expirationDate = DateTime.ParseExact(DefaultExpiration, DateTimeFormat, CultureInfo.InvariantCulture);
        private DateTime m_fixedDate = DateTime.ParseExact(DefaultExpiration, DateTimeFormat, CultureInfo.InvariantCulture);
        private TimeSpan m_expirationTime = TimeSpan.ParseExact(Constants.DefaultFortsExpiryTimeStr, TimeFormat, CultureInfo.InvariantCulture);

        /// <summary>
        /// Локальный кеш времени до экспирации
        /// </summary>
        private Dictionary<DateTime, double> LocalHistory
        {
            get
            {
                Dictionary<DateTime, double> times = m_context.LoadObject(VariableId + "_times") as Dictionary<DateTime, double>;
                if (times == null)
                {
                    times = new Dictionary<DateTime, double>();
                    Context.StoreObject(VariableId + "_times", times);
                }

                return times;
            }
        }

        #region Parameters
        /// <summary>
        /// \~english Current date algorythm
        /// \~russian Алгоритм поиска 'сейчас'
        /// </summary>
        [HelperName("Current date algo", Constants.En)]
        [HelperName("Алгоритм поиска 'сейчас'", Constants.Ru)]
        [Description("Алгоритм поиска 'сейчас'")]
        [HelperDescription("Current date algorythm", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "CurrentDate")]
        public CurrentDateMode CurDateMode
        {
            get { return m_dateMode; }
            set { m_dateMode = value; }
        }

        /// <summary>
        /// \~english Algorythm to determine expiration date
        /// \~russian Алгоритм определения даты экспирации
        /// </summary>
        [HelperName("Expiration algo", Constants.En)]
        [HelperName("Алгоритм экспирации", Constants.Ru)]
        [Description("Алгоритм определения даты экспирации")]
        [HelperDescription("Algorythm to determine expiration date", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "FixedExpiry")]
        public ExpiryMode ExpirationMode
        {
            get { return m_expiryMode; }
            set { m_expiryMode = value; }
        }

        /// <summary>
        /// \~english Algorythm to estimate time-to-expiry
        /// \~russian Алгоритм расчета времени до экспирации
        /// </summary>
        [HelperName("Estimation algo", Constants.En)]
        [HelperName("Алгоритм расчета", Constants.Ru)]
        [Description("Алгоритм расчета времени до экспирации")]
        [HelperDescription("Algorythm to estimate time-to-expiry", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "PlainCalendar")]
        public TimeRemainMode DistanceMode
        {
            get { return m_tRemainMode; }
            set { m_tRemainMode = value; }
        }

        /// <summary>
        /// \~english Expiration datetime (including time of a day) for algorythm FixedExpiry
        /// \~russian Дата экспирации (включая время суток) для режима FixedExpiry
        /// </summary>
        [HelperName("Expiry", Constants.En)]
        [HelperName("Экспирация", Constants.Ru)]
        [Description("Дата экспирации (включая время суток) для режима FixedExpiry")]
        [HelperDescription("Expiration datetime (including time of a day) for algorythm FixedExpiry", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultExpiration)]
        public string Expiry
        {
            get { return m_expDateStr; }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                    return;

                if (m_expDateStr.Equals(value, StringComparison.InvariantCultureIgnoreCase))
                    return;

                DateTime t;
                if (DateTime.TryParseExact(value, TimeToExpiry.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out t))
                {
                    m_expDateStr = value;
                    m_expirationDate = t;
                }
            }
        }

        /// <summary>
        /// \~english Expiration time (including time of a day) for algorythms except FixedExpiry
        /// \~russian Дата экспирации (включая время суток) для режима кроме FixedExpiry
        /// </summary>
        [HelperName(VisibleExpiryTimeNameEn, Constants.En)]
        [HelperName(VisibleExpiryTimeNameRu, Constants.Ru)]
        [Description("Дата экспирации (включая время суток) для режима КРОМЕ FixedExpiry")]
        [HelperDescription("Expiration time (including time of a day) for algorythms EXCEPT FixedExpiry", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = Constants.DefaultFortsExpiryTimeStr)]
        public string ExpiryTime
        {
            get { return m_expTimeStr; }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                    return;

                if (m_expTimeStr.Equals(value, StringComparison.InvariantCultureIgnoreCase))
                    return;

                TimeSpan t;
                if (TimeSpan.TryParseExact(value, TimeToExpiry.TimeFormat, CultureInfo.InvariantCulture, TimeSpanStyles.None, out t))
                {
                    m_expTimeStr = value;
                    m_expirationTime = t;
                }
            }
        }

        /// <summary>
        /// \~english Today datetime (including time of a day) for algorythm FixedDate
        /// \~russian Фиксированная дата (включая время суток). Используется в режиме FixedDate
        /// </summary>
        [HelperName("Frozen 'today'", Constants.En)]
        [HelperName("'Сегодня'", Constants.Ru)]
        [Description("Фиксированная дата (включая время суток). Используется в режиме FixedDate")]
        [HelperDescription("Today datetime (including time of a day) for algorythm FixedDate", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultExpiration)]
        public string FixedDate
        {
            get { return m_fixedDateStr; }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                    return;

                if (m_fixedDateStr.Equals(value, StringComparison.InvariantCultureIgnoreCase))
                    return;

                DateTime t;
                if (DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out t))
                {
                    m_fixedDateStr = value;
                    m_fixedDate = t;
                }
            }
        }

        /// <summary>
        /// \~english When true, handler calculates time to expiry as days
        /// \~russian При true будет считать дни, а не доли года
        /// </summary>
        [HelperName("Use days", Constants.En)]
        [HelperName("В днях", Constants.Ru)]
        [Description("При true будет считать дни, а не доли года")]
        [HelperDescription("When true, handler calculates time to expiry as days", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "false")]
        public bool UseDays
        {
            get { return m_useDays; }
            set { m_useDays = value; }
        }

        /// <summary>
        /// \~english Series index (only alive series) for algorythm ExpiryByNumber
        /// \~russian Индекс серии (учитываются только живые). Используется в режиме ExpiryByNumber.
        /// </summary>
        [HelperName("Series index", Constants.En)]
        [HelperName("Номер серии", Constants.Ru)]
        [Description("Индекс серии (учитываются только живые). Используется в режиме ExpiryByNumber.")]
        [HelperDescription("Series index (only alive series) for algorythm ExpiryByNumber", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "1")]
        public int SeriesIndex
        {
            get { return m_seriesIndex; }
            set { m_seriesIndex = value; }
        }

        /// <summary>
        /// \~english Time to expiry (just to show it on ControlPane)
        /// \~russian Время до экспирации (для отображения в интерфейсе агента)
        /// </summary>
        [HelperName("Time", Constants.En)]
        [HelperName("Время", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Время до экспирации (только для отображения на UI)")]
        [HelperDescription("Time to expiry (just to show it on ControlPane)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0.08", IsCalculable = true)]
        public OptimProperty Time
        {
            get { return m_dT; }
            set { m_dT = value; }
        }

        /// <summary>
        /// \~english Shift current date by calendar time interval
        /// \~russian Сдвинуть текущую дату на указанный интервал календарного времени
        /// </summary>
        [HelperName("Current date shift", Constants.En)]
        [HelperName("Сдвиг текущей даты", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Сдвинуть текущую дату на указанный интервал календарного времени")]
        [HelperDescription("Shift current date by calendar time interval", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0:0:0", Step = "24:0:0")]
        public TimeSpan CurrentDateShift
        {
            get { return m_dateShift; }
            set { m_dateShift = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION, чтобы подключаться к источнику-опциону
        /// </summary>
        public IList<double> Execute(IOption opt)
        {
            if (opt == null)
                return Constants.EmptyListDouble;

            return CalculateAll(opt, opt.UnderlyingAsset);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.SECURITY, чтобы подключаться к источнику-БА
        /// </summary>
        public IList<double> Execute(ISecurity sec)
        {
            if (sec == null)
                return Constants.EmptyListDouble;

            return CalculateAll(null, sec);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-БА
        /// </summary>
        public IList<double> Execute(IOptionSeries optSer)
        {
            if (optSer == null)
                return Constants.EmptyListDouble;

            return CalculateAll(null, optSer.UnderlyingAsset);
        }

        protected IList<double> CalculateAll(IOption opt, ISecurity sec)
        {
            int len = sec.Bars.Count;
            if (len <= 0)
                return Constants.EmptyListDouble;

            double[] res = Context.GetArray<double>(len);
            var history = LocalHistory;

            bool exceptionFlag = false;
            Exception lastEx = null;
            DateTime prevBarDate = new DateTime(), prevExpDate = new DateTime();
            for (int m = 0; m < len - 1; m++)
            {
                DateTime now = sec.Bars[m].Date;

                double time;
                if (history.TryGetValue(now, out time))
                {
                    res[m] = time;
                }
                else
                {
                    time = Constants.NaN;
                    try
                    {
                        double timeAsDays, timeAsYears;
                        time = EstimateTimeForGivenBar(opt, sec, sec.Bars[m].Date, prevBarDate, prevExpDate,
                            out timeAsDays, out timeAsYears, out prevBarDate, out prevExpDate);
                        history[now] = time;
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                        exceptionFlag = true;
                    }
                    res[m] = time;
                }
            }

            if (exceptionFlag && (lastEx != null))
            {
                m_context.Log("[TimeToExpiry] " + lastEx, MessageType.Error, true);
            }

            lastEx = null;
            exceptionFlag = false;
            if (len > 0)
            {
                double time = Constants.NaN, timeAsDays = Constants.NaN;
                try
                {
                    double timeAsYears;
                    time = EstimateTimeForGivenBar(opt, sec, sec.Bars[len - 1].Date, prevBarDate, prevExpDate,
                        out timeAsDays, out timeAsYears, out prevBarDate, out prevExpDate);
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    exceptionFlag = true;
                }
                res[len - 1] = time;
                // Принято решение, что для удобства пользователя время на UI показывается всегда в днях.
                double displayValue = timeAsDays;
                m_dT.Value = displayValue;
            }

            if (exceptionFlag && (lastEx != null))
            {
                m_context.Log("[TimeToExpiry] " + lastEx, MessageType.Error, true);
            }

            //return new ReadOnlyCollection<double>(times);
            return res;
        }

        private double EstimateTimeForGivenBar(IOption opt, ISecurity sec, DateTime now, DateTime prevBarDate, DateTime prevExpDate,
            out double timeAsDays, out double timeAsYears, out DateTime barDate, out DateTime expDate)
        {
            DateTime curDate;
            #region Get current date
            switch (m_dateMode)
            {
                case CurrentDateMode.FixedDate:
                    curDate = m_fixedDate;
                    break;

                case CurrentDateMode.CurrentDate:
                    {
                        //IDataBar bar = sec.Bars[j];
                        curDate = now.Date;
                    }
                    break;

                case CurrentDateMode.Tomorrow:
                    curDate = now.Date.AddDays(1);
                    break;

                case CurrentDateMode.NextWorkingDay:
                    curDate = now.Date.AddDays(1);
                    while (!CalendarWithoutHolidays.Russia.IsWorkingDay(curDate))
                    {
                        curDate = curDate.AddDays(1);
                    }
                    break;

                case CurrentDateMode.NextWeek:
                    curDate = now.Date.AddDays(7);
                    break;

                default:
                    throw new NotImplementedException("CurDateMode:" + m_dateMode);
            }
            #endregion Get current date

            //DateTime today = sec.Bars[j].Date;
            DateTime today = now.Date;

            // Если дата прежняя, то и дата экспирации ещё не должна измениться
            if (today == prevBarDate)
            {
                barDate = today;
                expDate = prevExpDate;
            }
            //else if ((prevExpDate - prevBarDate).TotalDays > 1) // А если бары недельные??? или месячные???
            else
            {
                barDate = today;
                #region Get expiration date
                switch (m_expiryMode)
                {
                    case ExpiryMode.FixedExpiry:
                        expDate = m_expirationDate;
                        break;

                    case ExpiryMode.FirstExpiry:
                        {
                            if (opt == null)
                                expDate = sec.SecurityDescription.ExpirationDate;
                            else
                            {
                                IOptionSeries optSer = (from ser in opt.GetSeries()
                                                        where (today <= ser.ExpirationDate.Date)
                                                        orderby ser.ExpirationDate ascending
                                                        select ser).FirstOrDefault();
                                // Если все серии уже умерли, вернуть последнюю, чтобы гарантировать возврат даты
                                if (optSer == null)
                                    optSer = (from ser in opt.GetSeries() orderby ser.ExpirationDate descending select ser).First();
                                expDate = optSer.ExpirationDate;
                            }
                        }
                        break;

                    case ExpiryMode.LastExpiry:
                        {
                            if (opt == null)
                                expDate = sec.SecurityDescription.ExpirationDate;
                            else
                            {
                                IOptionSeries optSer = (from ser in opt.GetSeries()
                                                        where (today <= ser.ExpirationDate.Date)
                                                        orderby ser.ExpirationDate descending
                                                        select ser).FirstOrDefault();
                                // Если все серии уже умерли, вернуть последнюю, чтобы гарантировать возврат даты
                                if (optSer == null)
                                    optSer = (from ser in opt.GetSeries() orderby ser.ExpirationDate descending select ser).First();
                                expDate = optSer.ExpirationDate;
                            }
                        }
                        break;

                    case ExpiryMode.ExpiryByNumber:
                        {
                            if (opt == null)
                                expDate = sec.SecurityDescription.ExpirationDate;
                            else
                            {
                                IOptionSeries[] optSers = (from ser in opt.GetSeries()
                                                           where (today <= ser.ExpirationDate.Date)
                                                           orderby ser.ExpirationDate ascending
                                                           select ser).ToArray();
                                int ind = Math.Min(m_seriesIndex - 1, optSers.Length - 1);
                                ind = Math.Max(ind, 0);
                                IOptionSeries optSer;
                                // Если все серии уже умерли, вернуть последнюю, чтобы гарантировать возврат даты
                                if (optSers.Length == 0)
                                    optSer = (from ser in opt.GetSeries() orderby ser.ExpirationDate descending select ser).First();
                                else
                                    optSer = optSers[ind];
                                expDate = optSer.ExpirationDate;
                            }
                        }
                        break;

                    default:
                        throw new NotImplementedException("ExpirationMode:" + m_expiryMode);
                }
                #endregion Get expiration date
            }

            // Грубое решение для определения точного времени экспирации
            if (m_expiryMode != ExpiryMode.FixedExpiry)
            {
                expDate = expDate.Date + m_expirationTime;
            }

            // Просто сдвигаю текущую дату?
            double time = GetDt(expDate, now + m_dateShift, m_tRemainMode, m_useDays, out timeAsDays, out timeAsYears);
            return time;
        }

        /// <summary>
        /// Вернуть время между двумя датами в соответствии с заказанным алгоритмом расчета
        /// </summary>
        /// <param name="expiry">дата экспирации</param>
        /// <param name="now">текущая дата</param>
        /// <param name="tRemainMode">режим расчета</param>
        /// <param name="returnDays">возвращать в долях года или в долях дней</param>
        /// <param name="timeAsDays">время в долях дня</param>
        /// <param name="timeAsYears">время в долях года</param>
        /// <returns>время между двумя датами</returns>
        public static double GetDt(DateTime expiry, DateTime now,
            TimeRemainMode tRemainMode, bool returnDays,
            out double timeAsDays, out double timeAsYears)
        {
            double days, daysInYear;
            double partOfDayForTrading;
            switch (tRemainMode)
            {
                case TimeRemainMode.PlainCalendar:
                    {
                        TimeSpan ts = expiry - now;
                        days = ts.TotalDays;

                        partOfDayForTrading = 1;
                        daysInYear = DaysInYearPlainCalendar;
                    }
                    break;

                case TimeRemainMode.PlainCalendarWithoutWeekends:
                    {
                        TimeSpan ts = OptionUtils.GetDtWithoutWeekendsSlow(expiry, now);
                        days = ts.TotalDays;

                        partOfDayForTrading = 1;
                        daysInYear = DaysInYearPlainCalendarWithoutWeekends;
                    }
                    break;

                case TimeRemainMode.PlainCalendarWithoutHolidays:
                    {
                        TimeSpan ts = OptionUtils.GetDtWithoutHolidaysSlow(expiry, now);
                        days = ts.TotalDays;

                        partOfDayForTrading = 1;
                        daysInYear = DaysInYearPlainCalendarWithoutHolidays;
                    }
                    break;

                case TimeRemainMode.RtsTradingTime:
                    {
                        TimeSpan ts = OptionUtils.GetDtRtsTradingTime(expiry, now);
                        days = ts.TotalDays;

                        partOfDayForTrading = ((double)OptionUtils.TradingMinutesInDayRts) / (double)OptionUtils.MinutesInDay;
                        daysInYear = DaysInYearRts;
                    }
                    break;

                case TimeRemainMode.LiquidProRtsTradingTime:
                    {
                        double tradingDaysInYear;
                        double dT = OptionUtils.GetDtLiquidProRtsTradingTime(expiry, now, out tradingDaysInYear);

                        timeAsDays = dT * tradingDaysInYear;
                        timeAsYears = dT;
                        if (returnDays)
                            return timeAsDays;
                        else
                            return timeAsYears;
                    }
                    //break;

                default:
                    throw new NotImplementedException("tRemainMode:" + tRemainMode);
            }

            timeAsDays = days / partOfDayForTrading;
            timeAsYears = days / daysInYear;
            if (returnDays)
                return timeAsDays;
            else
                return timeAsYears;
        }

        /// <summary>
        /// Это специальный паттерн для поддержки редактируемого строкового параметра
        /// </summary>
        public IEnumerable<string> GetValuesForParameter(string paramName)
        {
            if (paramName.Equals(nameof(Expiry), StringComparison.InvariantCultureIgnoreCase))
                return new[] { Expiry ?? "" };
            else if (paramName.Equals(nameof(ExpiryTime), StringComparison.InvariantCultureIgnoreCase) ||
                // GLSP-435 - Проверяю другие варианты названий
                paramName.Equals(VisibleExpiryTimeNameEn, StringComparison.InvariantCultureIgnoreCase) ||
                paramName.Equals(VisibleExpiryTimeNameRu, StringComparison.InvariantCultureIgnoreCase))
            {
                return new[] { ExpiryTime ?? "" };
            }
            else if (paramName.Equals(nameof(FixedDate), StringComparison.InvariantCultureIgnoreCase))
                return new[] { FixedDate ?? "" };
            else
                return new[] { "" };
        }
    }
}
