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
    /// \~english A single series position profile (change of currency rate is not included) as a function of BA price (special for Deribit)
    /// \~russian Профиль позиции для одиночной опционной серии (без учета валютной составляющей) как функция цены БА (c учетом необходимости домножить цены на курс конвертации)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTickDeribit /* HandlerCategories.OptionsPositions */)]
    [HelperName("Single series profile (Deribit)", Language = Constants.En)]
    [HelperName("Профиль позиции (одна серия, Deribit)", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(3, TemplateTypes.DOUBLE, Name = "ScaleMultiplier" /* Constants.Scale */)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Профиль позиции для одиночной опционной серии (без учета валютной составляющей) как функция цены БА (c учетом необходимости домножить цены на курс конвертации)")]
    [HelperDescription("A single series position profile (change of currency rate is not included) as a function of BA price (special for Deribit)", Constants.En)]
    public class SingleSeriesProfileDeribit : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0";
        private const string DefaultBtcTooltipFormat = "0.0000";

        /// <summary>
        /// Номинал фьючерса (в USD!) по умолчанию
        /// </summary>
        public const string DefaultFutNominal = "10";

        private bool m_twoSideDelta = false;
        private string m_tooltipFormat = DefaultTooltipFormat;
        private double m_futNominal = Double.Parse(DefaultFutNominal);
        private NumericalGreekAlgo m_greekAlgo = NumericalGreekAlgo.ShiftingSmile;

        #region Parameters
        /// <summary>
        /// \~english FrozenSmile - smile is frozen; ShiftingSmile - smile shifts horizontally without modification
        /// \~russian FrozenSmile - улыбка заморожена; ShiftingSmile - улыбка без искажений сдвигается по горизонтали вслед за БА
        /// </summary>
        [HelperName("Greek Algo", Constants.En)]
        [HelperName("Алгоритм улыбки", Constants.Ru)]
        [Description("FrozenSmile -- улыбка заморожена; ShiftingSmile -- улыбка без искажений сдвигается по горизонтали вслед за БА")]
        [HelperDescription("FrozenSmile - smile is frozen; ShiftingSmile - smile shifts horizontally without modification", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "ShiftingSmile")]
        public NumericalGreekAlgo GreekAlgo
        {
            get { return m_greekAlgo; }
            set { m_greekAlgo = value; }
        }

        /// <summary>
        /// \~english Tooltip format (i.e. '0.00', '0.0##' etc)
        /// \~russian Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.
        /// </summary>
        [HelperName("Tooltip Format", Constants.En)]
        [HelperName("Формат подсказки", Constants.Ru)]
        [Description("Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.")]
        [HelperDescription("Tooltip format (i.e. '0.00', '0.0##' etc)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultTooltipFormat)]
        public string TooltipFormat
        {
            get { return m_tooltipFormat; }
            set
            {
                if (!String.IsNullOrWhiteSpace(value))
                {
                    try
                    {
                        string yStr = Math.PI.ToString(value);
                        m_tooltipFormat = value;
                    }
                    catch
                    {
                        m_context.Log("Tooltip format error. I'll keep old one: " + m_tooltipFormat, MessageType.Warning, true);
                    }
                }
            }
        }

        ///// <summary>
        ///// \~english Number of additional nodes near money
        ///// \~russian Количество дополнительных узлов около денег
        ///// </summary>
        //[HelperName("Nodes Count", Constants.En)]
        //[HelperName("Доп. узлы", Constants.Ru)]
        //[Description("Количество дополнительных узлов около денег")]
        //[HelperDescription("Number of additional nodes near money", Constants.En)]
        //[HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Name = "Nodes Count",
        //    Default = "0", Min = "0", Max = "1000000", Step = "1", NumberDecimalDigits = 0)]
        //public int NodesCount
        //{
        //    get { return m_nodesCount; }
        //    set
        //    {
        //        if (value >= 0)
        //            m_nodesCount = value;
        //    }
        //}

        /// <summary>
        /// \~english Calculate delta to the left and to the right from the strike
        /// \~russian Вычислить дельту с двух сторон от страйка
        /// </summary>
        [HelperName("Two side delta", Constants.En)]
        [HelperName("Две дельты", Constants.Ru)]
        [Description("Вычислить дельту с двух сторон от страйка")]
        [HelperDescription("Calculate delta to the left and to the right from the strike", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool TwoSideDelta
        {
            get { return m_twoSideDelta; }
            set { m_twoSideDelta = value; }
        }

        /// <summary>
        /// \~english Nominal value of Deribit futures (by default is 10 USD)
        /// \~russian Номинальный размер фьючерса Дерибит (по умолчанию 10 USD)
        /// </summary>
        [HelperName("Futures nominal", Constants.En)]
        [HelperName("Номинал фьючерсов", Constants.Ru)]
        [Description("Номинальный размер фьючерса Дерибит (по умолчанию 10 USD)")]
        [HelperDescription("Nominal value of Deribit futures (by default is 10 USD)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultFutNominal)]
        public double FutNominal
        {
            get
            {
                return m_futNominal;
            }
            set
            {
                //// Новое значение принимается только если все хорошо?
                //if (DoubleUtil.IsPositive(value))
                m_futNominal = value;
            }
        }

        /// <summary>
        /// \~english Calculate profile as bitcoins
        /// \~russian Вычислить профиль позиции в биткойнах
        /// </summary>
        [HelperName("Profile in BTC?", Constants.En)]
        [HelperName("Профиль в биткойнах?", Constants.Ru)]
        [Description("Вычислить профиль позиции в биткойнах")]
        [HelperDescription("Calculate profile as bitcoins", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool ProfileAsBtc { get; set; }
        #endregion Parameters

        public InteractiveSeries Execute(double time, InteractiveSeries smile, IOptionSeries optSer, double btcUsdIndex, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (smile == null) || (optSer == null))
                return Constants.EmptySeries;

            int lastBarIndex = optSer.UnderlyingAsset.Bars.Count - 1;
            DateTime now = optSer.UnderlyingAsset.Bars[Math.Min(barNum, lastBarIndex)].Date;
            bool wasInitialized = HandlerInitializedToday(now);

            double dT = time;
            if (!DoubleUtil.IsPositive(dT))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (!DoubleUtil.IsPositive(btcUsdIndex))
            {
                // TODO: сделать ресурс! [{0}] Price of BTC/USD must be positive value. scaleMult:{1}
                //string msg = RM.GetStringFormat("OptHandlerMsg.CurrencyScaleMustBePositive", GetType().Name, scaleMult);
                string msg = String.Format("[{0}] Price of BTC/USD must be positive value. scaleMult:{1}", GetType().Name, btcUsdIndex);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if (oldInfo == null)
            {
                string msg = String.Format("[{0}] Property Tag of object smile must be filled with SmileInfo. Tag:{1}", GetType().Name, smile.Tag);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.EmptySeries;
            }

            double futPx = oldInfo.F;

            double ivAtm;
            if (!oldInfo.ContinuousFunction.TryGetValue(futPx, out ivAtm))
            {
                string msg = String.Format("[{0}] Unable to get IV ATM from smile. Tag:{1}", GetType().Name, smile.Tag);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            if (pairs.Length < 2)
            {
                string msg = String.Format("[{0}] optSer must contain few strike pairs. pairs.Length:{1}", GetType().Name, pairs.Length);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptySeries;
            }

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            double futStep = optSer.UnderlyingAsset.Tick;
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            ReadOnlyCollection<IPosition> basePositions = posMan.GetClosedOrActiveForBar(optSer.UnderlyingAsset);
            var optPositions = SingleSeriesProfile.GetAllOptionPositions(posMan, pairs);

            SortedDictionary<double, IOptionStrikePair> futPrices;
            if (!SmileImitation5.TryPrepareImportantPoints(pairs, futPx, futStep, -1, out futPrices))
            {
                string msg = String.Format("[{0}] It looks like there is no suitable points for the smile. pairs.Length:{1}", GetType().Name, pairs.Length);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptySeries;
            }

            // Чтобы учесть базис между фьючерсом и индексом, вычисляю их отношение:
            // Пример: BtcInd==9023; FutPx==8937 --> indexDivByFutRatio == 1.009623
            double indexDivByFutRatio = btcUsdIndex / futPx;

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            // Список точек для вычисления дельт + улыбки для этих точек
            var deltaPoints = new List<Tuple<double, InteractivePointActive>>();
            foreach (var kvp in futPrices)
            {
                // Если у нас новая цена фьючерса, значит, будет новая цена индекса
                double f = kvp.Key;
                // И при пересчете опционов в баксы НУЖНО ИСПОЛЬЗОВАТЬ ИМЕННО ЕЁ!!!
                double newScaleMult = f * indexDivByFutRatio;

                bool tradableStrike = (kvp.Value != null);

                CashPnlUsd cashPnlUsd;
                CashPnlBtc cashPnlBtc;
                GetBasePnl(basePositions, lastBarIndex, f, m_futNominal, out cashPnlUsd, out cashPnlBtc);
                double cashDollars = cashPnlUsd.CashUsd;
                double pnlDollars = cashPnlUsd.PnlUsd;
                double cashBtc = cashPnlBtc.CashBtc;
                double pnlBtc = cashPnlBtc.PnlBtc;

                SmileInfo actualSmile = SingleSeriesProfile.GetActualSmile(oldInfo, m_greekAlgo, f);

                // Флаг того, что ПНЛ по всем инструментам был расчитан верно
                bool pnlIsCorrect = true;
                for (int j = 0; (j < pairs.Length) && pnlIsCorrect; j++)
                {
                    var tuple = optPositions[j];
                    IOptionStrikePair pair = pairs[j];
                    //int putBarCount = pair.Put.UnderlyingAsset.Bars.Count;
                    //int callBarCount = pair.Call.UnderlyingAsset.Bars.Count;

                    CashPnlUsd pairCashUsd;
                    CashPnlBtc pairCashBtc;
                    bool localRes = TryGetPairPnl(actualSmile, pair.Strike, lastBarIndex, lastBarIndex,
                        tuple.Item1, tuple.Item2, f, dT, newScaleMult,
                        out pairCashUsd, out pairCashBtc);

                    pnlIsCorrect &= localRes;
                    cashDollars += pairCashUsd.CashUsd;
                    pnlDollars += pairCashUsd.PnlUsd;
                    cashBtc += pairCashBtc.CashBtc;
                    pnlBtc += pairCashBtc.PnlBtc;
                }

                // Профиль позиции будет рисоваться только если ПНЛ был посчитан верно по ВСЕМ инструментам!
                if (pnlIsCorrect)
                {
                    InteractivePointLight ip;
                    // Показаны будут только узлы, совпадающие с реальными страйками.
                    // Потенциально это позволит сделать эти узлы пригодными для торговли по клику наподобие улыбки.
                    if (m_showNodes && tradableStrike)
                    {
                        // ReSharper disable once UseObjectOrCollectionInitializer
                        InteractivePointActive tmp = new InteractivePointActive();

                        tmp.IsActive = m_showNodes && tradableStrike;
                        string pnlUsdStr = (cashDollars + pnlDollars).ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                        string pnlBtcStr = (cashBtc + pnlBtc).ToString(DefaultBtcTooltipFormat, CultureInfo.InvariantCulture);
                        tmp.Tooltip = String.Format(CultureInfo.InvariantCulture, " F: {0}\r\n PnL($): {1}\r\n PnL(B): {2}", f, pnlUsdStr, pnlBtcStr);

                        // Если у нас получился сплайн по профилю позиции, значит мы можем вычислить дельту!
                        if (m_showNodes && tradableStrike)
                        {
                            // Готовим важные точки
                            var tuple = new Tuple<double, InteractivePointActive>(f, tmp);
                            deltaPoints.Add(tuple);
                        }

                        ip = tmp;
                    }
                    else
                        ip = new InteractivePointLight();
                    
                    // PROD-6103 - Выводить профиль позиции в биткойнах
                    if (ProfileAsBtc)
                        ip.Value = new Point(f, cashBtc + pnlBtc);
                    else
                        ip.Value = new Point(f, cashDollars + pnlDollars);

                    controlPoints.Add(new InteractiveObject(ip));

                    xs.Add(f);
                    // PROD-6103 - Выводить профиль позиции в биткойнах
                    if (ProfileAsBtc)
                        ys.Add(cashBtc + pnlBtc);
                    else
                        ys.Add(cashDollars + pnlDollars);
                } // End if (pnlIsCorrect)
            } // End foreach (var kvp in futPrices)

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            try
            {
                if (xs.Count >= BaseCubicSpline.MinNumberOfNodes)
                {
                    SmileInfo info = new SmileInfo();
                    info.F = oldInfo.F;
                    info.dT = oldInfo.dT;
                    info.RiskFreeRate = oldInfo.RiskFreeRate;

                    NotAKnotCubicSpline spline = new NotAKnotCubicSpline(xs, ys);

                    info.ContinuousFunction = spline;
                    info.ContinuousFunctionD1 = spline.DeriveD1();

                    res.Tag = info;

                    // Если у нас получился сплайн по профилю позиции, значит мы можем вычислить дельту!
                    for (int j = 1; j < deltaPoints.Count - 1; j++)
                    {
                        var tuple = deltaPoints[j];
                        double f = tuple.Item1;
                        var ip = tuple.Item2;

                        double actualDelta, deltaLeft, deltaRight;
                        if (m_twoSideDelta)
                        {
                            double prevF = deltaPoints[j - 1].Item1;
                            double nextF = deltaPoints[j + 1].Item1;

                            double currY = deltaPoints[j].Item2.ValueY;
                            double prevY = deltaPoints[j - 1].Item2.ValueY;
                            double nextY = deltaPoints[j + 1].Item2.ValueY;

                            deltaLeft = (currY - prevY) / (f - prevF);
                            deltaRight = (nextY - currY) / (nextF - f);
                            // Считаем дельты слева и справа
                            // Мы передвинули улыбку в точку f и считаем дельту позиции В ЭТОЙ ЖЕ ТОЧКЕ(!)
                            //if (info.ContinuousFunction.TryGetValue(f - 100 * futStep, out deltaLeft) &&
                            //    info.ContinuousFunctionD1.TryGetValue(f + 100 * futStep, out deltaRight))
                            {
                                // Первый пробел уже учтен в Tooltip
                                ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "{0}\r\n LeftD: {1:0.0}; RightD: {2:0.0}",
                                    ip.Tooltip, deltaLeft, deltaRight);
                            }
                        }
                        else
                        {
                            // Мы передвинули улыбку в точку f и считаем дельту позиции В ЭТОЙ ЖЕ ТОЧКЕ(!)
                            if (info.ContinuousFunctionD1.TryGetValue(f, out actualDelta))
                            {
                                // Первый пробел уже учтен в Tooltip
                                ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "{0}\r\n D: {1:0.0}", ip.Tooltip, actualDelta);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_context.Log(ex.ToString(), MessageType.Error, true);
                return Constants.EmptySeries;
            }

            SetHandlerInitialized(now, true);

            return res;
        }

        public static void GetBasePnl(IList<IPosition> positions, int barNum, double f, double futNominal,
            out double baseCashUsd, out double basePnlUsd)
        {
            CashPnlUsd cashPnlUsd;
            CashPnlBtc cashPnlBtc;

            GetBasePnl(positions, barNum, f, futNominal,
                out cashPnlUsd, out cashPnlBtc);

            baseCashUsd = cashPnlUsd.CashUsd;
            basePnlUsd = cashPnlUsd.PnlUsd;

            //return res;
        }

        public static void GetBasePnl(IList<IPosition> positions, int barNum, double f, double futNominal,
            out CashPnlUsd cashPnlUsd, out CashPnlBtc cashPnlBtc)
        {
            double pnlBtc = 0;
            double pnlUsd = 0;
            double cashBtc = 0;
            double cashUsd = 0;
            int len = positions.Count;
            for (int j = 0; j < len; j++)
            {
                IPosition pos = positions[j];
                // Пока что State лучше не трогать 
                //if (pos.PositionState == PositionState.HaveError)
                {
                    int sign = pos.IsLong ? 1 : -1;
                    double qty = Math.Abs(pos.Shares);
                    double begPx = pos.GetBalancePrice(barNum);
                    // PROD-6085 - PnL(в битках) = (Позиция в лотах) * (номинал 10 долларов) * (1/BegPx - 1/EndPx)
                    // На обычном рынке знак "минус" стоит в честь того, что при покупке инструмента наличные средства уменьшаются,
                    // а прибыль вычисляется пропорционально величине (EndPx - BegPx).
                    // Но с биткойном все наоборот: из НАЧАЛЬНОЙ цены вычисляют конечную (1/BegPx - 1/EndPx)
                    // {Это нужно, чтобы на росте фьючерса получать прибыль}
                    // Поэтому требуется еще одно инвертирование знака.
                    cashBtc += sign * qty * futNominal / begPx;
                    pnlBtc -= sign * qty * futNominal / f;

                    //// TODO: Учет комиссии
                    //cashBtc -= pos.EntryCommission;

                    if (!pos.IsActiveForBar(barNum))
                    {
                        // Знак "ПЛЮС" стоит в честь того, что при ЗАКРЫТИИ ЛОНГА наличные средства УВЕЛИЧИВАЮТСЯ
                        // Но еще помним про инвертирование на Дерибите
                        cashBtc -= sign * qty * futNominal / pos.ExitPrice;
                        pnlBtc += sign * qty * futNominal / f;

                        //// TODO: Учет комиссии
                        //cashBtc -= pos.ExitCommission;
                    }
                }
            } // End for (int j = 0; j < len; j++)

            // Также насколько понял описание на сайте Дерибит, упрощается конвертация в доллары
            // https://www.deribit.com/main#/pages/docs/futures   (section 4: Example)
            // При этом для перевода в баксы используется КОНЕЧНЫЙ КУРС EndPx!
            cashUsd = cashBtc * f;
            pnlUsd = pnlBtc * f;

            cashPnlUsd = new CashPnlUsd(cashUsd, pnlUsd);
            cashPnlBtc = new CashPnlBtc(cashBtc, pnlBtc);
        }

        /// <summary>
        /// Получить финансовые параметры опционной позиции (колы и путы на одном страйке суммарно)
        /// </summary>
        /// <param name="smileInfo">улыбка</param>
        /// <param name="strike">страйк</param>
        /// <param name="putBarCount">количество баров для пута</param>
        /// <param name="callBarCount">количество баров для кола</param>
        /// <param name="putPositions">позиции пута</param>
        /// <param name="callPositions">позиции кола</param>
        /// <param name="f">текущая цена БА</param>
        /// <param name="dT">время до экспирации</param>
        /// <param name="cash">денежные затраты на формирование позы (могут быть отрицательными)</param>
        /// <param name="pnl">текущая цена позиции</param>
        /// <returns>true, если всё посчитано без ошибок</returns>
        public static bool TryGetPairPnl(SmileInfo smileInfo, double strike,
            int putBarCount, int callBarCount,
            IList<IPosition> putPositions, IList<IPosition> callPositions,
            double f, double dT, double btcUsdInd,
            out double cashUsd, out double pnlUsd)
        {
            CashPnlUsd cashPnlUsd;
            CashPnlBtc cashPnlBtc;

            bool res = TryGetPairPnl(smileInfo, strike,
                putBarCount, callBarCount,
                putPositions, callPositions,
                f, dT, btcUsdInd,
                out cashPnlUsd, out cashPnlBtc);

            cashUsd = cashPnlUsd.CashUsd;
            pnlUsd = cashPnlUsd.PnlUsd;

            return res;
        }

        /// <summary>
        /// Получить финансовые параметры опционной позиции (колы и путы на одном страйке суммарно)
        /// </summary>
        /// <param name="smileInfo">улыбка</param>
        /// <param name="strike">страйк</param>
        /// <param name="putBarCount">количество баров для пута</param>
        /// <param name="callBarCount">количество баров для кола</param>
        /// <param name="putPositions">позиции пута</param>
        /// <param name="callPositions">позиции кола</param>
        /// <param name="f">текущая цена БА</param>
        /// <param name="dT">время до экспирации</param>
        /// <param name="cash">денежные затраты на формирование позы (могут быть отрицательными)</param>
        /// <param name="pnl">текущая цена позиции</param>
        /// <returns>true, если всё посчитано без ошибок</returns>
        public static bool TryGetPairPnl(SmileInfo smileInfo, double strike,
            int putBarCount, int callBarCount,
            IList<IPosition> putPositions, IList<IPosition> callPositions,
            double f, double dT, double btcUsdInd,
            out CashPnlUsd cashPnlUsd, out CashPnlBtc cashPnlBtc)
        {
            if ((putPositions.Count <= 0) && (callPositions.Count <= 0))
            {
                cashPnlUsd = new CashPnlUsd();
                cashPnlBtc = new CashPnlBtc();
                return true;
            }

            double? sigma = null;
            if (smileInfo != null)
            {
                double tmp;
                if (smileInfo.ContinuousFunction.TryGetValue(strike, out tmp))
                    sigma = tmp;
            }

            if ((sigma == null) || (!DoubleUtil.IsPositive(sigma.Value)))
            {
                cashPnlUsd = new CashPnlUsd();
                cashPnlBtc = new CashPnlBtc();
                return false;
            }

            double pnlBtc = 0;
            double pnlUsd = 0;
            double cashBtc = 0;
            double cashUsd = 0;

            if (putPositions.Count > 0)
            {
                CashPnlUsd putUsd;
                CashPnlBtc putBtc;
                GetOptPnl(putPositions, putBarCount - 1,
                    f, strike, dT, sigma.Value, 0.0, false, btcUsdInd,
                    out putUsd, out putBtc);
                pnlUsd += putUsd.PnlUsd;
                cashUsd += putUsd.CashUsd;
                pnlBtc += putBtc.PnlBtc;
                cashBtc += putBtc.CashBtc;
            }

            if (callPositions.Count > 0)
            {
                CashPnlUsd callUsd;
                CashPnlBtc callBtc;
                GetOptPnl(callPositions, callBarCount - 1,
                    f, strike, dT, sigma.Value, 0.0, true, btcUsdInd,
                    out callUsd, out callBtc);
                pnlUsd += callUsd.PnlUsd;
                cashUsd += callUsd.CashUsd;
                pnlBtc += callBtc.PnlBtc;
                cashBtc += callBtc.CashBtc;
            }

            cashPnlUsd = new CashPnlUsd(cashUsd, pnlUsd);
            cashPnlBtc = new CashPnlBtc(cashBtc, pnlBtc);

            return true;
        }

        /// <summary>
        /// Получить финансовые параметры опционной позиции (один опцион)
        /// </summary>
        /// <param name="positions">список закрытых и открытых позиций</param>
        /// <param name="curBar">номер рабочего бара</param>
        /// <param name="f">текущая цена БА</param>
        /// <param name="k">страйк</param>
        /// <param name="dT">время до экспирации</param>
        /// <param name="sigma">волатильность</param>
        /// <param name="r">процентная ставка</param>
        /// <param name="isCall">put-false; call-true</param>
        /// <param name="cash">денежные затраты на формирование позы (могут быть отрицательными)</param>
        /// <param name="pnl">текущая цена позиции</param>
        public static void GetOptPnl(IList<IPosition> positions, int curBar,
            double f, double k, double dT, double sigma, double r, bool isCall, double btcUsdInd,
            out double cashUsd, out double pnlUsd)
        {
            CashPnlUsd cashPnlUsd;
            CashPnlBtc cashPnlBtc;
            GetOptPnl(positions, curBar,
                f, k, dT, sigma, r, isCall, btcUsdInd,
                out cashPnlUsd, out cashPnlBtc);

            cashUsd = cashPnlUsd.CashUsd;
            pnlUsd = cashPnlUsd.PnlUsd;
        }

        /// <summary>
        /// Получить финансовые параметры опционной позиции (один опцион)
        /// </summary>
        /// <param name="positions">список закрытых и открытых позиций</param>
        /// <param name="curBar">номер рабочего бара</param>
        /// <param name="f">текущая цена БА</param>
        /// <param name="k">страйк</param>
        /// <param name="dT">время до экспирации</param>
        /// <param name="sigma">волатильность</param>
        /// <param name="r">процентная ставка</param>
        /// <param name="isCall">put-false; call-true</param>
        /// <param name="cash">денежные затраты на формирование позы (могут быть отрицательными)</param>
        /// <param name="pnl">текущая цена позиции</param>
        public static void GetOptPnl(IList<IPosition> positions, int curBar,
            double f, double k, double dT, double sigma, double r, bool isCall, double btcUsdInd,
            out CashPnlUsd cashPnlUsd, out CashPnlBtc cashPnlBtc)
        {
            if (positions.Count == 0)
            {
                cashPnlUsd = new CashPnlUsd();
                cashPnlBtc = new CashPnlBtc();
                return;
            }

            {
                var msg = $"Как получился отрицательный курс BTC/USD? btcUsdInd:{btcUsdInd}";
                Contract.Assert(DoubleUtil.IsPositive(btcUsdInd), msg);
                if (!DoubleUtil.IsPositive(btcUsdInd))
                    throw new ArgumentException(msg, nameof(btcUsdInd));
            }

            double pnlBtc = 0;
            double pnlUsd = 0;
            double cashBtc = 0;
            double cashUsd = 0;

            foreach (IPosition pos in positions)
            {
                int sign = pos.IsLong ? 1 : -1;
                double qty = Math.Abs(pos.Shares);
                // Знак "минус" стоит в честь того, что при покупке инструмента наличные средства уменьшаются
                double locCashBtcEntry = sign * pos.GetBalancePrice(curBar) * qty;
                cashBtc -= locCashBtcEntry;
                cashUsd -= locCashBtcEntry * btcUsdInd;
                double optPxUsd = FinMath.GetOptionPrice(f, k, dT, sigma, r, isCall);
                double locPnlUsdEntry = sign * optPxUsd * qty;
                pnlUsd += locPnlUsdEntry;
                pnlBtc += locPnlUsdEntry / btcUsdInd;

                // Учет комиссии (комиссия в битках по идее)
                cashBtc -= pos.EntryCommission;
                cashUsd -= pos.EntryCommission * btcUsdInd;

                if (!pos.IsActiveForBar(curBar))
                {
                    // Знак "ПЛЮС" стоит в честь того, что при ЗАКРЫТИИ ЛОНГА наличные средства УВЕЛИЧИВАЮТСЯ
                    double locCashBtcExit = sign * pos.ExitPrice * qty;
                    cashBtc += locCashBtcExit;
                    cashUsd += locCashBtcExit * btcUsdInd;
                    double locPnlUsdExit = sign * optPxUsd * qty;
                    pnlUsd -= locPnlUsdExit;
                    pnlBtc -= locPnlUsdExit / btcUsdInd;

                    // Учет комиссии (комиссия в битках по идее)
                    cashBtc -= pos.ExitCommission;
                    cashUsd -= pos.ExitCommission * btcUsdInd;
                }
            } // End foreach (IPosition pos in positions)

            cashPnlUsd = new CashPnlUsd(cashUsd, pnlUsd);
            cashPnlBtc = new CashPnlBtc(cashBtc, pnlBtc);
        }

        /// <summary>
        /// Класс для 'безопасной' передачи финреза в долларах
        /// </summary>
        public struct CashPnlUsd
        {
            public readonly double CashUsd;
            public readonly double PnlUsd;

            public CashPnlUsd(double cash, double pnl)
            {
                Contract.Assert(!Double.IsNaN(cash),  $"Как получился NaN при вычислении кеша? cash:{cash}");
                Contract.Assert(!Double.IsNaN(pnl), $"Как получился NaN при вычислении прибыли? pnl:{pnl}");

                CashUsd = cash;
                PnlUsd = pnl;
            }

            public override string ToString()
            {
                string res = String.Format(CultureInfo.InvariantCulture,
                    "Cash: ${0}; PnL: ${1}; Tot: ${2}", CashUsd, PnlUsd, CashUsd + PnlUsd);
                return res;
            }
        }

        /// <summary>
        /// Класс для 'безопасной' передачи финреза в биткойнах
        /// </summary>
        public struct CashPnlBtc
        {
            public readonly double CashBtc;
            public readonly double PnlBtc;

            public CashPnlBtc(double cash, double pnl)
            {
                Contract.Assert(!Double.IsNaN(cash), $"Как получился NaN при вычислении кеша? cash:{cash}");
                Contract.Assert(!Double.IsNaN(pnl), $"Как получился NaN при вычислении прибыли? pnl:{pnl}");

                CashBtc = cash;
                PnlBtc = pnl;
            }

            public override string ToString()
            {
                string res = String.Format(CultureInfo.InvariantCulture,
                    "Cash: B{0}; PnL: B{1}; Tot: B{2}", CashBtc, PnlBtc, CashBtc + PnlBtc);
                return res;
            }
        }
    }
}
