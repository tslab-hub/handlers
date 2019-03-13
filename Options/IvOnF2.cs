using System;
using System.Collections.Generic;
using System.ComponentModel;

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
    [HelperName("IV ATM (by tick)", Language = Constants.En)]
    [HelperName("IV ATM (by tick)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Подразумеваемая волатильность на деньгах")]
    [HelperDescription("Implied Volatility at-the-money", Constants.En)]
    public class IvOnF2 : BaseContextHandler, IValuesHandlerWithNumber, ICustomListValues
    {
        // GLSP-435 - Проверяю другие варианты названий
        private const string VisibleExpiryTimeNameEn = "Expiry Time";
        private const string VisibleExpiryTimeNameRu = "Время истечения";

        private bool m_repeatLastIv;

        /// <summary>
        /// Локальное кеширующее поле
        /// </summary>
        private double m_prevIv = Double.NaN;

        private bool m_rescaleTime;
        private TimeRemainMode m_tRemainMode = TimeRemainMode.RtsTradingTime;

        private TimeSpan m_expiryTime = TimeSpan.Parse(Constants.DefaultFortsExpiryTimeStr);
        private string m_expiryTimeStr = Constants.DefaultFortsExpiryTimeStr;

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
        public double Execute(IOptionSeries optSer, int barNum)
        {
            double failRes = Constants.NaN;
            if (m_repeatLastIv)
                failRes = Double.IsNaN(m_prevIv) ? Constants.NaN : m_prevIv;

            Dictionary<DateTime, double> ivSigmas;
            #region Get cache
            DateTime expiry = optSer.ExpirationDate.Date;
            string cashKey = IvOnF.GetCashKey(optSer.UnderlyingAsset.Symbol, expiry, m_rescaleTime, m_tRemainMode);
            ivSigmas = LoadOrCreateHistoryDict(UseGlobalCache, cashKey);
            #endregion Get cache

            ISecurity sec = optSer.UnderlyingAsset;
            int len = sec.Bars.Count;
            if (len <= 0)
                return failRes;

            DateTime lastBarDate = sec.Bars[barNum].Date;
            double iv;
            if ((ivSigmas.TryGetValue(lastBarDate, out iv)) && (!Double.IsNaN(iv)) && (iv > 0))
            {
                m_prevIv = iv;
                return iv;
            }
            else
            {
                int barsCount = ContextBarsCount;
                if (barNum < barsCount - 1)
                {
                    // Если история содержит осмысленное значение, то оно уже содержится в failRes
                    return failRes;
                }
                else
                {
                    #region Process last bar(s)
                    FinInfo baseFinInfo = optSer.UnderlyingAsset.FinInfo;
                    if (baseFinInfo.LastPrice == null)
                    {
                        string msg = "[IV ATM 2] (baseFinInfo.LastPrice == null)";
                        m_context.Log(msg, MessageType.Warning, false);
                        return failRes;
                    }

                    double futPx = baseFinInfo.LastPrice.Value;
                    if (futPx <= Double.Epsilon)
                        return failRes;

                    NotAKnotCubicSpline spline = null;
                    try
                    {
                        spline = IvOnF.PrepareExchangeSmileSpline(optSer, Double.MinValue, Double.MaxValue);
                    }
                    catch (ScriptException scriptEx)
                    {
                        m_context.Log(scriptEx.ToString(), MessageType.Error, false);
                        return failRes;
                    }
                    catch (Exception ex)
                    {
                        m_context.Log(ex.ToString(), MessageType.Error, false);
                        return failRes;
                    }

                    if (spline == null)
                        return failRes;

                    try
                    {
                        double sigma;
                        if (spline.TryGetValue(futPx, out sigma) && (sigma > 0))
                        {
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
                                    TimeToExpiry.GetDt(expDate, now, TimeRemainMode.PlainCalendar, false,
                                        out plainTimeAsDays, out plainTimeAsYears);
                                }

                                // 2. Вычисляем 'нормальное' время
                                double timeAsDays, timeAsYears;
                                TimeToExpiry.GetDt(expDate, now, m_tRemainMode, false, out timeAsDays, out timeAsYears);
                                sigma = FinMath.RescaleIvToAnotherTime(plainTimeAsYears, ivAtm, timeAsYears);
                                if (DoubleUtil.IsPositive(sigma))
                                {
                                    // Это просто запись на диск. К успешности вычисления волы success отношения не имеет
                                    lock (ivSigmas)
                                    {
                                        bool success = IvOnF.TryWrite(m_context, UseGlobalCache, AllowGlobalReadWrite,
                                            GlobalSavePeriod, cashKey, ivSigmas, lastBarDate, sigma);
                                        m_prevIv = sigma;

                                        // Теперь надо вычислить безразмерный наклон кодом в классе SmileImitation5
                                        bool successSkew = IvOnF.TryCalcAndWriteSkews(m_context, spline, UseGlobalCache, AllowGlobalReadWrite,
                                            GlobalSavePeriod, optSer.UnderlyingAsset.Symbol, expiry, futPx, lastBarDate,
                                            m_tRemainMode, plainTimeAsYears, timeAsYears);

                                        return sigma;
                                    }
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
                                lock (ivSigmas)
                                {
                                    bool success = IvOnF.TryWrite(m_context, UseGlobalCache, AllowGlobalReadWrite,
                                        GlobalSavePeriod, cashKey, ivSigmas, lastBarDate, sigma);
                                    m_prevIv = sigma;
                                    return sigma;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        m_context.Log(ex.ToString(), MessageType.Error, false);
                    }

                    return failRes;
                    #endregion Process last bar
                }
            }
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
