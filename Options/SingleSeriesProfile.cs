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
    /// \~english Single series position profile (change of currency rate is not included)
    /// \~russian Профиль позиции для одиночной опционной серии (без учета валютной составляющей) как функция цены БА
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Single Series Profile", Language = Constants.En)]
    [HelperName("Профиль позиции (одна серия)", Language = Constants.Ru)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Профиль позиции для одиночной опционной серии (без учета валютной составляющей) как функция цены БА")]
    [HelperDescription("A single series position profile (change of currency rate is not included) as a function of BA price", Constants.En)]
    public class SingleSeriesProfile : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0";

        private int m_nodesCount = 0;
        private bool m_twoSideDelta = false;
        private string m_tooltipFormat = DefaultTooltipFormat;
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

        /// <summary>
        /// \~english Number of additional nodes near money
        /// \~russian Количество дополнительных узлов около денег
        /// </summary>
        [HelperName("Nodes Count", Constants.En)]
        [HelperName("Доп. узлы", Constants.Ru)]
        [Description("Количество дополнительных узлов около денег")]
        [HelperDescription("Number of additional nodes near money", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Name = "Nodes Count",
            Default = "0", Min = "0", Max = "1000000", Step = "1", NumberDecimalDigits = 0)]
        public int NodesCount
        {
            get { return m_nodesCount; }
            set
            {
                if (value >= 0)
                    m_nodesCount = value;
            }
        }

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
        #endregion Parameters

        public InteractiveSeries Execute(double time, InteractiveSeries smile, IOptionSeries optSer, int barNum)
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
            var optPositions = GetAllOptionPositions(posMan, pairs);

            SortedDictionary<double, IOptionStrikePair> futPrices;
            if (!SmileImitation5.TryPrepareImportantPoints(pairs, futPx, futStep, -1, out futPrices))
            {
                string msg = String.Format("[{0}] It looks like there is no suitable points for the smile. pairs.Length:{1}", GetType().Name, pairs.Length);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptySeries;
            }

#if DEBUG
            System.Text.StringBuilder sb = new System.Text.StringBuilder("F;IsCorrect;Profit");
            sb.AppendLine();
#endif

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            // Список точек для вычисления дельт + улыбки для этих точек
            var deltaPoints = new List<Tuple<double, InteractivePointActive>>();
            //for (int index = 0; index < pairs.Length; index++)
            foreach (var kvp in futPrices)
            {
                //double f = pairs[index].Strike;
                double f = kvp.Key;
                //bool tradableStrike = true; // Пока ставлю "показывать все узлы"
                bool tradableStrike = (kvp.Value != null);

                //Чтобы найти эффективную волатильность покупок и продаж
                //    нужно посчитать финрез текущий ((cash + pnl))
                //    затем финрез БЕЗ ФЬЮЧЕРСОВ!!! отдельно длинных позиций и отдельно коротких.
                //    и после этого нужно варьировать сдвиг опорной улыбки вверх для лонгов и вниз для шортов
                //        до тех пор, пока сумма финрезов не станет равна ИСХОДНОМУ!!!

                double cash, pnl;
                GetBasePnl(basePositions, lastBarIndex, f, out cash, out pnl);

                //InteractiveSeries actualSmile = SingleSeriesProfile.GetActualSmile(smile, m_greekAlgo, f);
                SmileInfo actualSmile = SingleSeriesProfile.GetActualSmile(oldInfo, m_greekAlgo, f);

                // Флаг того, что ПНЛ по всем инструментам был расчитан верно
                bool pnlIsCorrect = true;
                for (int j = 0; (j < pairs.Length) && pnlIsCorrect; j++)
                {
                    var tuple = optPositions[j];
                    IOptionStrikePair pair = pairs[j];
                    //int putBarCount = pair.Put.UnderlyingAsset.Bars.Count;
                    //int callBarCount = pair.Call.UnderlyingAsset.Bars.Count;

                    double pairPnl, pairCash;
                    bool localRes = TryGetPairPnl(actualSmile, pair.Strike, lastBarIndex, lastBarIndex,
                        tuple.Item1, tuple.Item2, f, dT, out pairCash, out pairPnl);
#if DEBUG
                    if ((sb != null) && DoubleUtil.AreClose(futPx, f) && (!localRes))
                    {
                        sb.AppendLine();
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "   FutPx:{0}; Strike:{1}; dT:{2}; localRes:{3}; putPosCount:{4}; callPosCount:{5}",
                            futPx, pair.Strike, dT, localRes, tuple.Item1.Count, tuple.Item2.Count);
                        sb.AppendLine();
                        sb.AppendLine();

                        sb.AppendLine("Actual smile:");
                        sb.AppendLine(actualSmile.ToXElement().ToString());
                        sb.AppendLine();
                        sb.AppendLine();
                    }
#endif
                    pnlIsCorrect &= localRes;
                    cash += pairCash;
                    pnl += pairPnl;

                    //// Если хотя бы один из страйков лежит на заказанной f, его надо показать.
                    //if (!tradableStrike)
                    //    tradableStrike = DoubleUtil.AreClose(pair.Strike, f);
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
                        //tmp.DragableMode = DragableMode.None;
                        //tmp.Geometry = Geometries.Rect;
                        //tmp.Color = Colors.DarkOrange;
                        string pnlStr = (cash + pnl).ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                        tmp.Tooltip = String.Format(CultureInfo.InvariantCulture, "F:{0}; PnL:{1}", f, pnlStr);

                        // Если у нас получился сплайн по профилю позиции, значит мы можем вычислить дельту!
                        if (m_showNodes && tradableStrike)
                        {
                            // Готовим важные точки
                            //deltaPoints[f] = actualSmile;
                            var tuple = new Tuple<double, InteractivePointActive>(f, tmp);
                            deltaPoints.Add(tuple);
                        }

                        ip = tmp;
                    }
                    else
                        ip = new InteractivePointLight();
                    
                    ip.Value = new Point(f, cash + pnl);

                    controlPoints.Add(new InteractiveObject(ip));

                    xs.Add(f);
                    ys.Add(cash + pnl);
                }

#if DEBUG
                double dK = pairs[1].Strike - pairs[0].Strike;
                if ((!pnlIsCorrect) &&
                    (futPx - dK / 4.0 <= f) && (f <= futPx + dK / 4.0))
                {
                    string msg = String.Format(CultureInfo.InvariantCulture,
                        "{0};{1};{2}", f, pnlIsCorrect, cash + pnl);
                    sb.AppendLine(msg);
                }
#endif
            }

#if DEBUG
            if (sb != null)
            {
                sb.AppendLine();
                string msg = sb.ToString();
                if (!msg.TrimEnd('\r', '\n').Equals("F;IsCorrect;Profit"))
                {
                    m_context.Log(msg, MessageType.Info, false);
                }
            }
#endif

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
                        //SmileInfo actualSmile = tuple.Item2;
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
                                //int adLeft = (int)Math.Round(deltaLeft);
                                //int adRight = (int)Math.Round(deltaRight);
                                ip.Tooltip = String.Format(CultureInfo.InvariantCulture, " {0}\r\n LeftD:{1:0.0}; RightD:{2:0.0}",
                                    ip.Tooltip, deltaLeft, deltaRight);
                            }
                        }
                        else
                        {
                            // Мы передвинули улыбку в точку f и считаем дельту позиции В ЭТОЙ ЖЕ ТОЧКЕ(!)
                            if (info.ContinuousFunctionD1.TryGetValue(f, out actualDelta))
                            {
                                //int ad = (int)Math.Round(actualDelta);
                                ip.Tooltip = String.Format(CultureInfo.InvariantCulture, " {0}\r\n D:{1:0.0}", ip.Tooltip, actualDelta);
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

        /// <summary>
        /// Для каждой пары опционов создаётся Tuple.
        /// В первом элементе живут позиции путов, во втором -- колов.
        /// Индексы синхронизированы с индексами массива pairs.
        /// </summary>
        internal static Tuple<ReadOnlyCollection<IPosition>, ReadOnlyCollection<IPosition>>[]
            GetAllOptionPositions(PositionsManager posMan, IOptionStrikePair[] pairs,
            TotalProfitAlgo profitAlgo = TotalProfitAlgo.AllPositions, bool? isLong = null)
        {
            Tuple<ReadOnlyCollection<IPosition>, ReadOnlyCollection<IPosition>>[] res =
                new Tuple<ReadOnlyCollection<IPosition>, ReadOnlyCollection<IPosition>>[pairs.Length];

            for (int j = 0; j < pairs.Length; j++)
            {
                IOptionStrikePair pair = pairs[j];
                ISecurity putSec = pair.Put.Security, callSec = pair.Call.Security;
                ReadOnlyCollection<IPosition> putPositions = posMan.GetClosedOrActiveForBar(putSec, profitAlgo, isLong);
                ReadOnlyCollection<IPosition> callPositions = posMan.GetClosedOrActiveForBar(callSec, profitAlgo, isLong);

                res[j] = new Tuple<ReadOnlyCollection<IPosition>, ReadOnlyCollection<IPosition>>(putPositions, callPositions);
            }

            return res;
        }

        internal static void GetBasePnl(PositionsManager posMan, ISecurity sec, int barNum, double f, out double baseCash, out double basePnl)
        {
            baseCash = 0;
            basePnl = 0;

            var positions = posMan.GetClosedOrActiveForBar(sec);
            foreach (IPosition pos in positions)
            {
                //if (pos.EntrySignalName.StartsWith("CHT-RI-03.", StringComparison.InvariantCultureIgnoreCase))
                //{
                //    string str = "";
                //}

                // Пока что State лучше не трогать 
                //if (pos.PositionState == PositionState.HaveError)
                {
                    int sign = pos.IsLong ? 1 : -1;
                    double qty = Math.Abs(pos.Shares);
                    // Знак "минус" стоит в честь того, что при покупке инструмента наличные средства уменьшаются
                    //baseCash -= sign * pos.EntryPrice * qty;
                    baseCash -= sign * pos.GetBalancePrice(barNum) * qty;
                    //basePnl += sign * (f - pos.EntryPrice) * qty;
                    basePnl += sign * f * qty;

                    // Учет комиссии
                    baseCash -= pos.EntryCommission;
                    
                    if (!pos.IsActiveForBar(barNum))
                    {
                        // Знак "ПЛЮС" стоит в честь того, что при ЗАКРЫТИИ ЛОНГА наличные средства УВЕЛИЧИВАЮТСЯ
                        baseCash += sign * pos.ExitPrice * qty;
                        //basePnl -= sign * (f - pos.ExitPrice) * qty;
                        basePnl -= sign * f * qty;

                        // Учет комиссии
                        baseCash -= pos.ExitCommission;
                    }
                }
            }
        }

        public static void GetBasePnl(IList<IPosition> positions, int barNum, double f, out double baseCash, out double basePnl)
        {
            baseCash = 0;
            basePnl = 0;
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
                    // Знак "минус" стоит в честь того, что при покупке инструмента наличные средства уменьшаются
                    baseCash -= sign * begPx * qty;
                    basePnl += sign * f * qty;

                    // Учет комиссии
                    baseCash -= pos.EntryCommission;

                    if (!pos.IsActiveForBar(barNum))
                    {
                        // Знак "ПЛЮС" стоит в честь того, что при ЗАКРЫТИИ ЛОНГА наличные средства УВЕЛИЧИВАЮТСЯ
                        baseCash += sign * pos.ExitPrice * qty;
                        basePnl -= sign * f * qty;

                        // Учет комиссии
                        baseCash -= pos.ExitCommission;
                    }
                }
            }
        }

        /// <summary>
        /// Вычислить прибыль по всей опционной позиции (без фьючерсов).
        /// Индексация в массиве pairs и optPositions должна быть согласована!
        /// </summary>
        /// <param name="pairs">по сути можно заменить на массив страйков</param>
        /// <param name="context"/>
        public static bool TryGetOptionsPnl(SmileInfo actualSmile, IOptionStrikePair[] pairs,
            Tuple<ReadOnlyCollection<IPosition>, ReadOnlyCollection<IPosition>>[] optPositions,
            int lastBarIndex, double f, double dT, out double optCash, out double optPnl, IMemoryContext context = null)
        {
            double[] strikes = context?.GetArray<double>(pairs.Length) ?? new double[pairs.Length];
            for (int j = 0; j < pairs.Length; j++)
                strikes[j] = pairs[j].Strike;

            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect = TryGetOptionsPnl(actualSmile, strikes,
                optPositions, lastBarIndex, f, dT, out optCash, out optPnl);

            context?.ReleaseArray(strikes);
            return pnlIsCorrect;
        }

        /// <summary>
        /// Вычислить прибыль по всей опционной позиции (без фьючерсов).
        /// Индексация в массиве pairs и optPositions должна быть согласована!
        /// </summary>
        /// <param name="strikes">массив страйков</param>
        public static bool TryGetOptionsPnl(SmileInfo actualSmile, double[] strikes,
            Tuple<ReadOnlyCollection<IPosition>, ReadOnlyCollection<IPosition>>[] optPositions,
            int lastBarIndex, double f, double dT, out double optCash, out double optPnl)
        {
            optPnl = 0;
            optCash = 0;

            // Флаг того, что ПНЛ по всем инструментам был расчитан верно
            bool pnlIsCorrect = true;
            for (int j = 0; (j < strikes.Length) && pnlIsCorrect; j++)
            {
                var tuple = optPositions[j];
                double strike = strikes[j];

                double pairPnl, pairCash;
                bool localRes = TryGetPairPnl(actualSmile, strike, lastBarIndex, lastBarIndex,
                    tuple.Item1, tuple.Item2, f, dT, out pairCash, out pairPnl);
                pnlIsCorrect &= localRes;
                optCash += pairCash;
                optPnl += pairPnl;
            }

            return pnlIsCorrect;
        }

        internal static bool TryGetPairPnl(PositionsManager posMan, SmileInfo smile, IOptionStrikePair pair,
            double f, double dT, out double cash, out double pnl)
        {
            cash = 0;
            pnl = 0;

            ISecurity putSec = pair.Put.Security, callSec = pair.Call.Security;
            ReadOnlyCollection<IPosition> putPositions = posMan.GetClosedOrActiveForBar(putSec);
            ReadOnlyCollection<IPosition> callPositions = posMan.GetClosedOrActiveForBar(callSec);
            if ((putPositions.Count <= 0) && (callPositions.Count <= 0))
                return true;

            if (smile == null)
                throw new ArgumentNullException("smile");

            double? sigma = null;
            {
                double tmp;
                if (smile.ContinuousFunction.TryGetValue(pair.Strike, out tmp))
                {
                    //if (Double.IsNaN(tmp) || (tmp < Double.Ep))
                    //    throw new Exception(String.Format("K:{0}; IV:{1};   F:{2}; dT:{3}", pair.Strike, tmp, smile.F, smile.dT));

                    sigma = tmp;
                }
            }

            if ((sigma == null) || (!DoubleUtil.IsPositive(sigma.Value)))
                return false;

            if (putPositions.Count > 0)
            {
                double putPnl, putCash;
                GetOptPnl(putPositions, putSec.Bars.Count - 1,
                    f, pair.Strike, dT, sigma.Value, smile.RiskFreeRate, false, out putCash, out putPnl);
                pnl += putPnl;
                cash += putCash;
            }

            if (callPositions.Count > 0)
            {
                double callPnl, callCash;
                GetOptPnl(callPositions, callSec.Bars.Count - 1,
                    f, pair.Strike, dT, sigma.Value, smile.RiskFreeRate, true, out callCash, out callPnl);
                pnl += callPnl;
                cash += callCash;
            }

            return true;
        }

        internal static bool TryGetPairPnl(PositionsManager posMan, InteractiveSeries smile, IOptionStrikePair pair,
            double f, double dT, out double cash, out double pnl)
        {
            cash = 0;
            pnl = 0;

            ISecurity putSec = pair.Put.Security, callSec = pair.Call.Security;
            ReadOnlyCollection<IPosition> putPositions = posMan.GetClosedOrActiveForBar(putSec);
            ReadOnlyCollection<IPosition> callPositions = posMan.GetClosedOrActiveForBar(callSec);
            if ((putPositions.Count <= 0) && (callPositions.Count <= 0))
                return true;

            double? sigma = null;
            if ((smile.Tag != null) && (smile.Tag is SmileInfo))
            {
                double tmp;
                SmileInfo info = smile.GetTag<SmileInfo>();
                if (info.ContinuousFunction.TryGetValue(pair.Strike, out tmp))
                    sigma = tmp;
            }

            if ((sigma == null) || Double.IsNaN(sigma.Value) || (sigma < Double.Epsilon))
            {
                sigma = (from d2 in smile.ControlPoints
                         let point = d2.Anchor.Value
                         where (DoubleUtil.AreClose(pair.Strike, point.X))
                         select (double?)point.Y).FirstOrDefault();
            }

            if ((sigma == null) || Double.IsNaN(sigma.Value) || (sigma < Double.Epsilon))
                return false;

            {
                double putPnl, putCash;
                GetOptPnl(putPositions, putSec.Bars.Count - 1,
                    f, pair.Strike, dT, sigma.Value, 0.0, false, out putCash, out putPnl);
                pnl += putPnl;
                cash += putCash;
            }

            {
                double callPnl, callCash;
                GetOptPnl(callPositions, callSec.Bars.Count - 1,
                    f, pair.Strike, dT, sigma.Value, 0.0, true, out callCash, out callPnl);
                pnl += callPnl;
                cash += callCash;
            }

            return true;
        }

        /// <summary>
        /// Цена формирования указанной позиции в данном страйке
        /// </summary>
        internal static bool TryGetPairPrice(double putQty, double callQty, InteractiveSeries smile, IOptionStrikePair pair,
            double f, double dT, double riskFreeRate, out double price)
        {
            price = 0;

            double cash = 0;
            double? sigma = null;
            if ((smile.Tag != null) && (smile.Tag is SmileInfo))
            {
                double tmp;
                SmileInfo info = smile.GetTag<SmileInfo>();
                if (info.ContinuousFunction.TryGetValue(pair.Strike, out tmp))
                    sigma = tmp;
            }

            if ((sigma == null) || Double.IsNaN(sigma.Value) || (sigma < Double.Epsilon))
            {
                sigma = (from d2 in smile.ControlPoints
                         let point = d2.Anchor.Value
                         where (DoubleUtil.AreClose(pair.Strike, point.X))
                         select (double?)point.Y).FirstOrDefault();
            }

            if ((sigma == null) || Double.IsNaN(sigma.Value) || (sigma < Double.Epsilon))
                return false;

            {
                double putPnl, putCash;
                GetOptPnl(putQty, f, pair.Strike, dT, sigma.Value, riskFreeRate, false, out putCash, out putPnl);
                price += putPnl;
                cash += putCash;
            }

            {
                double callPnl, callCash;
                GetOptPnl(callQty, f, pair.Strike, dT, sigma.Value, riskFreeRate, true, out callCash, out callPnl);
                price += callPnl;
                cash += callCash;
            }

            return true;
        }

        /// <summary>
        /// Цена формирования указанной позиции в данном страйке
        /// </summary>
        public static bool TryGetPairPrice(double putQty, double callQty, SmileInfo info, IOptionStrikePair pair,
            double f, double dT, double riskFreeRate, out double price)
        {
            price = 0;

            if (info == null)
                return false;

            double cash = 0;
            double? sigma = null;
            {
                double tmp;
                if (info.ContinuousFunction.TryGetValue(pair.Strike, out tmp))
                    sigma = tmp;
            }

            if ((sigma == null) || Double.IsNaN(sigma.Value) || (sigma < Double.Epsilon))
                return false;

            {
                double putPnl, putCash;
                GetOptPnl(putQty, f, pair.Strike, dT, sigma.Value, riskFreeRate, false, out putCash, out putPnl);
                price += putPnl;
                cash += putCash;
            }

            {
                double callPnl, callCash;
                GetOptPnl(callQty, f, pair.Strike, dT, sigma.Value, riskFreeRate, true, out callCash, out callPnl);
                price += callPnl;
                cash += callCash;
            }

            return true;
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
            double f, double dT, out double cash, out double pnl)
        {
            cash = 0;
            pnl = 0;
            if ((putPositions.Count <= 0) && (callPositions.Count <= 0))
                return true;

            double? sigma = null;
            if (smileInfo != null)
            {
                double tmp;
                if (smileInfo.ContinuousFunction.TryGetValue(strike, out tmp))
                    sigma = tmp;
            }

            if ((sigma == null) || Double.IsNaN(sigma.Value) || (sigma < Double.Epsilon))
                return false;

            {
                double putPnl, putCash;
                GetOptPnl(putPositions, putBarCount - 1,
                    f, strike, dT, sigma.Value, 0.0, false, out putCash, out putPnl);
                pnl += putPnl;
                cash += putCash;
            }

            {
                double callPnl, callCash;
                GetOptPnl(callPositions, callBarCount - 1,
                    f, strike, dT, sigma.Value, 0.0, true, out callCash, out callPnl);
                pnl += callPnl;
                cash += callCash;
            }

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
            double f, double k, double dT, double sigma, double r, bool isCall,
            out double cash, out double pnl)
        {
            pnl = 0;
            cash = 0;
            if (positions.Count == 0)
                return;

            // TODO: зарефакторить на использование метода из PositionsManager???
            foreach (IPosition pos in positions)
            {
                // Пока что State лучше не трогать 
                //if (pos.PositionState == PositionState.HaveError)
                {
                    int sign = pos.IsLong ? 1 : -1;
                    double qty = Math.Abs(pos.Shares);
                    double begPx = pos.GetBalancePrice(curBar);
                    // Знак "минус" стоит в честь того, что при покупке инструмента наличные средства уменьшаются
                    //cash -= sign * pos.EntryPrice * qty;
                    cash -= sign * begPx * qty;
                    double optPx = FinMath.GetOptionPrice(f, k, dT, sigma, r, isCall);
                    pnl += sign * optPx * qty;

                    // Учет комиссии
                    cash -= pos.EntryCommission;

                    if (!pos.IsActiveForBar(curBar))
                    {
                        // Знак "ПЛЮС" стоит в честь того, что при ЗАКРЫТИИ ЛОНГА наличные средства УВЕЛИЧИВАЮТСЯ
                        cash += sign * pos.ExitPrice * qty;
                        pnl -= sign * optPx * qty;

                        // Учет комиссии
                        cash -= pos.ExitCommission;
                    }
                }
            }
        }

        /// <summary>
        /// Получить финансовые параметры опционной позиции (один опцион)
        /// </summary>
        /// <param name="qty">количество опционов</param>
        /// <param name="f">текущая цена БА</param>
        /// <param name="k">страйк</param>
        /// <param name="dT">время до экспирации</param>
        /// <param name="sigma">волатильность</param>
        /// <param name="r">процентная ставка</param>
        /// <param name="isCall">put-false; call-true</param>
        /// <param name="cash">денежные затраты на формирование позы (могут быть отрицательными)</param>
        /// <param name="pnl">текущая цена позиции</param>
        internal static void GetOptPnl(double qty,
            double f, double k, double dT, double sigma, double r, bool isCall,
            out double cash, out double pnl)
        {
            pnl = 0;
            cash = 0;
            int sign = (qty >= 0) ? 1 : -1;
            // Знак "минус" стоит в честь того, что при покупке инструмента наличные средства уменьшаются
            double optPx = FinMath.GetOptionPrice(f, k, dT, sigma, r, isCall);
            cash -= sign * optPx * qty;
            pnl += sign * optPx * qty;

            // Учет комиссии
            //cash -= pos.EntryCommission;

            // Поскольку передали qty, то поза открыта
            //if (!pos.IsActiveForBar(curBar))
            //{
            //    // Знак "ПЛЮС" стоит в честь того, что при ЗАКРЫТИИ ЛОНГА наличные средства УВЕЛИЧИВАЮТСЯ
            //    cash += sign * pos.ExitPrice * qty;
            //    pnl -= sign * optPx * qty;

            //    // Учет комиссии
            //    cash -= pos.ExitCommission;
            //}
        }

        /// <summary>
        /// Получение сдвинутой в соответствии с парметром algo улыбки.
        /// Для скорости возвращается SmileInfo.
        /// </summary>
        /// <param name="smile">исходная улыбка</param>
        /// <param name="algo">алгоритм её трансформации</param>
        /// <param name="newF">новая цена БА</param>
        /// <returns>преобразованная улыбка</returns>
        public static SmileInfo GetActualSmile(SmileInfo smile, NumericalGreekAlgo algo, double newF)
        {
            SmileInfo res;
            switch (algo)
            {
                case NumericalGreekAlgo.FrozenSmile:
                    res = smile;
                    break;

                case NumericalGreekAlgo.ShiftingSmile:
                    if (smile == null)
                        throw new InvalidOperationException("Argument 'smile' must be filled with some value.");

                    double dF = newF - smile.F;
                    SmileInfo newInfo = new SmileInfo
                    {
                        F = newF, dT = smile.dT, RiskFreeRate = smile.RiskFreeRate,
                        IvAtm = smile.IvAtm, SkewAtm = smile.SkewAtm, Shape = smile.Shape,
                        SmileType = smile.SmileType,
                        // Вспомогательные поля
                        BaseTicker = smile.BaseTicker, Expiry = smile.Expiry, ScriptTime = smile.ScriptTime
                    };

                    newInfo.ContinuousFunction = smile.ContinuousFunction.HorizontalShift(dF);
                    newInfo.ContinuousFunctionD1 = smile.ContinuousFunctionD1.HorizontalShift(dF);

                    res = newInfo;
                    break;

                default:
                    throw new NotImplementedException("Algo " + algo + " is not implemented.");
            }

            return res;
        }

        /// <summary>
        /// Получение сдвинутой в соответствии с парметром algo улыбки.
        /// Для общности возвращается InteractiveSeries.
        /// 
        /// Предпочтительно переходить на использование аналогичного метода на базе SmileInfo.
        /// </summary>
        /// <param name="smile">исходная улыбка</param>
        /// <param name="algo">алгоритм её трансформации</param>
        /// <param name="newF">новая цена БА</param>
        /// <returns>преобразованная улыбка</returns>
        [Obsolete("Неэффективный код. Предпочтительно переходить на использование аналогичного метода на базе SmileInfo.")]
        private static InteractiveSeries GetActualSmile(InteractiveSeries smile, NumericalGreekAlgo algo, double newF)
        {
            InteractiveSeries res;
            switch (algo)
            {
                case NumericalGreekAlgo.FrozenSmile:
                    res = smile;
                    break;

                case NumericalGreekAlgo.ShiftingSmile:
                    SmileInfo info = smile.GetTag<SmileInfo>();
                    if (info == null)
                        throw new InvalidOperationException("Property Tag of object smile must be filled with SmileInfo");

                    double dF = newF - info.F;
                    res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
                    List<InteractiveObject> controlPoints = new List<InteractiveObject>();
                    foreach (InteractiveObject oldObj in smile.ControlPoints)
                    {
                        InteractiveObject obj = (InteractiveObject)oldObj.Clone();

                        obj.Anchor.Value = new Point(oldObj.Anchor.ValueX + dF, oldObj.Anchor.ValueY);
                        if (oldObj.ControlPoint1 != null)
                            obj.ControlPoint1.Value = new Point(oldObj.ControlPoint1.ValueX + dF, oldObj.ControlPoint1.ValueY);
                        if (oldObj.ControlPoint2 != null)
                            obj.ControlPoint2.Value = new Point(oldObj.ControlPoint2.ValueX + dF, oldObj.ControlPoint2.ValueY);

                        // По идее необходимо также обновить теги, но в данном случае профиль не предполагается для дальнейшего использования,
                        // поэтому просто зануляю их.
                        obj.Anchor.Tag = null;
                        if (oldObj.ControlPoint1 != null)
                            obj.ControlPoint1.Tag = null;
                        if (oldObj.ControlPoint2 != null)
                            obj.ControlPoint2.Tag = null;

                        controlPoints.Add(obj);
                    }

                    res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

                    SmileInfo newInfo = new SmileInfo();
                    newInfo.F = newF;
                    newInfo.dT = info.dT;
                    newInfo.RiskFreeRate = info.RiskFreeRate;

                    newInfo.ContinuousFunction = info.ContinuousFunction.HorizontalShift(dF);
                    newInfo.ContinuousFunctionD1 = info.ContinuousFunctionD1.HorizontalShift(dF);

                    res.Tag = newInfo;
                    break;

                default:
                    throw new NotImplementedException("Algo " + algo + " is not implemented.");
            }

            return res;
        }

        /// <summary>
        /// Получение сдвинутой в соответствии с парметром algo улыбки.
        /// Для скорости возвращается SmileInfo.
        /// </summary>
        /// <param name="smile">исходная улыбка</param>
        /// <param name="algo">алгоритм её трансформации</param>
        /// <param name="dSigma">УВЕЛИЧЕНИЕ уровня волатильности (только неотрицательные значения!)</param>
        /// <returns>преобразованная улыбка</returns>
        public static SmileInfo GetRaisedSmile(SmileInfo smile, NumericalGreekAlgo algo, double dSigma)
        {
            SmileInfo res;
            switch (algo)
            {
                case NumericalGreekAlgo.FrozenSmile:
                    res = smile;
                    break;

                case NumericalGreekAlgo.ShiftingSmile:
                    if (smile == null)
                        throw new InvalidOperationException("Argument 'smile' must be filled with some value.");

                    if (dSigma < 0)
                    {
                        double newIvAtm = smile.IvAtm + dSigma;
                        if (!DoubleUtil.IsPositive(newIvAtm))
                            throw new ArgumentException($"IV ATM after shift must be strictly positive. dSigma:{dSigma}; newIvAtm:{newIvAtm}");

                        if (smile.SmileType == typeof(SmileImitation5).FullName)
                        {
                            // Для этого типа улыбок мы умеем сдвигать волатильность вниз
                            var lowSmileFunc = new SmileFunctionExtended(
                                SmileFunction5.TemplateFuncRiz4Nov1,
                                newIvAtm, smile.SkewAtm, smile.Shape, smile.F, smile.dT);

                            // Нижняя волатильность на-деньгах
                            double testLowIvAtm = lowSmileFunc.Value(smile.F);
                            // Поскольку применен алгоритм ShiftingSmile, волатильность на-деньгах должна остаться прежней
                            Contract.Assert(DoubleUtil.AreClose(testLowIvAtm, newIvAtm), "#01: Начальная вола на-деньгах должна быть равна требуемой");

                            var lowSmileInfo = new SmileInfo();
                            lowSmileInfo.F = smile.F;
                            lowSmileInfo.dT = smile.dT;
                            lowSmileInfo.RiskFreeRate = smile.RiskFreeRate;

                            lowSmileInfo.IvAtm = newIvAtm;
                            lowSmileInfo.SkewAtm = smile.SkewAtm;
                            lowSmileInfo.Shape = smile.Shape;
                            lowSmileInfo.SmileType = typeof(SmileImitation5).FullName;

                            // Вспомогательные поля
                            lowSmileInfo.BaseTicker = smile.BaseTicker;
                            lowSmileInfo.Expiry = smile.Expiry;
                            lowSmileInfo.ScriptTime = smile.ScriptTime;

                            lowSmileInfo.ContinuousFunction = lowSmileFunc;
                            lowSmileInfo.ContinuousFunctionD1 = lowSmileFunc.DeriveD1();

                            // Нижняя волатильность на-деньгах
                            testLowIvAtm = lowSmileInfo.ContinuousFunction.Value(smile.F);

                            // Поскольку применен алгоритм ShiftingSmile, волатильность на-деньгах должна остаться прежней
                            Contract.Assert(lowSmileInfo.IsValidSmileParams, "#05: Преобразование улыбки должно заполнять все нужные поля!");

                            res = lowSmileInfo;
                            break;
                        }
                        else
                            throw new NotImplementedException($"Not implemented. dSigma:{dSigma}; SmileType:{smile.SmileType}");
                    }

                    SmileInfo newInfo = new SmileInfo
                    {
                        F = smile.F, dT = smile.dT, RiskFreeRate = smile.RiskFreeRate,
                        IvAtm = smile.IvAtm + dSigma, SkewAtm = smile.SkewAtm, Shape = smile.Shape,
                        SmileType = smile.SmileType,
                        // Вспомогательные поля
                        BaseTicker = smile.BaseTicker, Expiry = smile.Expiry, ScriptTime = smile.ScriptTime
                    };

                    newInfo.ContinuousFunction = smile.ContinuousFunction.VerticalShift(dSigma);
                    newInfo.ContinuousFunctionD1 = smile.ContinuousFunctionD1.VerticalShift(0);

                    res = newInfo;
                    break;

                default:
                    throw new NotImplementedException("Algo " + algo + " is not implemented.");
            }

            return res;
        }

        /// <summary>
        /// Получение сдвинутой в соответствии с парметром algo улыбки.
        /// Для общности возвращается InteractiveSeries.
        /// 
        /// Предпочтительно переходить на использование аналогичного метода на базе SmileInfo.
        /// </summary>
        /// <param name="smile">исходная улыбка</param>
        /// <param name="algo">алгоритм её трансформации</param>
        /// <param name="dSigma">УВЕЛИЧЕНИЕ уровня волатильности (только неотрицательные значения!)</param>
        /// <returns>преобразованная улыбка</returns>
        [Obsolete("Неэффективный код. Предпочтительно пер1еходить на использование аналогичного метода на базе SmileInfo.")]
        internal static InteractiveSeries GetRaisedSmile(InteractiveSeries smile, NumericalGreekAlgo algo, double dSigma)
        {
            InteractiveSeries res;
            switch (algo)
            {
                case NumericalGreekAlgo.FrozenSmile:
                    res = smile;
                    break;

                case NumericalGreekAlgo.ShiftingSmile:
                    SmileInfo info = smile.GetTag<SmileInfo>();
                    if (info == null)
                        throw new InvalidOperationException("Property Tag of object smile must be filled with SmileInfo");

                    if (dSigma >= 0)
                    {
                        res = new InteractiveSeries();  // Здесь так надо -- мы делаем новую улыбку
                        List<InteractiveObject> controlPoints = new List<InteractiveObject>();
                        foreach (InteractiveObject oldObj in smile.ControlPoints)
                        {
                            InteractiveObject obj = (InteractiveObject)oldObj.Clone();

                            obj.Anchor.Value = new Point(oldObj.Anchor.ValueX, oldObj.Anchor.ValueY + dSigma);
                            if (oldObj.ControlPoint1 != null)
                                obj.ControlPoint1.Value = new Point(oldObj.ControlPoint1.ValueX, oldObj.ControlPoint1.ValueY + dSigma);
                            if (oldObj.ControlPoint2 != null)
                                obj.ControlPoint2.Value = new Point(oldObj.ControlPoint2.ValueX, oldObj.ControlPoint2.ValueY + dSigma);

                            // По идее необходимо также обновить теги, но в данном случае профиль не предполагается для дальнейшего использования,
                            // поэтому просто зануляю теги.
                            obj.Anchor.Tag = null;
                            if (oldObj.ControlPoint1 != null)
                                obj.ControlPoint1.Tag = null;
                            if (oldObj.ControlPoint2 != null)
                                obj.ControlPoint2.Tag = null;

                            controlPoints.Add(obj);
                        }

                        res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);
                    }
                    else
                        throw new NotImplementedException("Not implemented. dSigma:" + dSigma);

                    SmileInfo newInfo = new SmileInfo();
                    newInfo.F = info.F;
                    newInfo.dT = info.dT;
                    newInfo.RiskFreeRate = info.RiskFreeRate;

                    newInfo.ContinuousFunction = info.ContinuousFunction.VerticalShift(dSigma);
                    newInfo.ContinuousFunctionD1 = info.ContinuousFunctionD1.VerticalShift(0);

                    res.Tag = newInfo;
                    break;

                default:
                    throw new NotImplementedException("Algo " + algo + " is not implemented.");
            }

            return res;
        }

        /// <summary>
        /// Получение сдвинутой в соответствии с парметром algo улыбки.
        /// Для скорости возвращается SmileInfo.
        /// </summary>
        /// <param name="smile">исходная улыбка</param>
        /// <param name="algo">алгоритм её трансформации</param>
        /// <param name="newDt">новое время до экспирации в долях года (только неотрицательные значения!)</param>
        /// <returns>преобразованная улыбка</returns>
        internal static SmileInfo GetSmileAtTime(SmileInfo smile, NumericalGreekAlgo algo, double newDt)
        {
            SmileInfo res;
            switch (algo)
            {
                case NumericalGreekAlgo.FrozenSmile:
                    res = smile;
                    break;

                default:
                    throw new NotImplementedException("Algo " + algo + " is not implemented.");
            }

            return res;
        }

        /// <summary>
        /// Получение сдвинутой в соответствии с парметром algo улыбки.
        /// Для общности возвращается InteractiveSeries.
        /// 
        /// Предпочтительно переходить на использование аналогичного метода на базе SmileInfo.
        /// </summary>
        /// <param name="smile">исходная улыбка</param>
        /// <param name="algo">алгоритм её трансформации</param>
        /// <param name="newDt">новая цена БА</param>
        /// <returns>преобразованная улыбка</returns>
        internal static InteractiveSeries GetSmileAtTime(InteractiveSeries smile, NumericalGreekAlgo algo, double newDt)
        {
            InteractiveSeries res;
            switch (algo)
            {
                case NumericalGreekAlgo.FrozenSmile:
                    res = smile;
                    break;

                default:
                    throw new NotImplementedException("Algo " + algo + " is not implemented.");
            }

            return res;
        }

        internal static void GetAveragePrice(IList<IPosition> positions, int barNum, bool longPositions, out double avgPx, out double totalQty)
        {
            totalQty = 0;
            avgPx = Double.NaN;
            double baseCash = 0;
            int len = positions.Count;
            for (int j = 0; j < len; j++)
            {
                IPosition pos = positions[j];
                // Должен пройти по всем позициям и если какие-то закрыты учесть их цены закрытия!
                //if (pos.IsLong == longPositions)
                {
                    int sign = pos.IsLong ? 1 : -1;
                    if (pos.IsLong == longPositions)
                    {
                        double qty = Math.Abs(pos.Shares);
                        totalQty += qty;
                        // Знак "минус" стоит в честь того, что при покупке инструмента наличные средства уменьшаются
                        baseCash -= sign * pos.GetBalancePrice(barNum) * qty;

                        // Учет комиссии
                        baseCash -= pos.EntryCommission;
                    }

                    // Если поза закрыта, но она ПРОТИВОПОЛОЖНА ФЛАГУ longPositions(!), то надо учитывать её цену выхода
                    // TODO: разобраться с ценой выхода!
                    if ((!pos.IsActiveForBar(barNum)) && (pos.IsLong != longPositions))
                    {
                        double qty = Math.Abs(pos.Shares);
                        totalQty += qty;
                        // Знак "ПЛЮС" стоит в честь того, что при ЗАКРЫТИИ ЛОНГА наличные средства УВЕЛИЧИВАЮТСЯ
                        baseCash += sign * pos.ExitPrice * qty;

                        // Учет комиссии
                        baseCash -= pos.ExitCommission;
                    }
                }
            }

            if (!DoubleUtil.IsZero(totalQty))
            {
                avgPx = Math.Abs(baseCash) / totalQty;
            }
        }

        internal static void GetTotalCommission(IList<IPosition> positions, int barNum, bool longPositions, out double commission, out double totalQty)
        {
            totalQty = 0;
            commission = 0;
            int len = positions.Count;
            for (int j = 0; j < len; j++)
            {
                IPosition pos = positions[j];
                // Должен пройти по всем позициям и если какие-то закрыты учесть их цены закрытия!
                //if (pos.IsLong == longPositions)
                {
                    int sign = pos.IsLong ? 1 : -1;
                    if (pos.IsLong == longPositions)
                    {
                        double qty = Math.Abs(pos.Shares);
                        totalQty += qty;
                        // Учет комиссии
                        commission += pos.EntryCommission;
                    }

                    // Если поза закрыта, но она ПРОТИВОПОЛОЖНА ФЛАГУ longPositions(!), то надо учитывать её цену выхода
                    // TODO: разобраться с ценой выхода!
                    if ((!pos.IsActiveForBar(barNum)) && (pos.IsLong != longPositions))
                    {
                        double qty = Math.Abs(pos.Shares);
                        totalQty += qty;
                        // Учет комиссии
                        commission += pos.ExitCommission;
                    }
                }
            }
        }
    }
}
