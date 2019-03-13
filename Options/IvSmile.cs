using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;

using TSLab.DataSource;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Smile derived from option quotes (stream handler)
    /// \~russian Улыбка, построенная по котировкам опционов (потоковый обработчик)
    /// </summary>
    [HandlerCategory(HandlerCategories.Options)]
    [HelperName("IV Smile", Language = Constants.En)]
    [HelperName("IV Smile", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(3, TemplateTypes.DOUBLE, Name = Constants.RiskFreeRate)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Улыбка, построенная по котировкам опционов (потоковый обработчик)")]
    [HelperDescription("Smile derived from option quotes (stream handler)", Constants.En)]
    public class IvSmile : BaseSmileDrawing, IStreamHandler
    {
        private double m_maxSigma = 2.0;
        private double m_shiftBid = 0, m_shiftAsk = 0;
        private StrikeType m_optionType = StrikeType.Any;
        private OptionPxMode m_optionPxMode = OptionPxMode.Ask;

        private static readonly Dictionary<string, DateTime> s_conflictDate = new Dictionary<string, DateTime>();

        #region Parameters
        /// <summary>
        /// \~english Algorythm to get option price
        /// \~russian Алгоритм расчета цены опциона
        /// </summary>
        [HelperName("Price Mode", Constants.En)]
        [HelperName("Вид цены", Constants.Ru)]
        [Description("Алгоритм расчета цены опциона")]
        [HelperDescription("Algorythm to get option price", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "Ask")]
        public OptionPxMode OptPxMode
        {
            get { return m_optionPxMode; }
            set { m_optionPxMode = value; }
        }

        /// <summary>
        /// \~english Option type to be used by handler (call, put, best volatility)
        /// \~russian Тип опционов для расчетов (колл, пут, лучшая волатильность)
        /// </summary>
        [HelperName("Option Type", Constants.En)]
        [HelperName("Тип опциона", Constants.Ru)]
        [Description("Тип опционов для расчетов (колл, пут, лучшая волатильность)")]
        [HelperDescription("Option type to be used by handler (call, put, best volatility)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Any")]
        public StrikeType OptionType
        {
            get { return m_optionType; }
            set { m_optionType = value; }
        }

        /// <summary>
        /// \~english Shift Bids down (price steps)
        /// \~russian Сдвиг котировок Bid на заданное количество шагов цены вниз
        /// </summary>
        [HelperName("Shift Bid", Constants.En)]
        [HelperName("Сдвиг бидов", Constants.Ru)]
        [Description("Сдвиг котировок Bid на заданное количество шагов цены вниз")]
        [HelperDescription("Shift Bids down (price steps)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "0", Min = "-10000000", Max = "10000000", Step = "1")]
        public double ShiftBid
        {
            get { return m_shiftBid; }
            set { m_shiftBid = value; }
        }

        /// <summary>
        /// \~english Shift Asks up (price steps)
        /// \~russian Сдвиг котировок Ask на заданное количество шагов цены вверх
        /// </summary>
        [HelperName("Shift Ask", Constants.En)]
        [HelperName("Сдвиг асков", Constants.Ru)]
        [Description("Сдвиг котировок Ask на заданное количество шагов цены вверх")]
        [HelperDescription("Shift Asks up (price steps)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "0", Min = "-10000000", Max = "10000000", Step = "1")]
        public double ShiftAsk
        {
            get { return m_shiftAsk; }
            set { m_shiftAsk = value; }
        }

        /// <summary>
        /// \~english Max volatility limit (percents)
        /// \~russian Предельная волатильность (в процентах)
        /// </summary>
        [HelperName("Max Sigma Pct", Constants.En)]
        [HelperName("Макс. вола (проценты)", Constants.Ru)]
        [Description("Предельная волатильность (в процентах)")]
        [HelperDescription("Max volatility limit (percents)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "200", Min = "-10000000", Max = "10000000", Step = "1")]
        public double MaxSigmaPct
        {
            get { return m_maxSigma * Constants.PctMult; }
            set { m_maxSigma = value / Constants.PctMult; }
        }
        #endregion Parameters

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES
        /// </summary>
        /// <param name="prices">цена БА</param>
        /// <param name="times">время до экспирации в долях года</param>
        /// <param name="optSer">опционная серия</param>
        /// <returns>улыбка, восстановленная из цен опционов</returns>
        public InteractiveSeries Execute(IList<double> prices, IList<double> times, IOptionSeries optSer)
        {
            InteractiveSeries res = Execute(prices, times, optSer, new[] { 0.0 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES
        /// </summary>
        /// <param name="prices">цена БА</param>
        /// <param name="times">время до экспирации в долях года</param>
        /// <param name="optSer">опционная серия</param>
        /// <param name="rates">процентные ставки</param>
        /// <returns>улыбка, восстановленная из цен опционов</returns>
        public InteractiveSeries Execute(IList<double> prices, IList<double> times, IOptionSeries optSer, IList<double> rates)
        {
            if (prices.Count <= 0)
                //throw new ScriptException("There should be some values in first argument 'prices'.");
                return Constants.EmptySeries;

            if (times.Count <= 0)
                //throw new ScriptException("There should be some values in second argument 'times'.");
                return Constants.EmptySeries;

            if (rates.Count <= 0)
                //throw new ScriptException("There should be some values in second argument 'rates'.");
                return Constants.EmptySeries;

            double f = prices[prices.Count - 1];
            double dT = times[times.Count - 1];
            double rate = rates[rates.Count - 1];

            if (Double.IsNaN(f))
                //throw new ScriptException("Argument 'prices' contains NaN for some strange reason. F:" + F);
                return Constants.EmptySeries;
            if ((dT < Double.Epsilon) || (Double.IsNaN(dT)))
                return Constants.EmptySeries;
            if (Double.IsNaN(rate))
                //throw new ScriptException("Argument 'rate' contains NaN for some strange reason. rate:" + rate);
                return Constants.EmptySeries;

            IOptionStrikePair[] strikes = (from strike in optSer.GetStrikePairs()
                                           //orderby strike.Strike ascending -- уже отсортировано
                                           select strike).ToArray();
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            for (int j = 0; j < strikes.Length; j++)
            {
                IOptionStrikePair sInfo = strikes[j];
                // Сверхдалекие страйки игнорируем
                if ((sInfo.Strike < m_minStrike) || (m_maxStrike < sInfo.Strike))
                    continue;

                double putPx, callPx;
                double putQty, callQty;
                DateTime putTime, callTime;
                {
                    putPx = GetOptPrice(m_context, f, sInfo.Put, m_optionPxMode, sInfo.Tick * m_shiftAsk, sInfo.Tick * m_shiftBid, out putQty, out putTime);
                }

                {
                    callPx = GetOptPrice(m_context, f, sInfo.Call, m_optionPxMode, sInfo.Tick * m_shiftAsk, sInfo.Tick * m_shiftBid, out callQty, out callTime);
                }

                double putSigma = Double.NaN, callSigma = Double.NaN, precision;
                if (!Double.IsNaN(putPx))
                {
                    putSigma = FinMath.GetOptionSigma(f, sInfo.Strike, dT, putPx, rate, false, out precision);
                    putSigma = Math.Min(putSigma, m_maxSigma);
                    if (putSigma <= 0)
                        putSigma = Double.NaN;
                }
                if (!Double.IsNaN(callPx))
                {
                    callSigma = FinMath.GetOptionSigma(f, sInfo.Strike, dT, callPx, rate, true, out precision);
                    callSigma = Math.Min(callSigma, m_maxSigma);
                    if (callSigma <= 0)
                        callSigma = Double.NaN;
                }

                InteractivePointActive ip = new InteractivePointActive();
                {
                    //ip.Color = (m_optionPxMode == OptionPxMode.Ask) ? Colors.DarkOrange : Colors.DarkCyan;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect; // (optionPxMode == OptionPxMode.Ask) ? Geometries.Rect : Geometries.Rect;
                    //ip.IsActive = true;
                    //ip.Value = new Point(d2.V1, d2.V2);
                    //ip.Tooltip = String.Format("K:{0}; IV:{1:#0.00}", d2.V1, d2.V2 * PctMult);
                }

                InteractiveObject obj = new InteractiveObject(ip);

                if (m_optionType == StrikeType.Put)
                {
                    if (!Double.IsNaN(putSigma))
                    {
                        FillNodeInfo(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPx, putQty, putSigma, putTime, false, rate);
                        controlPoints.Add(obj);
                    }
                }
                else if (m_optionType == StrikeType.Call)
                {
                    if (!Double.IsNaN(callSigma))
                    {
                        FillNodeInfo(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPx, callQty, callSigma, callTime, false, rate);
                        controlPoints.Add(obj);
                    }
                }
                else if (m_optionType == StrikeType.Any)
                {
                    if ((!Double.IsNaN(putSigma)) && (!Double.IsNaN(callSigma)))
                    {
                        if (m_optionPxMode == OptionPxMode.Ask)
                        {
                            if (putSigma < callSigma)
                                FillNodeInfo(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPx, putQty, putSigma, putTime, false, rate);
                            else
                                FillNodeInfo(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPx, callQty, callSigma, callTime, false, rate);
                        }
                        else if (m_optionPxMode == OptionPxMode.Bid)
                        {
                            if (putSigma > callSigma)
                                FillNodeInfo(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPx, putQty, putSigma, putTime, false, rate);
                            else
                                FillNodeInfo(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPx, callQty, callSigma, callTime, false, rate);
                        }

                        controlPoints.Add(obj);
                    }
                    else if ((!Double.IsNaN(putSigma)) && (Double.IsNaN(callSigma)))
                    {
                        FillNodeInfo(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPx, putQty, putSigma, putTime, false, rate);
                        controlPoints.Add(obj);
                    }
                    else if ((Double.IsNaN(putSigma)) && (!Double.IsNaN(callSigma)))
                    {
                        FillNodeInfo(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPx, callQty, callSigma, callTime, false, rate);
                        controlPoints.Add(obj);
                    }
                }
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            SmileInfo info;
            var baseSec = optSer.UnderlyingAsset;
            DateTime scriptTime = baseSec.Bars[baseSec.Bars.Count - 1].Date;
            if (!IvSmile.TryPrepareSmileInfo(m_context, f, dT, rate, optSer.ExpirationDate, scriptTime, baseSec.Symbol, res, out info))
                return Constants.EmptySeries;

            return res;
        }

        /// <summary>
        /// Вычисление цены опциона различными способами. Есть возможность сразу сделать сдвиг цены (предусмотрены проверки).
        /// </summary>
        /// <param name="externalContext">Контекст блока для логгирования проблемы</param>
        /// <param name="f">Цена БА (нажна как верхняя оценка цены колла</param>
        /// <param name="sec">Инструмент</param>
        /// <param name="mode">алгоритм расчета (покупка, продажа, середина спреда)</param>
        /// <param name="shiftAsk">сдвиг асков</param>
        /// <param name="shiftBid">сдвиг бидов</param>
        /// <param name="qty">количество лотов в заявке</param>
        /// <param name="optTime">время получения этих котировок из рыночного провайдера</param>
        /// <returns>цена опциона (в случае проблем возвращает NaN)</returns>
        public static double GetOptPrice(IContext externalContext, double f, IOptionStrike sec, OptionPxMode mode,
            double shiftAsk, double shiftBid, out double qty, out DateTime optTime)
        {
            qty = Double.NaN;
            optTime = new DateTime();
            double optPx = Double.NaN;

            // PROD-5952 - Не надо дергать стакан без нужды
            //sec.UpdateQueueData();
            if (mode == OptionPxMode.Ask)
            {
                if (/* sec.FinInfo.AskSize.HasValue && */ sec.FinInfo.Ask.HasValue)
                {
                    optPx = sec.FinInfo.Ask.Value;
                    optTime = sec.FinInfo.LastUpdate;

                    qty = 0;
                    if (sec.FinInfo.AskSize.HasValue)
                        qty = sec.FinInfo.AskSize.Value;
                    else
                    {
                        // PROD-5952 - Не надо дергать стакан без нужды
                        //// Аски отсортированы по возрастанию
                        //var queue = sec.GetSellQueue(externalContext.BarsCount - 1);
                        //if ((queue != null) && (queue.Count > 0))
                        //{
                        //    IQueueData ask = queue[0]; // queue.First();
                        //    qty = ask.Quantity;
                        //}
                    }
                }
                else
                {
                    // PROD-5952 - Не надо дергать стакан без нужды
                    #region Нет данных в FinInfo
//                    // Аски отсортированы по возрастанию
//                    var queue = sec.GetSellQueue(externalContext.BarsCount - 1);
//                    if ((queue != null) && (queue.Count > 0))
//                    {
//                        IQueueData ask = queue[0]; // queue.First();

//#if DEBUG
//                        if (!s_conflictDate.ContainsKey(sec.Security.SecurityDescription.FullName))
//                        {
//                            bool check = (ask.Security.FullName == sec.Security.SecurityDescription.FullName);
//                            if (!check)
//                                s_conflictDate[sec.Security.SecurityDescription.FullName] = ask.LastUpdate;

//                            Debug.Assert(check,
//                                String.Format("Expected security: {0}; actual security: {1}",
//                                    sec.Security.SecurityDescription.FullName, ask.Security.FullName));
//                        }
//#endif

//                        optPx = ask.Price;
//                        qty = ask.Quantity;
//                        optTime = ask.LastUpdate;

//                        //string msg =
//                        //    String.Format(
//                        //        "[DEBUG:{0}] I was forced to use 'sec.GetSellQueue'. sec.Bars.Count:{1}; ask.LastUpdate:{2}; sec:{3}",
//                        //        typeof(IvSmile).Name, sec.Bars.Count,
//                        //        ask.LastUpdate.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture),
//                        //        sec);
//                        //externalContext.Log(msg, MessageType.Warning, true);
//                    }
                    #endregion Нет данных в FinInfo
                }

                if (Double.IsNaN(optPx) || (optPx <= Double.Epsilon))
                    optPx = 2 * f; // Это одна из верхних оценок цены опциона.
                else
                    optPx += shiftAsk;
            }
            else if (mode == OptionPxMode.Bid)
            {
                if (/* sec.FinInfo.BidSize.HasValue && */ sec.FinInfo.Bid.HasValue)
                {
                    optPx = sec.FinInfo.Bid.Value;
                    optTime = sec.FinInfo.LastUpdate;

                    qty = 0;
                    if (sec.FinInfo.BidSize.HasValue)
                        qty = sec.FinInfo.BidSize.Value;
                    else
                    {
                        // PROD-5952 - Не надо дергать стакан без нужды
                        //// Биды отсортированы ПО УБЫВАНИЮ!!!
                        //var queue = sec.GetBuyQueue(externalContext.BarsCount - 1);
                        //if ((queue != null) && (queue.Count > 0))
                        //{
                        //    IQueueData bid = queue[0]; // queue.First();
                        //    qty = bid.Quantity;
                        //}
                    }
                }
                else
                {
                    // PROD-5952 - Не надо дергать стакан без нужды
                    #region Нет данных в FinInfo
//                    // Биды отсортированы ПО УБЫВАНИЮ!!!
//                    var queue = sec.GetBuyQueue(externalContext.BarsCount - 1);
//                    if ((queue != null) && (queue.Count > 0))
//                    {
//                        IQueueData bid = queue[0]; // queue.First();

//#if DEBUG
//                        if (!s_conflictDate.ContainsKey(sec.Security.SecurityDescription.FullName))
//                        {
//                            bool check = (bid.Security.FullName == sec.Security.SecurityDescription.FullName);
//                            if (!check)
//                                s_conflictDate[sec.Security.SecurityDescription.FullName] = bid.LastUpdate;

//                            Debug.Assert(check,
//                                String.Format("Expected security: {0}; actual security: {1}",
//                                    sec.Security.SecurityDescription.FullName, bid.Security.FullName));
//                        }
//#endif

//                        optPx = bid.Price;
//                        qty = bid.Quantity;
//                        optPx -= shiftBid;
//                        optPx = Math.Max(optPx, 0);

//                        optTime = bid.LastUpdate;

//                        //string msg =
//                        //    String.Format(
//                        //        "[DEBUG:{0}] I was forced to use 'sec.GetSellQueue'. sec.Bars.Count:{1}; bid.LastUpdate:{2}; sec:{3}",
//                        //        typeof(IvSmile).Name, sec.Bars.Count,
//                        //        bid.LastUpdate.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture),
//                        //        sec);
//                        //externalContext.Log(msg, MessageType.Warning, true);
//                    }
                    #endregion Нет данных в FinInfo
                }
            }
            else if (mode == OptionPxMode.Mid)
            {
                if (/* sec.FinInfo.BidSize.HasValue && */ sec.FinInfo.Bid.HasValue &&
                    /* sec.FinInfo.AskSize.HasValue && */ sec.FinInfo.Ask.HasValue)
                {
                    double askPx = sec.FinInfo.Ask.Value;
                    //double askQty = sec.FinInfo.AskSize.Value;
                    if (Double.IsNaN(askPx) || (askPx <= Double.Epsilon))
                        askPx = 2.0 * f; // Это одна из верхних оценок цены опциона.
                    else
                        askPx += shiftAsk;

                    double bidPx = sec.FinInfo.Bid.Value;
                    //double bidQty = sec.FinInfo.BidSize.Value;
                    bidPx -= shiftBid;
                    bidPx = Math.Max(bidPx, 0);

                    optPx = (askPx + bidPx) / 2.0;
                    //qty = Math.Min(askQty, bidQty);

                    double askQty = 0, bidQty = 0;
                    #region AskQty
                    if (sec.FinInfo.AskSize.HasValue)
                        askQty = sec.FinInfo.AskSize.Value;
                    else
                    {
                        // PROD-5952 - Не надо дергать стакан без нужды
                        //// Аски отсортированы по возрастанию
                        //var queue = sec.GetSellQueue(externalContext.BarsCount - 1);
                        //if ((queue != null) && (queue.Count > 0))
                        //{
                        //    IQueueData ask = queue[0]; // queue.First();
                        //    askQty = ask.Quantity;
                        //}
                    }
                    #endregion AskQty

                    #region BidQty
                    if (sec.FinInfo.BidSize.HasValue)
                        bidQty = sec.FinInfo.BidSize.Value;
                    else
                    {
                        // PROD-5952 - Не надо дергать стакан без нужды
                        //// Биды отсортированы ПО УБЫВАНИЮ!!!
                        //var queue = sec.GetBuyQueue(externalContext.BarsCount - 1);
                        //if ((queue != null) && (queue.Count > 0))
                        //{
                        //    IQueueData bid = queue[0]; // queue.First();
                        //    bidQty = bid.Quantity;
                        //}
                    }
                    #endregion BidQty

                    qty = Math.Min(askQty, bidQty);

                    optTime = sec.FinInfo.LastUpdate;
                }
                else
                {
                    // PROD-5952 - Не надо дергать стакан без нужды
                    #region Нет данных в FinInfo
//                    var askQueue = sec.GetSellQueue(externalContext.BarsCount - 1);
//                    var bidQueue = sec.GetBuyQueue(externalContext.BarsCount - 1);
//                    if ((askQueue != null) && (askQueue.Count > 0) &&
//                        (bidQueue != null) && (bidQueue.Count > 0))
//                    {
//                        // Аски отсортированы по возрастанию
//                        IQueueData ask = askQueue[0]; // askQueue.First();

//#if DEBUG
//                        if (!s_conflictDate.ContainsKey(sec.Security.SecurityDescription.FullName))
//                        {
//                            bool check = (ask.Security.FullName == sec.Security.SecurityDescription.FullName);
//                            if (!check)
//                                s_conflictDate[sec.Security.SecurityDescription.FullName] = ask.LastUpdate;

//                            Debug.Assert(check,
//                                String.Format("Expected security: {0}; actual security: {1}",
//                                    sec.Security.SecurityDescription.FullName, ask.Security.FullName));
//                        }
//#endif

//                        double askPx = ask.Price;
//                        double askQty = ask.Quantity;
//                        if (Double.IsNaN(askPx) || (askPx <= Double.Epsilon))
//                            askPx = 2.0 * f; // Это одна из верхних оценок цены опциона.
//                        else
//                            askPx += shiftAsk;

//                        // Биды отсортированы ПО УБЫВАНИЮ!!!
//                        IQueueData bid = bidQueue[0]; // bidQueue.First();

//#if DEBUG
//                        if (!s_conflictDate.ContainsKey(sec.Security.SecurityDescription.FullName))
//                        {
//                            bool check = (bid.Security.FullName == sec.Security.SecurityDescription.FullName);
//                            if (!check)
//                                s_conflictDate[sec.Security.SecurityDescription.FullName] = bid.LastUpdate;

//                            Debug.Assert(check,
//                                String.Format("Expected security: {0}; actual security: {1}",
//                                    sec.Security.SecurityDescription.FullName, bid.Security.FullName));
//                        }
//#endif

//                        double bidPx = bid.Price;
//                        double bidQty = bid.Quantity;
//                        bidPx -= shiftBid;
//                        bidPx = Math.Max(bidPx, 0);

//                        optPx = (askPx + bidPx) / 2.0;
//                        qty = Math.Min(askQty, bidQty);

//                        optTime = (bid.LastUpdate < ask.LastUpdate) ? ask.LastUpdate : bid.LastUpdate;
//                    }
                    #endregion Нет данных в FinInfo
                }
            }
            else
                throw new NotImplementedException("OptPxMode:" + mode);

            return optPx;
        }

        /// <summary>
        /// Создание сплайна и заполнение SmileInfo по предложенной таблице
        /// </summary>
        /// <param name="externalContext">контекст блока для логгирования проблемы</param>
        /// <param name="f">цена БА (нажна как верхняя оценка цены колла</param>
        /// <param name="dT">время до экспирации</param>
        /// <param name="riskFreeRate">беспроцентная ставка</param>
        /// <param name="expiry">дата экспирации (без времени)</param>
        /// <param name="scriptTime">время в скрипте</param>
        /// <param name="baseTicker">тикер БА</param>
        /// <param name="res">Таблица с данными для интерполирования</param>
        /// <param name="info">Заполненный SmileInfo</param>
        public static bool TryPrepareSmileInfo(IContext externalContext,
            double f, double dT, double riskFreeRate, DateTime expiry, DateTime scriptTime,
            string baseTicker, InteractiveSeries res, out SmileInfo info)
        {
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            foreach (InteractiveObject obj in res.ControlPoints)
            {
                Point point = obj.Anchor.Value;
                xs.Add(point.X);
                ys.Add(point.Y);
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            info = new SmileInfo();
            info.F = f;
            info.dT = dT;
            info.Expiry = expiry;
            info.ScriptTime = scriptTime;
            info.RiskFreeRate = riskFreeRate;
            info.BaseTicker = baseTicker;

            try
            {
                if (xs.Count >= BaseCubicSpline.MinNumberOfNodes)
                {
                    NotAKnotCubicSpline spline = new NotAKnotCubicSpline(xs, ys);

                    info.ContinuousFunction = spline;
                    info.ContinuousFunctionD1 = spline.DeriveD1();

                    res.Tag = info;
                }
            }
            catch (DivideByZeroException dvbz)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine(dvbz.GetType().Name);
                sb.AppendLine();
                sb.AppendLine("X;Y");
                for (int j = 0; j < xs.Count; j++)
                {
                    sb.AppendFormat("{0};{1}",
                        xs[j].ToString(CultureInfo.InvariantCulture),
                        ys[j].ToString(CultureInfo.InvariantCulture));
                    sb.AppendLine();
                }
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine(dvbz.ToString());
                sb.AppendLine();

                externalContext.Log(sb.ToString(), MessageType.Error, true);

                return false;
            }
            catch (Exception ex)
            {
                externalContext.Log(ex.ToString(), MessageType.Error, true);
                return false;
            }

            xs.Clear();
            ys.Clear();

            return true;
        }
    }
}
