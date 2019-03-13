using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~englisg Base class for handlers with stream AND bar processing (implements of BaseContextTemplate, IStreamHandler, IValuesHandlerWithNumber)
    /// \~russian Базовый класс для блоков с потоковой И побарной обработкой (реализует BaseContextTemplate, IStreamHandler, IValuesHandlerWithNumber)
    /// </summary>
    public abstract class BaseContextBimodal<T> : BaseContextTemplate<T>, IStreamHandler, IValuesHandlerWithNumber
        where T : struct
    {
        // ReSharper disable once VirtualMemberNeverOverriden.Global
        protected virtual IList<T> CommonStreamExecute(string resultsCashKey, string historyCashKey, ISecurity sec,
            bool repeatLastValue, bool printInMainLog, bool useGlobalCacheForHistory, params object[] args)
        {
            if (sec == null)
                return new T[0];

            int len = m_context.BarsCount;
            if (len <= 0)
                return new T[0];

            // 1. Извлекаю лист с результатами из ЛОКАЛЬНОГО кеша (настройка useGlobalCacheForHistory только для передачи в функцию CommonExecute)
            List<T> results = m_context.LoadObject(resultsCashKey) as List<T>;
            if (results == null)
            {
                results = new List<T>();
                m_context.StoreObject(resultsCashKey, results);
            }

            // 3. Выравниваю список, если он слишком длинный
            if (results.Count > len)
            {
                results.RemoveRange(len, results.Count - len);

                Debug.Assert(results.Count == len, "(results.Count != len). It is a mistake #1.");
            }
            else if (results.Count < len)
            {
                results.AddRange(new T[len - results.Count]);

                Debug.Assert(results.Count == len, "(results.Count != len). It is a mistake #2.");
            }

            // 5. Пошел главный цикл
            for (int barNum = 0; barNum < len; barNum++)
            {
                DateTime now = sec.Bars[barNum].Date;
                T t = CommonExecute(historyCashKey, now, repeatLastValue, printInMainLog, useGlobalCacheForHistory, barNum, args);
                results[barNum] = t;
            }

            return results;
        }
    }
}
