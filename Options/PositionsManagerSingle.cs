using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.DataSource;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Script.Realtime;
using TSLab.Utils;

using IvTargetInfo = TSLab.Script.Handlers.Options.PositionsManager.IvTargetInfo;
using PosInfo = TSLab.Script.Handlers.Options.PositionsManager.PosInfo;
using SecInfo = TSLab.Script.Handlers.Options.PositionsManager.SecInfo;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Manager for virtual and real positions (single security)
    /// \~russian Управление виртуальными и реальными позициями (один инструмент)
    /// </summary>
    /// <remarks>
    /// \~english The handler takes care about virtual and real position. It should be the only point to work with virtual positions.
    /// You can create real positions in any way, the handler will use them during next execution.
    /// \~russian Блок управляет виртуальными и реальными позициями. При работе с виртуальными позициями следует использовать только его.
    /// Реальные позиции можно создавать любым способом и блок их увидит при следующем исполнении.
    /// 
    /// Блок необходимо ставить напосредственно после торгового источника с одним инструментом,
    /// чтобы он мог правильно проинициализировать виртуальные позиции до передачи управления в последующие узлы скрипта.
    /// В силу особенностей реализации блок является не потоковым, а побарным.
    /// Поэтому для взаимодействия с ним также приходится использовать обработчики побарного типа.
    /// </remarks>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Positions Manager (single security)", Language = Constants.En)]
    [HelperName("Менеджер позиций (один инструмент)", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = "Security")]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Управление виртуальными и реальными позициями (один инструмент)")]
    [HelperDescription("Manager for virtual and real positions (single security)", Constants.En)]
#if !DEBUG
    [HandlerInvisible]
#endif
    public partial class PositionsManagerSingle : BaseContextHandler /* IContextUses, INeedVariableId */, IValuesHandlerWithNumber
    {
        /// <summary>PosManSingle</summary>
        private const string MsgId = "PosManSingle";
        private const int VirtPosShift = 2;

        private string m_longIvTargetsCashKey, m_shortIvTargetsCashKey;
        private string m_longVirtualPositionsCashKey, m_shortVirtualPositionsCashKey;
        private string m_clickEventsCashKey, m_clickQuoteIvCashKey;

        /// <summary>
        /// Флаг подтверждения того, что инстанс уже исполнял метод Execute и,
        /// следовательно, что все виртуальные позиции уже восстановлены.
        /// </summary>
        private bool m_executed = false;
        private bool m_blockTrading = true;
        private bool m_importRealPos = false;
        private bool m_agregatePositions = true;
        private bool m_useVirtualPositions = true;
        private bool m_dropVirtualPositions = false;

        private bool m_portfolioReady = false;
        private bool m_checkSecurityTime = false;

        public const string PositionsManagerCashKey = "PositionsManagerSingle";

        public override string VariableId
        {
            get { return m_variableId; }
            set
            {
                m_variableId = value;
                m_clickEventsCashKey = String.Intern("clickEvents_" + PositionsManagerCashKey + "_" + m_variableId);
                m_clickQuoteIvCashKey = String.Intern("clickQuoteIvEvents_" + PositionsManagerCashKey + "_" + m_variableId);
                m_longIvTargetsCashKey = String.Intern("longIvTargets_" + PositionsManagerCashKey + "_" + m_variableId);
                m_shortIvTargetsCashKey = String.Intern("shortIvTargets_" + PositionsManagerCashKey + "_" + m_variableId);
                m_longVirtualPositionsCashKey = String.Intern("longVirtualPositions_" + PositionsManagerCashKey + "_" + m_variableId);
                m_shortVirtualPositionsCashKey = String.Intern("shortVirtualPositions_" + PositionsManagerCashKey + "_" + m_variableId);
            }
        }

        private string LongGlobalKey
        {
            get
            {
                return String.Intern("longPositions_" + Context.Runtime.TradeName);
            }
        }

        private string ShortGlobalKey
        {
            get
            {
                return String.Intern("shortPositions_" + Context.Runtime.TradeName);
            }
        }

        /// <summary>
        /// Коллекция всех длинных виртуальных позиций
        /// </summary>
        private List<Tuple<SecInfo, PosInfo>> LongVirtualPositions
        {
            get
            {
                List<Tuple<SecInfo, PosInfo>> virtualPositions;
                if (UseGlobalCache)
                {
                    #region Global cache
                    object globalObj = Context.LoadGlobalObject(LongGlobalKey, true);
                    virtualPositions = globalObj as List<Tuple<SecInfo, PosInfo>>;
                    // PROD-3970 - 'Важный' объект
                    if (virtualPositions == null)
                    {
                        var container = globalObj as NotClearableContainer;
                        if ((container != null) && (container.Content != null))
                            virtualPositions = container.Content as List<Tuple<SecInfo, PosInfo>>;
                    }
                    if (virtualPositions == null)
                    {
                        virtualPositions = new List<Tuple<SecInfo, PosInfo>>();
                        var container = new NotClearableContainer(virtualPositions);
                        Context.StoreGlobalObject(LongGlobalKey, container, true);
                    }
                    #endregion Global cache
                }
                else
                {
                    #region Local cache
                    object locObj = Context.LoadObject(m_longVirtualPositionsCashKey, true);
                    virtualPositions = locObj as List<Tuple<SecInfo, PosInfo>>;
                    // PROD-3970 - 'Важный' объект
                    if (virtualPositions == null)
                    {
                        var container = locObj as NotClearableContainer;
                        if ((container != null) && (container.Content != null))
                            virtualPositions = container.Content as List<Tuple<SecInfo, PosInfo>>;
                    }
                    if (virtualPositions == null)
                    {
                        virtualPositions = new List<Tuple<SecInfo, PosInfo>>();
                        var container = new NotClearableContainer(virtualPositions);
                        Context.StoreObject(m_longVirtualPositionsCashKey, container, true);
                    }
                    #endregion Local cache
                }

                return virtualPositions;
            }
        }

        /// <summary>
        /// Коллекция всех коротких виртуальных позиций
        /// </summary>
        private List<Tuple<SecInfo, PosInfo>> ShortVirtualPositions
        {
            get
            {
                List<Tuple<SecInfo, PosInfo>> virtualPositions;
                if (UseGlobalCache)
                {
                    #region Global cache
                    object globalObj = Context.LoadGlobalObject(ShortGlobalKey, true);
                    virtualPositions = globalObj as List<Tuple<SecInfo, PosInfo>>;
                    // PROD-3970 - 'Важный' объект
                    if (virtualPositions == null)
                    {
                        var container = globalObj as NotClearableContainer;
                        if ((container != null) && (container.Content != null))
                            virtualPositions = container.Content as List<Tuple<SecInfo, PosInfo>>;
                    }
                    if (virtualPositions == null)
                    {
                        virtualPositions = new List<Tuple<SecInfo, PosInfo>>();
                        var container = new NotClearableContainer(virtualPositions);
                        Context.StoreGlobalObject(ShortGlobalKey, container, true);
                    }
                    #endregion Global cache
                }
                else
                {
                    #region Local cache
                    object locObj = Context.LoadObject(m_shortVirtualPositionsCashKey, true);
                    virtualPositions = locObj as List<Tuple<SecInfo, PosInfo>>;
                    // PROD-3970 - 'Важный' объект
                    if (virtualPositions == null)
                    {
                        var container = locObj as NotClearableContainer;
                        if ((container != null) && (container.Content != null))
                            virtualPositions = container.Content as List<Tuple<SecInfo, PosInfo>>;
                    }
                    if (virtualPositions == null)
                    {
                        virtualPositions = new List<Tuple<SecInfo, PosInfo>>();
                        var container = new NotClearableContainer(virtualPositions);
                        Context.StoreObject(m_shortVirtualPositionsCashKey, container, true);
                    }
                    #endregion Local cache
                }

                return virtualPositions;
            }
        }

        /// <summary>
        /// Коллекция всех длинных котировок по волатильности
        /// </summary>
        private List<Tuple<SecInfo, IvTargetInfo>> LongIvTargets
        {
            get
            {
                object locObj = Context.LoadObject(m_longIvTargetsCashKey, false);
                List<Tuple<SecInfo, IvTargetInfo>> ivTargets = locObj as List<Tuple<SecInfo, IvTargetInfo>>;
                // PROD-3970 - 'Важный' объект
                if (ivTargets == null)
                {
                    var container = locObj as NotClearableContainer;
                    if ((container != null) && (container.Content != null))
                        ivTargets = container.Content as List<Tuple<SecInfo, IvTargetInfo>>;
                }
                if (ivTargets == null)
                {
                    ivTargets = new List<Tuple<SecInfo, IvTargetInfo>>();
                    var container = new NotClearableContainer(ivTargets);
                    Context.StoreObject(m_longIvTargetsCashKey, container, false);
                }

                return ivTargets;
            }
        }

        /// <summary>
        /// Коллекция всех коротких котировок по волатильности
        /// </summary>
        private List<Tuple<SecInfo, IvTargetInfo>> ShortIvTargets
        {
            get
            {
                object locObj = Context.LoadObject(m_shortIvTargetsCashKey, false);
                List<Tuple<SecInfo, IvTargetInfo>> ivTargets = locObj as List<Tuple<SecInfo, IvTargetInfo>>;
                // PROD-3970 - 'Важный' объект
                if (ivTargets == null)
                {
                    var container = locObj as NotClearableContainer;
                    if ((container != null) && (container.Content != null))
                        ivTargets = container.Content as List<Tuple<SecInfo, IvTargetInfo>>;
                }
                if (ivTargets == null)
                {
                    ivTargets = new List<Tuple<SecInfo, IvTargetInfo>>();
                    var container = new NotClearableContainer(ivTargets);
                    Context.StoreObject(m_shortIvTargetsCashKey, container, false);
                }

                return ivTargets;
            }
        }

        /// <summary>
        /// Провайдер СмартКом почему-то при формировании имени агента кодирует точку комбинацией '&point;'.
        /// Чтобы это не мелькало в логах, заменяю обратно.
        /// </summary>
        private static string NiceTradeName(string tradeName)
        {
            string readableTradeName = tradeName.Replace(Constants.HtmlDot, ".");
            return readableTradeName;
        }

        private static List<InteractiveActionEventArgs> GetOnClickEvents(IContext externalContext, string key)
        {
            List<InteractiveActionEventArgs> onClickEvents = null;

            object locObj = externalContext.LoadObject(key, false);
            var container = locObj as NotClearableContainer;
            if ((container != null) && (container.Content != null))
                onClickEvents = container.Content as List<InteractiveActionEventArgs>;

            if (onClickEvents == null)
                onClickEvents = locObj as List<InteractiveActionEventArgs>;

            if (onClickEvents == null)
            {
                onClickEvents = new List<InteractiveActionEventArgs>();
                container = new NotClearableContainer(onClickEvents);
                externalContext.StoreObject(key, container, false);
            }

            return onClickEvents;
        }

        #region Parameters
        /// <summary>
        /// \~english When true trading is completely blocked to avoid user misclick errors
        /// \~russian При true торговля будет полностью блокирована, чтобы избежать случайных кликов по котировкам
        /// </summary>
        [HelperName("Block Trading", Constants.En)]
        [HelperName("Блокировать торговлю", Constants.Ru)]
        [Description("При true торговля будет полностью блокирована, чтобы избежать случайных кликов по котировкам")]
        [HelperDescription("When true trading is completely blocked to avoid user misclick errors", Language = Constants.En)]
        [HandlerParameter(true, Name = "Block Trading", NotOptimized = false, IsVisibleInBlock = true, Default = "True")]
        public bool BlockTrading
        {
            get { return m_blockTrading; }
            set { m_blockTrading = value; }
        }

        /// <summary>
        /// \~english When true it will create only virtual positions without sending orders in market
        /// \~russian При true будет создавать виртуальные позиции без выставления заявок в рынок
        /// </summary>
        [HelperName("Virtual Positions", Constants.En)]
        [HelperName("Виртуальные позиции", Constants.Ru)]
        [Description("При true будет создавать виртуальные позиции без выставления заявок в рынок")]
        [HelperDescription("When true it will create only virtual positions without sending orders in market", Language = Constants.En)]
        [HandlerParameter(true, Name = "Virtual Positions", NotOptimized = false, IsVisibleInBlock = true, Default = "True")]
        public bool UseVirtualPositions
        {
            get { return m_useVirtualPositions; }
            set { m_useVirtualPositions = value; }
        }

        /// <summary>
        /// \~english When true it will agregate positions of similar directions in one
        /// \~russian При true будет агрегировать позиции одинакового направления в одну общую
        /// </summary>
        [HelperName("Agregate positions", Constants.En)]
        [HelperName("Агрегировать позиции", Constants.Ru)]
        [Description("При true будет агрегировать позиции одинакового направления в одну общую")]
        [HelperDescription("When true it will agregate positions of similar directions in one", Language = Constants.En)]
        [HandlerParameter(true, Name = "Agregate positions", NotOptimized = false, IsVisibleInBlock = true, Default = "True")]
        public bool AgregatePositions
        {
            get { return m_agregatePositions; }
            set { m_agregatePositions = value; }
        }

        /// <summary>
        /// \~english Drop virtual positions
        /// \~russian Очистить виртуальные позиции
        /// </summary>
        [HelperName("Drop Virtual Positions", Constants.En)]
        [HelperName("Очистить виртуальные позиции", Constants.Ru)]
        [Description("Очистить виртуальные позиции")]
        [HelperDescription("Drop virtual positions", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "False")]
        public bool DropVirtualPos
        {
            get { return m_dropVirtualPositions; }
            set { m_dropVirtualPositions = value; }
        }

        /// <summary>
        /// \~english Button to import real positions
        /// \~russian Кнопка импорта реальных позиций
        /// </summary>
        [HelperName("Import Real Pos", Constants.En)]
        [HelperName("Импорт позиций", Constants.Ru)]
        [Description("Кнопка импорта реальных позиций")]
        [HelperDescription("Button to import real positions", Language = Constants.En)]
        [HandlerParameter(true, Name = "Import Real Pos", NotOptimized = false, IsVisibleInBlock = true, Default = "False")]
        public bool ImportRealPos
        {
            get { return m_importRealPos; }
            set { m_importRealPos = value; }
        }

        /// <summary>
        /// \~english Use global cache
        /// \~russian Использовать глобальный кеш?
        /// </summary>
        [HelperName("Use Global Cache", Constants.En)]
        [HelperName("Использовать глобальный кеш", Constants.Ru)]
        [HandlerParameter(Name = "Use Global Cache", NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        [Description("Использовать глобальный кеш?")]
        [HelperDescription("Use global cache", Language = Constants.En)]
        public bool UseGlobalCache { get; set; }

        /// <summary>
        /// \~english When true it will validate security time before sending order to market
        /// \~russian При true будет проверять время последнего бара в инструменте перед тем, как отправить заявку в рынок
        /// </summary>
        [HelperName("Check security time", Constants.En)]
        [HelperName("Проверять время инструмента", Constants.Ru)]
        [Description("При true будет проверять время последнего бара в инструменте перед тем, как отправить заявку в рынок")]
        [HelperDescription("When true it will validate security time before sending order to market", Language = Constants.En)]
        [HandlerParameter(true, Name = "Check security time", NotOptimized = true, IsVisibleInBlock = true, Default = "False")]
        public bool CheckSecurityTime
        {
            get { return m_checkSecurityTime; }
            set { m_checkSecurityTime = value; }
        }
        #endregion Parameters

        public static PositionsManager GetManager(IContext context)
        {
            object obj = context.LoadObject(PositionsManagerCashKey);
            PositionsManager res = obj as PositionsManager;
            if (res == null)
            {
                var container = obj as NotClearableContainer<PositionsManager>;
                if (container != null)
                    res = container.Content;
            }
            return res;
        }

        /// <summary>
        /// Вариант метода для трансформации инструмент --> инструмент
        /// </summary>
        /// <param name="source">инструмент -- торгуемый источник</param>
        /// <param name="barNum">индекс бара</param>
        /// <returns>тот же самый торгуемый источник</returns>
        public ISecurity Execute(ISecurity source, int barNum)
        {
            // Возможно, позднее придется восстанавливать состояние позиций с
            // учетом истории (времени) их возникновения. Но пока сделаю просто "чтобы работало".
            int barsCount = Context.BarsCount;
            if (!Context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return source;

            m_executed = true;

            int lastBarIndex = source.Bars.Count - 1;
            DateTime now = source.Bars[Math.Min(barNum, lastBarIndex)].Date;
            bool wasInitialized = HandlerInitializedToday(now);

            // Если в кеше уже есть информация об обычном PositionsManager, мне лучше сразу упасть.
            PositionsManager optionsPosMan = PositionsManager.GetManager(Context);
            if (optionsPosMan != null)
            {
                string msg = String.Format("[{0}] Conflict with existing Positions Manager. Please, use advanced manager for options source.", MsgId);
                throw new InvalidOperationException(msg);
            }

            // Безусловно помещаю себя в кеш, поскольку только я являюсь носителем Истины
            // При этом большая часть моей деятельности носит характер поведения
            // статического класса. Поэтому по большому счету должно быть без разницы,
            // какой именно инстанс на самом деле лежит в кеше.
            var container = new NotClearableContainer<PositionsManagerSingle>(this);
            Context.StoreObject(PositionsManagerCashKey, container, false);

            #region Restore virtual positions
            {
                List<Tuple<SecInfo, PosInfo>> virtualPositions = LongVirtualPositions;
                if (virtualPositions.Count > 0)
                {
                    RestoreVirtualPositions(source, virtualPositions);
                }
            }

            {
                List<Tuple<SecInfo, PosInfo>> virtualPositions = ShortVirtualPositions;
                if (virtualPositions.Count > 0)
                {
                    RestoreVirtualPositions(source, virtualPositions);
                }
            }
            #endregion Restore virtual positions

            #region Check that portfolio is ready to trade
            // Проверяю ПОЛНУЮ готовность опционного источника к совершению реальных торговых операций
            // Это надо делать ДО обработки кликов мыши.
            m_portfolioReady = true;
            foreach (ISecurity sec in Context.Runtime.Securities)
            {
                if (!sec.IsPortfolioReady)
                {
                    m_portfolioReady = false;
                    break;
                }
            }

            if (!m_portfolioReady)
            {
                // Это важное предупреждение. Имхо, оно должно быть в Главном Логе.
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.PortfolioIsNotReady",
                    Context.Runtime.TradeName + ":" + MsgId);
                if (wasInitialized)
                    Context.Log(msg, MessageType.Warning, true);
            }
            #endregion Check that portfolio is ready to trade

            // Это надо ОБЯЗАТЕЛЬНО делать ПОСЛЕ восстановления виртуальных позиций
            // и ПОСЛЕ выставления флага m_portfolioReady.
            #region Process clicks
            {
                List<InteractiveActionEventArgs> onClickEvents = GetOnClickEvents(Context, m_clickEventsCashKey);

                if (onClickEvents.Count > 0)
                {
                    //string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.AmountOfEvents"),
                    //    GetType().Name + ".Execute", onClickEvents.Count);
                    //Context.Log(msg, MessageType.Info);

                    RouteOnClickEvents(source, onClickEvents);
                }
            }

            {
                List<InteractiveActionEventArgs> onClickEvents = GetOnClickEvents(Context, m_clickQuoteIvCashKey);

                if (onClickEvents.Count > 0)
                {
                    string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.AmountOfEvents",
                        GetType().Name + ".Execute", onClickEvents.Count);
                    Context.Log(msg, MessageType.Info);

                    // TODO: реализовать выставление задачи котирования? А смысл делать это для одиночного инструмента???
                    // RouteOnQuoteIvEvents(source, onClickEvents);
                }
            }
            #endregion Process clicks

            if (m_importRealPos)
            {
                #region Import real positions
                if (m_blockTrading)
                {
                    string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked", MsgId);
                    Context.Log(msg, MessageType.Warning, true);
                }
                else if (!m_useVirtualPositions)
                {
                    string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.ImportBlocked", MsgId);
                    Context.Log(msg, MessageType.Warning, true);
                }
                else
                {
                    int count = 0;

                    // TODO: или нужно делать source.FinInfo.Security as ISecurityRt?
                    ISecurityRt securityRt = source as ISecurityRt;
                    if (securityRt != null)
                    {
                        double qty = securityRt.BalanceQuantity;
                        if ((!DoubleUtil.IsZero(qty)) && securityRt.FinInfo.TheoreticalPrice.HasValue)
                        {
                            double px = securityRt.FinInfo.TheoreticalPrice.Value;
                            string pxStr = px.ToString(CultureInfo.InvariantCulture);

                            if (qty > 0)
                                BuyAtPrice(Context, source, Math.Abs(qty), px, "Import: " + securityRt.Symbol + "; TheorPx: " + pxStr, null);
                            else
                                SellAtPrice(Context, source, Math.Abs(qty), px, "Import: " + securityRt.Symbol + "; TheorPx: " + pxStr, null);
                            count++;
                        }
                        else if ((!DoubleUtil.IsZero(qty)) && securityRt.FinInfo.LastPrice.HasValue)
                        {
                            double px = securityRt.FinInfo.LastPrice.Value;
                            string pxStr = px.ToString(CultureInfo.InvariantCulture);

                            if (qty > 0)
                                BuyAtPrice(Context, source, Math.Abs(qty), px, "Import: " + securityRt.Symbol + "; LastPx: " + pxStr, null);
                            else
                                SellAtPrice(Context, source, Math.Abs(qty), px, "Import: " + securityRt.Symbol + "; LastPx: " + pxStr, null);
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.ImportSuccess", MsgId, count);
                        Context.Log(msg, MessageType.Warning, true);
                    }
                }
                #endregion Import real positions
            }

            if (m_dropVirtualPositions)
            {
                #region Drop logic
                try
                {
                    string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.DropWarning", MsgId);
                    Context.Log(msg, MessageType.Warning, true);

                    DropVirtualPositions(Context);
                }
                finally
                {
                    m_dropVirtualPositions = false;
                }
                #endregion Drop logic
            }

            return source;
        }

        /// <summary>
        /// Восстановить виртуальные позиции опционного источника
        /// </summary>
        /// <param name="sec">торгуемый источник (одиночный инструмент)</param>
        /// <param name="virtualPositions">перечень виртуальных позиций</param>
        private void RestoreVirtualPositions(ISecurity source, List<Tuple<SecInfo, PosInfo>> virtualPositions)
        {
            if (virtualPositions == null)
                return;

            int posLen = virtualPositions.Count;
            //if (posLen > 0)
            //{
            //    string msg = String.Format("[DEBUG:{0}] I'm restoring {1} virtual positions.", MsgId, posLen);
            //    Context.Log(msg, MessageType.Info, true);
            //}

            int posShift = VirtPosShift + 1;
            for (int j = 0; j < posLen; j++)
            {
                var tuple = virtualPositions[j];
                SecInfo secDesc = tuple.Item1;
                PosInfo posInfo = tuple.Item2;

                try
                {
                    // TODO: Сделать локальный кеш?
                    ISecurity sec = null;
                    {
                        foreach (ISecurity s in Context.Runtime.Securities)
                        {
                            if (!secDesc.Equals(s.SecurityDescription))
                                continue;

                            // Нашли? Выходим.
                            sec = s;
                            break;
                        }
                    }

                    // Попробую подписаться через текущий source...
                    if (sec == null)
                    {
                        //foreach (IOptionStrike optStrike in opt.GetStrikes())
                        {
                            if (secDesc.Equals(source.FinInfo.Security))
                            {
                                // Нашли? Выходим.
                                int bc = source.Bars.Count;
                                string msg = String.Format("[{0}_#2] There is security DsName: {1}; Symbol: {2}; Security: {3} with {4} bars available.",
                                    MsgId, secDesc.DsName, secDesc.Name, secDesc.FullName, bc);
                                Context.Log(msg, MessageType.Info, false);
                                sec = source;
                            }
                        }
                    }

                    if (sec == null)
                    {
                        string msg = String.Format("[{0}_#2] There is no security. DsName: {1}; Symbol: {2}; Security: {3}",
                            MsgId, secDesc.DsName, secDesc.Name, secDesc.FullName);
                        Context.Log(msg, MessageType.Warning, true);
                        continue;
                    }

                    int entryBarIndex = posInfo.EntryBarNum;
                    if (entryBarIndex >= Context.BarsCount - VirtPosShift)
                    {
                        entryBarIndex = Math.Max(0, Context.BarsCount - posShift);
                        posShift++;
                    }
                    int sign = posInfo.IsLong ? +1 : -1;
                    sec.Positions.MakeVirtualPosition(
                        entryBarIndex, sign * Math.Abs(posInfo.Shares), posInfo.EntryPrice, posInfo.EntrySignalName, posInfo.EntryNotes);

                    // Вот в ТАКОМ варианте всегда будут лонговые сделки. Потому что pos.Shares всегда неотрицательное число.
                    //sec.Positions.MakeVirtualPosition(pos.EntryBarNum, pos.Shares, pos.EntryPrice, pos.EntrySignalName);
                }
                catch (Exception ex)
                {
                    string msg = String.Format("[{0}_#2] {1} in RestoreVirtualPositions. Message: {2}\r\n{3}",
                            MsgId, ex.GetType().FullName, ex.Message, ex);
                    Context.Log(msg, MessageType.Warning, true);
                }
            }
        }

        public void InteractiveSplineOnClickEvent(IContext externalContext, object sender, InteractiveActionEventArgs eventArgs)
        {
            // Все виртуальные позиции уже восстановлены?
            if (!m_executed)
            {
                externalContext.Log("[PositionsManager.InteractiveSplineOnClickEvent] Not executed???", MessageType.Error, true);
            }

            if (m_blockTrading)
            {
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked",
                    MsgId + ".InteractiveSplineOnClickEvent");
                externalContext.Log(msg, MessageType.Info, true);
                return;
            }

            List<InteractiveActionEventArgs> onClickEvents = GetOnClickEvents(externalContext, m_clickEventsCashKey);
            onClickEvents.Add(eventArgs);

            //{
            //    string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.AmountOfLeftClicks"),
            //        GetType().Name + ".InteractiveSplineOnClickEvent", onClickEvents.Count);
            //    externalContext.Log(msg, MessageType.Info);
            //}

            externalContext.Recalc();
        }

        public void InteractiveSplineOnQuoteIvEvent(IContext externalContext, object sender, InteractiveActionEventArgs eventArgs)
        {
            // Все виртуальные позиции уже восстановлены?
            if (!m_executed)
            {
                externalContext.Log("[PositionsManager.InteractiveSplineOnQuoteIvEvent] Not executed???", MessageType.Error, true);
            }

            if (m_blockTrading)
            {
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked",
                    MsgId + ".InteractiveSplineOnQuoteIvEvent");
                externalContext.Log(msg, MessageType.Info, true);
                return;
            }

            List<InteractiveActionEventArgs> onClickEvents = GetOnClickEvents(externalContext, m_clickQuoteIvCashKey);
            onClickEvents.Add(eventArgs);

            {
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.AmountOfLeftClicks",
                    GetType().Name + ".InteractiveSplineOnQuoteIvEvent", onClickEvents.Count);
                externalContext.Log(msg, MessageType.Info);
            }

            externalContext.Recalc();
        }

        /// <summary>
        /// Обработать клики мыши для совершения сделок в одиночном инструменте (применимо только для опционного источника?)
        /// Вызывать ТОЛЬКО ИЗ МЕТОДА Execute при валидном контексте!
        /// </summary>
        private void RouteOnClickEvents(ISecurity source, List<InteractiveActionEventArgs> eventArgs)
        {
            if ((source == null) || (eventArgs == null))
                return;

            int argLen = eventArgs.Count;
            if (BlockTrading && (argLen > 0))
            {
                eventArgs.Clear();
                string msg = String.Format("[{0}.RouteOnClickEvents_#2] ERROR! Trading is blocked. Should NOT be here. All events were removed.", GetType().Name);
                Context.Log(msg, MessageType.Error, true);
                return;
            }

            bool recalc = false;
            for (int j = argLen - 1; j >= 0; j--)
            {
                InteractiveActionEventArgs eventArg = eventArgs[j];
                eventArgs.RemoveAt(j);

                #region Processing
                try
                {
                    SmileNodeInfo nodeInfo = eventArg.Point.Tag as SmileNodeInfo;
                    if (nodeInfo == null)
                    {
                        //string msg = String.Format("[{0}] There is no nodeInfo. Strike: {1}", MsgId);
                        string msg = RM.GetStringFormat(CultureInfo.InvariantCulture, "OptHandlerMsg.PositionsManager.ThereIsNoNodeInfo",
                            MsgId, eventArg.Point.ValueX);
                        Context.Log(msg, MessageType.Error, true);
                        continue;
                    }

                    if ((!UseVirtualPositions) && nodeInfo.Expired)
                    {
                        string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.SecurityExpired",
                            MsgId, nodeInfo.Symbol, nodeInfo.Security.SecurityDescription.ExpirationDate);
                        Context.Log(msg, MessageType.Error, true);
                        continue;
                    }

                    ISecurity sec = (from s in Context.Runtime.Securities
                                     let secDesc = s.SecurityDescription
                                     where secDesc.FullName.Equals(nodeInfo.FullName, StringComparison.InvariantCultureIgnoreCase) &&
                                           secDesc.DSName.Equals(nodeInfo.DSName, StringComparison.InvariantCultureIgnoreCase) &&
                                           secDesc.Name.Equals(nodeInfo.Symbol, StringComparison.InvariantCultureIgnoreCase)
                                     select s).SingleOrDefault();

                    if (sec == null)
                    {
                        //IOptionStrike opStrike = (from s in opt.GetStrikes()
                        //                          let secDesc = s.FinInfo.Security
                        //                          where secDesc.FullName.Equals(nodeInfo.FullName, StringComparison.InvariantCultureIgnoreCase) &&
                        //                                secDesc.DSName.Equals(nodeInfo.DSName, StringComparison.InvariantCultureIgnoreCase) &&
                        //                                secDesc.Name.Equals(nodeInfo.Symbol, StringComparison.InvariantCultureIgnoreCase)
                        //                          select s).SingleOrDefault();

                        var secDesc = source.FinInfo.Security;
                        if (secDesc.FullName.Equals(nodeInfo.FullName, StringComparison.InvariantCultureIgnoreCase) &&
                            secDesc.DSName.Equals(nodeInfo.DSName, StringComparison.InvariantCultureIgnoreCase) &&
                            secDesc.Name.Equals(nodeInfo.Symbol, StringComparison.InvariantCultureIgnoreCase))
                        {
                            //sec = opStrike.Security;
                            sec = source;
                            int bc = sec.Bars.Count;
                            string msg = String.Format("[{0}_#2] There is security DsName: {1}; Symbol: {2}; Security: {3} with {4} bars available.",
                                MsgId, nodeInfo.DSName, nodeInfo.Symbol, nodeInfo.FullName, bc);
                            Context.Log(msg, MessageType.Info, false);
                        }
                    }

                    if (sec == null)
                    {
                        string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.NoSecurity",
                            MsgId, nodeInfo.Symbol);
                        Context.Log(msg, MessageType.Error, true);
                        continue;
                    }

                    int len = sec.Bars.Count;
                    //// Валидирую наличие правильной котировки
                    //if (sec.FinInfo != null)
                    //{
                    //    Debug.WriteLine("AskPx: " + sec.FinInfo.Ask);
                    //    Debug.WriteLine("BidPx: " + sec.FinInfo.Bid);
                    //}

                    double qty = nodeInfo.Qty;
                    OptionPxMode optionPxMode = nodeInfo.PxMode;
                    if ((optionPxMode == OptionPxMode.Ask) && DoubleUtil.IsPositive(qty) ||
                        (optionPxMode == OptionPxMode.Bid) && DoubleUtil.IsPositive(-qty)) // TODO: Странная конструкция, потому что пока нет готового метода IsNegative
                    {
                        recalc = true;
                        string signalName = "\r\nLeft-Click BUY \r\n" + eventArg.Point.Tooltip + "\r\n";
                        BuyAtPrice(Context, sec, Math.Abs(qty), nodeInfo.OptPx, signalName, signalName);
                    }
                    else if ((optionPxMode == OptionPxMode.Bid) && DoubleUtil.IsPositive(qty) ||
                             (optionPxMode == OptionPxMode.Ask) && DoubleUtil.IsPositive(-qty)) // TODO: Странная конструкция, потому что пока нет готового метода IsNegative
                    {
                        recalc = true;
                        string signalName = "\r\nLeft-Click SELL \r\n" + eventArg.Point.Tooltip + "\r\n";
                        SellAtPrice(Context, sec, Math.Abs(qty), nodeInfo.OptPx, signalName, signalName);
                    }
                    else if (optionPxMode == OptionPxMode.Mid)
                    {
                        string msg = String.Format("[{0}] OptionPxMode.Mid is not implemented.", optionPxMode);
                        Context.Log(msg, MessageType.Error, true);
                    }
                    else if (DoubleUtil.IsZero(qty))
                    {
                        //string msg = String.Format("[{0}] Qty: {1}. Trading is blocked for zero quantity.", optionPxMode, qty);
                        string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlockedForZeroQty", optionPxMode, qty);
                        Context.Log(msg, MessageType.Info, true);
                    }
                    else
                    {
                        // TODO: Тут будет всякий мусор типа NaN и прочего. Надо ли как-то реагировать?
                    }
                }
                catch (Exception ex)
                {
                    string msg = String.Format("[{0}_#2] {1} in RouteOnClickEvents. Message: {2}\r\n{3}",
                            MsgId, ex.GetType().FullName, ex.Message, ex);
                    Context.Log(msg, MessageType.Error, true);
                }
                #endregion Processing
            }

            if (recalc)
                Context.Recalc();
        }

        /// <summary>
        /// Купить лимитником заданное количество по заданной цене.
        /// Допускается передача отрицательного количества. В этой ситуации будет выполнена продажа.
        /// </summary>
        /// <param name="externalContext">контекст вызывающего блока</param>
        /// <param name="sec">инструмент, полученный из context.Runtime.Securities</param>
        /// <param name="qty">при 0 команда игнорируется и ничего не происходит</param>
        /// <param name="px">цена</param>
        /// <param name="signalName">имя сигнала (для системы!)</param>
        /// <param name="notes">строковый комментарий для пользователя</param>
        public void BuyAtPrice(IContext externalContext, ISecurity sec, double qty, double px, string signalName, string notes)
        {
            BuyAtPrice(externalContext, sec, qty, px, signalName, notes, m_agregatePositions);
        }

        /// <summary>
        /// Купить лимитником заданное количество по заданной цене.
        /// Допускается передача отрицательного количества. В этой ситуации будет выполнена продажа.
        /// </summary>
        /// <param name="externalContext">контекст вызывающего блока</param>
        /// <param name="sec">инструмент, полученный из context.Runtime.Securities</param>
        /// <param name="qty">при 0 команда игнорируется и ничего не происходит</param>
        /// <param name="px">цена</param>
        /// <param name="signalName">имя сигнала (для системы!)</param>
        /// <param name="notes">строковый комментарий для пользователя</param>
        /// <param name="agregateRealPositions">агрегировать реальные сделки в разбивке по тикерам и направлениям</param>
        private void BuyAtPrice(IContext externalContext, ISecurity sec, double qty, double px, string signalName, string notes,
            bool agregateRealPositions)
        {
            //int len = sec.Bars.Count;
            int len = externalContext.BarsCount;

            string extTradeName = NiceTradeName(externalContext.Runtime.TradeName);
            if (m_blockTrading)
            {
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked",
                    externalContext.Runtime.TradeName + ":" + MsgId);
                externalContext.Log(msg, MessageType.Warning, true);
                return;
            }

            {
#if DEBUG
                string msg = String.Format(CultureInfo.InvariantCulture,
                    "[{0}] BuyAtPrice. Qty:{1}; px:{2}; sec:{3}; SignalName:{4}; Notes:{5}",
                    extTradeName + ":" + MsgId, qty, px, sec, signalName, notes);
#else
                string msg = String.Format(CultureInfo.InvariantCulture,
                    "[{0}] BuyAtPrice. Qty:{1}; px:{2}; sec:{3}",
                    extTradeName + ":" + MsgId, qty, px, sec);
#endif
                externalContext.Log(msg, MessageType.Warning);
            }

            if (!m_portfolioReady)
            {
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.PortfolioIsNotReady",
                    Context.Runtime.TradeName + ":" + MsgId);
                Context.Log(msg, MessageType.Warning, true);
                return;
            }

            if (qty > 0)
            {
                string origSignalName;
                if (String.IsNullOrWhiteSpace(signalName))
                {
                    origSignalName = String.Empty;
                    signalName = String.Empty;
                }
                else
                {
                    origSignalName = signalName;
                    signalName += " ~ ";
                }
                signalName += DateTime.Now.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture);

                if (m_useVirtualPositions)
                {
                    #region Virtual positions
                    externalContext.Log(
                        String.Format(CultureInfo.InvariantCulture, "[{0}] VIRTUAL BUY {1} {2} @ {3}",
                        externalContext.Runtime.TradeName + ":" + MsgId, Math.Abs(qty), sec.Symbol, px),
                        MessageType.Info, true);

                    IPosition virtPos = null;
                    try
                    {
                        // Здесь будут отобраны виртуальные и среди них вторым проходом длинные
                        virtPos = GetActiveForBar(sec, len, TotalProfitAlgo.VirtualPositions).SingleOrDefault(p => p.IsLong);
                    }
                    catch (InvalidOperationException)
                    {
                        string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TooManyVirtualLongPositions",
                            externalContext.Runtime.TradeName + ":" + MsgId, sec.Symbol, len, sec);
                        externalContext.Log(msg, MessageType.Warning, true);
                        return;
                    }

                    if (virtPos == null)
                    {
                        #region Виртуальной позы нет -- создаём
                        virtPos = sec.Positions.MakeVirtualPosition(len - VirtPosShift, Math.Abs(qty), px, signalName, notes);
                        Tuple<SecInfo, PosInfo> tuple = new Tuple<SecInfo, PosInfo>(new SecInfo(sec.SecurityDescription), new PosInfo(virtPos));
                        var lvp = LongVirtualPositions;
                        lvp.Add(tuple);

                        if (UseGlobalCache)
                        {
                            var container = new NotClearableContainer(lvp);
                            externalContext.StoreGlobalObject(LongGlobalKey, container, true);
                        }
                        #endregion Виртуальной позы нет -- создаём
                    }
                    else if (len - VirtPosShift > virtPos.EntryBarNum)
                    {
                        #region Виртуальная поза есть давно -- просто меняем среднюю цену и количество
                        if (virtPos.IsLong)
                        {
                            //double money = virtPos.EntryPrice * Math.Abs(virtPos.Shares);
                            double money = virtPos.AverageEntryPrice * Math.Abs(virtPos.Shares);
                            money += px * Math.Abs(qty);
                            double avgPx = money / (Math.Abs(virtPos.Shares) + Math.Abs(qty));
                            //virtPos.VirtualChange(len - VirtPosShift, avgPx, Math.Abs(virtPos.Shares) + Math.Abs(qty), signalName, notes);
                            virtPos.VirtualChange(len - VirtPosShift, px, Math.Abs(virtPos.Shares) + Math.Abs(qty), signalName, notes);

                            System.Diagnostics.Debug.Assert(DoubleUtil.AreClose(avgPx, virtPos.AverageEntryPrice),
                                "ActualPx:" + virtPos.AverageEntryPrice + "; Expected:" + avgPx);

                            // Сохраняем обновленное состояние позиции.
                            // К сожалению, для этого надо сохранить весь список.
                            if (UseGlobalCache)
                            {
                                var lvp = LongVirtualPositions;
                                var tuple = (from t in lvp
                                             where t.Item1.Equals(sec.SecurityDescription)
                                             select t).SingleOrDefault();
                                if (tuple != null)
                                {
                                    lvp.Remove(tuple);
                                    tuple = new Tuple<SecInfo, PosInfo>(tuple.Item1, new PosInfo(virtPos));
                                }
                                else
                                {
                                    SecInfo si = new SecInfo(sec.SecurityDescription);
                                    tuple = new Tuple<SecInfo, PosInfo>(si, new PosInfo(virtPos));
                                }
                                lvp.Add(tuple);
                                var container = new NotClearableContainer(lvp);
                                externalContext.StoreGlobalObject(LongGlobalKey, container, true);
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException("[PositionsManager.BuyAtPrice] В эту ветку не должны заходить! virtPos.IsLong:" + virtPos.IsLong);
                        }
                        #endregion Виртуальная поза есть давно -- просто меняем среднюю цену и количество
                    }
                    else
                    {
                        #region Виртуальная поза есть, но создана в будущем -- создаём новую чуть раньше
                        if (virtPos.IsLong)
                        {
                            double money = virtPos.EntryPrice * Math.Abs(virtPos.Shares);
                            money += px * Math.Abs(qty);
                            double avgPx = money / (Math.Abs(virtPos.Shares) + Math.Abs(qty));

                            var lvp = LongVirtualPositions;
                            var tuple = (from t in lvp
                                         where t.Item1.Equals(sec.SecurityDescription)
                                         select t).SingleOrDefault();
                            if (tuple != null)
                                lvp.Remove(tuple);

                            virtPos = sec.Positions.MakeVirtualPosition(len - VirtPosShift, Math.Abs(virtPos.Shares) + Math.Abs(qty), avgPx, signalName, notes);
                            tuple = new Tuple<SecInfo, PosInfo>(new SecInfo(sec.SecurityDescription), new PosInfo(virtPos));
                            lvp.Add(tuple);

                            // Сохраняем обновленное состояние позиции.
                            // К сожалению, для этого надо сохранить весь список.
                            if (UseGlobalCache)
                            {
                                var container = new NotClearableContainer(lvp);
                                externalContext.StoreGlobalObject(LongGlobalKey, container, true);
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException("[PositionsManager.BuyAtPrice] В эту ветку не должны заходить! virtPos.IsLong:" + virtPos.IsLong);
                        }
                        #endregion Виртуальная поза есть, но создана в будущем -- создаём новую чуть раньше
                    }
                    #endregion Virtual positions
                }
                else
                {
                    if (!sec.IsRealtime)
                    {
                        string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.RunAsAgent",
                            externalContext.Runtime.TradeName + ":" + MsgId, sec.IsRealtime);
                        externalContext.Log(msg, MessageType.Warning, true);
                        return;
                    }

                    DateTime lastBarTime = sec.Bars[len - 1].Date;
                    double intervalSec = externalContext.Runtime.IntervalInstance.ToSeconds();
                    if (m_checkSecurityTime && ((DateTime.Now - lastBarTime).TotalSeconds > 3 * intervalSec))
                    {
                        string msg = String.Format(CultureInfo.InvariantCulture,
                            RM.GetString("OptHandlerMsg.PositionsManager.BarIsOld"),
                            externalContext.Runtime.TradeName + ":" + MsgId, sec.Symbol, intervalSec,
                            lastBarTime.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture),
                            DateTime.Now.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture));
                        externalContext.Log(msg, MessageType.Warning, true);
                        return;
                    }

                    if (agregateRealPositions)
                    {
                        // Здесь будут отобраны РЕАЛЬНЫЕ и среди них вторым проходом длинные
                        IPosition realPos = null;
                        var posList = GetActiveForBar(sec, len, TotalProfitAlgo.RealPositions).Where(p => p.IsLong).ToList();
                        if (posList.Count >= 1)
                        {
                            realPos = posList[0];
                            //realPos = GetActiveForBar(sec, len, TotalProfitAlgo.RealPositions).SingleOrDefault(p => p.IsLong);
                        }

                        // Вывожу предупреждение, если несмотря на режим агрегирования в агенте слишком много позиций
                        if (posList.Count > 1)
                        {
                            string msg = String.Format(CultureInfo.InvariantCulture,
                                RM.GetString("OptHandlerMsg.PositionsManager.TooManyLongPositions"),
                                externalContext.Runtime.TradeName + ":" + MsgId, sec, posList.Count);
                            externalContext.Log(msg, MessageType.Warning, false);
                        }

                        if (realPos == null)
                        {
                            // [2015-12-29] Если мы агрегируем позиции, но в рынке ещё не находимся,
                            // то скорее всего безопасно использовать первоначальное имя сигнала?
                            // [2016-09-08] Хотя жалобы юзеров в GLSP-169 (ситуация воспроизвелась у меня сегодня на СмартКом)
                            // говорят об обратном.
                            //sec.Positions.BuyAtPrice(len, Math.Abs(qty), px, signalName, notes);
                            // [2017-03-02] PROD-4980 - Было исправлено несколько важных вещей с частичным зафилом заявок. Восстанавливаю старую логику.
                            sec.Positions.BuyAtPrice(len, Math.Abs(qty), px, origSignalName, notes);

#if DEBUG
                            string msg = String.Format(CultureInfo.InvariantCulture,
                                    "[{0}] BuyAtPrice-BUY. Qty:{1}; px:{2}; sec:{3}; SignalName:{4}; Notes:{5}",
                                    extTradeName + ":" + MsgId, Math.Abs(qty), px, sec, signalName, notes);
#else
                            string msg = String.Format(CultureInfo.InvariantCulture,
                                    "[{0}] BuyAtPrice-BUY. Qty:{1}; px:{2}; sec:{3}",
                                    extTradeName + ":" + MsgId, Math.Abs(qty), px, sec);
#endif
                            externalContext.Log(msg, MessageType.Warning);
                        }
                        else
                        {
                            realPos.ChangeAtPrice(len, px, Math.Abs(realPos.Shares) + Math.Abs(qty), realPos.EntrySignalName, notes);

#if DEBUG
                            string msg = String.Format(CultureInfo.InvariantCulture,
                                "[{0}] ChangeAtPrice-BUY. Qty:{1}; px:{2}; sec:{3}; SignalName:{4}; new Notes:{5}",
                                extTradeName + ":" + MsgId, Math.Abs(realPos.Shares) + Math.Abs(qty), px, sec, realPos.EntrySignalName, notes);
#else
                            string msg = String.Format(CultureInfo.InvariantCulture,
                                "[{0}] ChangeAtPrice-BUY. Qty:{1}; px:{2}; sec:{3}",
                                extTradeName + ":" + MsgId, Math.Abs(realPos.Shares) + Math.Abs(qty), px, sec);
#endif
                            externalContext.Log(msg, MessageType.Warning);
                        }
                    }
                    else
                    {
                        sec.Positions.BuyAtPrice(len, Math.Abs(qty), px, signalName, notes);
                    }

                    //externalContext.Log(
                    //    String.Format("[{0}] BUY {1} {2} @ {3}. BarNum:{4}; sec.Bars.Count:{5}; IsLastBarUsed:{6}",
                    //        MsgId, Math.Abs(qty), sec.Symbol, px, len, sec.Bars.Count, externalContext.IsLastBarUsed),
                    //    MessageType.Warning, true);
                }
            }
            else if (qty < 0)
            {
                SellAtPrice(externalContext, sec, Math.Abs(qty), px, signalName, notes);
            }
        }

        /// <summary>
        /// Продать лимитником заданное количество по заданной цене.
        /// Допускается передача отрицательного количества. В этой ситуации будет выполнена покупка.
        /// </summary>
        /// <param name="externalContext">контекст вызывающего блока</param>
        /// <param name="sec">инструмент, полученный из context.Runtime.Securities</param>
        /// <param name="qty">при 0 команда игнорируется и ничего не происходит</param>
        /// <param name="px">цена</param>
        /// <param name="signalName">имя сигнала (для системы!)</param>
        /// <param name="notes">строковый комментарий для пользователя</param>
        public void SellAtPrice(IContext externalContext, ISecurity sec, double qty, double px, string signalName, string notes)
        {
            SellAtPrice(externalContext, sec, qty, px, signalName, notes, m_agregatePositions);
        }

        /// <summary>
        /// Продать лимитником заданное количество по заданной цене.
        /// Допускается передача отрицательного количества. В этой ситуации будет выполнена покупка.
        /// </summary>
        /// <param name="externalContext">контекст вызывающего блока</param>
        /// <param name="sec">инструмент, полученный из context.Runtime.Securities</param>
        /// <param name="qty">при 0 команда игнорируется и ничего не происходит</param>
        /// <param name="px">цена</param>
        /// <param name="signalName">имя сигнала (для системы!)</param>
        /// <param name="notes">строковый комментарий для пользователя</param>
        /// <param name="agregateRealPositions">агрегировать реальные сделки в разбивке по тикерам и направлениям</param>
        private void SellAtPrice(IContext externalContext, ISecurity sec, double qty, double px, string signalName, string notes,
            bool agregateRealPositions)
        {
            //int len = sec.Bars.Count;
            int len = externalContext.BarsCount;

            string extTradeName = NiceTradeName(externalContext.Runtime.TradeName);
            if (m_blockTrading)
            {
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked",
                    externalContext.Runtime.TradeName + ":" + MsgId);
                externalContext.Log(msg, MessageType.Warning, true);
                return;
            }

            {
#if DEBUG
                string msg = String.Format(CultureInfo.InvariantCulture,
                    "[{0}] SellAtPrice. Qty:{1}; px:{2}; sec:{3}; SignalName:{4}; Notes:{5}",
                    extTradeName + ":" + MsgId, qty, px, sec, signalName, notes);
#else
                string msg = String.Format(CultureInfo.InvariantCulture,
                    "[{0}] SellAtPrice. Qty:{1}; px:{2}; sec:{3}",
                    extTradeName + ":" + MsgId, qty, px, sec);

#endif
                externalContext.Log(msg, MessageType.Warning);
            }

            if (!m_portfolioReady)
            {
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.PortfolioIsNotReady",
                    Context.Runtime.TradeName + ":" + MsgId);
                Context.Log(msg, MessageType.Warning, true);
                return;
            }

            if (qty > 0)
            {
                string origSignalName;
                if (String.IsNullOrWhiteSpace(signalName))
                {
                    origSignalName = String.Empty;
                    signalName = String.Empty;
                }
                else
                {
                    origSignalName = signalName;
                    signalName += " ~ ";
                }
                signalName += DateTime.Now.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture);

                if (m_useVirtualPositions)
                {
                    #region Virtual positions
                    externalContext.Log(
                        String.Format(CultureInfo.InvariantCulture, "[{0}] VIRTUAL SELL {1} {2} @ {3}",
                            externalContext.Runtime.TradeName + ":" + MsgId, Math.Abs(qty), sec.Symbol, px),
                        MessageType.Info, true);

                    IPosition virtPos = null;
                    try
                    {
                        // Здесь будут отобраны виртуальные и среди них вторым проходом КОРОТКИЕ
                        virtPos = GetActiveForBar(sec, len, TotalProfitAlgo.VirtualPositions).SingleOrDefault(p => !p.IsLong);
                    }
                    catch (InvalidOperationException)
                    {
                        string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TooManyVirtualShortPositions",
                            externalContext.Runtime.TradeName + ":" + MsgId, sec.Symbol, len, sec);
                        externalContext.Log(msg, MessageType.Warning, true);
                        return;
                    }
                    
                    if (virtPos == null)
                    {
                        #region Виртуальной позы нет -- создаём
                        virtPos = sec.Positions.MakeVirtualPosition(len - VirtPosShift, -Math.Abs(qty), px, signalName, notes);
                        Tuple<SecInfo, PosInfo> tuple = new Tuple<SecInfo, PosInfo>(new SecInfo(sec.SecurityDescription), new PosInfo(virtPos));
                        var svp = ShortVirtualPositions;
                        svp.Add(tuple);

                        if (UseGlobalCache)
                        {
                            var container = new NotClearableContainer(svp);
                            externalContext.StoreGlobalObject(ShortGlobalKey, container, true);
                        }
                        #endregion Виртуальной позы нет -- создаём
                    }
                    else if (len - VirtPosShift > virtPos.EntryBarNum)
                    {
                        #region Виртуальная поза есть давно -- просто меняем среднюю цену и количество
                        if (virtPos.IsShort)
                        {
                            //double money = virtPos.EntryPrice * Math.Abs(virtPos.Shares);
                            double money = virtPos.AverageEntryPrice * Math.Abs(virtPos.Shares);
                            money += px * Math.Abs(qty);
                            double avgPx = money / (Math.Abs(virtPos.Shares) + Math.Abs(qty));
                            //virtPos.VirtualChange(len - VirtPosShift, avgPx, -Math.Abs(virtPos.Shares) - Math.Abs(qty), signalName, notes);
                            virtPos.VirtualChange(len - VirtPosShift, px, -Math.Abs(virtPos.Shares) - Math.Abs(qty), signalName, notes);

                            System.Diagnostics.Debug.Assert(DoubleUtil.AreClose(avgPx, virtPos.AverageEntryPrice),
                                "ActualPx:" + virtPos.AverageEntryPrice + "; Expected:" + avgPx);

                            // Сохраняем обновленное состояние позиции.
                            // К сожалению, для этого надо сохранить весь список.
                            if (UseGlobalCache)
                            {
                                var svp = ShortVirtualPositions;
                                var tuple = (from t in svp
                                             where t.Item1.Equals(sec.SecurityDescription)
                                             select t).SingleOrDefault();
                                if (tuple != null)
                                {
                                    svp.Remove(tuple);
                                    tuple = new Tuple<SecInfo, PosInfo>(tuple.Item1, new PosInfo(virtPos));
                                }
                                else
                                {
                                    SecInfo si = new SecInfo(sec.SecurityDescription);
                                    tuple = new Tuple<SecInfo, PosInfo>(si, new PosInfo(virtPos));
                                }
                                svp.Add(tuple);
                                var container = new NotClearableContainer(svp);
                                externalContext.StoreGlobalObject(ShortGlobalKey, container, true);
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                "[PositionsManager.SellAtPrice] В эту ветку не должны заходить! virtPos.IsShort:"
                                + virtPos.IsShort);
                        }
                        #endregion Виртуальная поза есть давно -- просто меняем среднюю цену и количество
                    }
                    else
                    {
                        #region Виртуальная поза есть, но создана в будущем -- создаём новую чуть раньше
                        if (virtPos.IsShort)
                        {
                            double money = virtPos.EntryPrice * Math.Abs(virtPos.Shares);
                            money += px * Math.Abs(qty);
                            double avgPx = money / (Math.Abs(virtPos.Shares) + Math.Abs(qty));

                            var svp = ShortVirtualPositions;
                            var tuple = (from t in svp
                                         where t.Item1.Equals(sec.SecurityDescription)
                                         select t).SingleOrDefault();
                            if (tuple != null)
                                svp.Remove(tuple);

                            virtPos = sec.Positions.MakeVirtualPosition(len - VirtPosShift, -Math.Abs(virtPos.Shares) - Math.Abs(qty), avgPx, signalName, notes);
                            tuple = new Tuple<SecInfo, PosInfo>(new SecInfo(sec.SecurityDescription), new PosInfo(virtPos));
                            svp.Add(tuple);

                            // Сохраняем обновленное состояние позиции.
                            // К сожалению, для этого надо сохранить весь список.
                            if (UseGlobalCache)
                            {
                                var container = new NotClearableContainer(svp);
                                externalContext.StoreGlobalObject(ShortGlobalKey, container, true);
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException("[PositionsManager.SellAtPrice] В эту ветку не должны заходить! virtPos.IsShort:" + virtPos.IsShort);
                        }
                        #endregion Виртуальная поза есть, но создана в будущем -- создаём новую чуть раньше
                    }
                    #endregion Virtual positions
                }
                else
                {
                    if (!sec.IsRealtime)
                    {
                        string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.RunAsAgent",
                            externalContext.Runtime.TradeName + ":" + MsgId, sec.IsRealtime);
                        externalContext.Log(msg, MessageType.Warning, true);
                        return;
                    }

                    DateTime lastBarTime = sec.Bars[len - 1].Date;
                    double intervalSec = externalContext.Runtime.IntervalInstance.ToSeconds();
                    if (m_checkSecurityTime && ((DateTime.Now - lastBarTime).TotalSeconds > 3 * intervalSec))
                    {
                        string msg = String.Format(CultureInfo.InvariantCulture,
                            RM.GetString("OptHandlerMsg.PositionsManager.BarIsOld"),
                            externalContext.Runtime.TradeName + ":" + MsgId, sec.Symbol, intervalSec,
                            lastBarTime.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture),
                            DateTime.Now.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture));
                        externalContext.Log(msg, MessageType.Warning, true);
                        return;
                    }

                    if (agregateRealPositions)
                    {
                        // Здесь будут отобраны РЕАЛЬНЫЕ и среди них вторым проходом КОРОТКИЕ
                        IPosition realPos = null;
                        var posList = GetActiveForBar(sec, len, TotalProfitAlgo.RealPositions).Where(p => !p.IsLong).ToList();
                        if (posList.Count >= 1)
                        {
                            realPos = posList[0];
                            //realPos = GetActiveForBar(sec, len, TotalProfitAlgo.RealPositions).FirstOrDefault(p => !p.IsLong);
                        }

                        // Вывожу предупреждение, если несмотря на режим агрегирования в агенте слишком много позиций
                        if (posList.Count > 1)
                        {
                            string msg = String.Format(CultureInfo.InvariantCulture,
                                RM.GetString("OptHandlerMsg.PositionsManager.TooManyShortPositions"),
                                externalContext.Runtime.TradeName + ":" + MsgId, sec, posList.Count);
                            externalContext.Log(msg, MessageType.Warning, false);
                        }

                        if (realPos == null)
                        {
                            // [2015-12-29] Если мы агрегируем позиции, но в рынке ещё не находимся,
                            // то скорее всего безопасно использовать первоначальное имя сигнала
                            // [2016-09-08] Хотя жалобы юзеров в GLSP-169 (ситуация воспроизвелась у меня сегодня на СмартКом)
                            // говорят об обратном.
                            //sec.Positions.SellAtPrice(len, Math.Abs(qty), px, signalName, notes);
                            // [2017-03-02] PROD-4980 - Было исправлено несколько важных вещей с частичным зафилом заявок. Восстанавливаю старую логику.
                            sec.Positions.SellAtPrice(len, Math.Abs(qty), px, origSignalName, notes);

#if DEBUG
                            string msg = String.Format(CultureInfo.InvariantCulture,
                                    "[{0}] SellAtPrice-SELL. Qty:{1}; px:{2}; sec:{3}; SignalName:{4}; Notes:{5}",
                                    extTradeName + ":" + MsgId, Math.Abs(qty), px, sec, signalName, notes);
#else
                            string msg = String.Format(CultureInfo.InvariantCulture,
                                    "[{0}] SellAtPrice-SELL. Qty:{1}; px:{2}; sec:{3}",
                                    extTradeName + ":" + MsgId, Math.Abs(qty), px, sec);
#endif
                            externalContext.Log(msg, MessageType.Warning);
                        }
                        else
                        {
                            realPos.ChangeAtPrice(len, px, -Math.Abs(realPos.Shares) - Math.Abs(qty), realPos.EntrySignalName, notes);

#if DEBUG
                            string msg = String.Format(CultureInfo.InvariantCulture,
                                "[{0}] ChangeAtPrice-SELL. Qty:{1}; px:{2}; sec:{3}; SignalName:{4}; New notes:{5}",
                                extTradeName + ":" + MsgId, -Math.Abs(realPos.Shares) - Math.Abs(qty), px, sec, realPos.EntrySignalName, notes);
#else
                            string msg = String.Format(CultureInfo.InvariantCulture,
                                "[{0}] ChangeAtPrice-SELL. Qty:{1}; px:{2}; sec:{3}",
                                extTradeName + ":" + MsgId, -Math.Abs(realPos.Shares) - Math.Abs(qty), px, sec);
#endif
                            externalContext.Log(msg, MessageType.Warning);
                        }
                    }
                    else
                    {
                        sec.Positions.SellAtPrice(len, Math.Abs(qty), px, signalName, notes);
                    }

                    //externalContext.Log(
                    //    String.Format("[{0}] SELL {1} {2} @ {3}. BarNum:{4}; sec.Bars.Count:{5}; IsLastBarUsed:{6}",
                    //        MsgId, Math.Abs(qty), sec.Symbol, px, len, sec.Bars.Count, externalContext.IsLastBarUsed),
                    //    MessageType.Warning, true);
                }
            }
            else if (qty < 0)
            {
                BuyAtPrice(externalContext, sec, Math.Abs(qty), px, signalName, notes);
            }
        }

        public void BuyVolatility(IContext externalContext, IOptionStrike sec,
            double qty, QuoteIvMode quoteMode, double iv, string signalName, string notes)
        {
            BuyVolatility(externalContext, sec, qty, quoteMode, iv, 0, signalName, notes);
        }

        public void BuyVolatility(IContext externalContext, IOptionStrike sec,
            double qty, QuoteIvMode quoteMode, double iv, int shiftPrice, string signalName, string notes)
        {
            int len = externalContext.BarsCount;

            string extTradeName = NiceTradeName(externalContext.Runtime.TradeName);
            if (m_blockTrading)
            {
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked",
                    externalContext.Runtime.TradeName + ":" + MsgId);
                externalContext.Log(msg, MessageType.Warning, true);
                return;
            }

            {
                string ivLabel = (quoteMode == QuoteIvMode.Relative) ? "rIV" : "IV";
#if DEBUG
                string msg = String.Format(CultureInfo.InvariantCulture,
                    "[{0}] BuyVolatility. Qty:{1}; {2}:{3:P2}+{4}; sec:{5}; SignalName:{6}; Notes:{7}",
                    extTradeName + ":" + MsgId, qty, ivLabel, iv, sec, shiftPrice, signalName, notes);
#else
                string msg = String.Format(CultureInfo.InvariantCulture,
                    "[{0}] BuyVolatility. Qty:{1}; {2}:{3:P2}+{4}; sec:{5}",
                    extTradeName + ":" + MsgId, qty, ivLabel, iv, sec, shiftPrice);
#endif
                externalContext.Log(msg, MessageType.Warning);
            }

            if (!m_portfolioReady)
            {
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.PortfolioIsNotReady",
                    Context.Runtime.TradeName + ":" + MsgId);
                Context.Log(msg, MessageType.Warning, true);
                return;
            }

            if (qty > 0)
            {
                var longTargets = LongIvTargets;
                try
                {
                    // Здесь будут отобраны длинные с заданным страйком
                    var tmp = longTargets.SingleOrDefault(tuple => tuple.Item1.Equals(sec));
                    if (tmp != null)
                        longTargets.Remove(tmp); // Сразу удаляем
                }
                catch (InvalidOperationException)
                {
                    string msg = String.Format("[{0}] Too many LONG IvTargets in security {1}",
                        externalContext.Runtime.TradeName + ":" + MsgId, sec);
                    externalContext.Log(msg, MessageType.Warning, true);
                    return;
                }

                {
                    #region Cоздаём котировку
                    //virtPos = sec.Positions.MakeVirtualPosition(len - VirtPosShift, -Math.Abs(qty), px, signalName, notes);
                    const bool IsLong = true;
                    double totalQty = GetTotalQty(sec.Security, len, TotalProfitAlgo.AllPositions, IsLong);
                    double targetQty = totalQty + qty;

                    SecInfo secInfo = new SecInfo(sec.FinInfo.Security);
                    IvTargetInfo ivTarget = new IvTargetInfo(IsLong, targetQty, quoteMode, iv, shiftPrice, signalName, notes);
                    ivTarget.SecInfo = secInfo;
                    Tuple<SecInfo, IvTargetInfo> tuple = new Tuple<SecInfo, IvTargetInfo>(secInfo, ivTarget);
                    longTargets.Add(tuple);

                    var container = new NotClearableContainer(longTargets);
                    Context.StoreObject(m_longIvTargetsCashKey, container, false);

                    //if (UseGlobalCache)
                    //    externalContext.StoreObject(...);
                    #endregion Cоздаём котировку
                }
            }
            else if (qty < 0)
            {
                SellVolatility(externalContext, sec, Math.Abs(qty), quoteMode, iv, shiftPrice, signalName, notes);
            }
        }

        public void SellVolatility(IContext externalContext, IOptionStrike sec,
            double qty, QuoteIvMode quoteMode, double iv, string signalName, string notes)
        {
            SellVolatility(externalContext, sec, qty, quoteMode, iv, 0, signalName, notes);
        }

        public void SellVolatility(IContext externalContext, IOptionStrike sec,
            double qty, QuoteIvMode quoteMode, double iv, int shiftPrice, string signalName, string notes)
        {
            int len = externalContext.BarsCount;

            string extTradeName = NiceTradeName(externalContext.Runtime.TradeName);
            if (m_blockTrading)
            {
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked",
                    extTradeName + ":" + MsgId);
                externalContext.Log(msg, MessageType.Warning, true);
                return;
            }

            {
                string ivLabel = (quoteMode == QuoteIvMode.Relative) ? "rIV" : "IV";
#if DEBUG
                string msg = String.Format(CultureInfo.InvariantCulture,
                    "[{0}] SellVolatility. Qty:{1}; {2}:{3:P2}; sec:{4}; SignalName:{5}; Notes:{6}",
                    extTradeName + ":" + MsgId, qty, ivLabel, iv, sec, signalName, notes);
#else
                string msg = String.Format(CultureInfo.InvariantCulture,
                    "[{0}] SellVolatility. Qty:{1}; {2}:{3:P2}; sec:{4}",
                    extTradeName + ":" + MsgId, qty, ivLabel, iv, sec);
#endif
                externalContext.Log(msg, MessageType.Warning);
            }

            if (!m_portfolioReady)
            {
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.PortfolioIsNotReady",
                    Context.Runtime.TradeName + ":" + MsgId);
                Context.Log(msg, MessageType.Warning, true);
                return;
            }

            if (qty > 0)
            {
                var shortTargets = ShortIvTargets;
                try
                {
                    // Здесь будут отобраны длинные с заданным страйком
                    var tmp = shortTargets.SingleOrDefault(tuple => tuple.Item1.Equals(sec));
                    if (tmp != null)
                        shortTargets.Remove(tmp); // Сразу удаляем
                }
                catch (InvalidOperationException)
                {
                    string msg = String.Format("[{0}] Too many SHORT IvTargets in security {1}",
                        externalContext.Runtime.TradeName + ":" + MsgId, sec);
                    externalContext.Log(msg, MessageType.Warning, true);
                    return;
                }

                {
                    #region Cоздаём котировку
                    //virtPos = sec.Positions.MakeVirtualPosition(len - VirtPosShift, -Math.Abs(qty), px, signalName, notes);
                    const bool IsLong = false;
                    double totalQty = GetTotalQty(sec.Security, len, TotalProfitAlgo.AllPositions, IsLong);
                    double targetQty = totalQty + qty;

                    SecInfo secInfo = new SecInfo(sec.FinInfo.Security);
                    IvTargetInfo ivTarget = new IvTargetInfo(IsLong, targetQty, quoteMode, iv, shiftPrice, signalName, notes);
                    ivTarget.SecInfo = secInfo;
                    Tuple<SecInfo, IvTargetInfo> tuple = new Tuple<SecInfo, IvTargetInfo>(secInfo, ivTarget);
                    shortTargets.Add(tuple);

                    var container = new NotClearableContainer(shortTargets);
                    Context.StoreObject(m_shortIvTargetsCashKey, container, false);

                    //if (UseGlobalCache)
                    //    externalContext.StoreObject(...);
                    #endregion Cоздаём котировку
                }
            }
            else if (qty < 0)
            {
                BuyVolatility(externalContext, sec, Math.Abs(qty), quoteMode, iv, shiftPrice, signalName, notes);
            }
        }

        public ReadOnlyCollection<IvTargetInfo> GetIvTargets(bool isLong, bool sortByStrike = false)
        {
            List<IvTargetInfo> res;
            if (sortByStrike)
            {
                if (isLong)
                    res = (from tuple in LongIvTargets orderby tuple.Item1.Strike ascending select tuple.Item2).ToList();
                else
                    res = (from tuple in ShortIvTargets orderby tuple.Item1.Strike ascending select tuple.Item2).ToList();
            }
            else
            {
                if (isLong)
                    res = (from tuple in LongIvTargets select tuple.Item2).ToList();
                else
                    res = (from tuple in ShortIvTargets select tuple.Item2).ToList();
            }

            return new ReadOnlyCollection<IvTargetInfo>(res);
        }

        public ReadOnlyCollection<IPosition> GetClosedOrActiveForBar(ISecurity sec,
            TotalProfitAlgo profitAlgo = TotalProfitAlgo.AllPositions)
        {
            // Все виртуальные позиции уже восстановлены?
            if (!m_executed)
            {
                string msg = String.Format("[{0}] Not executed???", Context.Runtime.TradeName + ":" + MsgId + ".GetClosedOrActiveForBar");
                Context.Log(msg, MessageType.Error, true);
            }

            if (sec == null)
                return Constants.EmptyListPositions;

            if (sec.Positions.HavePositions)
            {
                List<IPosition> positions;
                if (profitAlgo == TotalProfitAlgo.AllPositions)
                    positions = (from p in sec.Positions.GetActiveForBar(Context.BarsCount) select p).ToList();
                else if (profitAlgo == TotalProfitAlgo.RealPositions)
                    positions = (from p in sec.Positions.GetActiveForBar(Context.BarsCount) where (!p.IsVirtual) select p).ToList();
                else if (profitAlgo == TotalProfitAlgo.VirtualPositions)
                    positions = (from p in sec.Positions.GetActiveForBar(Context.BarsCount) where p.IsVirtual select p).ToList();
                else
                {
                    string msg = String.Format("[{0}] Profit algo '{1}' is not implemented.", MsgId, profitAlgo);
                    throw new NotImplementedException(msg);
                }

                return new ReadOnlyCollection<IPosition>(positions);
            }
            else
            {
                return Constants.EmptyListPositions;
            }
        }

        public ReadOnlyCollection<IPosition> GetClosedOrActiveForBar(ISecurity sec, int barNum,
            TotalProfitAlgo profitAlgo = TotalProfitAlgo.AllPositions)
        {
            // Все виртуальные позиции уже восстановлены?
            if (!m_executed)
            {
                string msg = String.Format("[{0}] Not executed??? barNum:{1}",
                    Context.Runtime.TradeName + ":" + MsgId + ".GetClosedOrActiveForBar", barNum);
                Context.Log(msg, MessageType.Error, true);
            }

            if (sec == null)
                return Constants.EmptyListPositions;

            if (sec.Positions.HavePositions)
            {
                List<IPosition> positions;
                if (profitAlgo == TotalProfitAlgo.AllPositions)
                    positions = (from p in sec.Positions.GetClosedOrActiveForBar(barNum) select p).ToList();
                else if (profitAlgo == TotalProfitAlgo.RealPositions)
                    positions = (from p in sec.Positions.GetClosedOrActiveForBar(barNum) where (!p.IsVirtual) select p).ToList();
                else if (profitAlgo == TotalProfitAlgo.VirtualPositions)
                    positions = (from p in sec.Positions.GetClosedOrActiveForBar(barNum) where p.IsVirtual select p).ToList();
                else
                {
                    string msg = String.Format("[{0}] Profit algo '{1}' is not implemented.", MsgId, profitAlgo);
                    throw new NotImplementedException(msg);
                }

                return new ReadOnlyCollection<IPosition>(positions);
            }
            else
            {
                return Constants.EmptyListPositions;
            }
        }

        public ReadOnlyCollection<IPosition> GetActiveForBar(ISecurity sec,
            TotalProfitAlgo profitAlgo = TotalProfitAlgo.AllPositions)
        {
            // Все виртуальные позиции уже восстановлены?
            if (!m_executed)
            {
                string msg = String.Format("[{0}] Not executed???", Context.Runtime.TradeName + ":" + MsgId + ".GetActiveForBar");
                Context.Log(msg, MessageType.Error, true);
            }

            if (sec == null)
                return Constants.EmptyListPositions;

            if (sec.Positions.HavePositions)
            {
                List<IPosition> positions;
                if (profitAlgo == TotalProfitAlgo.AllPositions)
                    positions = (from p in sec.Positions.GetActiveForBar(Context.BarsCount) select p).ToList();
                else if (profitAlgo == TotalProfitAlgo.RealPositions)
                    positions = (from p in sec.Positions.GetActiveForBar(Context.BarsCount) where (!p.IsVirtual) select p).ToList();
                else if (profitAlgo == TotalProfitAlgo.VirtualPositions)
                    positions = (from p in sec.Positions.GetActiveForBar(Context.BarsCount) where p.IsVirtual select p).ToList();
                else
                {
                    string msg = String.Format("[{0}] Profit algo '{1}' is not implemented.", MsgId, profitAlgo);
                    throw new NotImplementedException(msg);
                }

                return new ReadOnlyCollection<IPosition>(positions);
            }
            else
            {
                return Constants.EmptyListPositions;
            }
        }

        public ReadOnlyCollection<IPosition> GetActiveForBar(ISecurity sec, int barNum,
            TotalProfitAlgo profitAlgo = TotalProfitAlgo.AllPositions)
        {
            // Все виртуальные позиции уже восстановлены?
            if (!m_executed)
            {
                string msg = String.Format("[{0}] Not executed??? barNum:{1}",
                    Context.Runtime.TradeName + ":" + MsgId + ".GetActiveForBar", barNum);
                Context.Log(msg, MessageType.Error, true);
            }

            ReadOnlyCollection<IPosition> positions = PositionsManager.GetActiveForBar(sec, barNum, profitAlgo, null);
            return positions;
        }

        public static ReadOnlyCollection<IPosition> GetActiveForBar(ISecurity sec, int barNum,
            TotalProfitAlgo profitAlgo, bool? isLong)
        {
            if (sec == null)
                return Constants.EmptyListPositions;

            if (sec.Positions.HavePositions)
            {
                List<IPosition> positions;
                if (profitAlgo == TotalProfitAlgo.AllPositions)
                    positions = (from p in sec.Positions.GetActiveForBar(barNum) select p).ToList();
                else if (profitAlgo == TotalProfitAlgo.RealPositions)
                    positions = (from p in sec.Positions.GetActiveForBar(barNum) where (!p.IsVirtual) select p).ToList();
                else if (profitAlgo == TotalProfitAlgo.VirtualPositions)
                    positions = (from p in sec.Positions.GetActiveForBar(barNum) where p.IsVirtual select p).ToList();
                else
                {
                    string msg = String.Format("[{0}] Profit algo '{1}' is not implemented.", MsgId, profitAlgo);
                    throw new NotImplementedException(msg);
                }

                if (isLong != null)
                    positions = (from p in positions where (p.IsLong == isLong.Value) select p).ToList();

                return new ReadOnlyCollection<IPosition>(positions);
            }
            else
            {
                return Constants.EmptyListPositions;
            }
        }

        /// <summary>
        /// Полный суммарный открытый объём в данном инструменте
        /// </summary>
        /// <param name="sec">инструмент</param>
        /// <param name="barNum">индекс бара</param>
        /// <returns>суммарный объём (в лотах)</returns>
        public double GetTotalQty(ISecurity sec, int barNum)
        {
            double res = GetTotalQty(sec, barNum, TotalProfitAlgo.AllPositions);
            return res;
        }

        /// <summary>
        /// Полный суммарный открытый объём в данном инструменте
        /// </summary>
        /// <param name="sec">инструмент</param>
        /// <param name="barNum">индекс бара</param>
        /// <param name="profitAlgo">алгоритм расчета</param>
        /// <returns>суммарный объём (в лотах)</returns>
        public double GetTotalQty(ISecurity sec, int barNum, TotalProfitAlgo profitAlgo)
        {
            double res = 0;
            ReadOnlyCollection<IPosition> positions = GetActiveForBar(sec, barNum, profitAlgo);
            for (int j = 0; j < positions.Count; j++)
            {
                IPosition pos = positions[j];
                int sign = pos.IsLong ? 1 : -1;
                double qty = Math.Abs(pos.Shares);
                res += sign * qty;
            }
            return res;
        }

        /// <summary>
        /// Полный суммарный открытый объём в данном инструменте в позициях указанного направления
        /// </summary>
        /// <param name="sec">инструмент</param>
        /// <param name="barNum">индекс бара</param>
        /// <param name="profitAlgo">алгоритм расчета</param>
        /// <param name="isLong">обрабатывать длинные или короткие позиции?</param>
        /// <returns>суммарный объём (в лотах); всегда неотрицательный</returns>
        public double GetTotalQty(ISecurity sec, int barNum, TotalProfitAlgo profitAlgo, bool isLong)
        {
            // Все виртуальные позиции уже восстановлены?
            if (!m_executed)
            {
                string msg = String.Format("[{0}] Not executed??? barNum:{1}",
                    Context.Runtime.TradeName + ":" + MsgId + ".GetActiveForBar", barNum);
                Context.Log(msg, MessageType.Error, true);
            }

            double res = 0;
            ReadOnlyCollection<IPosition> positions = PositionsManager.GetActiveForBar(sec, barNum, profitAlgo, isLong);
            for (int j = 0; j < positions.Count; j++)
            {
                IPosition pos = positions[j];
                if (pos.IsLong == isLong)
                {
                    double qty = Math.Abs(pos.Shares);
                    res += qty;
                }
                else
                    throw new InvalidOperationException("Метод PositionsManager.GetActiveForBar (выше по коду) должен был вернуть позиции только правильного направления!");
            }
            return res;
        }

        /// <summary>
        /// Оценка суммарного профита по всем позициям в данном инструменте
        /// (вместе реальные и виртуальные позиции)
        /// </summary>
        /// <param name="sec">инструмент</param>
        /// <param name="barNum">индекс бара</param>
        /// <param name="secPrice">текущая цена инструмента</param>
        /// <param name="cash">денежные затраты на формирование позы (могут быть отрицательными)</param>
        /// <param name="pnl">текущая цена позиции</param>
        /// <returns>(cash+pnl) -- текущий суммарный плавающий профит в данном инструменте</returns>
        public double GetTotalProfit(ISecurity sec, int barNum,
            double secPrice, out double cash, out double pnl)
        {
            double res = GetTotalProfit(sec, barNum, TotalProfitAlgo.AllPositions, secPrice, out cash, out pnl);
            return res;
        }

        /// <summary>
        /// Оценка суммарного профита по всем позициям в данном инструменте
        /// </summary>
        /// <param name="sec">инструмент</param>
        /// <param name="barNum">индекс бара</param>
        /// <param name="profitAlgo">алгоритм расчета</param>
        /// <param name="secPrice">текущая цена инструмента</param>
        /// <param name="cash">денежные затраты на формирование позы (могут быть отрицательными)</param>
        /// <param name="pnl">текущая цена позиции</param>
        /// <returns>(cash+pnl) -- текущий суммарный плавающий профит в данном инструменте</returns>
        public double GetTotalProfit(ISecurity sec, int barNum,
            TotalProfitAlgo profitAlgo, double secPrice, out double cash, out double pnl)
        {
            cash = 0;
            pnl = 0;
            ReadOnlyCollection<IPosition> positions = GetClosedOrActiveForBar(sec, barNum, profitAlgo);
            int len = positions.Count;
            for (int j = 0; j < len; j++)
            {
                IPosition pos = positions[j];
                // Пока что State лучше не трогать 
                //if (pos.PositionState == PositionState.HaveError)
                {
                    int sign = pos.IsLong ? 1 : -1;
                    double qty = Math.Abs(pos.Shares);
                    // Знак "минус" стоит в честь того, что при покупке инструмента наличные средства уменьшаются
                    //cash -= sign * pos.EntryPrice * qty;
                    cash -= sign * pos.GetBalancePrice(barNum) * qty;
                    //pnl += sign * (f - pos.EntryPrice) * qty;
                    pnl += sign * secPrice * qty;

                    // Учет комиссии
                    cash -= pos.EntryCommission;

                    if (!pos.IsActiveForBar(barNum))
                    {
                        // Знак "ПЛЮС" стоит в честь того, что при ЗАКРЫТИИ ЛОНГА наличные средства УВЕЛИЧИВАЮТСЯ
                        cash += sign * pos.ExitPrice * qty;
                        //pnl -= sign * (f - pos.ExitPrice) * qty;
                        pnl -= sign * secPrice * qty;

                        // Учет комиссии
                        cash -= pos.ExitCommission;
                    }
                }
            }

            double res = cash + pnl;
            return res;
        }

        /// <summary>
        /// Безусловно и немедленно удалить все виртуальные позиции по всем инструментам
        /// </summary>
        /// <returns>количество удаленных позиций</returns>
        public int DropVirtualPositions(IContext externalContext)
        {
            // Из практики торговли часто бывает ситуация, что торговля заблокирована, а удалить виртуальные позиции УЖЕ хочется.
            //if (m_blockTrading)
            //{
            //    string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked"),
            //        externalContext.Runtime.TradeName + ":" + MsgId);
            //    externalContext.Log(msg, MessageType.Warning, true);
            //    return 0;
            //}

            int res = 0;
            {
                var virtualPositions = LongVirtualPositions;
                res += virtualPositions.Count;
                virtualPositions.Clear();

                // Сохраняем обновленное состояние позиции.
                // К сожалению, для этого надо сохранить весь список.
                if (UseGlobalCache)
                {
                    var container = new NotClearableContainer(virtualPositions);
                    externalContext.StoreGlobalObject(LongGlobalKey, container, true);
                }
            }

            {
                var virtualPositions = ShortVirtualPositions;
                res += virtualPositions.Count;
                virtualPositions.Clear();

                // Сохраняем обновленное состояние позиции.
                // К сожалению, для этого надо сохранить весь список.
                if (UseGlobalCache)
                {
                    var container = new NotClearableContainer(virtualPositions);
                    externalContext.StoreGlobalObject(ShortGlobalKey, container, true);
                }
            }

            return res;
        }

        /// <summary>
        /// Безусловно и немедленно удалить все длинные лимитные котировки по всем инструментам
        /// </summary>
        /// <returns>количество удаленных котировок</returns>
        public int DropAllLongIvTargets(IContext externalContext)
        {
            // Из практики торговли часто бывает ситуация, что торговля заблокирована, а снять задачу котирования УЖЕ хочется.
            //if (m_blockTrading)
            //{
            //    string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked"),
            //        externalContext.Runtime.TradeName + ":" + MsgId);
            //    externalContext.Log(msg, MessageType.Warning, true);
            //    return 0;
            //}

            int res = 0;
            {
                var longTargets = LongIvTargets;
                res += longTargets.Count;
                longTargets.Clear();

                // Сохраняем обновленное состояние позиции.
                // К сожалению, для этого надо сохранить весь список.
                //if (UseGlobalCache)
                //    externalContext.StoreGlobalObject(LongGlobalKey, virtualPositions, true);
                //externalContext.StoreObject(...);
            }

            return res;
        }

        /// <summary>
        /// Безусловно и немедленно удалить все короткие лимитные котировки по всем инструментам
        /// </summary>
        /// <returns>количество удаленных котировок</returns>
        public int DropAllShortIvTargets(IContext externalContext)
        {
            // Из практики торговли часто бывает ситуация, что торговля заблокирована, а снять задачу котирования УЖЕ хочется.
            //if (m_blockTrading)
            //{
            //    string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked"),
            //        externalContext.Runtime.TradeName + ":" + MsgId);
            //    externalContext.Log(msg, MessageType.Warning, true);
            //    return 0;
            //}

            int res = 0;
            {
                var shortTargets = ShortIvTargets;
                res += shortTargets.Count;
                shortTargets.Clear();

                // Сохраняем обновленное состояние позиции.
                // К сожалению, для этого надо сохранить весь список.
                //if (UseGlobalCache)
                //    externalContext.StoreGlobalObject(ShortGlobalKey, virtualPositions, true);
                //externalContext.StoreObject(...);
            }

            return res;
        }

        /// <summary>
        /// Безусловно и немедленно удалить все лимитные котировки по всем инструментам
        /// </summary>
        /// <returns>количество удаленных котировок</returns>
        public int DropAllIvTargets(IContext externalContext)
        {
            // Из практики торговли часто бывает ситуация, что торговля заблокирована, а снять задачу котирования УЖЕ хочется.
            //if (m_blockTrading)
            //{
            //    string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked"),
            //        externalContext.Runtime.TradeName + ":" + MsgId);
            //    externalContext.Log(msg, MessageType.Warning, true);
            //    return 0;
            //}

            int res = 0;
            res += DropAllLongIvTargets(externalContext);
            res += DropAllShortIvTargets(externalContext);

            return res;
        }

        internal int CancelVolatility(IContext externalContext, IvTargetInfo ivTarget, string comment)
        {
            // Из практики торговли часто бывает ситуация, что торговля заблокирована, а снять задачу котирования УЖЕ хочется.
            //if (m_blockTrading)
            //{
            //    string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked"),
            //        externalContext.Runtime.TradeName + ":" + MsgId);
            //    externalContext.Log(msg, MessageType.Warning, true);
            //    return 0;
            //}

            if (ivTarget == null)
                return 0;

            int res = 0;
            var ivTargets = ivTarget.IsLong ? LongIvTargets : ShortIvTargets;
            for (int j = ivTargets.Count - 1; j >= 0; j--)
            {
                // TODO: наверно, хорошо бы добавить более точное сравнение...
                if (ivTarget.SecInfo.FullName.Equals(ivTargets[j].Item1.FullName, StringComparison.InvariantCultureIgnoreCase))
                {
                    ivTargets.RemoveAt(j);
                    res++;
                }
            }

            return res;
        }
    }
}
