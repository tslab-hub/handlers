using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

using TSLab.DataSource;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english This block takes precalculated skew at the money from global cache.
    /// \~russian Брать готовый наклон на деньгах из глобального кеша (чтобы не путаться с 'пишущей' версией)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsIndicators)]
    [HelperName("Global Skew ATM", Language = Constants.En)]
    [HelperName("Global Skew ATM", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES, Name = "Security or Option Series")]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Брать готовый наклон на деньгах из глобального кеша (чтобы не путаться с 'пишущей' версией)")]
    [HelperDescription("This block takes precalculated skew at the money from global cache.", Constants.En)]
    public class GlobalSkewOnF : BaseContextHandler, IStreamHandler, ICustomListValues
    {
        private bool m_repeatLastIv;
        private bool m_rescaleTime = false;
        /// <summary>
        /// Специально для скрипта-сборщика делаю настройку для подавления исключений при отсутствии данных в Глобальном Кеше
        /// </summary>
        private bool m_ignoreCacheError = false;
        private ExpiryMode m_expiryMode = ExpiryMode.FixedExpiry;

        #region Parameters
        /// <summary>
        /// \~english Algorythm to get expiration date
        /// \~russian Алгоритм определения даты экспирации
        /// </summary>
        [HelperName("Search mode", Constants.En)]
        [HelperName("Режим поиска", Constants.Ru)]
        [Description("Алгоритм определения даты экспирации")]
        [HelperDescription("Algorythm to get expiration date", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "FixedExpiry")]
        public ExpiryMode ExpirationMode
        {
            get { return m_expiryMode; }
            set { m_expiryMode = value; }
        }

        /// <summary>
        /// \~english Option series index (only alive). Parameter is used in mode ExpiryByNumber.
        /// \~russian Индекс серии (учитываются только живые). Используется в режиме ExpiryByNumber.
        /// </summary>
        [HelperName("Number", Constants.En)]
        [HelperName("Номер", Constants.Ru)]
        [Description("Индекс серии (учитываются только живые). Используется в режиме ExpiryByNumber.")]
        [HelperDescription("Option series index (only alive). Parameter is used in mode ExpiryByNumber.", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "1", Min = "1", Max = "100000", Step = "1")]
        public int Number { get; set; }

        /// <summary>
        /// \~english Handler should repeat last known value to avoid further logic errors
        /// \~russian При true будет находить и использовать последнее известное значение
        /// </summary>
        [HelperName("Repeat last skew", Constants.En)]
        [HelperName("Повтор значения", Constants.Ru)]
        [Description("При true будет находить и использовать последнее известное значение")]
        [HelperDescription("Handler should repeat last known value to avoid further logic errors", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool RepeatLastSkew
        {
            get { return m_repeatLastIv; }
            set { m_repeatLastIv = value; }
        }

        /// <summary>
        /// \~english Expiration date (format yyyy-MM-dd)
        /// \~russian Дата экспирации в формате гггг-ММ-дд
        /// </summary>
        [HelperName("Expiry", Constants.En)]
        [HelperName("Экспирация", Constants.Ru)]
        [Description("Дата экспирации в формате гггг-ММ-дд")]
        [HelperDescription("Expiration date (format yyyy-MM-dd)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "2015-03-16")]
        public string Expiry { get; set; }

        /// <summary>
        /// \~english Rescale time-to-expiry to our internal?
        /// \~russian Заменять время на 'правильное'?
        /// </summary>
        [HelperName("Rescale time", Constants.En)]
        [HelperName("Заменить время", Constants.Ru)]
        [Description("Заменять время на 'правильное'?")]
        [HelperDescription("Rescale time-to-expiry to our internal?", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "false")]
        public bool RescaleTime
        {
            get { return m_rescaleTime; }
            set { m_rescaleTime = value; }
        }

        /// <summary>
        /// \~english Algorythm to estimate time-to-expiry
        /// \~russian Алгоритм расчета времени до экспирации
        /// </summary>
        [HelperName("Estimation algo", Constants.En)]
        [HelperName("Алгоритм расчета", Constants.Ru)]
        [Description("Алгоритм расчета времени до экспирации")]
        [HelperDescription("Algorythm to estimate time-to-expiry", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "PlainCalendar")]
        public TimeRemainMode DistanceMode { get; set; }

        /// <summary>
        /// \~english Handler should ignore cache errors in agent mode
        /// \~russian Специально для агента-сборщика волатильности делаю настройку для подавления исключений при отсутствии данных в Глобальном Кеше
        /// </summary>
        [HelperName("Ignore cache error", Constants.En)]
        [HelperName("Игнорировать ошибки кеша", Constants.Ru)]
        [Description("Специально для агента-сборщика волатильности делаю настройку для подавления исключений при отсутствии данных в Глобальном Кеше")]
        [HelperDescription("Handler should ignore cache errors in agent mode", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "false")]
        public bool IgnoreCacheError
        {
            get { return m_ignoreCacheError; }
            set { m_ignoreCacheError = value; }
        }

        /// <summary>
        /// \~english Algorythm to get smile skew
        /// \~russian Алгоритм определения наклона улыбки
        /// </summary>
        [HelperName("Skew mode", Constants.En)]
        [HelperName("Режим наклона", Constants.Ru)]
        [Description("Алгоритм определения наклона улыбки")]
        [HelperDescription("Algorythm to get smile skew", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "RescaledSkew")]
        public SmileSkewMode SkewMode { get; set; }
        #endregion Parameters

        /// <summary>
        /// Обработчик под тип входных данных SECURITY
        /// </summary>
        public IList<double> Execute(ISecurity sec)
        {
            if (sec == null)
            {
                // [{0}] Empty input (security is NULL).
                string msg = RM.GetStringFormat("OptHandlerMsg.SecurityIsNull", GetType().Name);
                return Constants.EmptyListDouble;
            }

            var res = PrepareData(sec, Expiry);
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES
        /// </summary>
        public IList<double> Execute(IOptionSeries optSer)
        {
            if (optSer == null)
            {
                // [{0}] Empty input (option series is NULL).
                string msg = RM.GetStringFormat("OptHandlerMsg.OptionSeriesIsNull", GetType().Name);
                m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptyListDouble;
            }

            IList<double> res;
            ISecurity sec = optSer.UnderlyingAsset;
            switch (m_expiryMode)
            {
                case ExpiryMode.FixedExpiry:
                    res = PrepareData(sec, Expiry);
                    break;

                default:
                    string optSerExpiry = optSer.ExpirationDate.ToString(IvOnF.DateFormat, CultureInfo.InvariantCulture);
                    res = PrepareData(sec, optSerExpiry);
                    break;
            }

            return res;
        }

        private IList<double> PrepareData(ISecurity sec, string expiryDate)
        {
            DateTime expiry;
            if ((!DateTime.TryParseExact(expiryDate, IvOnF.DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out expiry)) &&
                (!DateTime.TryParseExact(expiryDate, IvOnF.DateFormat + " HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out expiry)))
            {
                string msg = String.Format("[{0}.{1}.PrepareData] Unable to parse expiration date '{2}'. Expected date format '{3}'.",
                    Context.Runtime.TradeName, GetType().Name, expiryDate, IvOnF.DateFormat);
                m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptyListDouble;
            }

            //// и тут я понял, что дату экспирации в любом случае надо задавать руками...
            //string cashKey = typeof(IvOnF).Name + "_ivExchangeSigmas_" + sec.Symbol + "_" +
            //    expiryDate.Replace(':', '-');
            string skewKey = IvOnF.GetSkewCashKey(sec.Symbol, expiry, SkewMode, DistanceMode);
            Dictionary<DateTime, double> ivSkews = null;
            try
            {
                object globalObj = Context.LoadGlobalObject(skewKey, true);
                ivSkews = globalObj as Dictionary<DateTime, double>;
                // PROD-3970 - 'Важный' объект
                if (ivSkews == null)
                {
                    var container = globalObj as NotClearableContainer;
                    if ((container != null) && (container.Content != null))
                        ivSkews = container.Content as Dictionary<DateTime, double>;
                }
            }
            catch (NotSupportedException nse)
            {
                string fName = "", path = "";
                if (nse.Data["fName"] != null)
                    fName = nse.Data["fName"].ToString();
                if (nse.Data["path"] != null)
                    path = nse.Data["path"].ToString();
                string msg = String.Format("[{0}.PrepareData] {1} when loading 'ivSkews' from global cache. cashKey: {2}; Message: {3}\r\n\r\nfName: {4}; path: {5}\r\n\r\n{6}",
                    GetType().Name, nse.GetType().FullName, skewKey, nse.Message, fName, path, nse);
                m_context.Log(msg, MessageType.Warning, true);
            }
            catch (Exception ex)
            {
                string msg = String.Format("[{0}.PrepareData] {1} when loading 'ivSkews' from global cache. cashKey: {2}; Message: {3}\r\n\r\n{4}",
                    GetType().Name, ex.GetType().FullName, skewKey, ex.Message, ex);
                m_context.Log(msg, MessageType.Warning, true);
            }
            
            if (ivSkews == null)
            {
                // Данного ключа в глобальном кеше нет? Тогда выход.
                // [{0}.PrepareData] There is no Skew ATM in global cache. Probably, you have to start agent 'Collect IV (ALL)' for security '{1}'.
                string msg = RM.GetStringFormat("OptHandlerMsg.GlobalSkewOnF.CacheNotFound", GetType().Name, expiryDate, sec);
                if (m_context.Runtime.IsAgentMode && (!m_ignoreCacheError))
                    throw new ScriptException(msg); // PROD-4624 - Андрей велит кидать исключение.

                bool isExpired = true;
                if (m_context.Runtime.IsAgentMode)
                {
                    int amount = sec.Bars.Count;
                    DateTime today = (amount > 0) ? sec.Bars[amount - 1].Date : new DateTime();
                    isExpired = expiry.Date.AddDays(1) < today.Date;
                }
                // А если в режиме лаборатории, тогда только жалуемся и продолжаем.
                m_context.Log(msg, MessageType.Warning, !isExpired); // Если серия уже умерла, пишем только в локальный лог
                return Constants.EmptyListDouble;
            }

            List<double> res = new List<double>();

            int len = sec.Bars.Count;
            if (len <= 0)
                return res;

            int oldResLen = res.Count;
            double prevSkew = Double.NaN;
            for (int j = oldResLen; j < len; j++)
            {
                DateTime now = sec.Bars[j].Date;
                double skew;
                if (ivSkews.TryGetValue(now, out skew) && (!DoubleUtil.IsNaN(skew)))
                {
                    prevSkew = skew;
                    res.Add(skew);
                }
                else
                {
                    if (m_repeatLastIv)
                    {
                        if (!Double.IsNaN(prevSkew))
                        {
                            skew = prevSkew;
                        }
                        else
                        {
                            skew = Constants.NaN;
                            if (j == 0)
                            {
                                #region Отдельно обрабатываю нулевой бар
                                double tmp = Double.NaN;
                                DateTime foundKey = new DateTime(1, 1, 1);
                                // [2016-01-19] Когда история становится слишком длинной, это может вызывать проблемы
                                // при итерировании в foreach. Потому что другой поток может в этот момент добавить новую точку в коллекцию.
                                int repeat = 7;
                                while (repeat > 0)
                                {
                                    tmp = Double.NaN;
                                    foundKey = new DateTime(1, 1, 1);
                                    try
                                    {
                                        lock (ivSkews)
                                        {
                                            foreach (var kvp in ivSkews)
                                            {
                                                if (kvp.Key > now)
                                                    continue;

                                                if (foundKey < kvp.Key)
                                                {
                                                    foundKey = kvp.Key;
                                                    tmp = kvp.Value;
                                                }
                                            }
                                        }
                                        repeat = -100;
                                    }
                                    catch (InvalidOperationException invOpEx)
                                    {
                                        repeat--;
                                        Thread.Sleep(10);
                                        if (repeat <= 0)
                                        {
                                            string msg = String.Format("[{0}.PrepareData] {1} when iterate through 'ivSkews'. cashKey: {2}; Message: {3}\r\n\r\n{4}",
                                                GetType().Name, invOpEx.GetType().FullName, skewKey, invOpEx.Message, invOpEx);
                                            m_context.Log(msg, MessageType.Warning, true);
                                            throw;
                                        }
                                    }
                                }

                                if ((foundKey.Year > 1) && DoubleUtil.IsPositive(tmp))
                                {
                                    skew = tmp;
                                    prevSkew = skew;
                                }
                                #endregion Отдельно обрабатываю нулевой бар
                            }
                        }
                        res.Add(skew);
                    }
                    else
                        res.Add(Constants.NaN);
                }
            }

            return res;
        }

        /// <summary>
        /// Это специальный паттерн для поддержки редактируемого строкового параметра
        /// </summary>
        public IEnumerable<string> GetValuesForParameter(string paramName)
        {
            if (paramName.Equals("Expiry", StringComparison.InvariantCultureIgnoreCase) ||
                paramName.Equals("Экспирация", StringComparison.InvariantCultureIgnoreCase))
                return new[] { Expiry ?? "" };
            else
                return new[] { "" };
        }
    }
}
