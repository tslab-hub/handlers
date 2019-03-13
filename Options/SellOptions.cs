using System;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Sell options while position risk is 'small'
    /// \~region Продажа опционов до заданного уровня риска
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Sell Options", Language = Constants.En)]
    [HelperName("Продажа опционов", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(8)]
    [Input(0, TemplateTypes.DOUBLE | TemplateTypes.BOOL, Name = Constants.Permission)]
    [Input(1, TemplateTypes.DOUBLE, Name = "Strike")]
    [Input(2, TemplateTypes.DOUBLE, Name = "Current Risk")]
    [Input(3, TemplateTypes.DOUBLE, Name = "Max Risk")]
    [Input(4, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(5, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(6, TemplateTypes.DOUBLE, Name = "Call Risk")]
    [Input(7, TemplateTypes.DOUBLE, Name = "Put Risk")]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Продажа опционов до заданного уровня риска")]
    [HelperDescription("Sell options while position risk is 'small'", Constants.En)]
    public class SellOptions : IContextUses, IValuesHandlerWithNumber
    {
        private const string DefaultQty = "1";

        private IContext m_context;

        private StrikeType m_optionType = StrikeType.Call;
        private int m_fixedQty = Int32.Parse(DefaultQty);
        private int m_entryShift = 0, m_exitShift = 0;

        public IContext Context
        {
            get { return m_context; }
            set { m_context = value; }
        }

        #region Parameters
        /// <summary>
        /// \~english Limit trading to options of given type (call, put, both)
        /// \~russian Ограничение на тип торгуемого инструмента (колл, пут, котируются одновременно оба)
        /// </summary>
        [HelperName("Option type", Constants.En)]
        [HelperName("Тип опциона", Constants.Ru)]
        [Description("Ограничение на тип торгуемого инструмента (колл, пут, котируются одновременно оба)")]
        [HelperDescription("Limit trading to options of given type (call, put, both)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Call")]
        public StrikeType OptionType
        {
            get { return m_optionType; }
            set { m_optionType = value; }
        }

        /// <summary>
        /// \~english Order size
        /// \~russian Объём заявки
        /// </summary>
        [HelperName("Order size", Constants.En)]
        [HelperName("Объём заявки", Constants.Ru)]
        [Description("Объём заявки")]
        [HelperDescription("Order size", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = DefaultQty, Min = "0", Max = "1000000", Step = "1")]
        public int FixedQty
        {
            get { return m_fixedQty; }
            set { m_fixedQty = value; }
        }

        /// <summary>
        /// \~english Entry shift to get higher sell price (price step)
        /// \~russian Сдвиг входа для увеличения цены продажи (шаг цены)
        /// </summary>
        [HelperName("Entry shift", Constants.En)]
        [HelperName("Сдвиг входа", Constants.Ru)]
        [Description("Сдвиг входа для увеличения цены продажи (шаг цены)")]
        [HelperDescription("Entry shift to get higher sell price (price step)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-1000000", Max = "1000000", Step = "1")]
        public int EntryShift
        {
            get { return m_entryShift; }
            set { m_entryShift = value; }
        }

        /// <summary>
        /// \~english Exit shift to get quick execution (price step)
        /// \~russian Сдвиг выхода для быстрого закрытия позиции (шаг цены)
        /// </summary>
        [HelperName("Exit shift", Constants.En)]
        [HelperName("Сдвиг выхода", Constants.Ru)]
        [Description("Сдвиг выхода для быстрого закрытия позиции (шаг цены)")]
        [HelperDescription("Exit shift to get quick execution (price step)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-1000000", Max = "1000000", Step = "1")]
        public int ExitShift
        {
            get { return m_exitShift; }
            set { m_exitShift = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public double Execute(double entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            double res = Process(entryPermission, strike, risk, maxRisk, smile, optSer, 0, 0, barNum);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public double Execute(bool entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            double permission = entryPermission ? 1 : -1;
            double res = Process(permission, strike, risk, maxRisk, smile, optSer, 0, 0, barNum);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// (при этом можно указать риск покупки одного кола)
        /// </summary>
        public double Execute(double entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer,
            double callRisk, int barNum)
        {
            double res = Process(entryPermission, strike, risk, maxRisk, smile, optSer, callRisk, 0, barNum);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// (при этом можно указать риск покупки одного кола)
        /// </summary>
        public double Execute(bool entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer,
            double callRisk, int barNum)
        {
            double permission = entryPermission ? 1 : -1;
            double res = Process(permission, strike, risk, maxRisk, smile, optSer, callRisk, 0, barNum);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// (при этом можно указать риск покупки одного кола и риск покупки одного пута)
        /// </summary>
        public double Execute(double entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer,
            double callRisk, double putRisk, int barNum)
        {
            double res = Process(entryPermission, strike, risk, maxRisk, smile, optSer, callRisk, putRisk, barNum);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// (при этом можно указать риск покупки одного кола и риск покупки одного пута)
        /// </summary>
        public double Execute(bool entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer,
            double callRisk, double putRisk, int barNum)
        {
            double permission = entryPermission ? 1 : -1;
            double res = Process(permission, strike, risk, maxRisk, smile, optSer, callRisk, putRisk, barNum);
            return res;
        }

        /// <summary>
        /// Основной метод, который выполняет всю торговую логику по котированию и следит за риском
        /// </summary>
        protected double Process(double entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer,
            double callRisk, double putRisk, int barNum)
        {
            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if ((barNum < barsCount - 1) || (optSer == null) || (smile == null))
                return Constants.NaN;

            IOptionStrikePair pair;
            if (!optSer.TryGetStrikePair(strike, out pair))
                return Constants.NaN;

            // Если риск не был измерен говорить вообще не о чем!
            if (Double.IsNaN(risk))
                return Constants.NaN;

            // Если риск разумен и условие входа НЕ ВЫПОЛНЕНО -- отдыхаем.
            // А вот если риск превышен -- тогда по идее надо бы его подсократить!
            if ((risk < maxRisk) && (entryPermission <= 0))
                return Constants.NaN;

            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (posMan.BlockTrading)
            {
                //string msg = String.Format("Trading is blocked. Please, change 'Block Trading' parameter.");
                //m_context.Log(msg, MessageType.Info, true);
                return Constants.NaN;
            }

            SmileInfo sInfo = smile.GetTag<SmileInfo>();
            if ((sInfo == null) || (sInfo.ContinuousFunction == null))
                return Constants.NaN;

            double dT = sInfo.dT;
            double futPx = sInfo.F;

            // Набираем риск
            if (risk < maxRisk)
            {
                double ivAtm;
                if ((!sInfo.ContinuousFunction.TryGetValue(strike, out ivAtm)) || Double.IsNaN(ivAtm) || (ivAtm < Double.Epsilon))
                {
                    string msg = String.Format("[{0}.{1}] Unable to get IV at strike {2}. ivAtm:{3}",
                        Context.Runtime.TradeName, GetType().Name, pair.Strike, ivAtm);
                    m_context.Log(msg, MessageType.Error, true);
                    return Constants.NaN;
                }

                double theorPutPx = FinMath.GetOptionPrice(futPx, strike, dT, ivAtm, 0, false);
                double theorCallPx = FinMath.GetOptionPrice(futPx, strike, dT, ivAtm, 0, true);

                #region Набираем риск
                double putPx, callPx;
                {
                    double putQty, callQty;
                    DateTime putTime, callTime;
                    putPx = IvSmile.GetOptPrice(m_context, futPx, pair.Put, OptionPxMode.Bid, 0, 0, out putQty, out putTime);
                    callPx = IvSmile.GetOptPrice(m_context, futPx, pair.Call, OptionPxMode.Bid, 0, 0, out callQty, out callTime);
                }

                if (m_optionType == StrikeType.Put)
                {
                    #region В путах
                    ISecurity sec = pair.Put.Security;
                    double qty = Math.Abs(m_fixedQty);
                    qty = BuyOptions.GetSafeQty(risk, maxRisk, qty, putRisk);
                    if (qty > 0)
                    {
                        double px = SafeMaxPrice(theorPutPx + m_entryShift * sec.Tick, putPx, sec);
                        double iv = FinMath.GetOptionSigma(futPx, pair.Strike, dT, px, 0, false);
                        string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                        posMan.SellAtPrice(m_context, sec, qty, px, "Open SELL", sigName);

                        m_context.Log(sigName, MessageType.Info, false);
                    }
                    #endregion В путах
                }
                else if (m_optionType == StrikeType.Call)
                {
                    #region В колах
                    ISecurity sec = pair.Call.Security;
                    double qty = Math.Abs(m_fixedQty);
                    qty = BuyOptions.GetSafeQty(risk, maxRisk, qty, callRisk);
                    if (qty > 0)
                    {
                        double px = SafeMaxPrice(theorCallPx + m_entryShift * sec.Tick, callPx, sec);
                        double iv = FinMath.GetOptionSigma(futPx, pair.Strike, dT, px, 0, true);
                        string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                        posMan.SellAtPrice(m_context, sec, qty, px, "Open SELL", sigName);

                        m_context.Log(sigName, MessageType.Info, false);
                    }
                    #endregion В колах
                }
                else
                {
                    #region В оба вида опционов сразу встаю
                    int executedQty = 0;
                    {
                        ISecurity sec = pair.Put.Security;
                        double px = SafeMaxPrice(theorPutPx + m_entryShift * sec.Tick, putPx, sec);
                        double iv = FinMath.GetOptionSigma(futPx, pair.Strike, dT, px, 0, false);
                        double qty = Math.Max(1, Math.Abs(m_fixedQty / 2));
                        qty = BuyOptions.GetSafeQty(risk, maxRisk, qty, putRisk);
                        if (qty > 0)
                        {
                            string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                            posMan.SellAtPrice(m_context, sec, qty, px, "Open SELL", sigName);

                            m_context.Log(sigName, MessageType.Info, false);

                            executedQty += (int)qty;
                        }
                    }

                    if (Math.Abs(executedQty) < Math.Abs(m_fixedQty))
                    {
                        ISecurity sec = pair.Call.Security;
                        double px = SafeMaxPrice(theorCallPx + m_entryShift * sec.Tick, callPx, sec);
                        double iv = FinMath.GetOptionSigma(futPx, pair.Strike, dT, px, 0, true);
                        double qty = Math.Abs(m_fixedQty) - Math.Abs(executedQty);
                        // Делаю оценку изменения текущего риска, если нам зафилят заявку в путах
                        qty = BuyOptions.GetSafeQty(risk + Math.Abs(executedQty) * putRisk, maxRisk, qty, callRisk);
                        if (qty > 0)
                        {
                            string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                            posMan.SellAtPrice(m_context, sec, qty, px, "Open SELL", sigName);

                            m_context.Log(sigName, MessageType.Info, false);

                            //executedQty += (int)qty;
                        }
                    }
                    #endregion В оба вида опционов сразу встаю
                }
                #endregion Набираем риск
            }
            else if (risk > maxRisk)
            {
                string msg;
                //string msg = String.Format("[DEBUG:{0}] risk:{1}; maxRisk:{2}", Context.Runtime.TradeName, risk, maxRisk);
                //m_context.Log(msg, MessageType.Info, true);

                // Надо взять пары, начиная от центральной и далее по возрастанию расстояния
                var orderedPairs = (from p in optSer.GetStrikePairs()
                                    orderby Math.Abs(p.Strike - strike) ascending
                                    select p).ToArray();
                if (orderedPairs.Length > 0)
                {
                    foreach (IOptionStrikePair candidPair in orderedPairs)
                    {
                        #region Проверяю, что в страйке есть КОРОТКАЯ позиция
                        double putOpenQty = posMan.GetTotalQty(candidPair.Put.Security, barNum);
                        double callOpenQty = posMan.GetTotalQty(candidPair.Call.Security, barNum);

                        if ((putOpenQty >= 0) && (callOpenQty >= 0))
                            continue;
                        if (DoubleUtil.IsZero(putOpenQty) && DoubleUtil.IsZero(callOpenQty))
                            continue;

                        {
                            msg = String.Format("[{0}:{1}] Strike:{2}; putOpenQty:{3}; callOpenQty:{4}",
                                Context.Runtime.TradeName, GetType().Name, candidPair.Strike, putOpenQty, callOpenQty);
                            m_context.Log(msg, MessageType.Info, true);
                        }
                        #endregion Проверяю, что в страйке есть КОРОТКАЯ позиция

                        double theorPutPx, theorCallPx;
                        {
                            double iv;
                            if ((!sInfo.ContinuousFunction.TryGetValue(candidPair.Strike, out iv)) ||
                                Double.IsNaN(iv) || (iv < Double.Epsilon))
                            {
                                msg = String.Format("[{0}.{1}] Unable to get IV at strike {2}. IV:{3}",
                                    Context.Runtime.TradeName, GetType().Name, candidPair.Strike, iv);
                                m_context.Log(msg, MessageType.Error, true);
                                continue;
                            }

                            theorPutPx = FinMath.GetOptionPrice(futPx, candidPair.Strike, dT, iv, 0, false);
                            theorCallPx = FinMath.GetOptionPrice(futPx, candidPair.Strike, dT, iv, 0, true);
                        }

                        #region Сдаём риск (один квант объёма за раз)
                        double putPx, callPx;
                        {
                            DateTime putTime, callTime;
                            double putAskQty, callAskQty;
                            putPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Put, OptionPxMode.Ask, 0, 0, out putAskQty, out putTime);
                            callPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Call, OptionPxMode.Ask, 0, 0, out callAskQty, out callTime);
                        }

                        if (m_optionType == StrikeType.Put)
                        {
                            #region В путах
                            if (putOpenQty < 0) // Это означает, что в страйке есть короткие путы
                            {
                                ISecurity sec = candidPair.Put.Security;
                                double px = SafeMinPrice(theorPutPx + m_exitShift * sec.Tick, putPx, sec);
                                double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, false);
                                double qty = Math.Min(Math.Abs(m_fixedQty), Math.Abs(putOpenQty));
                                string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                posMan.BuyAtPrice(m_context, sec, qty, px, "Close BUY", sigName);

                                m_context.Log(sigName, MessageType.Info, false);

                                // Выход из foreach (IOptionStrikePair candidPair in orderedPairs)
                                break;
                            }
                            #endregion В путах
                        }
                        else if (m_optionType == StrikeType.Call)
                        {
                            #region В колах
                            if (callOpenQty < 0) // Это означает, что в страйке есть короткие колы
                            {
                                ISecurity sec = candidPair.Call.Security;
                                double px = SafeMinPrice(theorCallPx + m_exitShift * sec.Tick, callPx, sec);
                                double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, true);
                                double qty = Math.Min(Math.Abs(m_fixedQty), Math.Abs(callOpenQty));
                                string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                posMan.BuyAtPrice(m_context, sec, qty, px, "Close BUY", sigName);

                                m_context.Log(sigName, MessageType.Info, false);

                                // Выход из foreach (IOptionStrikePair candidPair in orderedPairs)
                                break;
                            }
                            #endregion В колах
                        }
                        else
                        {
                            #region В оба вида опционов сразу встаю
                            int executedQty = 0;
                            if (putOpenQty < 0) // Это означает, что в страйке есть короткие путы
                            {
                                ISecurity sec = candidPair.Put.Security;
                                double px = SafeMinPrice(theorPutPx + m_exitShift * sec.Tick, putPx, sec);
                                double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, false);
                                double qty = Math.Min(Math.Abs(m_fixedQty), Math.Abs(putOpenQty));
                                string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                posMan.BuyAtPrice(m_context, sec, qty, px, "Close BUY", sigName);

                                m_context.Log(sigName, MessageType.Info, false);

                                executedQty += (int)qty;
                            }

                            if ((callOpenQty < 0) && // Это означает, что в страйке есть короткие колы
                                (Math.Abs(executedQty) < Math.Abs(m_fixedQty)))
                            {
                                ISecurity sec = candidPair.Call.Security;
                                double px = SafeMinPrice(theorCallPx + m_exitShift * sec.Tick, callPx, sec);
                                double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, true);
                                double qty = Math.Min(Math.Abs(m_fixedQty) - Math.Abs(executedQty), Math.Abs(callOpenQty));
                                string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                posMan.BuyAtPrice(m_context, sec, qty, px, "Close BUY", sigName);

                                m_context.Log(sigName, MessageType.Info, false);

                                executedQty += (int)qty;
                            }

                            if (executedQty > 0)
                            {
                                // Выход из foreach (IOptionStrikePair candidPair in orderedPairs)
                                break;
                            }
                            #endregion В оба вида опционов сразу встаю
                        }
                        #endregion Сдаём риск (один квант объёма за раз)
                    }
                }
                else
                {
                    msg = String.Format("[{0}.{1}] risk:{2}; maxRisk:{3}; orderedPairs.Length:{4}",
                        Context.Runtime.TradeName, GetType().Name, risk, maxRisk, orderedPairs.Length);
                    m_context.Log(msg, MessageType.Warning, true);
                }
            }

            return Constants.NaN;
        }

        /// <summary>
        /// \~english Minimum of theoretical price and real quote (if present). But not less then 1 price step.
        /// \~russian Минимальная величина из теоретической цены и реальной котировки (если есть). Но не менее 1 тика.
        /// </summary>
        /// <param name="sellPx"></param>
        /// <param name="bidPx"></param>
        /// <param name="sec"></param>
        /// <returns></returns>
        public static double SafeMaxPrice(double sellPx, double bidPx, ISecurity sec)
        {
            double px = sellPx;
            if ((!Double.IsNaN(bidPx)) && (bidPx > 0))
                px = Math.Max(bidPx, sellPx);
            px = sec.Tick * Math.Round(px / sec.Tick);
            // Минимум 1 тик
            px = Math.Max(px, sec.Tick);
            return px;
        }

        /// <summary>
        /// \~english Minimum of theoretical price and real quote (if present). But not less then 1 price step.
        /// \~russian Минимальная величина из теоретической цены и реальной котировки (если есть). Но не менее 1 тика.
        /// </summary>
        /// <param name="buyPx"></param>
        /// <param name="askPx"></param>
        /// <param name="sec"></param>
        /// <returns></returns>
        public static double SafeMinPrice(double buyPx, double askPx, ISecurity sec)
        {
            double px = buyPx;
            if ((!Double.IsNaN(askPx)) && (askPx > 0))
                px = Math.Min(askPx, buyPx);
            px = sec.Tick * Math.Round(px / sec.Tick);
            // Минимум 1 тик
            px = Math.Max(px, sec.Tick);
            return px;
        }
    }
}
