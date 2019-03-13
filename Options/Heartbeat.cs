using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;

using TSLab.DataSource;
using TSLab.Script.Options;
using TSLab.Utils.Profiling;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Recalculate script by timer
    /// \~russian Автоматический принудительный пересчет скрипта через заданный промежуток времени
    /// </summary>
    [HandlerCategory(HandlerCategories.Options)]
    [HelperName("Heartbeat", Language = Constants.En)]
    [HelperName("Метроном", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION | TemplateTypes.OPTION_SERIES)]
    [OutputType(TemplateTypes.DOUBLE)] // Номер вызова (может достигать ОГРОМНЫХ значений)
    [Description("Автоматический принудительный пересчет скрипта через заданный промежуток времени")]
    [HelperDescription("Recalculate script by timer", Constants.En)]
    public sealed class Heartbeat : BaseContextHandler, IStreamHandler
    {
        private long m_id;
        //private IThreadingTimerProfiler m_timer;
        private int m_delayMs = 30000;
        /// <summary>Пересчет скрипта будет происходить только во время торгов</summary>
        private bool m_onlyAtTradingSession = true;
        
        private static long s_counter = 0;

        /// <summary>Счетчики проблемных ситуаций, возникших с таймером [ PROD-5427 ]</summary>
        private static readonly ConcurrentDictionary<string, int> s_problemCounters = new ConcurrentDictionary<string, int>();

        public Heartbeat()
        {
            m_id = Interlocked.Increment(ref s_counter) - 1;
        }

        #region Parameters
        /// <summary>
        /// \~english Delay between calls (ms)
        /// \~russian Задержка между вызовами (мс)
        /// </summary>
        [HelperName("Delay", Constants.En)]
        [HelperName("Задержка", Constants.Ru)]
        [Description("Задержка между вызовами (мс)")]
        [HelperDescription("Delay between calls (ms)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "30000")]
        public int DelayMs
        {
            get { return m_delayMs; }
            set { m_delayMs = value; }
        }
        
        /// <summary>
        /// \~english If the flag is set, handler initiates execution only in agent mode at trading time
        ///           when data provider is connected to market and the instrument is actually traded.
        /// \~russian Если выставить этот флаг, блок работает только в режиме агента во время торгов,
        ///           когда подключен провайдер и использованный инструмент действительно торгуется.
        /// </summary>
        [HelperName("Trading session only", Constants.En)]
        [HelperName("Только в торговое время", Constants.Ru)]
        [Description("Если выставить этот флаг, блок работает только в режиме агента во время торгов, когда подключен провайдер и использованный инструмент действительно торгуется.")]
        [HelperDescription("If the flag is set, handler initiates execution only in agent mode at trading time when data provider is connected to market and the instrument is actually traded.", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "true")]
        public bool OnlyAtTradingSession
        {
            get { return m_onlyAtTradingSession; }
            set { m_onlyAtTradingSession = value; }
        }
        #endregion Parameters

        public IList<double> Execute(IOption opt)
        {
            IList<double> res = Execute(opt.UnderlyingAsset);
            return res;
        }

        public IList<double> Execute(IOptionSeries optSer)
        {
            IList<double> res = Execute(optSer.UnderlyingAsset);
            return res;
        }

        public IList<double> Execute(ISecurity sec)
        {
            //Context.Log(String.Format("[Heartbeat.Execute ( ID:{0} )] I'm checking timer settings.", m_id), MessageType.Warning, false);

            // PROD-5496 - В режиме оптимизации отключаюсь
            if (Context.IsOptimization)
                return Constants.EmptyListDouble;

            CallState timerState = null;
            string cashKey = VariableId + "_timerState";
            {
                object localObj = Context.LoadObject(cashKey, false);
                timerState = localObj as CallState;
                // PROD-3970 - 'Важный' объект
                if (timerState == null)
                {
                    var container = localObj as NotClearableContainer;
                    if ((container != null) && (container.Content != null))
                        timerState = container.Content as CallState;
                }
            }

            //m_timer = Context.LoadObject(VariableId + "m_timer") as IThreadingTimerProfiler;
            if (timerState == null)
            {
                string msg = String.Format("[Heartbeat.Execute ( ID:{0} )] Preparing new timer for agent '{1}'...",
                    m_id, Context.Runtime.TradeName);
                Context.Log(msg, MessageType.Info, false);

                var secInfo = new PositionsManager.SecInfo(sec.SecurityDescription);
                timerState = new CallState(m_id, Context, secInfo, m_onlyAtTradingSession);
                var timer = new ThreadingTimerProfiler(Recalculate, timerState, m_delayMs, Timeout.Infinite);
                // Обязательно дозаполняем ссылку на таймер
                timerState.Timer = timer;

                var container = new NotClearableContainer(timerState);
                Context.StoreObject(cashKey, container, false);
            }
            else if (timerState.Timer == null)
            {
                // PROD-5427 - Добавляю счетчик этого аварийного события и логгирую
                int problemCounter = 0;
                if (s_problemCounters.ContainsKey(Context.Runtime.TradeName))
                    problemCounter = s_problemCounters[Context.Runtime.TradeName];

                s_problemCounters[Context.Runtime.TradeName] = Interlocked.Increment(ref problemCounter);

                string msg = String.Format("[Heartbeat.Execute ( ID:{0} )] Timer is null in agent '{1}'. Problem counter: {2}",
                    m_id, Context.Runtime.TradeName, problemCounter);
                Context.Log(msg, MessageType.Warning, false);

                if (problemCounter > 3)
                {
                    // Если проблема систематически повторяется -- выбрасываю ассерт для дальнейшего анализа ситуации
                    Contract.Assert(timerState.Timer != null, msg);
                }
            }
            else
            {
                //Contract.Assert(timerState.Timer != null, "Почему вдруг (timerState.Timer==null) ??");

                // Если при изменении скрипта пересоздается агент, то контекст становится невалидным?
                if (Object.ReferenceEquals(Context, timerState.CallContext))
                {
                    // Если контекст совпадает, то обновляем режим работы...
                    timerState.OnlyAtTradingSession = m_onlyAtTradingSession;
                    // и перезапускаем таймер
                    try
                    {
                        timerState.Timer.Change(m_delayMs, Timeout.Infinite);
                        // PROD-5427 - При штатной работе блока обнуляю счетчик проблем
                        s_problemCounters[Context.Runtime.TradeName] = 0;
                    }
                    catch (ObjectDisposedException)
                    {
                        // Если таймер уже убит, то надо создать новый
                        timerState.Timer = null;
                        timerState = null;

                        string msg = String.Format("[Heartbeat.Execute ( ID:{0} )] Replacing DISPOSED timer for agent '{1}'...",
                            m_id, Context.Runtime.TradeName);
                        Context.Log(msg, MessageType.Warning, false);

                        // Создаём новый таймер. При этом используем НОВЫЙ m_id
                        var secInfo = new PositionsManager.SecInfo(sec.SecurityDescription);
                        timerState = new CallState(m_id, Context, secInfo, m_onlyAtTradingSession);
                        var timer = new ThreadingTimerProfiler(Recalculate, timerState, m_delayMs, Timeout.Infinite);
                        // Обязательно дозаполняем ссылку на таймер
                        timerState.Timer = timer;

                        var container = new NotClearableContainer(timerState);
                        Context.StoreObject(cashKey, container, false);
                    }
                }
                else
                {
                    // Если по какой-то причине изменился контекст, то создаём новый таймер...
                    timerState.Timer.Dispose();
                    timerState.Timer = null;
                    timerState = null;

                    string msg = String.Format("[Heartbeat.Execute ( ID:{0} )] Replacing timer for agent '{1}'...",
                        m_id, Context.Runtime.TradeName);
                    Context.Log(msg, MessageType.Warning, false);

                    // Создаём новый таймер. При этом используем НОВЫЙ m_id
                    var secInfo = new PositionsManager.SecInfo(sec.SecurityDescription);
                    timerState = new CallState(m_id, Context, secInfo, m_onlyAtTradingSession);
                    var timer = new ThreadingTimerProfiler(Recalculate, timerState, m_delayMs, Timeout.Infinite);
                    // Обязательно дозаполняем ссылку на таймер
                    timerState.Timer = timer;

                    var container = new NotClearableContainer(timerState);
                    Context.StoreObject(cashKey, container, false);
                }
            }

            int len = Context.BarsCount;
            double[] res = Context.GetArray<double>(len);
            if (len > 0)
                res[len - 1] = m_id;

            return res;
        }

        /// <summary>
        /// Метод должен принять на вход объект класса CallState и через его контекст сделать пересчет агента.
        /// </summary>
        /// <param name="state">объект класса CallState</param>
        private static void Recalculate(object state)
        {
            if ((state != null) && (state is CallState))
            {
                var callState = (CallState)state;
                IContext context = callState.CallContext;
                //context?.Log(String.Format("[Heartbeat.Recalculate ( ID:{0} )] I'll call Recalc now...", callState.CallId), MessageType.Warning, false);

                // PROD-5496 - В режиме оптимизации отключаюсь
                if (context.IsOptimization)
                    return;

                if (callState.OnlyAtTradingSession)
                {
                    // Проверяю ПОЛНУЮ готовность всех инструментов агента к совершению реальных торговых операций
                    ISecurity rtSec = null;
                    foreach (ISecurity sec in context.Runtime.Securities)
                    {
                        // Если хотя бы один инструмент не готов к торговле, пересчет игнорируется
                        if (!sec.IsPortfolioReady)
                            return; // Пересчет не буду вызывать!

                        var secDesc = sec.SecurityDescription;
                        if (secDesc.DSName.Equals(callState.SecInfo.DsName, StringComparison.InvariantCultureIgnoreCase) &&
                            secDesc.Name.Equals(callState.SecInfo.Name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            rtSec = sec;

                            // Проверка состояния инструмента. Полностью полагаюсь на то, что провайдеры корректно выставляют флаг IsTradingEnabled
                            if ((rtSec.FinInfo == null) || (!rtSec.FinInfo.IsTradingEnabled))
                                return; // Пересчет не буду вызывать!
                        }
                    }

                    // Если сюда пришли, значит всё хорошо и можно вызвать принудительный пересчет
                    context?.Recalc();
                }
                else
                    context?.Recalc();
            }
        }

        /// <summary>
        /// Класс хранит таймер, контекст вызова и идентификатор вызова на котором таймер БЫЛ СОЗДАН.
        /// </summary>
        private class CallState
        {
            public readonly long CallId;
            /// <summary>
            /// Контекст вызова кубика (всегда заполняется и не может быть null)
            /// </summary>
            public readonly IContext CallContext;
            /// <summary>
            /// Описание инструмента
            /// </summary>
            public readonly PositionsManager.SecInfo SecInfo;

            /// <summary>
            /// Заполнить таймер сразу через конструктор не получится, но это нужно обязательно сделать сразу после его инициализации.
            /// </summary>
            public IThreadingTimerProfiler Timer;

            /// <summary>
            /// Пересчет скрипта будет происходить только во время торгов?
            /// </summary>
            public bool OnlyAtTradingSession;

            public CallState(long id, IContext context, PositionsManager.SecInfo secInfo, bool onlyAtTradingSession)
            {
                Contract.Assert(context != null, "Почему вдруг (context==null) ??");
                Contract.Assert(secInfo != null, "Почему вдруг (secInfo==null) ??");

                CallId = id;
                CallContext = context;
                SecInfo = secInfo;
                OnlyAtTradingSession = onlyAtTradingSession;
            }
        }
    }
}
