using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.DataSource;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Implied Volatility at-the-money (all option series are processed)
    /// \~russian Подразумеваемая волатильность на деньгах (обработка сразу всех серий)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsIndicators)]
    [HelperName("IV ATM (all series)", Language = Constants.En)]
    [HelperName("IV ATM (все серии)", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION, Name = Constants.OptionSource)]
    [OutputsCount(0)]
    [Description("Подразумеваемая волатильность на деньгах (обработка сразу всех серий)")]
    [HelperDescription("Implied Volatility at-the-money (all option series are processed)", Constants.En)]
    public class IvOnFAllSeries : BaseContextHandler, IStreamHandler
    {
        private bool m_rescaleTime = false;
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
        [HelperName("Expiry Time", Constants.En)]
        [HelperName("Время истечения", Constants.Ru)]
        [Description("Точное время экспирации (ЧЧ:мм)")]
        [HelperDescription("Exact expiration time of day (HH:mm)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
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
        #endregion Parameters

        /// <summary>
        /// Обработчик под тип входных данных OPTION
        /// </summary>
        public void Execute(IOption opt)
        {
            if (opt == null)
                return;

            DateTime now = opt.UnderlyingAsset.FinInfo.LastUpdate;
            DateTime today = now.Date;
            IOptionSeries[] series = opt.GetSeries().ToArray();
            for (int j = 0; j < series.Length; j++)
            {
                IOptionSeries optSer = series[j];
                if (optSer.ExpirationDate.Date < today)
                    continue;

                try
                {
                    double ivAtm;
                    TryProcessSeries(optSer, now, out ivAtm);
                }
                catch (Exception ex)
                {
                    string msg = String.Format("[{0}] {1} when processing option series: {2}", GetType().Name, ex.GetType().FullName, ex);
                    m_context.Log(msg, MessageType.Warning, true);
                }
            }
        }

        private bool TryProcessSeries(IOptionSeries optSer, DateTime now, out double ivAtm)
        {
            ivAtm = Constants.NaN;
            if (optSer == null)
                return false;

            Dictionary<DateTime, double> ivSigmas;
            #region Get cache
            DateTime expiry = optSer.ExpirationDate.Date;
            string cashKey = IvOnF.GetCashKey(optSer.UnderlyingAsset.Symbol, expiry, m_rescaleTime, m_tRemainMode);
            object globalObj = Context.LoadGlobalObject(cashKey, true);
            ivSigmas = globalObj as Dictionary<DateTime, double>;
            // PROD-3970 - 'Важный' объект
            if (ivSigmas == null)
            {
                var container = globalObj as NotClearableContainer;
                if ((container != null) && (container.Content != null))
                    ivSigmas = container.Content as Dictionary<DateTime, double>;
            }
            if (ivSigmas == null)
                ivSigmas = new Dictionary<DateTime, double>();
            #endregion Get cache

            ISecurity sec = optSer.UnderlyingAsset;
            int len = sec.Bars.Count;
            if (len <= 0)
                return false;

            FinInfo baseFinInfo = optSer.UnderlyingAsset.FinInfo;
            if (baseFinInfo.LastPrice == null)
            {
                string msg = "[IV ATM (all series)] (baseFinInfo.LastPrice == null)";
                m_context.Log(msg, MessageType.Warning, false);
                return false;
            }

            double futPx = baseFinInfo.LastPrice.Value;
            if (futPx <= Double.Epsilon)
                return false;

            NotAKnotCubicSpline spline = null;
            try
            {
                spline = IvOnF.PrepareExchangeSmileSpline(optSer, Double.MinValue, Double.MaxValue);
            }
            catch (ScriptException scriptEx)
            {
                m_context.Log(scriptEx.ToString(), MessageType.Error, false);
                return false;
            }
            catch (Exception ex)
            {
                m_context.Log(ex.ToString(), MessageType.Error, false);
                return false;
            }

            if (spline == null)
                return false;

            try
            {
                double sigma;
                if (spline.TryGetValue(futPx, out sigma) && (!Double.IsNaN(sigma)) && (sigma > 0))
                {
                    ivAtm = sigma;
                    DateTime lastBarDate = sec.Bars[len - 1].Date;

                    if (m_rescaleTime)
                    {
                        #region Зверская ветка по замене времени
                        DateTime expDate = optSer.ExpirationDate.Date + m_expiryTime;

                        // 1. Надо перевести волатильность в абсолютную цену
                        // с учетом плоского календарного времени применяемого РТС
                        double plainTimeAsYears;
                        {
                            double plainTimeAsDays;
                            TimeToExpiry.GetDt(expDate, now, TimeRemainMode.PlainCalendar,
                                false, out plainTimeAsDays, out plainTimeAsYears);
                        }

                        // 2. Вычисляем 'нормальное' время
                        double timeAsDays, timeAsYears;
                        TimeToExpiry.GetDt(expDate, now, m_tRemainMode,
                            false, out timeAsDays, out timeAsYears);
                        sigma = FinMath.RescaleIvToAnotherTime(plainTimeAsYears, ivAtm, timeAsYears);
                        if (DoubleUtil.IsPositive(sigma))
                        {
                            ivAtm = sigma;
                            // Это просто запись на диск. К успешности вычисления волы success отношения не имеет
                            bool success = IvOnF.TryWrite(m_context, true, true, 1, cashKey, ivSigmas,
                                lastBarDate, sigma);

                            // Теперь надо вычислить безразмерный наклон кодом в классе SmileImitation5
                            bool successSkew = IvOnF.TryCalcAndWriteSkews(m_context, spline, true, true, 1,
                                optSer.UnderlyingAsset.Symbol, expiry, futPx, lastBarDate,
                                m_tRemainMode, plainTimeAsYears, timeAsYears);

                            return true;
                        }
                        else
                        {
                            // Если перемасштабировать улыбку не получается придется эту точку проигнорировать
                            // Надо ли сделать соответствующую запись в логе???
                            return false;
                        }
                        #endregion Зверская ветка по замене времени
                    }
                    else
                    {
                        // Это просто запись на диск. К успешности вычисления волы success отношения не имеет
                        bool success = IvOnF.TryWrite(m_context, true, true, 1, cashKey, ivSigmas,
                            lastBarDate, sigma);
                        return true;
                    }
                }
            }
            catch (ScriptException scriptEx)
            {
                m_context.Log(scriptEx.ToString(), MessageType.Error, false);
            }
            catch (Exception ex)
            {
                m_context.Log(ex.ToString(), MessageType.Error, false);
                //throw;
            }

            return false;
        }
    }
}
