using System;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Handlers.Options;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.OptionsPublic
{
    /// <summary>
    /// \~english Buy options while position risk is 'small'
    /// \~region Покупка опционов до заданного уровня риска
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPublic)]
    [HelperName("Buy Options", Language = Constants.En)]
    [HelperName("Покупка опционов", Language = Constants.Ru)]
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
    [Description("Покупка опционов до заданного уровня риска")]
    [HelperDescription("Buy options while position risk is 'small'", Constants.En)]
#if !DEBUG
    // Этот атрибут УБИРАЕТ блок из списка доступных в Редакторе Скриптов.
    // В своих блоках можете просто удалить его вместе с директивами условной компилляции.
    [HandlerInvisible]
#endif
    // ReSharper disable once UnusedMember.Global
    public class BuyOptionsPublic : IContextUses, IValuesHandlerWithNumber
    {
        private const string DefaultQty = "1";

        private IContext m_context;

        private StrikeType m_optionType = StrikeType.Call;
        private int m_fixedQty = Int32.Parse(DefaultQty);
        private int m_entryShift, m_exitShift;

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
        [HelperName("Option Type", Constants.En)]
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
        /// \~english Entry shift to get lower buy price (price step)
        /// \~russian Сдвиг входа для уменьшения цены покупки (шаг цены)
        /// </summary>
        [HelperName("Entry Shift", Constants.En)]
        [HelperName("Сдвиг входа", Constants.Ru)]
        [Description("Сдвиг входа для уменьшения цены покупки (шаг цены)")]
        [HelperDescription("Entry shift to get lower buy price (price step)", Language = Constants.En)]
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
        [HelperName("Exit Shift", Constants.En)]
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
                    string msg = String.Format("Unable to get IV at strike {0}. ivAtm:{1}", strike, ivAtm);
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
                    putPx = IvSmile.GetOptPrice(m_context, futPx, pair.Put, OptionPxMode.Ask, 0, 0, out putQty, out putTime);
                    callPx = IvSmile.GetOptPrice(m_context, futPx, pair.Call, OptionPxMode.Ask, 0, 0, out callQty, out callTime);
                }

                if (m_optionType == StrikeType.Put)
                {
                    #region В путах
                    ISecurity sec = pair.Put.Security;
                    double qty = Math.Abs(m_fixedQty);
                    qty = GetSafeQty(risk, maxRisk, qty, putRisk);
                    if (qty > 0)
                    {
                        double px = SellOptions.SafeMinPrice(theorPutPx + m_entryShift * sec.Tick, putPx, sec);
                        double iv = FinMath.GetOptionSigma(futPx, pair.Strike, dT, px, 0, false);
                        string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                        posMan.BuyAtPrice(m_context, sec, qty, px, "Open BUY", sigName);

                        m_context.Log(sigName, MessageType.Debug, false);
                    }
                    #endregion В путах
                }
                else if (m_optionType == StrikeType.Call)
                {
                    #region В колах
                    ISecurity sec = pair.Call.Security;
                    double qty = Math.Abs(m_fixedQty);
                    qty = GetSafeQty(risk, maxRisk, qty, callRisk);
                    if (qty > 0)
                    {
                        double px = SellOptions.SafeMinPrice(theorCallPx + m_entryShift * sec.Tick, callPx, sec);
                        double iv = FinMath.GetOptionSigma(futPx, pair.Strike, dT, px, 0, true);
                        string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                        posMan.BuyAtPrice(m_context, sec, qty, px, "Open BUY", sigName);

                        m_context.Log(sigName, MessageType.Debug, false);
                    }
                    #endregion В колах
                }
                else
                {
                    #region В оба вида опционов сразу встаю
                    int executedQty = 0;
                    {
                        ISecurity sec = pair.Put.Security;
                        double px = SellOptions.SafeMinPrice(theorPutPx + m_entryShift * sec.Tick, putPx, sec);
                        double iv = FinMath.GetOptionSigma(futPx, pair.Strike, dT, px, 0, false);
                        double qty = Math.Max(1, Math.Abs(m_fixedQty / 2));
                        qty = GetSafeQty(risk, maxRisk, qty, putRisk);
                        if (qty > 0)
                        {
                            string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                            posMan.BuyAtPrice(m_context, sec, qty, px, "Open BUY", sigName);

                            m_context.Log(sigName, MessageType.Debug, false);

                            executedQty += (int)qty;
                        }
                    }

                    if (Math.Abs(executedQty) < Math.Abs(m_fixedQty))
                    {
                        ISecurity sec = pair.Call.Security;
                        double px = SellOptions.SafeMinPrice(theorCallPx + m_entryShift * sec.Tick, callPx, sec);
                        double iv = FinMath.GetOptionSigma(futPx, pair.Strike, dT, px, 0, true);
                        double qty = Math.Abs(m_fixedQty) - Math.Abs(executedQty);
                        // Делаю оценку изменения текущего риска, если нам зафилят заявку в путах
                        qty = GetSafeQty(risk + Math.Abs(executedQty) * putRisk, maxRisk, qty, callRisk);
                        if (qty > 0)
                        {
                            string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                            posMan.BuyAtPrice(m_context, sec, qty, px, "Open BUY", sigName);

                            m_context.Log(sigName, MessageType.Debug, false);

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
                //m_context.Log(msg, MessageType.Debug, true);

                // Надо взять пары, начиная от центральной и далее по возрастанию расстояния
                var orderedPairs = (from p in optSer.GetStrikePairs()
                                    orderby Math.Abs(p.Strike - strike) ascending
                                    select p).ToArray();
                if (orderedPairs.Length > 0)
                {
                    foreach (IOptionStrikePair candidPair in orderedPairs)
                    {
                        #region Проверяю, что в страйке есть ДЛИННАЯ позиция
                        double putOpenQty = posMan.GetTotalQty(candidPair.Put.Security, barNum);
                        double callOpenQty = posMan.GetTotalQty(candidPair.Call.Security, barNum);

                        if ((putOpenQty <= 0) && (callOpenQty <= 0))
                            continue;
                        if (DoubleUtil.IsZero(putOpenQty) && DoubleUtil.IsZero(callOpenQty))
                            continue;

                        {
                            msg = String.Format("[DEBUG:{0}] putOpenQty:{1}; callOpenQty:{2}", Context.Runtime.TradeName, putOpenQty, callOpenQty);
                            m_context.Log(msg, MessageType.Debug, true);
                        }
                        #endregion Проверяю, что в страйке есть ДЛИННАЯ позиция

                        double theorPutPx, theorCallPx;
                        {
                            double iv;
                            if ((!sInfo.ContinuousFunction.TryGetValue(candidPair.Strike, out iv)) || Double.IsNaN(iv) ||
                                (iv < Double.Epsilon))
                            {
                                msg = String.Format("Unable to get IV at strike {0}. IV:{1}", candidPair.Strike, iv);
                                m_context.Log(msg, MessageType.Error, true);
                                continue;
                            }

                            theorPutPx = FinMath.GetOptionPrice(futPx, candidPair.Strike, dT, iv, 0, false);
                            theorCallPx = FinMath.GetOptionPrice(futPx, candidPair.Strike, dT, iv, 0, true);
                        }

                        #region Сдаём риск (один квант объёма за раз)
                        double putPx, callPx;
                        {
                            double putQty, callQty;
                            DateTime putTime, callTime;
                            putPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Put, OptionPxMode.Bid, 0, 0, out putQty, out putTime);
                            callPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Call, OptionPxMode.Bid, 0, 0, out callQty, out callTime);
                        }

                        if (m_optionType == StrikeType.Put)
                        {
                            #region В путах
                            if (putOpenQty > 0) // Это означает, что в страйке есть длинные путы
                            {
                                ISecurity sec = candidPair.Put.Security;
                                double px = SellOptions.SafeMaxPrice(theorPutPx + m_exitShift * sec.Tick, putPx, sec);
                                double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, false);
                                double qty = Math.Min(Math.Abs(m_fixedQty), Math.Abs(putOpenQty));
                                string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                posMan.SellAtPrice(m_context, sec, qty, px, "Close SELL", sigName);

                                m_context.Log(sigName, MessageType.Debug, false);

                                // Выход из foreach (IOptionStrikePair candidPair in orderedPairs)
                                break;
                            }
                            #endregion В путах
                        }
                        else if (m_optionType == StrikeType.Call)
                        {
                            #region В колах
                            if (callOpenQty > 0) // Это означает, что в страйке есть длинные колы
                            {
                                ISecurity sec = candidPair.Call.Security;
                                double px = SellOptions.SafeMaxPrice(theorCallPx + m_exitShift * sec.Tick, callPx, sec);
                                double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, true);
                                double qty = Math.Min(Math.Abs(m_fixedQty), Math.Abs(callOpenQty));
                                string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                posMan.SellAtPrice(m_context, sec, qty, px, "Close SELL", sigName);

                                m_context.Log(sigName, MessageType.Debug, false);

                                // Выход из foreach (IOptionStrikePair candidPair in orderedPairs)
                                break;
                            }
                            #endregion В колах
                        }
                        else
                        {
                            #region В оба вида опционов сразу встаю
                            int executedQty = 0;
                            if (putOpenQty > 0) // Это означает, что в страйке есть длинные путы
                            {
                                ISecurity sec = candidPair.Put.Security;
                                double px = SellOptions.SafeMaxPrice(theorPutPx + m_exitShift * sec.Tick, putPx, sec);
                                double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, false);
                                double qty = Math.Min(Math.Abs(m_fixedQty), Math.Abs(putOpenQty));
                                string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                posMan.SellAtPrice(m_context, sec, qty, px, "Close SELL", sigName);

                                m_context.Log(sigName, MessageType.Debug, false);

                                executedQty += (int)qty;
                            }

                            if ((callOpenQty > 0) && // Это означает, что в страйке есть длинные колы
                                (Math.Abs(executedQty) < Math.Abs(m_fixedQty)))
                            {
                                ISecurity sec = candidPair.Call.Security;
                                double px = SellOptions.SafeMaxPrice(theorCallPx + m_exitShift * sec.Tick, callPx, sec);
                                double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, true);
                                double qty = Math.Min(Math.Abs(m_fixedQty) - Math.Abs(executedQty), Math.Abs(callOpenQty));
                                string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                posMan.SellAtPrice(m_context, sec, qty, px, "Close SELL", sigName);

                                m_context.Log(sigName, MessageType.Debug, false);

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
                    msg = String.Format("[DEBUG:{0}] risk:{1}; maxRisk:{2}; orderedPairs.Length:{3}",
                        Context.Runtime.TradeName, risk, maxRisk, orderedPairs.Length);
                    m_context.Log(msg, MessageType.Warning, true);
                }
            }

            return Constants.NaN;
        }

        /// <summary>
        /// Проверить, что при зафиле данного количество лотов итоговая позиция не превысит уровень максимального риска.
        /// В противном случае попробовать уменьшить количество лотов для зафила.
        /// </summary>
        /// <param name="currentRisk">текущий риск</param>
        /// <param name="maxRisk">максимальный риск</param>
        /// <param name="qty">предполагаемый объём заявки</param>
        /// <param name="optRisk">изменение риска при покупке одного опциона</param>
        /// <returns>максимальное безопасное qty при котором ещё не будет превышен maxRisk</returns>
        internal static double GetSafeQty(double currentRisk, double maxRisk, double qty, double optRisk)
        {
            double targetRisk = currentRisk + qty * optRisk;

            // Если при полном исполнении риск ещё не будет превышен, то выставляемся полностью
            if (targetRisk <= maxRisk + Double.Epsilon)
                return qty;

            // TODO: Попробовать переписаться на бинарный поиск для оптимизации?..
            while ((qty > 0) && (maxRisk < targetRisk))
            {
                qty -= 1;
                targetRisk = currentRisk + qty * optRisk;
            }

            // Защита от попытки перевернуть позицию в другую сторону
            if (qty <= Double.Epsilon)
                qty = 0;

            return qty;
        }
    }
}
