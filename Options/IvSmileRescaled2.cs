using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Smile derived from option quotes with scale multiplier (bar handler, for Deribit)
    /// \~russian "Подразумеваемые волатильности опционов на основании их рыночных цен (c учетом необходимости домножить цены на курс конвертации)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTickDeribit)]
    [HelperName("IV Smile (Deribit)", Language = Constants.En)]
    [HelperName("IV Smile (Deribit)", Language = Constants.Ru)]
    [InputsCount(5)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.OPTION_SERIES | TemplateTypes.INTERACTIVESPLINE, Name = Constants.OptionSeries)]
    [Input(3, TemplateTypes.DOUBLE, Name = "ScaleMultiplier" /* Constants.Scale */)]
    [Input(4, TemplateTypes.DOUBLE, Name = Constants.RiskFreeRate)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Подразумеваемые волатильности опционов на основании их рыночных цен (c учетом необходимости домножить цены на курс конвертации)")]
    [HelperDescription("Smile derived from option quotes with scale multiplier (bar handler, for Deribit)", Constants.En)]
    public class IvSmileRescaled2 : BaseSmileDrawing, IValuesHandlerWithNumber
    {
        private double m_maxSigma = 2.0;
        private double m_shiftBid = 0, m_shiftAsk = 0;
        private StrikeType m_optionType = StrikeType.Any;
        private OptionPxMode m_optionPxMode = OptionPxMode.Ask;

        #region Parameters
        /// <summary>
        /// \~english Algorythm to get option price
        /// \~russian Алгоритм расчета цены опциона
        /// </summary>
        [HelperName("Price mode", Constants.En)]
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
        [HelperName("Option type", Constants.En)]
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
        /// \~english Maximum volatility (percents)
        /// \~russian Максимальное допустимое значение волатильности
        /// </summary>
        [HelperName("Max sigma, %", Constants.En)]
        [HelperName("Максимальная волатильность, %", Constants.Ru)]
        [Description("Максимальное допустимое значение волатильности")]
        [HelperDescription("Maximum volatility (percents)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "200", Min = "0", Max = "1000000", Step = "1")]
        public double MaxSigmaPct
        {
            get { return m_maxSigma * Constants.PctMult; }
            set { m_maxSigma = value / Constants.PctMult; }
        }

        //[Description("Цвет бидов")]
        //[HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "DarkCyan")]
        //public System.Windows.Media.Color BidColor
        //{
        //    get { return bidColor; }
        //    set { bidColor = value; }
        //}
        #endregion Parameters

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES
        /// </summary>
        /// <param name="price">цена БА</param>
        /// <param name="time">время до экспирации в долях года</param>
        /// <param name="optSer">опционная серия</param>
        /// <param name="barNum">индекс бара в серии</param>
        /// <returns>улыбка, восстановленная из цен опционов</returns>
        public InteractiveSeries Execute(double price, double time, IOptionSeries optSer, double scaleMult, int barNum)
        {
            InteractiveSeries res = Execute(price, time, optSer, scaleMult, 0.0, barNum);
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES
        /// </summary>
        /// <param name="price">цена БА</param>
        /// <param name="time">время до экспирации в долях года</param>
        /// <param name="optSer">опционная серия</param>
        /// <param name="rate">процентная ставка</param>
        /// <param name="barNum">индекс бара в серии</param>
        /// <returns>улыбка, восстановленная из цен опционов</returns>
        public InteractiveSeries Execute(double price, double time, IOptionSeries optSer, double scaleMult, double rate, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return Constants.EmptySeries;

            double f = price;
            double dT = time;
            if (!DoubleUtil.IsPositive(f))
                //throw new ScriptException("Argument 'price' contains NaN for some strange reason. f:" + f);
                return Constants.EmptySeries;
            if (!DoubleUtil.IsPositive(scaleMult))
                //throw new ScriptException("Argument 'scaleMult' contains NaN for some strange reason. scaleMult:" + scaleMult);
                return Constants.EmptySeries;
            if (!DoubleUtil.IsPositive(dT))
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

                double putPxBtc, callPxBtc, putPxUsd, callPxUsd;
                double putQty, callQty;
                DateTime putTime, callTime;
                {
                    putPxBtc = IvSmile.GetOptPrice(m_context, f, sInfo.Put, m_optionPxMode, sInfo.Tick * m_shiftAsk, sInfo.Tick * m_shiftBid, out putQty, out putTime);
                    putPxUsd = putPxBtc * scaleMult;
                    // Здесь нельзя сразу домножать на scaleMultiplier! Потому что тогда в метод FillNodeInfo пойдут бредовые цены.
                }

                {
                    callPxBtc = IvSmile.GetOptPrice(m_context, f, sInfo.Call, m_optionPxMode, sInfo.Tick * m_shiftAsk, sInfo.Tick * m_shiftBid, out callQty, out callTime);
                    callPxUsd = callPxBtc * scaleMult;
                    // Здесь нельзя сразу домножать на scaleMultiplier! Потому что тогда в метод FillNodeInfo пойдут бредовые цены.
                }

                double putSigma = Double.NaN, callSigma = Double.NaN, precision;
                if (DoubleUtil.IsPositive(putPxBtc))
                {
                    // Цену опциона переводим в баксы только в момент вычисления айви
                    putSigma = FinMath.GetOptionSigma(f, sInfo.Strike, dT, putPxUsd, rate, false, out precision);
                    putSigma = Math.Min(putSigma, m_maxSigma);
                    if (putSigma <= 0)
                        putSigma = Double.NaN;
                }
                if (DoubleUtil.IsPositive(callPxBtc))
                {
                    // Цену опциона переводим в баксы только в момент вычисления айви
                    callSigma = FinMath.GetOptionSigma(f, sInfo.Strike, dT, callPxUsd, rate, true, out precision);
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
                    if (DoubleUtil.IsPositive(putSigma))
                    {
                        // Здесь используем первичную цену в том виде, как ее нам дал Дерибит
                        FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPxBtc, putQty, putSigma, putTime, false, rate, scaleMult);
                        controlPoints.Add(obj);
                    }
                }
                else if (m_optionType == StrikeType.Call)
                {
                    if (DoubleUtil.IsPositive(callSigma))
                    {
                        // Здесь используем первичную цену в том виде, как ее нам дал Дерибит
                        FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPxBtc, callQty, callSigma, callTime, false, rate, scaleMult);
                        controlPoints.Add(obj);
                    }
                }
                else if (m_optionType == StrikeType.Any)
                {
                    if (DoubleUtil.IsPositive(putSigma) && DoubleUtil.IsPositive(callSigma))
                    {
                        // Здесь используем первичную цену в том виде, как ее нам дал Дерибит
                        if (m_optionPxMode == OptionPxMode.Ask)
                        {
                            if (putSigma < callSigma)
                                FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPxBtc, putQty, putSigma, putTime, false, rate, scaleMult);
                            else
                                FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPxBtc, callQty, callSigma, callTime, false, rate, scaleMult);
                        }
                        else if (m_optionPxMode == OptionPxMode.Bid)
                        {
                            if (putSigma > callSigma)
                                FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPxBtc, putQty, putSigma, putTime, false, rate, scaleMult);
                            else
                                FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPxBtc, callQty, callSigma, callTime, false, rate, scaleMult);
                        }

                        controlPoints.Add(obj);
                    }
                    else if (DoubleUtil.IsPositive(putSigma) && Double.IsNaN(callSigma))
                    {
                        // Здесь используем первичную цену в том виде, как ее нам дал Дерибит
                        FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPxBtc, putQty, putSigma, putTime, false, rate, scaleMult);
                        controlPoints.Add(obj);
                    }
                    else if (Double.IsNaN(putSigma) && DoubleUtil.IsPositive(callSigma))
                    {
                        // Здесь используем первичную цену в том виде, как ее нам дал Дерибит
                        FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPxBtc, callQty, callSigma, callTime, false, rate, scaleMult);
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
        /// Обработчик под тип входных данных INTERACTIVESPLINE
        /// </summary>
        /// <param name="price">цена БА</param>
        /// <param name="time">время до экспирации в долях года</param>
        /// <param name="optPrices">опционные цены</param>
        /// <param name="barNum">индекс бара в серии</param>
        /// <returns>улыбка, восстановленная из цен опционов</returns>
        public InteractiveSeries Execute(double price, double time, InteractiveSeries optPrices, double scaleMult, int barNum)
        {
            InteractiveSeries res = Execute(price, time, optPrices, scaleMult, 0.0, barNum);
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных INTERACTIVESPLINE
        /// </summary>
        /// <param name="price">цена БА</param>
        /// <param name="time">время до экспирации в долях года</param>
        /// <param name="optPrices">опционные цены</param>
        /// <param name="rate">процентная ставка</param>
        /// <param name="barNum">индекс бара в серии</param>
        /// <returns>улыбка, восстановленная из цен опционов</returns>
        public InteractiveSeries Execute(double price, double time, InteractiveSeries optPrices, double scaleMult, double rate, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optPrices == null) || (optPrices.ControlPoints.Count <= 0))
                return Constants.EmptySeries;

            IReadOnlyList<InteractiveObject> cps = optPrices.ControlPoints;

            double f = price;
            double dT = time;
            if (!DoubleUtil.IsPositive(f))
                //throw new ScriptException("Argument 'price' contains NaN for some strange reason. f:" + f);
                return Constants.EmptySeries;
            if (!DoubleUtil.IsPositive(scaleMult))
                //throw new ScriptException("Argument 'scaleMult' contains NaN for some strange reason. scaleMult:" + scaleMult);
                return Constants.EmptySeries;
            if (!DoubleUtil.IsPositive(dT))
                return Constants.EmptySeries;
            if (Double.IsNaN(rate))
                //throw new ScriptException("Argument 'rate' contains NaN for some strange reason. rate:" + rate);
                return Constants.EmptySeries;

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            for (int j = 0; j < cps.Count; j++)
            {
                InteractiveObject strikeObj = cps[j];
                double strike = strikeObj.Anchor.ValueX;
                // Сверхдалекие страйки игнорируем
                if ((strike < m_minStrike) || (m_maxStrike < strike))
                    continue;

                double optPx;
                double stradlePx = Double.NaN;
                double putPx = Double.NaN, callPx = Double.NaN;
                if (m_optionType == StrikeType.Put)
                {
                    putPx = strikeObj.Anchor.ValueY;
                    optPx = putPx;
                    // Здесь нельзя сразу домножать на scaleMultiplier! Потому что тогда в метод FillNodeInfo пойдут бредовые цены.
                }
                else if (m_optionType == StrikeType.Call)
                {
                    callPx = strikeObj.Anchor.ValueY;
                    optPx = callPx;
                    // Здесь нельзя сразу домножать на scaleMultiplier! Потому что тогда в метод FillNodeInfo пойдут бредовые цены.
                }
                else
                {
                    stradlePx = strikeObj.Anchor.ValueY;
                    optPx = stradlePx;
                    // Здесь нельзя сразу домножать на scaleMultiplier! Потому что тогда в метод FillNodeInfo пойдут бредовые цены.
                }

                double sigma = Double.NaN;
                double putSigma = Double.NaN, callSigma = Double.NaN, stradleSigma = Double.NaN, precision;
                if (DoubleUtil.IsPositive(putPx))
                {
                    // Цену опциона переводим в баксы только в момент вычисления айви
                    putSigma = FinMath.GetOptionSigma(f, strike, dT, putPx * scaleMult, rate, false, out precision);
                    putSigma = Math.Min(putSigma, m_maxSigma);
                    if (putSigma <= 0)
                        putSigma = Double.NaN;
                    else
                    {
                        if (m_optionType == StrikeType.Put)
                            sigma = putSigma;
                    }
                }
                if (DoubleUtil.IsPositive(callPx))
                {
                    // Цену опциона переводим в баксы только в момент вычисления айви
                    callSigma = FinMath.GetOptionSigma(f, strike, dT, callPx * scaleMult, rate, true, out precision);
                    callSigma = Math.Min(callSigma, m_maxSigma);
                    if (callSigma <= 0)
                        callSigma = Double.NaN;
                    else
                    {
                        if (m_optionType == StrikeType.Call)
                            sigma = callSigma;
                    }
                }
                if (DoubleUtil.IsPositive(stradlePx))
                {
                    // Цену опциона переводим в баксы только в момент вычисления айви
                    stradleSigma = FinMath.GetStradleSigma(f, strike, dT, stradlePx * scaleMult, rate, out precision);
                    stradleSigma = Math.Min(stradleSigma, m_maxSigma);
                    if (stradleSigma <= 0)
                        stradleSigma = Double.NaN;
                    else
                    {
                        if (m_optionType == StrikeType.Any)
                            sigma = stradleSigma;
                    }
                }

                if (Double.IsNaN(sigma) || (sigma <= 0) ||
                    Double.IsNaN(optPx) || (optPx <= 0))
                    continue;

                InteractivePointActive ip = new InteractivePointActive();
                {
                    //ip.Color = (m_optionPxMode == OptionPxMode.Ask) ? Colors.DarkOrange : Colors.DarkCyan;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect; // (optionPxMode == OptionPxMode.Ask) ? Geometries.Rect : Geometries.Rect;
                    //ip.IsActive = true;
                    ip.Value = new Point(strike, sigma);
                    string nowStr = DateTime.Now.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture);
                    ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "K:{0}; IV:{1:#0.00}%\r\n{2} {3} @ {4}\r\nDate: {5}",
                        strike, sigma * Constants.PctMult, m_optionType, optPx, 1, nowStr);
                }

                InteractiveObject obj = new InteractiveObject(ip);

                if (m_optionType == StrikeType.Put)
                {
                    if (!Double.IsNaN(putSigma))
                    {
                        //FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPx, putQty, putSigma, putTime, false);
                        controlPoints.Add(obj);
                    }
                }
                else if (m_optionType == StrikeType.Call)
                {
                    if (!Double.IsNaN(callSigma))
                    {
                        //FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPx, callQty, callSigma, callTime, false);
                        controlPoints.Add(obj);
                    }
                }
                else if (m_optionType == StrikeType.Any)
                {
                    if (!Double.IsNaN(stradleSigma))
                    {
                        if (m_optionPxMode == OptionPxMode.Ask)
                        {
                            //if (putSigma < callSigma)
                            //    FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPx, putQty, putSigma, putTime, false);
                            //else
                            //    FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPx, callQty, callSigma, callTime, false);
                        }
                        else if (m_optionPxMode == OptionPxMode.Bid)
                        {
                            //if (putSigma > callSigma)
                            //    FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPx, putQty, putSigma, putTime, false);
                            //else
                            //    FillNodeInfoDeribit(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPx, callQty, callSigma, callTime, false);
                        }

                        controlPoints.Add(obj);
                    }
                }
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            SmileInfo info;
            if (!IvSmile.TryPrepareSmileInfo(m_context, f, dT, rate, new DateTime(), new DateTime(), null, res, out info))
                return Constants.EmptySeries;

            return res;
        }

        internal static void FillNodeInfoDeribit(InteractivePointActive ip,
            double f, double dT, IOptionStrikePair sInfo,
            StrikeType optionType, OptionPxMode optPxMode,
            double optPx, double optQty, double optSigma, DateTime optTime, bool returnPct, double riskfreeRatePct, double scaleMult)
        {
            // Вызов базовой реализации
            FillNodeInfo(ip, f, dT, sInfo, optionType, optPxMode, optPx, optQty, optSigma, optTime, returnPct, riskfreeRatePct);
            
            // Заменяю тултип, чтобы в нем были и биткойны и доллары
            var opTypeStr = String.Intern(optionType.ToString());
            var opTypeSpace = String.Intern(new string(' ', opTypeStr.Length));
            // Пробелы по виду занимают меньше места, чем буквы, поэтому чуть-чуть подравниваю?
            opTypeSpace += "  ";
            if (optQty > 0)
            {
                ip.Tooltip = String.Format(CultureInfo.InvariantCulture,
                    " K:{0}; IV:{1:###0.00}%\r\n" +
                    " {2} px(B) {3:##0.0000} qty {4}\r\n" +
                    " {5} px($) {6:######0.00} qty {4}",
                    sInfo.Strike, optSigma * Constants.PctMult, opTypeStr, optPx, optQty,
                    opTypeSpace, optPx * scaleMult);
            }
            else
            {
                ip.Tooltip = String.Format(CultureInfo.InvariantCulture,
                    " K:{0}; IV:{1:###0.00}%\r\n" +
                    " {2} px(B) {3:##0.0000}\r\n" +
                    " {4} px($) {5:######0.00}",
                    sInfo.Strike, optSigma * Constants.PctMult, opTypeStr, optPx,
                    opTypeSpace, optPx * scaleMult);
            }
        }
    }
}
