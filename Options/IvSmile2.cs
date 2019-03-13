using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Smile derived from option quotes (bar handler)
    /// \~russian Улыбка, построенная по котировкам опционов (побарный обработчик)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("IV Smile", Language = Constants.En)]
    [HelperName("IV Smile", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.OPTION_SERIES | TemplateTypes.INTERACTIVESPLINE, Name = Constants.OptionSeries)]
    [Input(3, TemplateTypes.DOUBLE, Name = Constants.RiskFreeRate)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Подразумеваемые волатильности опционов на основании их рыночных цен")]
    [HelperDescription("Smile derived from option quotes (bar handler)", Constants.En)]
    public class IvSmile2 : BaseSmileDrawing, IValuesHandlerWithNumber
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
        /// \~english Maximum volatility (percents)
        /// \~russian Максимальное допустимое значение волатильности
        /// </summary>
        [HelperName("Max Sigma, %", Constants.En)]
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
        public InteractiveSeries Execute(double price, double time, IOptionSeries optSer, int barNum)
        {
            InteractiveSeries res = Execute(price, time, optSer, 0.0, barNum);
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
        public InteractiveSeries Execute(double price, double time, IOptionSeries optSer, double rate, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return Constants.EmptySeries;

            double f = price;
            double dT = time;
            if (Double.IsNaN(f))
                //throw new ScriptException("Argument 'price' contains NaN for some strange reason. f:" + f);
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
                    putPx = IvSmile.GetOptPrice(m_context, f, sInfo.Put, m_optionPxMode, sInfo.Tick * m_shiftAsk, sInfo.Tick * m_shiftBid, out putQty, out putTime);
                }

                {
                    callPx = IvSmile.GetOptPrice(m_context, f, sInfo.Call, m_optionPxMode, sInfo.Tick * m_shiftAsk, sInfo.Tick * m_shiftBid, out callQty, out callTime);
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
        /// Обработчик под тип входных данных INTERACTIVESPLINE
        /// </summary>
        /// <param name="price">цена БА</param>
        /// <param name="time">время до экспирации в долях года</param>
        /// <param name="optPrices">опционные цены</param>
        /// <param name="barNum">индекс бара в серии</param>
        /// <returns>улыбка, восстановленная из цен опционов</returns>
        public InteractiveSeries Execute(double price, double time, InteractiveSeries optPrices, int barNum)
        {
            InteractiveSeries res = Execute(price, time, optPrices, 0.0, barNum);
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
        public InteractiveSeries Execute(double price, double time, InteractiveSeries optPrices, double rate, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optPrices == null) || (optPrices.ControlPoints.Count <= 0))
                return Constants.EmptySeries;

            IReadOnlyList<InteractiveObject> cps = optPrices.ControlPoints;

            double f = price;
            double dT = time;
            if (Double.IsNaN(f))
                //throw new ScriptException("Argument 'price' contains NaN for some strange reason. f:" + f);
                return Constants.EmptySeries;
            if ((dT < Double.Epsilon) || (Double.IsNaN(dT)))
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
                }
                else if (m_optionType == StrikeType.Call)
                {
                    callPx = strikeObj.Anchor.ValueY;
                    optPx = callPx;
                }
                else
                {
                    stradlePx = strikeObj.Anchor.ValueY;
                    optPx = stradlePx;
                }

                double sigma = Double.NaN;
                double putSigma = Double.NaN, callSigma = Double.NaN, stradleSigma = Double.NaN, precision;
                if (!Double.IsNaN(putPx))
                {
                    putSigma = FinMath.GetOptionSigma(f, strike, dT, putPx, rate, false, out precision);
                    putSigma = Math.Min(putSigma, m_maxSigma);
                    if (putSigma <= 0)
                        putSigma = Double.NaN;
                    else
                    {
                        if (m_optionType == StrikeType.Put)
                        {
                            sigma = putSigma;
                        }
                    }
                }
                if (!Double.IsNaN(callPx))
                {
                    callSigma = FinMath.GetOptionSigma(f, strike, dT, callPx, rate, true, out precision);
                    callSigma = Math.Min(callSigma, m_maxSigma);
                    if (callSigma <= 0)
                        callSigma = Double.NaN;
                    else
                    {
                        if (m_optionType == StrikeType.Call)
                        {
                            sigma = callSigma;
                        }
                    }
                }
                if (!Double.IsNaN(stradlePx))
                {
                    stradleSigma = FinMath.GetStradleSigma(f, strike, dT, stradlePx, rate, out precision);
                    stradleSigma = Math.Min(stradleSigma, m_maxSigma);
                    if (stradleSigma <= 0)
                        stradleSigma = Double.NaN;
                    else
                    {
                        if (m_optionType == StrikeType.Any)
                        {
                            sigma = stradleSigma;
                        }
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
                        //FillNodeInfo(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPx, putQty, putSigma, putTime, false);
                        controlPoints.Add(obj);
                    }
                }
                else if (m_optionType == StrikeType.Call)
                {
                    if (!Double.IsNaN(callSigma))
                    {
                        //FillNodeInfo(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPx, callQty, callSigma, callTime, false);
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
                            //    FillNodeInfo(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPx, putQty, putSigma, putTime, false);
                            //else
                            //    FillNodeInfo(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPx, callQty, callSigma, callTime, false);
                        }
                        else if (m_optionPxMode == OptionPxMode.Bid)
                        {
                            //if (putSigma > callSigma)
                            //    FillNodeInfo(ip, f, dT, sInfo, StrikeType.Put, m_optionPxMode, putPx, putQty, putSigma, putTime, false);
                            //else
                            //    FillNodeInfo(ip, f, dT, sInfo, StrikeType.Call, m_optionPxMode, callPx, callQty, callSigma, callTime, false);
                        }

                        controlPoints.Add(obj);
                    }
                }
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            SmileInfo info;
            if (!IvSmile.TryPrepareSmileInfo(m_context, f, dT, rate, new DateTime(), new DateTime(), null,  res, out info))
                return Constants.EmptySeries;

            return res;
        }
    }
}
