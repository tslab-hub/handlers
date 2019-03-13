using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Threading;
using TSLab.DataSource;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Load indicator from Global Cache
    /// \~russian Загрузить значение индикатора из Глобального Кеша
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsIndicators)]
    [HelperName("Load from Global Cache", Language = Constants.En)]
    [HelperName("Загрузить из Глобального Кеша", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION, Name = Constants.AnyOption)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Загрузить значение индикатора из Глобального Кеша")]
    [HelperDescription("Load indicator from Global Cache", Constants.En)]
    public class LoadFromGlobalCache : BaseContextBimodal<double>
    {
        ///// <summary>Сохранять значения в файл на диске для повторного использования между перезапусками ТСЛаб?</summary>
        //private bool m_saveToStorage = false;

        #region Parameters
        /// <summary>
        /// \~english Handler should repeat last known value to avoid further logic errors
        /// \~russian При true будет находить и использовать последнее известное значение
        /// </summary>
        [HelperName("Repeat last value", Constants.En)]
        [HelperName("Повтор значения", Constants.Ru)]
        [Description("При true будет находить и использовать последнее известное значение")]
        [HelperDescription("Handler should repeat last known value to avoid further logic errors", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool RepeatLastValue { get; set; }

        ///// <summary>
        ///// \~english Save to HDD to use indicator values across different TSLab sessions?
        ///// \~russian Сохранять значения в файл на диске для повторного использования между перезапусками ТСЛаб?
        ///// </summary>
        //[HelperName("Save to disk?", Constants.En)]
        //[HelperName("Сохранять на диск?", Constants.Ru)]
        //[Description("Сохранять значения в файл на диске для повторного использования между перезапусками ТСЛаб?")]
        //[HelperDescription("Save to HDD to use indicator values across different TSLab sessions?", Language = Constants.En)]
        //[HandlerParameter(NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        //public bool SaveToStorage
        //{
        //    get { return m_saveToStorage; }
        //    set { m_saveToStorage = value; }
        //}

        /// <summary>
        /// \~english Name of the agent that writes to Global Cache
        /// \~russian Имя агента, который пишет в Глобальный Кеш
        /// </summary>
        [HelperName("Agent name", Constants.En)]
        [HelperName("Имя агента", Constants.Ru)]
        [Description("Имя агента, который пишет в Глобальный Кеш")]
        [HelperDescription("Name of the agent that writes to Global Cache", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "")]
        public string AgentName { get; set; }

        /// <summary>
        /// \~english Unique indicator name to be used to store values in Global Cache
        /// \~russian Уникальное название индикатора для целей сохранения в Глобальный Кеш
        /// </summary>
        [HelperName("Values name", Constants.En)]
        [HelperName("Название значений", Constants.Ru)]
        [Description("Уникальное название индикатора для целей сохранения в Глобальный Кеш")]
        [HelperDescription("Unique indicator name to be used to store values in Global Cache", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "")]
        public string ValuesName { get; set; }

        /// <summary>
        /// \~english Override security (use this ticker instead of handler's input)
        /// \~russian Переопределить инструмент (использовать это значение вместо полученного на входе блока)
        /// </summary>
        [HelperName("Override security", Constants.En)]
        [HelperName("Переопределить инструмент", Constants.Ru)]
        [Description("Переопределить инструмент (использовать это значение вместо полученного на входе блока)")]
        [HelperDescription("Override security (use this ticker instead of handler's input)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "")]
        public string OverrideSymbol { get; set; }
        #endregion Parameters

        protected override bool IsValid(double val)
        {
            return !Double.IsNaN(val);
        }

        #region Потоковые обработчики
        /// <summary>
        /// Обработчик под тип входных данных OPTION (с потоковой обработкой)
        /// </summary>
        public IList<double> Execute(IOption opt)
        {
            if ((opt == null) || (opt.UnderlyingAsset == null))
                return Constants.EmptyListDouble;

            ISecurity sec = opt.UnderlyingAsset;
            var res = ExecuteStream(sec, sec.Symbol);
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES (с потоковой обработкой)
        /// </summary>
        public IList<double> Execute(IOptionSeries optSer)
        {
            if ((optSer == null) || (optSer.UnderlyingAsset == null))
                return Constants.EmptyListDouble;

            ISecurity sec = optSer.UnderlyingAsset;
            string expiry = optSer.ExpirationDate.ToString(IvOnF.DateFormat, CultureInfo.InvariantCulture);
            string symbolKey = String.Intern(String.Join("_", sec.Symbol, expiry));
            var res = ExecuteStream(sec, symbolKey);
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных SECURITY (с потоковой обработкой)
        /// </summary>
        public IList<double> Execute(ISecurity sec)
        {
            if (sec == null)
                return Constants.EmptyListDouble;

            var res = ExecuteStream(sec, sec.Symbol);
            return res;
        }

        /// <summary>
        /// Общая реализация потоковой обработки для всех типов входных аргументов
        /// </summary>
        private IList<double> ExecuteStream(ISecurity sec, string symbolKey)
        {
            if ((sec == null) || String.IsNullOrWhiteSpace(symbolKey))
                return Constants.EmptyListDouble;

            // Здесь используется имя агента-источника-данных из свойства AgentName.
            // Это _НЕ_ имя агента, внутри которого мы сейчас находимся!
            string tradeName = AgentName.Replace(Constants.HtmlDot, ".");
            if (String.IsNullOrWhiteSpace(ValuesName))
            {
                string msg = String.Format("[{0}:{1}] ValuesName is null or empty. Please, provide unique series name. ValuesName: '{2}'",
                    tradeName, GetType().Name, ValuesName ?? "NULL");
                m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptyListDouble;
            }

            string symbol = String.IsNullOrWhiteSpace(OverrideSymbol) ? symbolKey : OverrideSymbol;
            string cashKey = SaveToGlobalCache.GetGlobalCashKey(tradeName, ValuesName, symbol);
            if (String.IsNullOrWhiteSpace(cashKey))
            {
                string msg = String.Format("[{0}:{1}] cashKey is null or empty. cashKey: '{2}'",
                    tradeName, GetType().Name, cashKey ?? "NULL");
                m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptyListDouble;
            }

            // Проверяю наличие в Глобальном Кеше объекта с указанным именем
            object testObj = m_context.LoadGlobalObject(cashKey);
            var testDict = testObj as Dictionary<DateTime, double>;
            if (testDict == null)
            {
                var container = testObj as NotClearableContainer;
                if ((container != null) && (container.Content != null))
                    testDict = container.Content as Dictionary<DateTime, double>;
            }
            if ((testObj == null) || (testDict == null))
            {
                // Данного ключа в глобальном кеше нет? Тогда выход.
                // [{0}] There is no value in global cache. Probably, you have to start agent '{1}' for security '{2}' to collect values '{3}'.
                string msg = RM.GetStringFormat("OptHandlerMsg.LoadFromGlobalCache.CacheNotFound", GetType().Name, tradeName, symbol, ValuesName);
                if (m_context.Runtime.IsAgentMode)
                    throw new ScriptException(msg); // PROD-4624 - Андрей велит кидать исключение.

                // А если в режиме лаборатории, тогда только жалуемся и продолжаем.
                m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptyListDouble;
            }

            // По факту передаю управление в метод CommonExecute
            IList<double> updatedValues = CommonStreamExecute(m_variableId + "_" + cashKey, cashKey,
                sec, RepeatLastValue, true, true, EmptyArrays.Object);

            //if (basePrices.Count > 0)
            //{
            //    double px = basePrices[basePrices.Count - 1];
            //    double displayValue = FixedValue.ConvertToDisplayUnits(m_valueMode, px);
            //    m_displayPrice.Value = displayValue;
            //}

            return new ReadOnlyCollection<double>(updatedValues);
        }
        #endregion Потоковые обработчики

        #region Побарные обработчики
        /// <summary>
        /// Обработчик под тип входных данных OPTION (с побарным вызовом)
        /// </summary>
        public double Execute(IOption opt, int barNum)
        {
            if ((opt == null) || (opt.UnderlyingAsset == null))
                return Double.NaN; // Здесь намеренно возвращаю Double.NaN?

            ISecurity sec = opt.UnderlyingAsset;
            var res = ExecuteWithBarNumber(sec, sec.Symbol, barNum);
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES (с побарным вызовом)
        /// </summary>
        public double Execute(IOptionSeries optSer, int barNum)
        {
            if ((optSer == null) || (optSer.UnderlyingAsset == null))
                return Double.NaN; // Здесь намеренно возвращаю Double.NaN?

            ISecurity sec = optSer.UnderlyingAsset;
            string expiry = optSer.ExpirationDate.ToString(IvOnF.DateFormat, CultureInfo.InvariantCulture);
            string symbolKey = String.Intern(String.Join("_", sec.Symbol, expiry));
            var res = ExecuteWithBarNumber(sec, symbolKey, barNum);
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных SECURITY (с побарным вызовом)
        /// </summary>
        public double Execute(ISecurity sec, int barNum)
        {
            if (sec == null)
                return Double.NaN; // В данном случае намеренно возвращаю Double.NaN

            var res = ExecuteWithBarNumber(sec, sec.Symbol, barNum);
            return res;
        }

        /// <summary>
        /// Общая реализация побарной обработки для всех типов входных аргументов
        /// </summary>
        private double ExecuteWithBarNumber(ISecurity sec, string symbolKey, int barNum)
        {
            if ((sec == null) || String.IsNullOrWhiteSpace(symbolKey))
                return Double.NaN; // В данном случае намеренно возвращаю Double.NaN

            int len = m_context.BarsCount;
            if (len <= 0)
                return Double.NaN; // В данном случае намеренно возвращаю Double.NaN

            if (len <= barNum)
            {
                string msg = String.Format("[{0}:{1}] (BarsCount <= barNum)! BarsCount:{2}; barNum:{3}",
                    m_context.Runtime.TradeName, GetType().Name, m_context.BarsCount, barNum);
                m_context.Log(msg, MessageType.Warning, true);
                barNum = len - 1;
            }

            // Здесь используется имя агента-источника-данных из свойства AgentName.
            // Это _НЕ_ имя агента, внутри которого мы сейчас находимся!
            string tradeName = AgentName.Replace(Constants.HtmlDot, ".");
            if (String.IsNullOrWhiteSpace(ValuesName))
            {
                string msg = String.Format("[{0}:{1}] ValuesName is null or empty. Please, provide unique series name. ValuesName: '{2}'",
                    tradeName, GetType().Name, ValuesName ?? "NULL");
                m_context.Log(msg, MessageType.Warning, true);
                return Double.NaN; // В данном случае намеренно возвращаю Double.NaN
            }

            string symbol = String.IsNullOrWhiteSpace(OverrideSymbol) ? symbolKey : OverrideSymbol;
            string cashKey = SaveToGlobalCache.GetGlobalCashKey(tradeName, ValuesName, symbol);
            if (String.IsNullOrWhiteSpace(cashKey))
            {
                string msg = String.Format("[{0}:{1}] cashKey is null or empty. cashKey: '{2}'",
                    tradeName, GetType().Name, cashKey ?? "NULL");
                m_context.Log(msg, MessageType.Warning, true);
                return Double.NaN; // В данном случае намеренно возвращаю Double.NaN
            }

            DateTime now = sec.Bars[barNum].Date;
            double updatedIndicValue = CommonExecute(cashKey, now, RepeatLastValue, true, true, barNum, EmptyArrays.Object);

            //// [2017-06-27] Отключаю вывод отладочных сообщений в лог агента.
            //if (barNum >= 0.9 * len)
            //{
            //    string msg = String.Format("[{0}:{1}] barNum:{2}; updatedIndicValue:{3}; now:{4}",
            //        tradeName, GetType().Name, barNum, updatedIndicValue, now.ToString("dd-MM-yyyy HH:mm:ss.fff"));
            //    m_context.Log(msg, MessageType.Info, false);
            //}

            //// Просто заполнение свойства для отображения на UI
            //int barsCount = ContextBarsCount;
            //if (barNum >= barsCount - 1)
            //{
            //    double displayValue = FixedValue.ConvertToDisplayUnits(m_valueMode, risk);
            //    m_displayRisk.Value = displayValue;
            //}

            return updatedIndicValue;
        }
        #endregion Побарные обработчики

        protected override bool TryCalculate(Dictionary<DateTime, double> history, DateTime now, int barNum, object[] args, out double val)
        {
            double prevIv = Double.NaN;
            double iv;
            if (history.TryGetValue(now, out iv) && IsValid(iv))
            {
                // все просто: мы сразу нашли значение для указанного момента времени now
                prevIv = iv;
                val = iv;
                return true;
            }
            else
            {
                if (RepeatLastValue)
                {
                    if (!Double.IsNaN(prevIv))
                    {
                        // Эта ветка никогда не будет срабатывать...
                        iv = prevIv;
                    }
                    else
                    {
                        iv = Constants.NaN;
                        //if (j == 0)
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
                                    lock (history)
                                    {
                                        foreach (var kvp in history)
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
                                        string msg = String.Format("[{0}.TryCalculate] {1} when iterate through 'history'. Message: {2}\r\n\r\n{3}",
                                            GetType().Name, invOpEx.GetType().FullName, invOpEx.Message, invOpEx);
                                        m_context.Log(msg, MessageType.Warning, true);
                                        throw;
                                    }
                                }
                            }

                            if ((foundKey.Year > 1) && IsValid(tmp))
                            {
                                iv = tmp;
                                prevIv = iv;
                            }
                            #endregion Отдельно обрабатываю нулевой бар
                        }
                    }

                    prevIv = iv;
                    val = iv;
                    return true;
                }
                else
                {
                    val = Double.NaN;
                    return false;
                } // End if (m_repeatLastValue)
            } // End if (history.TryGetValue(now, out iv) && IsValid(iv))

            //val = Double.NaN;
            //return false;
        }
    }
}
