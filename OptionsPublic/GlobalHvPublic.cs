using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

using TSLab.DataSource;
using TSLab.Script.Handlers.Options;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.OptionsPublic
{
    /// <summary>
    /// \~english Precalculated HV from global cache
    /// \~russian Брать готовый HV из глобального кеша (чтобы не путаться с 'пишущей' версией)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPublic)]
    [HelperName("Global HV", Language = Constants.En)]
    [HelperName("Global HV", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION, Name = Constants.AnyOption)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Брать готовый HV из глобального кеша (чтобы не путаться с 'пишущей' версией)")]
    [HelperDescription("This block takes precalculated HV from global cache.", Constants.En)]
#if !DEBUG
    // Этот атрибут УБИРАЕТ блок из списка доступных в Редакторе Скриптов.
    // В своих блоках можете просто удалить его вместе с директивами условной компилляции.
    [HandlerInvisible]
#endif
    public class GlobalHvPublic : BaseContextHandler, IStreamHandler
    {
        private const string DefaultPeriod = "810";
        private const string DefaultMult = "452";
        private const string DefaultTimeframeSec = "60";

        private bool m_useAllData;
        private bool m_repeatLastHv;
        private int m_period = Int32.Parse(DefaultPeriod);
        private int m_timeframe = Int32.Parse(DefaultTimeframeSec);
        private double m_annualizingMultiplier = Double.Parse(DefaultMult);

        #region Parameters
        /// <summary>
        /// \~english Should handler use all data including overnight gaps?
        /// \~russian При true будет учитывать все данные, включая ночные гепы
        /// </summary>
        [HelperName("Use All Data", Constants.En)]
        [HelperName("Все данные", Constants.Ru)]
        [Description("При true будет учитывать все данные, включая ночные гепы")]
        [HelperDescription("Should handler use all data including overnight gaps?", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "false", Name = "Use All Data")]
        public bool UseAllData
        {
            get { return m_useAllData; }
            set { m_useAllData = value; }
        }

        /// <summary>
        /// \~english Calculation period
        /// \~russian Период расчета исторической волатильности
        /// </summary>
        [HelperName("Period", Constants.En)]
        [HelperName("Период", Constants.Ru)]
        [Description("Период расчета исторической волатильности")]
        [HelperDescription("Calculation period", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = DefaultPeriod, Min = "2", Max = "10000000", Step = "1", EditorMin = "2")]
        public int Period
        {
            get { return m_period; }
            set { m_period = Math.Max(2, value); }
        }

        /// <summary>
        /// \~english Timeframe (seconds)
        /// \~russian Таймфрейм (секунды)
        /// </summary>
        [HelperName("Timeframe", Constants.En)]
        [HelperName("Таймфрейм", Constants.Ru)]
        [Description("Таймфрейм (секунды)")]
        [HelperDescription("Timeframe (seconds)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = DefaultTimeframeSec, Min = "1", Max = "10000000", Step = "1")]
        public int Timeframe
        {
            get { return m_timeframe; }
            set { m_timeframe = Math.Max(1, value); }
        }

        /// <summary>
        /// \~english Multiplier to convert volatility to annualized value
        /// \~russian Множитель для перевода волатильности в годовое исчисление
        /// </summary>
        [HelperName("Annualizing Multiplier", Constants.En)]
        [HelperName("Масштабный множитель", Constants.Ru)]
        [Description("Множитель для перевода волатильности в годовое исчисление")]
        [HelperDescription("Multiplier to convert volatility to annualized value", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = DefaultMult, Min = "0", Max = "10000000", Step = "1", Name = "Annualizing Multiplier")]
        public double AnnualizingMultiplier
        {
            get { return m_annualizingMultiplier; }
            set
            {
                if (value > 0)
                    m_annualizingMultiplier = value;
            }
        }

        /// <summary>
        /// \~english When true it will find and repeat last known value in case when current value is unavailable
        /// \~russian При true будет находить и использовать последнее известное значение
        /// </summary>
        [HelperName("Repeat Last HV", Constants.En)]
        [HelperName("Повтор последней волатильности", Constants.Ru)]
        [Description("При true будет находить и использовать последнее известное значение")]
        [HelperDescription("When true it will find and repeat last known value in case when current value is unavailable", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "false", Name = "Repeat Last HV")]
        public bool RepeatLastHv
        {
            get { return m_repeatLastHv; }
            set { m_repeatLastHv = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION, чтобы подключаться к источнику-БА
        /// </summary>
        public IList<double> Execute(IOption opt)
        {
            if (opt == null)
            {
                // [{0}] Empty input (option is NULL).
                string msg = RM.GetStringFormat("OptHandlerMsg.OptionIsNull", GetType().Name);
                m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptyListDouble;
            }

            return Execute(opt.UnderlyingAsset);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-БА
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

            return Execute(optSer.UnderlyingAsset);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.SECURITY, чтобы подключаться к источнику-БА
        /// </summary>
        public IList<double> Execute(ISecurity sec)
        {
            if (sec == null)
            {
                // [{0}] Empty input (security is NULL).
                string msg = RM.GetStringFormat("OptHandlerMsg.SecurityIsNull", GetType().Name);
                m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptyListDouble;
            }

            Dictionary<DateTime, double> hvSigmas = null;
            int barLengthInSeconds = m_timeframe;
            string cashKey = HV.GetGlobalCashKey(sec.Symbol, false, m_useAllData, barLengthInSeconds, m_annualizingMultiplier, m_period);
            try
            {
                object globalObj = Context.LoadGlobalObject(cashKey, true);
                hvSigmas = globalObj as Dictionary<DateTime, double>;
                // 'Важный' объект
                if (hvSigmas == null)
                {
                    var container = globalObj as NotClearableContainer;
                    if ((container != null) && (container.Content != null))
                        hvSigmas = container.Content as Dictionary<DateTime, double>;
                }
            }
            catch (NotSupportedException nse)
            {
                string fName = "", path = "";
                if (nse.Data["fName"] != null)
                    fName = nse.Data["fName"].ToString();
                if (nse.Data["path"] != null)
                    path = nse.Data["path"].ToString();
                string msg = String.Format("[{0}.PrepareData] {1} when loading 'hvSigmas' from global cache. cashKey: {2}; Message: {3}\r\n\r\nfName: {4}; path: {5}\r\n\r\n{6}",
                    GetType().Name, nse.GetType().FullName, cashKey, nse.Message, fName, path, nse);
                m_context.Log(msg, MessageType.Warning, true);
            }
            catch (Exception ex)
            {
                string msg = String.Format("[{0}.PrepareData] {1} when loading 'hvSigmas' from global cache. cashKey: {2}; Message: {3}\r\n\r\n{4}",
                    GetType().Name, ex.GetType().FullName, cashKey, ex.Message, ex);
                m_context.Log(msg, MessageType.Warning, true);
            }

            if (hvSigmas == null)
            {
                // Данного ключа в глобальном кеше нет? Тогда выход.
                // [{0}.PrepareData] There is no HV in global cache. Probably, you have to start agent 'HV (ALL)' for security '{1}'.
                string msg = RM.GetStringFormat("OptHandlerMsg.GlobalHv.CacheNotFound", GetType().Name, sec);
                if (m_context.Runtime.IsAgentMode)
                    throw new ScriptException(msg);

                // А если в режиме лаборатории, тогда только жалуемся и продолжаем.
                m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptyListDouble;
            }

            List<double> res = new List<double>();

            int len = sec.Bars.Count;
            if (len <= 0)
                return res;

            int oldResLen = res.Count;
            double prevHv = Double.NaN;
            for (int j = oldResLen; j < len; j++)
            {
                DateTime now = sec.Bars[j].Date;
                double hv;
                if ((hvSigmas.TryGetValue(now, out hv)) && (!Double.IsNaN(hv)) && (hv > 0))
                {
                    prevHv = hv;
                    res.Add(hv);
                }
                else
                {
                    if (m_repeatLastHv)
                    {
                        if (!Double.IsNaN(prevHv))
                        {
                            hv = prevHv;
                        }
                        else
                        {
                            hv = Constants.NaN;
                            if (j == 0)
                            {
                                #region Отдельно обрабатываю нулевой бар
                                double tmp = Double.NaN;
                                DateTime foundKey = new DateTime(1, 1, 1);
                                // [2016-02-02] Когда история становится слишком длинной, это может вызывать проблемы
                                // при итерировании в foreach. Потому что другой поток может в этот момент добавить новую точку в коллекцию.
                                int repeat = 7;
                                while (repeat > 0)
                                {
                                    tmp = Double.NaN;
                                    foundKey = new DateTime(1, 1, 1);
                                    try
                                    {
                                        lock (hvSigmas)
                                        {
                                            foreach (var kvp in hvSigmas)
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
                                            string msg = String.Format("[{0}.Execute] {1} when iterate through 'hvSigmas'. cashKey: {2}; Message: {3}\r\n\r\n{4}",
                                                GetType().Name, invOpEx.GetType().FullName, cashKey, invOpEx.Message, invOpEx);
                                            m_context.Log(msg, MessageType.Warning, true);
                                            throw;
                                        }
                                    }
                                }

                                if ((foundKey.Year > 1) && (!Double.IsNaN(tmp)) && (tmp > 0))
                                {
                                    hv = tmp;
                                    prevHv = hv;
                                }
                                #endregion Отдельно обрабатываю нулевой бар
                            }
                        }
                        res.Add(hv);
                    }
                    else
                        res.Add(Constants.NaN);
                }
            }

            return res;
        }
    }
}
