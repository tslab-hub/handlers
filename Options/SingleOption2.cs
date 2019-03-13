using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Get single option from its option series
    /// \~russian Извлечь из серии один опцион с заданным страйком
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Single Option", Language = Constants.En)]
    [HelperName("Один опцион", Language = Constants.Ru)]
    [InputsCount(1, 2)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Strike)]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Извлечь из серии один опцион с заданным страйком")]
    [HelperDescription("Get single option from its option series", Constants.En)]
    public class SingleOption2 : IContextUses, IValuesHandlerWithNumber, ISecurityReturns
    {
        private const string DefaultStrike = "120000";

        private StrikeType m_optionType = StrikeType.Call;
        private double m_fixedStrike = Double.Parse(DefaultStrike);
        private StrikeSelectionMode m_selectionMode = StrikeSelectionMode.FixedStrike;

        public IContext Context { get; set; }

        #region Parameters
        /// <summary>
        /// \~english Option strike
        /// \~russian Страйк опциона
        /// </summary>
        [HelperName("Strike", Constants.En)]
        [HelperName("Страйк", Constants.Ru)]
        [Description("Страйк опциона")]
        [HelperDescription("Option strike", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultStrike)]
        public double FixedStrike
        {
            get { return m_fixedStrike; }
            set { m_fixedStrike = value; }
        }

        /// <summary>
        /// \~english Option type (parameter Any is not recommended)
        /// \~russian Тип опционов (использование типа Any может привести к неожиданному поведению)
        /// </summary>
        [HelperName("Option Type", Constants.En)]
        [HelperName("Вид опционов", Constants.Ru)]
        [Description("Тип опционов (использование типа Any может привести к неожиданному поведению)")]
        [HelperDescription("Option type (parameter Any is not recommended)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Call")]
        public StrikeType OptionType
        {
            get { return m_optionType; }
            set { m_optionType = value; }
        }

        /// <summary>
        /// \~english Search algorythm
        /// \~russian Алгоритм поиска
        /// </summary>
        [HelperName("Search Algo", Constants.En)]
        [HelperName("Алгоритм поиска", Constants.Ru)]
        [Description("Алгоритм поиска")]
        [HelperDescription("Search algorythm", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "FixedStrike")]
        public StrikeSelectionMode SelectionMode
        {
            get { return m_selectionMode; }
            set { m_selectionMode = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Обработчик для одиночного аргумента -- опционной серии
        /// </summary>
        public ISecurity Execute(IOptionSeries optSer, int barNum)
        {
            if ((optSer == null) || (optSer.UnderlyingAsset == null))
                return null;

            int barsCount = Context.BarsCount;
            if (!Context.IsLastBarUsed)
                barsCount--;

            double actualStrike = m_fixedStrike;

            // TODO: При работе с фиксированным страйком можно и кеширование сделать.
            // А вот для второго метода кеширование не подойдет (страйк может измениться на любом индексе)
            IOptionStrikePair pair = SingleOption.GetStrikePair(optSer, m_selectionMode, actualStrike);
            if (pair == null)
            {
                if (barNum < barsCount - 1)
                {
                    // Чтобы не спамить зря на старых барах молча возвращаю null
                    return null;
                }
                else
                {
                    // Данного страйка нет в списке? Тогда выход.
                    string expiryDate = optSer.ExpirationDate.Date.ToString(IvOnF.DateFormat, CultureInfo.InvariantCulture);
                    // [{0}.Execute] There is no strike '{1}' in the option series '{2} ~ {3}'. Search mode: '{4}'
                    string msg = RM.GetStringFormat("OptHandlerMsg.SingleOption.OptionNotFound",
                        GetType().Name, actualStrike, optSer.UnderlyingAsset, expiryDate, m_selectionMode);
                    if (Context.Runtime.IsAgentMode /* && (!m_ignoreCacheError) */)
                        throw new ScriptException(msg); // PROD-5501 - Кидаем исключение.

                    //bool isExpired = true;
                    //// Поскольку в этом блоке настройки m_ignoreCacheError нет, то здесь всегда IsAgentMode == false!
                    //if (Context.Runtime.IsAgentMode)
                    //{
                    //    int amount = optSer.UnderlyingAsset.Bars.Count;
                    //    DateTime today = (amount > 0) ? optSer.UnderlyingAsset.Bars[amount - 1].Date : new DateTime();
                    //    isExpired = optSer.ExpirationDate.Date.AddDays(1) < today.Date;
                    //}
                    // А если в режиме лаборатории, тогда только жалуемся в Главный Лог и продолжаем.
                    Context.Log(msg, MessageType.Warning, true /* !isExpired */);
                    return null;
                }
            }

            ISecurity res;
            if (m_optionType == StrikeType.Put)
                res = pair.Put.Security;
            else if (m_optionType == StrikeType.Call)
                res = pair.Call.Security;
            else
                throw new NotSupportedException("Не могу найти опцион вида: " + m_optionType);

            return res;
        }

        /// <summary>
        /// Обработчик для двух аргументов -- опционной серии и страйка
        /// </summary>
        public ISecurity Execute(IOptionSeries optSer, double actualStrike, int barNum)
        {
            if ((optSer == null) || (optSer.UnderlyingAsset == null))
                return null;

            int barsCount = Context.BarsCount;
            if (!Context.IsLastBarUsed)
                barsCount--;

            // Для второго метода кеширование не подойдет (страйк может измениться на любом индексе)
            IOptionStrikePair pair = SingleOption.GetStrikePair(optSer, m_selectionMode, actualStrike);
            if (pair == null)
            {
                if (barNum < barsCount - 1)
                {
                    // Чтобы не спамить зря на старых барах молча возвращаю null
                    return null;
                }
                else
                {
                    // Данного страйка нет в списке? Тогда выход.
                    string expiryDate = optSer.ExpirationDate.Date.ToString(IvOnF.DateFormat, CultureInfo.InvariantCulture);
                    // [{0}.Execute] There is no strike '{1}' in the option series '{2} ~ {3}'. Search mode: '{4}'
                    string msg = RM.GetStringFormat("OptHandlerMsg.SingleOption.OptionNotFound",
                        GetType().Name, actualStrike, optSer.UnderlyingAsset, expiryDate, m_selectionMode);
                    if (Context.Runtime.IsAgentMode /* && (!m_ignoreCacheError) */)
                        throw new ScriptException(msg); // PROD-5501 - Кидаем исключение.

                    //bool isExpired = true;
                    //// Поскольку в этом блоке настройки m_ignoreCacheError нет, то здесь всегда IsAgentMode == false!
                    //if (Context.Runtime.IsAgentMode)
                    //{
                    //    int amount = optSer.UnderlyingAsset.Bars.Count;
                    //    DateTime today = (amount > 0) ? optSer.UnderlyingAsset.Bars[amount - 1].Date : new DateTime();
                    //    isExpired = optSer.ExpirationDate.Date.AddDays(1) < today.Date;
                    //}
                    // А если в режиме лаборатории, тогда только жалуемся в Главный Лог и продолжаем.
                    Context.Log(msg, MessageType.Warning, true /* !isExpired */);
                    return null;
                }
            }

            ISecurity res;
            if (m_optionType == StrikeType.Put)
                res = pair.Put.Security;
            else if (m_optionType == StrikeType.Call)
                res = pair.Call.Security;
            else
                throw new NotSupportedException("Не могу найти опцион вида: " + m_optionType);

            return res;
        }
    }
}
