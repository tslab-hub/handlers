using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Quote volatility (special for Deribit)
    /// \~russian Котирование волатильности (c учетом необходимости домножить цены на курс конвертации)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTickDeribit)]
    [HelperName("Quote volatility (Deribit)", Language = Constants.En)]
    [HelperName("Котирование волатильности (Deribit)", Language = Constants.Ru)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(1, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(2, TemplateTypes.DOUBLE, Name = "ScaleMultiplier" /* Constants.Scale */)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Котирование волатильности (c учетом необходимости домножить цены на курс конвертации)")]
    [HelperDescription("Quote volatility (special for Deribit)", Constants.En)]
    public class QuoteIvDeribit : BaseContextHandler, IValuesHandlerWithNumber, ICustomListValues, IDisposable
    {
        private const string DefaultStrike = "120 000";
        private const string StrikeFormat = "### ### ##0.### ###";

        private int m_qty = 0;
        private double m_shiftIv = 0;
        private int m_shiftPriceStep = 0;
        private double m_strikeStep = 0;
        private string m_strike = DefaultStrike;
        private StrikeType m_optionType = StrikeType.Any;
        private bool m_executeCommand = false;
        private bool m_cancelAllLong = false;
        private bool m_cancelAllShort = false;

        private InteractiveSeries m_clickableSeries;

        /// <summary>
        /// Множество опционных страйков в локальном кеше кубика
        /// </summary>
        private HashSet<string> StrikeList
        {
            get
            {
                HashSet<string> strikeList = Context.LoadObject(VariableId + "_strikeList") as HashSet<string>;
                if (strikeList == null)
                {
                    strikeList = new HashSet<string> { DefaultStrike };
                    Context.StoreObject(VariableId + "_strikeList", strikeList);
                }

                if (strikeList.Count == 0)
                    strikeList.Add(DefaultStrike);

                return strikeList;
            }
        }

        #region Parameters
        /// <summary>
        /// \~english Option strike
        /// \~russian Страйк опциона
        /// </summary>
        [HelperName("Strike", Constants.En)]
        [HelperName("Страйк", Constants.Ru)]
        [Description("Страйк опциона")]
        [HelperDescription("Option strike", Constants.En)]
        [HandlerParameter(Default = DefaultStrike)]
        // ReSharper disable once UnusedMember.Global
        public string Strike
        {
            get { return m_strike; }
            set { m_strike = value; }
        }

        /// <summary>
        /// \~english Option type (when Any, the handler will choose out-of-the-money security)
        /// \~russian Тип опционов (Any предполагает автоматический выбор, чтобы котировать всегда опционы вне-денег)
        /// </summary>
        [HelperName("Option Type", Constants.En)]
        [HelperName("Вид опционов", Constants.Ru)]
        [Description("Тип опционов (Any предполагает автоматический выбор, чтобы котировать всегда опционы вне-денег)")]
        [HelperDescription("Option type (when Any, the handler will choose out-of-the-money security)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Any")]
        public StrikeType OptionType
        {
            get { return m_optionType; }
            set { m_optionType = value; }
        }

        ///// <summary>
        ///// \~english Quote type (ask or bid)
        ///// \~russian Тип котировки (аск или бид)
        ///// </summary>
        //[HelperName("Quote Type", Constants.En)]
        //[HelperName("Тип котировки", Constants.Ru)]
        //[Description("Тип котировки (аск или бид)")]
        //[HelperDescription("Quote type (ask or bid)", Language = Constants.En)]
        //[HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "Ask")]
        //public OptionPxMode OptPxMode
        //{
        //    get { return m_optionPxMode; }
        //    set { m_optionPxMode = value; }
        //}

        ///// <summary>
        ///// \~english Buy or sell
        ///// \~russian Покупка или продажа
        ///// </summary>
        //[HelperName("Buy", Constants.En)]
        //[HelperName("Покупка", Constants.Ru)]
        //[Description("Покупка или продажа")]
        //[HelperDescription("Buy or sell", Language = Constants.En)]
        //[HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "true")]
        //public bool IsBuy
        //{
        //    get { return m_isBuy; }
        //    set { m_isBuy = value; }
        //}

        /// <summary>
        /// \~english Execute
        /// \~russian Исполнить
        /// </summary>
        [HelperName("Execute", Constants.En)]
        [HelperName("Исполнить", Constants.Ru)]
        [Description("Исполнить")]
        [HelperDescription("Execute", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool ExecuteCommand
        {
            get { return m_executeCommand; }
            set { m_executeCommand = value; }
        }

        /// <summary>
        /// \~english Cancel long quotes in all strikes
        /// \~russian Отменить котирование покупок во всех страйках
        /// </summary>
        [HelperName("Cancel all long", Constants.En)]
        [HelperName("Отменить все покупки", Constants.Ru)]
        [Description("Отменить котирование покупок во всех страйках")]
        [HelperDescription("Cancel long quotes in all strikes", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool CancelAllLong
        {
            get { return m_cancelAllLong; }
            set { m_cancelAllLong = value; }
        }

        /// <summary>
        /// \~english Cancel short quotes in all strikes
        /// \~russian Отменить котирование продаж во всех страйках
        /// </summary>
        [HelperName("Cancel all short", Constants.En)]
        [HelperName("Отменить все продажи", Constants.Ru)]
        [Description("Отменить котирование продаж во всех страйках")]
        [HelperDescription("Cancel short quotes in all strikes", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool CancelAllShort
        {
            get { return m_cancelAllShort; }
            set { m_cancelAllShort = value; }
        }

        /// <summary>
        /// \~english Trade quantity. Negative value reverts the signal.
        /// \~russian Торговый объём. Отрицательное значение приведет к перевороту сигнала.
        /// </summary>
        [HelperName("Size", Constants.En)]
        [HelperName("Кол-во", Constants.Ru)]
        [Description("Торговый объём. Отрицательное значение приведет к перевороту сигнала.")]
        [HelperDescription("Trade quantity. Negative value reverts the signal.", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-1000000", Max = "1000000", Step = "1")]
        public int Qty
        {
            get { return m_qty; }
            set { m_qty = value; }
        }

        /// <summary>
        /// \~english Shift quote relative to the smile (in percents of volatility)
        /// \~russian Сдвиг котировки относительно улыбки (в процентах волатильности)
        /// </summary>
        [HelperName("Shift IV", Constants.En)]
        [HelperName("Сдвиг волатильности", Constants.Ru)]
        [Description("Сдвиг котировки относительно улыбки (в процентах волатильности)")]
        [HelperDescription("Shift quote relative to the smile (in percents of volatility)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-10000000", Max = "10000000", Step = "1")]
        public double ShiftIvPct
        {
            get { return m_shiftIv * Constants.PctMult; }
            set { m_shiftIv = value / Constants.PctMult; }
        }

        /// <summary>
        /// \~english Shift quote relative to the shifted smile (in price steps)
        /// \~russian Сдвиг котировки относительно сдвинутой улыбки (в шагах цены)
        /// </summary>
        [HelperName("Shift price", Constants.En)]
        [HelperName("Сдвиг цены", Constants.Ru)]
        [Description("Сдвиг котировки относительно сдвинутой улыбки (в шагах цены)")]
        [HelperDescription("Shift quote relative to the shifted smile (in price steps)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-10000000", Max = "10000000", Step = "1")]
        public int ShiftPrice
        {
            get { return m_shiftPriceStep; }
            set { m_shiftPriceStep = value; }
        }

        /// <summary>
        /// \~english Strike step to extract most important options
        /// \~russian Шаг страйков для выделения главных подсерий
        /// </summary>
        [HelperName("Strike step", Constants.En)]
        [HelperName("Шаг страйков", Constants.Ru)]
        [HandlerParameter(Name = "Strike Step", NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "0", Max = "10000000", Step = "1")]
        [Description("Шаг страйков для выделения главных подсерий")]
        [HelperDescription("Strike step to extract most important options", Constants.En)]
        public double StrikeStep
        {
            get { return m_strikeStep; }
            set { m_strikeStep = value; }
        }
        #endregion

        /// <summary>
        /// Метод под флаг TemplateTypes.INTERACTIVESPLINE
        /// </summary>
        public InteractiveSeries Execute(InteractiveSeries smile, IOptionSeries optSer, double scaleMult, int barNum)
        {
            if ((smile == null) || (optSer == null))
                return Constants.EmptySeries;

            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if ((oldInfo == null) || (oldInfo.ContinuousFunction == null))
                return Constants.EmptySeries;

            double futPx = oldInfo.F;
            double dT = oldInfo.dT;

            if (m_executeCommand)
            {
                string msg = String.Format("[{0}.StartButton] Strike: {1}", GetType().Name, m_strike);
                m_context.Log(msg, MessageType.Info, false);
            }

            #region 1. Список страйков
            HashSet<string> serList = StrikeList;
            serList.Clear();

            IOptionStrikePair[] pairs;
            if (Double.IsNaN(m_strikeStep) || (m_strikeStep <= Double.Epsilon))
            {
                pairs = optSer.GetStrikePairs().ToArray();
            }
            else
            {
                // Выделяем страйки, которые нацело делятся на StrikeStep
                pairs = (from p in optSer.GetStrikePairs()
                         let test = m_strikeStep * Math.Round(p.Strike / m_strikeStep)
                         where DoubleUtil.AreClose(p.Strike, test)
                         select p).ToArray();

                // [2015-12-24] Если шаг страйков по ошибке задан совершенно неправильно,
                // то в коллекцию ставим все имеющиеся страйки.
                // Пользователь потом разберется
                if (pairs.Length <= 0)
                    pairs = optSer.GetStrikePairs().ToArray();
            }
            //if (pairs.Length < 2)
            //    return Constants.EmptyListDouble;

            foreach (IOptionStrikePair pair in pairs)
            {
                double k = pair.Strike;
                serList.Add(k.ToString(StrikeFormat, CultureInfo.InvariantCulture));
            }
            #endregion 1. Список страйков

            InteractiveSeries res = Constants.EmptySeries;
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();

            #region 2. Формируем улыбку просто для отображения текущего положения потенциальной котировки
            // При нулевом рабочем объёме не утруждаемся рисованием лишних линий
            if (!DoubleUtil.IsZero(m_qty))
            {
                for (int j = 0; j < pairs.Length; j++)
                {
                    var pair = pairs[j];
                    double sigma = oldInfo.ContinuousFunction.Value(pair.Strike) + m_shiftIv;
                    if (!DoubleUtil.IsPositive(sigma))
                    {
                        //string msg = String.Format("[DEBUG:{0}] Invalid sigma:{1} for strike:{2}", GetType().Name, sigma, nodeInfo.Strike);
                        //m_context.Log(msg, MessageType.Warning, true);
                        continue;
                    }

                    //bool isCall = (futPx <= pair.Strike);
                    bool isCall;
                    if (m_optionType == StrikeType.Call)
                        isCall = true;
                    else if (m_optionType == StrikeType.Put)
                        isCall = false;
                    else
                        isCall = (futPx <= pair.Strike);

                    StrikeType optionType = isCall ? StrikeType.Call : StrikeType.Put;
                    Contract.Assert(pair.Tick < 1, $"#1 На тестовом контуре Дерибит присылает неправильный шаг цены! Tick:{pair.Tick}; Decimals:{pair.Put.Security.Decimals}");
                    double theorOptPxDollars = FinMath.GetOptionPrice(futPx, pair.Strike, dT, sigma, oldInfo.RiskFreeRate, isCall);
                    // Сразу(!!!) переводим котировку из баксов в битки
                    double theorOptPxBitcoins = theorOptPxDollars / scaleMult;

                    // Сдвигаем цену в долларах (с учетом ш.ц. в баксах)
                    theorOptPxDollars += m_shiftPriceStep * pair.Tick * scaleMult;
                    theorOptPxDollars = Math.Round(theorOptPxDollars / (pair.Tick * scaleMult)) * (pair.Tick * scaleMult);

                    // Сдвигаем цену в биткойнах (с учетом ш.ц. в битках)
                    theorOptPxBitcoins += m_shiftPriceStep * pair.Tick;
                    theorOptPxBitcoins = Math.Round(theorOptPxBitcoins / pair.Tick) * pair.Tick;
                    if ((!DoubleUtil.IsPositive(theorOptPxBitcoins)) || (!DoubleUtil.IsPositive(theorOptPxDollars)))
                    {
                        //string msg = String.Format("[DEBUG:{0}] Invalid theorOptPx:{1} for strike:{2}", GetType().Name, theorOptPx, nodeInfo.Strike);
                        //m_context.Log(msg, MessageType.Warning, true);
                        continue;
                    }

                    // Пересчитываем сигму обратно, ЕСЛИ мы применили сдвиг цены в абсолютном выражении
                    if (m_shiftPriceStep != 0)
                    {
                        // Обратный пересчет в волатильность
                        sigma = FinMath.GetOptionSigma(futPx, pair.Strike, dT, theorOptPxDollars, oldInfo.RiskFreeRate, isCall);
                        if (!DoubleUtil.IsPositive(sigma))
                        {
                            //string msg = String.Format("[DEBUG:{0}] Invalid sigma:{1} for strike:{2}", GetType().Name, sigma, nodeInfo.Strike);
                            //m_context.Log(msg, MessageType.Warning, true);
                            continue;
                        }
                    }

                    // ReSharper disable once UseObjectOrCollectionInitializer
                    SmileNodeInfo nodeInfo = new SmileNodeInfo();
                    var secDesc = isCall ? pair.CallFinInfo.Security : pair.PutFinInfo.Security;
                    nodeInfo.F = oldInfo.F;
                    nodeInfo.dT = oldInfo.dT;
                    nodeInfo.RiskFreeRate = oldInfo.RiskFreeRate;
                    nodeInfo.Strike = pair.Strike;
                    nodeInfo.Sigma = sigma;
                    nodeInfo.OptPx = theorOptPxBitcoins;
                    nodeInfo.OptionType = isCall ? StrikeType.Call : StrikeType.Put;
                    nodeInfo.Pair = pair;

                    nodeInfo.Symbol = secDesc.Name;
                    nodeInfo.DSName = secDesc.DSName;
                    nodeInfo.Expired = secDesc.Expired;
                    nodeInfo.FullName = secDesc.FullName;

                    // ReSharper disable once UseObjectOrCollectionInitializer
                    InteractivePointActive tmp = new InteractivePointActive();

                    tmp.IsActive = true;
                    tmp.ValueX = pair.Strike;
                    tmp.ValueY = sigma;
                    tmp.DragableMode = DragableMode.Yonly;
                    tmp.Tooltip = String.Format(CultureInfo.InvariantCulture,
                        " F: {0}\r\n K: {1}; IV: {2:P2}\r\n {3} px {4}",
                        futPx, pair.Strike, sigma, optionType, theorOptPxBitcoins);

                    tmp.Tag = nodeInfo;

                    //tmp.Color = Colors.White;
                    if (m_qty > 0)
                        tmp.Geometry = Geometries.Triangle;
                    else if (m_qty < 0)
                        tmp.Geometry = Geometries.TriangleDown;
                    else
                        tmp.Geometry = Geometries.None;

                    InteractiveObject obj = new InteractiveObject();
                    obj.Anchor = tmp;

                    controlPoints.Add(obj);
                }

                // ReSharper disable once UseObjectOrCollectionInitializer
                res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
                res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

                // ReSharper disable once UseObjectOrCollectionInitializer
                SmileInfo sInfo = new SmileInfo();
                sInfo.F = futPx;
                sInfo.dT = dT;
                sInfo.Expiry = oldInfo.Expiry;
                sInfo.ScriptTime = oldInfo.ScriptTime;
                sInfo.RiskFreeRate = oldInfo.RiskFreeRate;
                sInfo.BaseTicker = oldInfo.BaseTicker;

                res.Tag = sInfo;

                if (controlPoints.Count > 0)
                {
                    res.ClickEvent -= InteractiveSplineOnQuoteIvEvent;
                    res.ClickEvent += InteractiveSplineOnQuoteIvEvent;

                    m_clickableSeries = res;
                }
            }
            #endregion 2. Формируем улыбку просто для отображения текущего положения потенциальной котировки

            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (m_cancelAllLong)
                posMan.DropAllLongIvTargets(m_context);
            if (m_cancelAllShort)
                posMan.DropAllShortIvTargets(m_context);

            #region 4. Котирование
            {
                var longTargets = posMan.GetIvTargets(true);
                var shortTargets = posMan.GetIvTargets(false);
                var ivTargets = longTargets.Union(shortTargets).ToList();
                for (int j = 0; j < ivTargets.Count; j++)
                {
                    var ivTarget = ivTargets[j];

                    // PROD-6102 - Требуется точное совпадение опционной серии
                    if (optSer.ExpirationDate.Date != ivTarget.SecInfo.Expiry.Date)
                    {
                        // Вывести предупреждение???
                        continue;
                    }

                    IOptionStrikePair pair;
                    double k = ivTarget.SecInfo.Strike;
                    if (!optSer.TryGetStrikePair(k, out pair))
                    {
                        // Вывести предупреждение???
                        continue;
                    }

                    double sigma;
                    QuoteIvMode quoteMode = ivTarget.QuoteMode;
                    if (quoteMode == QuoteIvMode.Absolute)
                        sigma = ivTarget.EntryIv;
                    else
                    {
                        sigma = oldInfo.ContinuousFunction.Value(k) + ivTarget.EntryIv;
                        if (!DoubleUtil.IsPositive(sigma))
                        {
                            //string msg = String.Format("[DEBUG:{0}] Invalid sigma:{1} for strike:{2}", GetType().Name, sigma, nodeInfo.Strike);
                            //m_context.Log(msg, MessageType.Warning, true);
                            continue;
                        }
                    }

                    //bool isCall = (futPx <= pair.Strike);
                    // Определяю тип опциона на основании информации в Задаче
                    StrikeType taskOptionType = StrikeType.Any;
                    if (ivTarget.SecInfo.StrikeType.HasValue)
                        taskOptionType = ivTarget.SecInfo.StrikeType.Value;

                    bool isCall;
                    if (taskOptionType == StrikeType.Call)
                        isCall = true;
                    else if (taskOptionType == StrikeType.Put)
                        isCall = false;
                    else
                        isCall = (futPx <= pair.Strike); // Это аварийная ситуация?

                    StrikeType optionType = isCall ? StrikeType.Call : StrikeType.Put;
                    Contract.Assert(pair.Tick < 1, $"#3 На тестовом контуре Дерибит присылает неправильный шаг цены! Tick:{pair.Tick}; Decimals:{pair.Put.Security.Decimals}");
                    double theorOptPxDollars = FinMath.GetOptionPrice(futPx, pair.Strike, dT, sigma, oldInfo.RiskFreeRate, isCall);
                    // Сразу(!!!) переводим котировку из баксов в битки
                    double theorOptPxBitcoins = theorOptPxDollars / scaleMult;

                    // Сдвигаем цену в долларах (с учетом ш.ц. в баксах)
                    theorOptPxDollars += ivTarget.EntryShiftPrice * pair.Tick * scaleMult;
                    theorOptPxDollars = Math.Round(theorOptPxDollars / (pair.Tick * scaleMult)) * (pair.Tick * scaleMult);

                    // Сдвигаем цену в биткойнах (с учетом ш.ц. в битках)
                    theorOptPxBitcoins += ivTarget.EntryShiftPrice * pair.Tick;
                    theorOptPxBitcoins = Math.Round(theorOptPxBitcoins / pair.Tick) * pair.Tick;
                    if ((!DoubleUtil.IsPositive(theorOptPxBitcoins)) || (!DoubleUtil.IsPositive(theorOptPxDollars)))
                    {
                        //string msg = String.Format("[DEBUG:{0}] Invalid theorOptPx:{1} for strike:{2}", GetType().Name, theorOptPx, nodeInfo.Strike);
                        //m_context.Log(msg, MessageType.Warning, true);
                        continue;
                    }

                    IOptionStrike optStrike = isCall ? pair.Call : pair.Put;
                    ISecurity sec = optStrike.Security;
                    double totalQty = posMan.GetTotalQty(sec, m_context.BarsCount, TotalProfitAlgo.AllPositions, ivTarget.IsLong);
                    // Поскольку котирование страйка по волатильности -- это вопрос набора нужного количества СТРЕДДЛОВ,
                    // то учитывать надо суммарный объём опционов как в колах, так и в путах.
                    // НО ЗАДАЧУ-ТО Я СТАВЛЮ ДЛЯ КОНКРЕТНОГО ИНСТРУМЕНТА!
                    // Как быть?
                    //double totalQty = posMan.GetTotalQty(pair.Put.Security, m_context.BarsCount, TotalProfitAlgo.AllPositions, ivTarget.IsLong);
                    //totalQty += posMan.GetTotalQty(pair.Call.Security, m_context.BarsCount, TotalProfitAlgo.AllPositions, ivTarget.IsLong);
                    double targetQty = Math.Abs(ivTarget.TargetShares) - totalQty;
                    // Если имеется дробный LotTick (как в Дерибит к примеру), то надо предварительно округлить
                    targetQty = sec.RoundShares(targetQty);
                    if (targetQty > 0)
                    {
                        string note = String.Format(CultureInfo.InvariantCulture,
                            "{0}; ActQty:{1}; Px:{2}; IV:{3:P2}",
                            ivTarget.EntryNotes, targetQty, theorOptPxBitcoins, sigma);
                        if (ivTarget.IsLong)
                        {
                            posMan.BuyAtPrice(m_context, sec, targetQty, theorOptPxBitcoins, ivTarget.EntrySignalName, note);
                        }
                        else
                        {
                            posMan.SellAtPrice(m_context, sec, targetQty, theorOptPxBitcoins, ivTarget.EntrySignalName, note);
                        }
                    }
                    else
                    {
                        string msg = String.Format(CultureInfo.InvariantCulture,
                            "IvTarget cancelled. SignalName:{0}; Notes:{1}", ivTarget.EntrySignalName, ivTarget.EntryNotes);
                        posMan.CancelVolatility(m_context, ivTarget, msg);

                        // TODO: потом убрать из ГЛ
                        m_context.Log(msg, MessageType.Info, true, new Dictionary<string, object> { { "VOLATILITY_ORDER_CANCELLED", msg } });
                    }
                }
            }
            #endregion 4. Котирование

            #region 5. Торговля
            if (m_executeCommand && (!DoubleUtil.IsZero(m_qty)))
            {
                double k;
                if ((!Double.TryParse(m_strike, out k)) &&
                    (!Double.TryParse(m_strike, NumberStyles.Any, CultureInfo.InvariantCulture, out k)))
                    return res;

                var pair = (from p in pairs where DoubleUtil.AreClose(k, p.Strike) select p).SingleOrDefault();
                if (pair == null)
                    return res;

                InteractiveObject obj = (from o in controlPoints where DoubleUtil.AreClose(k, o.Anchor.ValueX) select o).SingleOrDefault();
                if (obj == null)
                    return res;

                // TODO: для режима котирования в абсолютных числах сделать отдельную ветку
                //double iv = obj.Anchor.ValueY;
                const QuoteIvMode QuoteMode = QuoteIvMode.Relative;
                if (posMan.BlockTrading)
                {
                    string msg = String.Format(RM.GetString("OptHandlerMsg.PositionsManager.TradingBlocked"),
                        m_context.Runtime.TradeName + ":QuoteIv");
                    m_context.Log(msg, MessageType.Warning, true);
                    return res;
                }

                // Выбираю тип инструмента пут или колл?
                bool isCall;
                if (m_optionType == StrikeType.Call)
                    isCall = true;
                else if (m_optionType == StrikeType.Put)
                    isCall = false;
                else
                    isCall = (futPx <= k);

                double iv = m_shiftIv;
                int shift = m_shiftPriceStep;
                var option = isCall ? pair.Call : pair.Put;
                if (m_qty > 0)
                {
                    // Пересчитываю целочисленный параметр Qty в фактические лоты конкретного инструмента
                    double actQty = m_qty * option.LotTick;
                    string sigName = String.Format(CultureInfo.InvariantCulture,
                        "Qty:{0}; IV:{1:P2}+{2}; dT:{3}; Mode:{4}", actQty, iv, shift, dT, QuoteMode);
                    posMan.BuyVolatility(m_context, option, Math.Abs(actQty), QuoteMode, iv, shift, "BuyVola", sigName);

                    m_context.Log(sigName, MessageType.Info, false);
                }
                else if (m_qty < 0)
                {
                    // Пересчитываю целочисленный параметр Qty в фактические лоты конкретного инструмента
                    double actQty = m_qty * option.LotTick;
                    string sigName = String.Format(CultureInfo.InvariantCulture,
                        "Qty:{0}; IV:{1:P2}+{2}; dT:{3}; Mode:{4}", actQty, iv, shift, dT, QuoteMode);
                    posMan.SellVolatility(m_context, option, Math.Abs(actQty), QuoteMode, iv, shift, "SellVola", sigName);

                    m_context.Log(sigName, MessageType.Info, false);
                }
            }
            #endregion 5. Торговля

            return res;
        }

        private void InteractiveSplineOnQuoteIvEvent(object sender, InteractiveActionEventArgs eventArgs)
        {
            OptionPxMode pxMode = (m_qty > 0) ? OptionPxMode.Ask : OptionPxMode.Bid;

            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (posMan.BlockTrading)
            {
                //string msg = String.Format("[{0}] Trading is blocked. Please, change 'Block Trading' parameter.", m_optionPxMode);
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked", pxMode);
                m_context.Log(msg, MessageType.Info, true);
                return;
            }

            InteractivePointActive tmp = eventArgs.Point;
            if ((tmp == null) || (tmp.IsActive == null) || (!tmp.IsActive.Value) ||
                DoubleUtil.IsZero(m_qty))
            {
                //string msg = String.Format("[{0}] Unable to get direction of the order. Qty:{1}", pxMode, m_qty);
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.UndefinedOrderDirection", pxMode, m_qty);
                m_context.Log(msg, MessageType.Error, true);
                return;
            }

            SmileNodeInfo nodeInfo = tmp.Tag as SmileNodeInfo;
            if (nodeInfo == null)
            {
                //string msg = String.Format("[{0}] There is no nodeInfo. Quote type: {1}; Strike: {2}", pxMode);
                string msg = RM.GetStringFormat(CultureInfo.InvariantCulture, "OptHandlerMsg.ThereIsNoNodeInfo",
                    m_context.Runtime.TradeName, pxMode, eventArgs.Point.ValueX);
                m_context.Log(msg, MessageType.Error, true);
                return;
            }

            {
                string msg = String.Format(CultureInfo.InvariantCulture, "[{0}.ClickEvent] Strike: {1}", GetType().Name, tmp.ValueX);
                m_context.Log(msg, MessageType.Info, false);
            }

            nodeInfo.OptPx = m_shiftIv;
            nodeInfo.ShiftOptPx = m_shiftPriceStep;
            nodeInfo.ClickTime = DateTime.Now;
            nodeInfo.PxMode = pxMode;

            // [2015-10-02] Подписка на данный инструмент, чтобы он появился в коллекции Context.Runtime.Securities
            var candids = (from s in Context.Runtime.Securities
                           where s.SecurityDescription.Name.Equals(nodeInfo.Symbol, StringComparison.InvariantCultureIgnoreCase)
                           select s).ToList();
            candids = (from s in candids
                       where s.SecurityDescription.DSName.Equals(nodeInfo.DSName, StringComparison.InvariantCultureIgnoreCase)
                       select s).ToList();
            ISecurity testSec = (from s in candids
                                 let secDesc = s.SecurityDescription
                                 where secDesc.FullName.Equals(nodeInfo.FullName, StringComparison.InvariantCultureIgnoreCase)
                                 select s).SingleOrDefault();
            if ((testSec == null) && (nodeInfo.Security != null))
            {
                ISecurity sec = nodeInfo.Security;
                int bc = sec.Bars.Count;
                string msg = String.Format("[{0}] There is security DsName: {1}; Symbol: {2}; Security: {3} with {4} bars available.",
                    pxMode, nodeInfo.DSName, nodeInfo.Symbol, nodeInfo.FullName, bc);
                Context.Log(msg, MessageType.Info, false);

                // Пересчитываю целочисленный параметр Qty в фактические лоты конкретного инструмента
                double actQty = Math.Abs(m_qty * sec.LotTick); // Модуль, потому что тут направление передается через PxMode
                nodeInfo.Qty = actQty;
            }
            else if ((nodeInfo.Pair != null) && (nodeInfo.Pair.Put != null))
            {
                // Аварийная ветка
                // Пересчитываю целочисленный параметр Qty в фактические лоты конкретного инструмента
                double actQty = Math.Abs(m_qty * nodeInfo.Pair.Put.LotTick); // Модуль, потому что тут направление передается через PxMode
                nodeInfo.Qty = actQty;
            }
            else
            {
                // Аварийная ветка
                // Не могу пересчитать целочисленный параметр Qty в фактические лоты конкретного инструмента!
                //double actQty = Math.Abs(m_qty * nodeInfo.Pair.Put.LotTick);
                nodeInfo.Qty = Math.Abs(m_qty); // Модуль, потому что тут направление передается через PxMode

                string msg = String.Format(CultureInfo.InvariantCulture, "[{0}.ClickEvent] LotTick will be set to 1.", GetType().Name);
                m_context.Log(msg, MessageType.Warning, false);
            }

            // Не страшно, если сюда пойдет null - posMan потом сам найдет нужный опцион
            nodeInfo.Security = testSec;

            // Передаю событие в PositionsManager
            //posMan.InteractiveSplineOnClickEvent(m_context, sender, eventArgs);
            posMan.InteractiveSplineOnQuoteIvEvent(m_context, sender, eventArgs);
        }

        #region Implementation of ICustomListValues
        public IEnumerable<string> GetValuesForParameter(string paramName)
        {
            if (paramName.Equals("Strike", StringComparison.InvariantCultureIgnoreCase) ||
                paramName.Equals("Страйк", StringComparison.InvariantCultureIgnoreCase))
            {
                HashSet<string> res = StrikeList;
                //res.Sort();
                //var res = from s in series
                //          where s.StartsWith(m_baseSecPrefix, StringComparison.InvariantCultureIgnoreCase)
                //          select s;
                return res;
            }

            throw new ArgumentException("Parameter '" + paramName + "' is not supported.", "paramName");
        }
        #endregion

        public void Dispose()
        {
            if (m_clickableSeries != null)
            {
                m_clickableSeries.ClickEvent -= InteractiveSplineOnQuoteIvEvent;
                //m_clickableSeries.EndDragEvent -= res_EndDragEvent;

                m_clickableSeries = null;
            }
        }
    }
}
