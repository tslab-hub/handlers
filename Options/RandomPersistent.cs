using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Optimization;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Random numbers generator stores its history in Global cache
    /// \~russian Генератор случайных чисел с сохранением истории в глобальный кеш
    /// </summary>
#if !DEBUG
    [HandlerInvisible]
#endif
    [HandlerCategory(HandlerCategories.OptionsBugs)]
    [HelperName("Random Persistent", Language = Constants.En)]
    [HelperName("ГСЧ (сохраняемый)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION, Name = Constants.AnyOption)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Генератор случайных чисел с сохранением истории в глобальный кеш")]
    [HelperDescription("Random numbers generator stores its history in Global cache", Constants.En)]
    public class RandomPersistent : BaseContextHandler, IValuesHandlerWithNumber, IDisposable
    {
        private const double MinVal = 0.0, MaxVal = 100.0, Step = 1.0;

        private BoolOptimProperty m_blockTrading = new BoolOptimProperty(true, false);
        private readonly System.Random m_rnd = new System.Random((int)DateTime.Now.Ticks);
        private OptimProperty m_prevRnd = new OptimProperty(3.1415, false, MinVal, MaxVal, Step, 3);

        #region Parameters
        [Description("Rnd")]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "3.1415", IsCalculable = true)]
        public OptimProperty Rnd
        {
            get { return m_prevRnd; }
            set { m_prevRnd = value; }
        }

        /// <summary>
        /// Свойство только для демонстрации некорректной обработки этого атрибута
        /// </summary>
        [HandlerParameter(Name = "Block Trading", NotOptimized = false, IsVisibleInBlock = true, Default = "true")]
        [Description("Свойство только для демонстрации некорректной обработки этого атрибута")]
        public BoolOptimProperty BlockTrading
        {
            get { return m_blockTrading; }
            set { m_blockTrading = value; }
        }

        /// <summary>
        /// Использовать глобальный кеш?
        /// </summary>
        [HandlerParameter(Name = "Use Global Cache", NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        [Description("Использовать глобальный кеш?")]
        public bool UseGlobalCache { get; set; }

        /// <summary>
        /// Разрешить чтение/запись в глобальный кеш или только чтение?
        /// </summary>
        [HandlerParameter(Name = "Allow Global Read/Write", NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        [Description("Разрешить чтение/запись в глобальный кеш или только чтение?")]
        public bool AllowGlobalReadWrite { get; set; }

        /// <summary>
        /// Сохранять в глобальный кеш через каждые N записей
        /// </summary>
        [HandlerParameter(Name = "Global Save Period", NotOptimized = false, IsVisibleInBlock = true, Default = "1")]
        [Description("Сохранять в глобальный кеш через каждые N записей")]
        public int GlobalSavePeriod { get; set; }
        #endregion Parameters

        public double Execute(IOption opt, int barNumber)
        {
            return Execute(opt.UnderlyingAsset, barNumber);
        }

        public double Execute(IOptionSeries optSer, int barNumber)
        {
            return Execute(optSer.UnderlyingAsset, barNumber);
        }

        public double Execute(ISecurity sec, int barNumber)
        {
            Dictionary<DateTime, double> historyRnd;
            string cashKey = GetType().Name + "_historyRnd_" + VariableId;
            if (UseGlobalCache)
            {
                historyRnd = Context.LoadGlobalObject(cashKey, true) as Dictionary<DateTime, double>;
                if (historyRnd == null)
                {
                    historyRnd = new Dictionary<DateTime, double>();
                    Context.StoreGlobalObject(cashKey, historyRnd, true);
                }
            }
            else
            {
                historyRnd = Context.LoadObject(cashKey) as Dictionary<DateTime, double>;
                if (historyRnd == null)
                {
                    historyRnd = new Dictionary<DateTime, double>();
                    Context.StoreObject(cashKey, historyRnd);
                }
            }

            double res;
            DateTime now = sec.Bars[barNumber].Date;
            
            int barsCount = Context.BarsCount;
            if (!Context.IsLastBarUsed)
                barsCount--;

            // не надо менять свойство на каждой свече, это приводит к жутким тормозам, т.к. прокидывает измнения по всему UI
            if (barNumber >= barsCount - 1)
            {
                if ((!historyRnd.TryGetValue(now, out res)) || Double.IsNaN(res))
                {
                    res = MaxVal * m_rnd.NextDouble();
                    // Это вызов СВОЕГО приватного метода для отладки работы этого кода
                    bool success = TryWrite(Context, UseGlobalCache, AllowGlobalReadWrite, GlobalSavePeriod,
                        cashKey, historyRnd, now, res);
                }

                m_prevRnd.Value = res;
                var msg = String.Format("[DEBUG:{0}] RND[{1}]:{2};   VariableId:{3}",
                    GetType().Name, barNumber, m_prevRnd.Value, VariableId);

                Context.Log(msg, MessageType.Warning, true);
            }
            else
            {
                if ((!historyRnd.TryGetValue(now, out res)) || Double.IsNaN(res))
                {
                    res = Double.NaN;
                    // Наверное, нет смысла пихать в СЛОВАРЬ NaN только чтобы забить дыры
                    //bool success = TryWrite(UseGlobalCache, AllowGlobalReadWrite, historyRnd, now, res);
                }
            }

            return res;
        }

        /// <summary>
        /// Обновление исторической серии, которая потенциально может быть сохранена в глобальный кеш.
        /// </summary>
        /// <returns>
        /// true, если новое значение было фактически помещено в серию;
        /// false возникает, если запись в глобальный кеш блокирована флагом allowGlobalWrite
        /// </returns>
        private static bool TryWrite(IContext context, bool useGlobal, bool allowGlobalWrite, int savePeriod,
            string cashKey, Dictionary<DateTime, double> series, DateTime now, double val)
        {
            if (useGlobal)
            {
                if (allowGlobalWrite)
                {
                    series[now] = val;
                    if (series.Count % savePeriod == 0)
                        context.StoreGlobalObject(cashKey, series, true);
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

        public void Dispose()
        {
            if (Context != null) // если скрипт не исполнялся - контекст будет null, подписки на события тоже не будет
                Context.Log(String.Format("[DEBUG:{0}] {1} disposed", GetType().Name, VariableId));
        }
    }
}
