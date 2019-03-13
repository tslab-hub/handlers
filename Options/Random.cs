using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Optimization;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Random numbers generator
    /// \~russian Генератор случайных чисел
    /// </summary>
#if !DEBUG
    [HandlerInvisible]
#endif
    [HandlerCategory(HandlerCategories.OptionsBugs)]
    [HelperName("Random", Language = Constants.En)]
    [HelperName("ГСЧ", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION, Name = Constants.AnyOption)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Генератор случайных чисел")]
    [HelperDescription("Random numbers generator", Constants.En)]
    public class Random : BaseContextHandler, IValuesHandlerWithNumber, ICustomListValues, IDisposable
    {
        private const double MinVal = 0.0, MaxVal = 100.0, Step = 1.0;

        private BoolOptimProperty m_blockTrading = new BoolOptimProperty(true, false);
        private readonly System.Random m_rnd = new System.Random((int)DateTime.Now.Ticks);
        private OptimProperty m_prevRnd = new OptimProperty(3.1415, false, MinVal, MaxVal, Step, 3);

        private ExpiryMode m_expiryMode = ExpiryMode.FixedExpiry;

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
        public BoolOptimProperty BlockTrading
        {
            get { return m_blockTrading; }
            set { m_blockTrading = value; }
        }

        /// <summary>
        /// Свойство только для демонстрации некорректного сохранения перечислений
        /// </summary>
        [Category("Mode")]
        [Description("Алгоритм определения даты экспирации")]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "FixedExpiry")]
        public ExpiryMode ExpirationMode
        {
            get { return m_expiryMode; }
            set { m_expiryMode = value; }
        }

        /// <summary>
        /// Свойство только для демонстрации некорректной обработки строковых "оптимизационных" параметров
        /// </summary>
        [Description("Свойство только для демонстрации некорректной обработки строковых 'оптимизационных' параметров")]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Change Me!")]
        public string TextString { get; set; }

        /// <summary>
        /// Свойство даёт возможность вывести в лог все типы сообщений
        /// </summary>
        [HandlerParameter(Name = "All Log Types", NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool AllLogTypes { get; set; }
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
            double res = MaxVal * m_rnd.NextDouble();

            // не надо менять свойство на каждой свече, это приводит к жутким тормозам, т.к. прокидывает измнения по всему UI
            if (barNumber >= Context.BarsCount - 1)
            {
                m_prevRnd.Value = res;
                var msg = String.Format("[DEBUG:{0}] RND[{1}]:{2};   VariableId:{3}",
                    GetType().Name, barNumber, m_prevRnd.Value, VariableId);

                if (AllLogTypes)
                {
                    foreach (MessageType notifyType in Enum.GetValues(typeof(MessageType)))
                    {
                        Context.Log(msg, notifyType, true);
                    }
                }
                else
                {
                    Context.Log(msg, MessageType.Warning, true);
                }
            }

            return res;
        }

        public IEnumerable<string> GetValuesForParameter(string paramName)
        {
            if (paramName.Equals(nameof(TextString), StringComparison.InvariantCultureIgnoreCase))
            {
                return new[] { "/*", "Change Me!", "*/" };
            }
            else if (paramName.Equals(nameof(ExpirationMode), StringComparison.InvariantCultureIgnoreCase))
            {
                return new[] { ExpiryMode.FixedExpiry.ToString() };
            }
            else
                throw new ArgumentException("Parameter '" + paramName + "' is not supported.", "paramName");
        }

        //public IEnumerable<ExpiryMode> GetValuesForParameter(string paramName)
        //{
        //    if (paramName.Equals("ExpirationMode", StringComparison.InvariantCultureIgnoreCase))
        //    {
        //        return new[] { ExpiryMode.FixedExpiry, ExpiryMode.FirstExpiry, ExpiryMode.LastExpiry, ExpiryMode.ExpiryByNumber };
        //    }
        //    else
        //        throw new ArgumentException("Parameter '" + paramName + "' is not supported.", "paramName");
        //}

        public void Dispose()
        {
            if (Context != null) // если скрипт не исполнялся - контекст будет null, подписки на события тоже не будет
                Context.Log(String.Format("[DEBUG:{0}] {1} disposed", GetType().Name, VariableId));
        }
    }
}
