using System;
using System.Collections.Generic;

using TSLab.DataSource;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~englisg Base class for handlers with context and unique name (implements of IContextUses, INeedVariableId)
    /// \~russian Базовый класс для блоков с контекстом и уникальным именем (реализует IContextUses, INeedVariableId)
    /// </summary>
    public abstract class BaseContextHandler : IContextUses, INeedVariableId
    {
        /// <summary>dd-MM-yyyy HH:mm:ss.fff</summary>
        public static readonly string DateTimeFormatWithMs = "dd-MM-yyyy HH:mm:ss.fff";

        // ReSharper disable once InconsistentNaming
        protected IContext m_context;
        // ReSharper disable once InconsistentNaming
        protected string m_variableId;

        public IContext Context
        {
            get { return m_context; }
            set { m_context = value; }
        }

        public virtual string VariableId
        {
            get { return m_variableId; }
            set { m_variableId = value; }
        }

        /// <summary>
        /// Эквивалент context.BarsCount. Только ещё проверяется флаг context.IsLastBarUsed
        /// и в случае его наличия barsCount уменьшается на 1.
        /// </summary>
        protected int ContextBarsCount
        {
            get
            {
                int barsCount = m_context.BarsCount;
                if (!m_context.IsLastBarUsed)
                    barsCount--;
                return barsCount;
            }
        }

        #region Parameters
        #endregion Parameters

        /// <summary>
        /// Записываем в локальный кеш признак и дату того,
        /// что данный блок уже работал правильно (например, был полностью готов к торговле)
        /// </summary>
        /// <param name="now">время выставления состояния</param>
        /// <param name="state">состояние</param>
        protected virtual void SetHandlerInitialized(DateTime now, bool state = true)
        {
            string key = "Initialized_" + m_variableId;
            var tuple = Tuple.Create(now, state);
            var container = new NotClearableContainer<Tuple<DateTime, bool>>(tuple);
            // Специально без записи на диск, чтобы после перезапуска ТСЛаб объект был пуст
            m_context.StoreObject(key, container, false);
        }

        /// <summary>
        /// Проверяем был ли блок уже проинициализирован именно сегодня?
        /// </summary>
        /// <param name="now">текущее время</param>
        /// <returns></returns>
        protected virtual bool HandlerInitializedToday(DateTime now)
        {
            DateTime stateDate;
            if (!HandlerInitialized(now, out stateDate))
                return false;

            bool res = (now.Date == stateDate.Date);
            return res;
        }

        /// <summary>
        /// Проверяем был ли блок уже проинициализирован?
        /// </summary>
        /// <param name="now">текущее время</param>
        /// <param name="stateDate">дата когда он был проинициализирован</param>
        /// <returns></returns>
        protected virtual bool HandlerInitialized(DateTime now, out DateTime stateDate)
        {
            stateDate = now.AddYears(10);

            string key = "Initialized_" + m_variableId;
            var container = m_context.LoadObject(key, false) as NotClearableContainer<Tuple<DateTime, bool>>;
            if ((container == null) || (container.Content == null))
                return false;

            var tuple = container.Content;
            if (!tuple.Item2)
                return false;

            stateDate = tuple.Item1;
            if (stateDate < now)
                return true;

            return false;
        }

        /// <summary>
        /// Загрузить из кеша (локального ИЛИ глобального) серию чисел во времени
        /// </summary>
        /// <param name="useGlobalCache">флаг глобальный или локальный кеш</param>
        /// <param name="cashKey">ключ кеша</param>
        /// <returns>серия из кеша, либо новый объект (который уже помещен в этот кеш)</returns>
        protected virtual Dictionary<DateTime, double> LoadOrCreateHistoryDict(bool useGlobalCache, string cashKey)
        {
            var res = LoadOrCreateHistoryDict(Context, useGlobalCache, cashKey);
            return res;
        }

        /// <summary>
        /// Загрузить из кеша (локального ИЛИ глобального) серию чисел во времени
        /// </summary>
        /// <param name="context">контекст кубика</param>
        /// <param name="useGlobalCache">флаг глобальный или локальный кеш</param>
        /// <param name="cashKey">ключ кеша</param>
        /// <returns>серия из кеша, либо новый объект (который уже помещен в этот кеш)</returns>
        public static Dictionary<DateTime, double> LoadOrCreateHistoryDict(IContext context, bool useGlobalCache, string cashKey)
        {
            Dictionary<DateTime, double> history;
            if (useGlobalCache)
            {
                object globalObj = context.LoadGlobalObject(cashKey, true);
                history = globalObj as Dictionary<DateTime, double>;
                // PROD-3970 - 'Важный' объект
                if (history == null)
                {
                    var container = globalObj as NotClearableContainer;
                    if ((container != null) && (container.Content != null))
                        history = container.Content as Dictionary<DateTime, double>;
                }
                if (history == null)
                {
                    history = new Dictionary<DateTime, double>();
                    var container = new NotClearableContainer(history);
                    context.StoreGlobalObject(cashKey, container, true);
                }
            }
            else
            {
                object locObj = context.LoadObject(cashKey);
                history = locObj as Dictionary<DateTime, double>;
                // PROD-3970 - 'Важный' объект
                if (history == null)
                {
                    var container = locObj as NotClearableContainer;
                    if ((container != null) && (container.Content != null))
                        history = container.Content as Dictionary<DateTime, double>;
                }
                if (history == null)
                {
                    history = new Dictionary<DateTime, double>();
                    var container = new NotClearableContainer(history);
                    context.StoreObject(cashKey, container);
                }
            }

            return history;
        }
    }
}
