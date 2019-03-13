using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using TSLab.DataSource;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Implied Volatility at-the-money
    /// \~russian Подразумеваемая волатильность на деньгах
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsIndicators)]
    [HelperName("IV ATM", Language = Constants.En)]
    [HelperName("IV ATM", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Подразумеваемая волатильность на деньгах")]
    [HelperDescription("Implied Volatility at-the-money", Constants.En)]
    public class IvOnF : BaseSmileDrawing, IStreamHandler, ICustomListValues
    {
        /// <summary>Формат сериализации даты (yyyy-MM-dd)</summary>
        public const string DateFormat = "yyyy-MM-dd";

        // GLSP-435 - Проверяю другие варианты названий
        private const string VisibleExpiryTimeNameEn = "Expiry Time";
        private const string VisibleExpiryTimeNameRu = "Время истечения";

        private bool m_repeatLastIv;

        private bool m_rescaleTime;
        private TimeRemainMode m_tRemainMode = TimeRemainMode.RtsTradingTime;

        private TimeSpan m_expiryTime = TimeSpan.Parse(Constants.DefaultFortsExpiryTimeStr);
        private string m_expiryTimeStr = Constants.DefaultFortsExpiryTimeStr;

        /// <summary>
        /// Локальный кеш волатильностей
        /// </summary>
        private List<double> LocalHistory
        {
            get
            {
                List<double> ivSigmas = Context.LoadObject(VariableId + "ivSigmas") as List<double>;
                if (ivSigmas == null)
                {
                    ivSigmas = new List<double>();
                    Context.StoreObject(VariableId + "ivSigmas", ivSigmas);
                }

                return ivSigmas;
            }
        }

        #region Parameters
        /// <summary>
        /// \~english Rescale time-to-expiry to our internal?
        /// \~russian Заменять время на 'правильное'?
        /// </summary>
        [HelperName("Rescale Time", Constants.En)]
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
        [HelperName("Estimation Algo", Constants.En)]
        [HelperName("Алгоритм расчета", Constants.Ru)]
        [Description("Алгоритм расчета времени до экспирации")]
        [HelperDescription("Algorythm to estimate time-to-expiry", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "PlainCalendar")]
        public TimeRemainMode DistanceMode
        {
            get { return m_tRemainMode; }
            set { m_tRemainMode = value; }
        }

        /// <summary>
        /// \~english Exact expiration time of day (HH:mm)
        /// \~russian Точное время экспирации (ЧЧ:мм)
        /// </summary>
        [HelperName(VisibleExpiryTimeNameEn, Constants.En)]
        [HelperName(VisibleExpiryTimeNameRu, Constants.Ru)]
        [Description("Точное время экспирации (ЧЧ:мм)")]
        [HelperDescription("Exact expiration time of day (HH:mm)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = Constants.DefaultFortsExpiryTimeStr, Name = "Expiry Time")]
        public string ExpiryTime
        {
            get { return m_expiryTimeStr; }
            set
            {
                TimeSpan tmp;
                if (TimeSpan.TryParse(value, out tmp))
                {
                    m_expiryTimeStr = value;
                    m_expiryTime = tmp;
                }
            }
        }

        /// <summary>
        /// \~english Handler should repeat last known value to avoid further logic errors
        /// \~russian При true будет находить и использовать последнее известное значение
        /// </summary>
        [HelperName("Repeat Last IV", Constants.En)]
        [HelperName("Повтор значения", Constants.Ru)]
        [Description("При true будет находить и использовать последнее известное значение")]
        [HelperDescription("Handler should repeat last known value to avoid further logic errors", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false", Name = "Repeat Last IV")]
        public bool RepeatLastIv
        {
            get { return m_repeatLastIv; }
            set { m_repeatLastIv = value; }
        }

        /// <summary>
        /// \~english Use global cache
        /// \~russian Использовать глобальный кеш
        /// </summary>
        [HelperName("Use Global Cache", Constants.En)]
        [HelperName("Использовать глобальный кеш", Constants.Ru)]
        [Description("Использовать глобальный кеш")]
        [HelperDescription("Use global cache", Language = Constants.En)]
        [HandlerParameter(Name = "Use Global Cache", NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool UseGlobalCache { get; set; }

        /// <summary>
        /// \~english Permission to write to Global Cache
        /// \~russian Разрешить чтение/запись в глобальный кеш или только чтение?
        /// </summary>
        [HelperName("Allow Global Write", Constants.En)]
        [HelperName("Разрешить запись", Constants.Ru)]
        [Description("Разрешить чтение/запись в глобальный кеш или только чтение?")]
        [HelperDescription("Permission to write to Global Cache", Language = Constants.En)]
        [HandlerParameter(Name = "Allow Global Write", NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool AllowGlobalReadWrite { get; set; }

        /// <summary>
        /// \~english Period to write to Global Cache
        /// \~russian Сохранять в глобальный кеш через каждые N баров
        /// </summary>
        [HelperName("Global Save Period", Constants.En)]
        [HelperName("Периодичность записи", Constants.Ru)]
        [Description("Сохранять в глобальный кеш через каждые N баров")]
        [HelperDescription("Period to write to Global Cache", Language = Constants.En)]
        [HandlerParameter(Name = "Global Save Period", NotOptimized = false, IsVisibleInBlock = true,
            Default = "2", Min = "1", Max = "10000000", Step = "1")]
        public int GlobalSavePeriod { get; set; }
        #endregion Parameters

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES
        /// </summary>
        public IList<double> Execute(IOptionSeries optSer)
        {
            if (optSer == null)
            {
                string msg = "[IV ATM] (optSer == null)";
                m_context.Log(msg, MessageType.Warning, false);

                return Constants.EmptyListDouble;
            }

            Dictionary<DateTime, double> ivSigmas;
            #region Get cache
            DateTime expiry = optSer.ExpirationDate.Date;
            string cashKey = IvOnF.GetCashKey(optSer.UnderlyingAsset.Symbol, expiry, m_rescaleTime, m_tRemainMode);
            ivSigmas = LoadOrCreateHistoryDict(UseGlobalCache, cashKey);
            #endregion Get cache

            List<double> res;
            ISecurity sec = optSer.UnderlyingAsset;
            int len = sec.Bars.Count;
            if (len <= 0)
                return Constants.EmptyListDouble;

            if (m_context.IsFixedBarsCount)
            {
                #region Ветка с ФИКСИРОВАННЫМ количеством баров
                double lastIv = Double.NaN;
                res = new List<double>(len);
                for (int j = 0; j < len; j++)
                {
                    DateTime now = sec.Bars[j].Date;
                    double iv;
                    if ((ivSigmas.TryGetValue(now, out iv)) && (!Double.IsNaN(iv)) && (iv > 0))
                    {
                        lastIv = iv;
                        res.Add(iv);
                    }
                    else
                    {
                        if (m_repeatLastIv && (!Double.IsNaN(lastIv)))
                            res.Add(lastIv);
                        else
                            res.Add(Constants.NaN);
                    }
                }
                #endregion Ветка с ФИКСИРОВАННЫМ количеством баров
            }
            else
            {
                #region Ветка с нарастающим количеством баров
                res = LocalHistory;
                // PROD-1933
                // 1. Выполняю очистку локального кеша в сценарии восстановления соединения после дисконнекта
                if (res.Count > len)
                    res.Clear();

                // 2. Ищу последнее валидное значение в кеше причем только если это может быть нужно
                double lastIv = Double.NaN;
                if (m_repeatLastIv)
                {
                    for (int j = res.Count - 1; j >= 0; j--)
                    {
                        if ((!Double.IsNaN(res[j])) && (res[j] > 0))
                        {
                            lastIv = res[j];
                            break;
                        }
                    }
                }

                for (int j = res.Count; j < len; j++)
                {
                    DateTime now = sec.Bars[j].Date;
                    double iv;
                    if ((ivSigmas.TryGetValue(now, out iv)) && (!Double.IsNaN(iv)) && (iv > 0))
                    {
                        lastIv = iv;
                        res.Add(iv);
                    }
                    else
                    {
                        if (m_repeatLastIv && (!Double.IsNaN(lastIv)))
                            res.Add(lastIv);
                        else
                            res.Add(Constants.NaN);
                    }
                }
                #endregion Ветка с нарастающим количеством баров
            }

            Debug.Assert(res != null, "How is it possible (res == null)?");
            Debug.Assert(res.Count == len, String.Format("Wrong res.Count. res.Count:{0}; expected len:{1}; IsFixedBarsCount:{2}",
                res.Count, len, m_context.IsFixedBarsCount));

            FinInfo baseFinInfo = optSer.UnderlyingAsset.FinInfo;
            // Эта проверка намекает на проблемы с маркет-датой.
            if (baseFinInfo.LastPrice == null)
            {
                string msg = "[IV ATM] (baseFinInfo.LastPrice == null)";
                m_context.Log(msg, MessageType.Warning, false);
                return res;
            }

            try
            {
                double sigma;
                double futPx = baseFinInfo.LastPrice.Value;
                NotAKnotCubicSpline spline = PrepareExchangeSmileSpline(optSer, Double.MinValue, Double.MaxValue);
                if ((spline != null) && spline.TryGetValue(futPx, out sigma) && DoubleUtil.IsPositive(sigma))
                {
                    DateTime lastBarDate = sec.Bars[len - 1].Date;
                    if (m_rescaleTime)
                    {
                        #region Зверская ветка по замене времени
                        double ivAtm = sigma;
                        DateTime expDate = optSer.ExpirationDate.Date + m_expiryTime;
                        DateTime now = baseFinInfo.LastUpdate;

                        // 1. Надо перевести волатильность в абсолютную цену
                        // с учетом плоского календарного времени применяемого РТС
                        double plainTimeAsYears;
                        {
                            double plainTimeAsDays;
                            TimeToExpiry.GetDt(expDate, now, TimeRemainMode.PlainCalendar, false, out plainTimeAsDays,
                                out plainTimeAsYears);
                        }

                        // 2. Вычисляем 'нормальное' время
                        double timeAsDays, timeAsYears;
                        TimeToExpiry.GetDt(expDate, now, m_tRemainMode, false, out timeAsDays, out timeAsYears);
                        sigma = FinMath.RescaleIvToAnotherTime(plainTimeAsYears, ivAtm, timeAsYears);
                        if (DoubleUtil.IsPositive(sigma))
                        {
                            // Это просто запись на диск. К успешности вычисления волы success отношения не имеет
                            bool success = TryWrite(m_context, UseGlobalCache, AllowGlobalReadWrite,
                                GlobalSavePeriod, cashKey, ivSigmas, lastBarDate, sigma);

                            // Теперь надо вычислить безразмерный наклон кодом в классе SmileImitation5
                            bool successSkew = TryCalcAndWriteSkews(m_context, spline, UseGlobalCache, AllowGlobalReadWrite, GlobalSavePeriod,
                                optSer.UnderlyingAsset.Symbol, expiry, futPx, lastBarDate, m_tRemainMode, plainTimeAsYears, timeAsYears);
                        }
                        else
                        {
                            // Если перемасштабировать улыбку не получается придется эту точку проигнорировать
                            // Надо ли сделать соответствующую запись в логе???
                            sigma = Constants.NaN;
                        }
                        #endregion Зверская ветка по замене времени
                    }
                    else
                    {
                        // Это просто запись на диск. К успешности вычисления волы success отношения не имеет
                        bool success = TryWrite(m_context, UseGlobalCache, AllowGlobalReadWrite,
                            GlobalSavePeriod, cashKey, ivSigmas, lastBarDate, sigma);
                    }
                }
                else
                    sigma = Constants.NaN;

                res[len - 1] = sigma;
            }
            catch (Exception ex)
            {
                m_context.Log(ex.ToString(), MessageType.Error, false);
                return res;
            }

            if (m_repeatLastIv)
            {
                if (DoubleUtil.AreClose(res[len - 1], Constants.NaN) || Double.IsNaN(res[len - 1]) || (res[len - 1] <= 0))
                {
                    // Итерируюсь с конца в начало пока не найду последний ненулевой элемент.
                    // Использую его в качестве ВСЕХ последних значений ряда.
                    for (int j = len - 1; j >= 0; j--)
                    {
                        if ((!DoubleUtil.AreClose(res[j], Constants.NaN)) && (!Double.IsNaN(res[j])) && (res[j] > 0))
                        {
                            double lastIv = res[j];
                            for (int k = j + 1; k < len; k++)
                            {
                                res[k] = lastIv;
                            }
                            break;
                        }
                    }
                }
            }

            return new ReadOnlyCollection<double>(res);
        }

        /// <summary>
        /// Ключ Глобального Кеша для хранения БИРЖЕВОГО НАКЛОНА на-деньгах
        /// </summary>
        public static string GetSkewCashKey(string baseSecuritySymbol, DateTime expiry,
            SmileSkewMode skewMode, TimeRemainMode tRemainMode)
        {
            string cashKey;
            if (skewMode == SmileSkewMode.RawSkew)
            {
                // Сырой размерный наклон ничего не знает о времени
                cashKey = typeof(IvOnF).Name + "_ivExchangeSkews_" + skewMode + "_" +
                    baseSecuritySymbol + "_" + expiry.ToString(IvOnF.DateFormat, CultureInfo.InvariantCulture);
            }
            else
            {
                // Безразмерный наклон обязан знать про время.
                // TODO: И, по-хорошему, про форму. Но это можно отложить пока что.
                cashKey = typeof(IvOnF).Name + "_ivExchangeSkews_" + skewMode + "_" + tRemainMode + "_" +
                    baseSecuritySymbol + "_" + expiry.ToString(IvOnF.DateFormat, CultureInfo.InvariantCulture);
            }
            return String.Intern(cashKey);
        }

        /// <summary>
        /// Ключ Глобального Кеша для хранения БИРЖЕВОЙ волатильности на-деньгах
        /// </summary>
        public static string GetCashKey(string baseSecuritySymbol, DateTime expiry,
            bool rescaleTime, TimeRemainMode tRemainMode)
        {
            string cashKey;
            if (rescaleTime)
            {
                // Если мы перемасштабируем время, то волу надо хранить в кеше с другим названием
                // (в зависимости от алгоритма)!
                cashKey = String.Format(CultureInfo.InvariantCulture, "{0}_ivExchangeSigmas_{1}_{2}_{3}",
                    typeof(IvOnF).Name, tRemainMode, baseSecuritySymbol,
                    expiry.ToString(IvOnF.DateFormat, CultureInfo.InvariantCulture));
            }
            else
            {
                cashKey = typeof(IvOnF).Name + "_ivExchangeSigmas_" + baseSecuritySymbol + "_" +
                    expiry.ToString(IvOnF.DateFormat, CultureInfo.InvariantCulture);
            }
            return String.Intern(cashKey);
        }

        public static NotAKnotCubicSpline PrepareExchangeSmileSpline(IOptionSeries optSer, double minStrike, double maxStrike)
        {
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            IOptionStrikePair[] pairs = (from pair in optSer.GetStrikePairs()
                                         //orderby pair.Strike ascending -- уже отсортировано!
                                         select pair).ToArray();
            for (int j = 0; j < pairs.Length; j++)
            {
                IOptionStrikePair sInfo = pairs[j];
                // Сверхдалекие страйки игнорируем
                if ((sInfo.Strike < minStrike) || (maxStrike < sInfo.Strike))
                    continue;

                if ((sInfo.PutFinInfo == null) || (sInfo.CallFinInfo == null) ||
                    (!sInfo.PutFinInfo.TheoreticalPrice.HasValue) || (!sInfo.PutFinInfo.Volatility.HasValue) ||
                    (sInfo.PutFinInfo.TheoreticalPrice.Value <= 0) || (sInfo.PutFinInfo.Volatility.Value <= 0) ||
                    (!sInfo.CallFinInfo.TheoreticalPrice.HasValue) || (!sInfo.CallFinInfo.Volatility.HasValue) ||
                    (sInfo.CallFinInfo.TheoreticalPrice.Value <= 0) || (sInfo.CallFinInfo.Volatility.Value <= 0))
                    continue;

                // TODO: вернуть ассерт потом
                //System.Diagnostics.Debug.Assert(
                //    DoubleUtil.AreClose(sInfo.PutFinInfo.Volatility.Value, sInfo.CallFinInfo.Volatility.Value),
                //    "Exchange volas on the same strike MUST be equal! PutVola:" + sInfo.PutFinInfo.Volatility.Value +
                //    "; CallVola:" + sInfo.CallFinInfo.Volatility.Value);

                xs.Add(sInfo.Strike);
                ys.Add(sInfo.PutFinInfo.Volatility.Value);
            }

            NotAKnotCubicSpline spline = null;
            if (xs.Count >= BaseCubicSpline.MinNumberOfNodes)
            {
                spline = new NotAKnotCubicSpline(xs, ys);
            }
            return spline;
        }

        /// <summary>
        /// Обновление исторической серии, которая потенциально может быть сохранена в глобальный кеш.
        /// При записи в кеш серия помещается в NotClearableContainer, чтобы выживать при очистке памяти.
        /// </summary>
        /// <returns>
        /// true, если новое значение было фактически помещено в серию;
        /// false возникает, если запись в глобальный кеш блокирована флагом allowGlobalWrite
        /// </returns>
        public static bool TryWrite(IContext context, bool useGlobal, bool allowGlobalWrite, int savePeriod,
            string cashKey, Dictionary<DateTime, double> series, DateTime now, double val)
        {
            if (useGlobal)
            {
                if (allowGlobalWrite)
                {
                    series[now] = val;
                    if (series.Count % savePeriod == 0)
                    {
                        var container = new NotClearableContainer(series);
                        context.StoreGlobalObject(cashKey, container, true);
                    }
                    return true;
                }
            }
            else
            {
                series[now] = val;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Вычисление нескольких видов наклона улыбки и обновление исторической серии,
        /// которая потенциально может быть сохранена в глобальный кеш.
        /// При записи в кеш серия помещается в NotClearableContainer, чтобы выживать при очистке памяти.
        /// </summary>
        public static bool TryCalcAndWriteSkews(IContext context, NotAKnotCubicSpline smileSpline,
            bool useGlobal, bool allowGlobalWrite, int savePeriod,
            string futSymbol, DateTime optExpiry, double futPx, DateTime lastBarDate,
            TimeRemainMode tRemainMode, double plainTimeAsYears, double timeAsYears)
        {
            bool successSkew = false;
            var splineD1 = smileSpline.DeriveD1();
            if (splineD1.TryGetValue(futPx, out var skewAtm))
            {
                //string symbol = optSer.UnderlyingAsset.Symbol;
                // А. Записываем в Глобальный Кеш РАЗМЕРНЫЙ наклон (модель времени неважна)
                {
                    string skewKey = IvOnF.GetSkewCashKey(futSymbol, optExpiry, SmileSkewMode.RawSkew, TimeRemainMode.PlainCalendar);
                    var ivSkews = LoadOrCreateHistoryDict(context, useGlobal, skewKey);
                    // Это просто запись на диск. К успешности вычисления наклона successSkew отношения не имеет
                    successSkew = TryWrite(context, useGlobal, allowGlobalWrite,
                        savePeriod, skewKey, ivSkews, lastBarDate, skewAtm);
                }

                // В. Записываем в Глобальный Кеш БЕЗРАЗМЕРНЫЙ наклон В БИРЖЕВОМ ВРЕМЕНИ (PlainCalendar)
                {
                    double dSigmaDxExchange = SmileImitation5.GetDSigmaDx(futPx, plainTimeAsYears, skewAtm, 0);
                    if (!DoubleUtil.IsNaN(dSigmaDxExchange))
                    {
                        string skewKey = IvOnF.GetSkewCashKey(futSymbol, optExpiry, SmileSkewMode.ExchangeSkew, TimeRemainMode.PlainCalendar);
                        var ivSkews = LoadOrCreateHistoryDict(context, useGlobal, skewKey);
                        // Это просто запись на диск. К успешности вычисления наклона successSkew отношения не имеет
                        successSkew = TryWrite(context, useGlobal, allowGlobalWrite,
                            savePeriod, skewKey, ivSkews, lastBarDate, dSigmaDxExchange);
                    }
                }

                // Д. Записываем в Глобальный Кеш БЕЗРАЗМЕРНЫЙ наклон В НАШЕМ ВРЕМЕНИ (в соответствии с tRemainMode)
                {
                    double dSigmaDxRescaled = SmileImitation5.GetDSigmaDx(futPx, timeAsYears, skewAtm, 0);
                    if (!DoubleUtil.IsNaN(dSigmaDxRescaled))
                    {
                        string skewKey = IvOnF.GetSkewCashKey(futSymbol, optExpiry, SmileSkewMode.RescaledSkew, tRemainMode);
                        var ivSkews = LoadOrCreateHistoryDict(context, useGlobal, skewKey);
                        // Это просто запись на диск. К успешности вычисления наклона successSkew отношения не имеет
                        successSkew = TryWrite(context, useGlobal, allowGlobalWrite,
                            savePeriod, skewKey, ivSkews, lastBarDate, dSigmaDxRescaled);
                    }
                }
            } // End if (splineD1.TryGetValue(futPx, out var skewAtm))

            return successSkew;
        }

        /// <summary>
        /// Это специальный паттерн для поддержки редактируемого строкового параметра
        /// </summary>
        public IEnumerable<string> GetValuesForParameter(string paramName)
        {
            if (paramName.Equals(nameof(ExpiryTime), StringComparison.InvariantCultureIgnoreCase) ||
                // GLSP-435 - Проверяю другие варианты названий
                paramName.Equals(VisibleExpiryTimeNameEn, StringComparison.InvariantCultureIgnoreCase) ||
                paramName.Equals(VisibleExpiryTimeNameRu, StringComparison.InvariantCultureIgnoreCase))
            {
                return new[] { ExpiryTime ?? "" };
            }
            else
                return new[] { "" };
        }
    }
}
