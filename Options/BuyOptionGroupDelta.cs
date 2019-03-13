using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Buy many options while position risk is 'small' (strike range is set in units of delta)
    /// \~region Покупка группы опционов до заданного уровня риска (диапазон страйков задается в единицах дельты)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Buy option group (delta range)", Language = Constants.En)]
    [HelperName("Покупка группы опционов (диапазон по дельте)", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(9)]
    [Input(0, TemplateTypes.DOUBLE | TemplateTypes.BOOL, Name = Constants.Permission)]
    [Input(1, TemplateTypes.DOUBLE, Name = "Strike")]
    [Input(2, TemplateTypes.DOUBLE, Name = "Current Risk")]
    [Input(3, TemplateTypes.DOUBLE, Name = "Max Risk")]
    [Input(4, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(5, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(6, TemplateTypes.INTERACTIVESPLINE, Name = "Call delta")]
    [Input(7, TemplateTypes.DOUBLE, Name = "Call Risk")]
    [Input(8, TemplateTypes.DOUBLE, Name = "Put Risk")]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Покупка группы опционов до заданного уровня риска (диапазон страйков задается в единицах дельты)")]
    [HelperDescription("Buy many options while position risk is 'small' (strike range is set in units of delta)", Constants.En)]
    // ReSharper disable once UnusedMember.Global
    public class BuyOptionGroupDelta : IContextUses, IValuesHandlerWithNumber
    {
        private const string DefaultQty = "1";

        private IContext m_context;

        ///// <summary>Тип опционов для котирования (Any - автоматический выбор)</summary>
        //private StrikeType m_optionType = StrikeType.Any;
        private int m_fixedQty = Int32.Parse(DefaultQty);
        /// <summary>Сдвиг котировки при входе в позицию (в шагах цены)</summary>
        private int m_entryShift;
        /// <summary>Сдвиг котировки при выходе из позиции (в шагах цены)</summary>
        private int m_exitShift;
        ///// <summary>Сдвиг котировки при входе в позицию (в шагах цены)</summary>
        //private int m_entryShiftIv;
        ///// <summary>Сдвиг котировки при выходе из позиции (в шагах цены)</summary>
        //private int m_exitShiftIv;

        /// <summary>Шаг страйков для выделения главных подсерий</summary>
        private double m_strikeStep = 0;
        /// <summary>Минимальная допустимая дельта с которой мы еще согласны работать</summary>
        private double m_minDelta = 0;
        /// <summary>Максимальная допустимая дельта с которой мы еще согласны работать</summary>
        private double m_maxDelta = 0;

        /// <summary>Флаг того, что дельту опционов следует проверять по модулю, а не в абсолютном выражении</summary>
        private bool m_checkAbsDelta = true;

        public IContext Context
        {
            get { return m_context; }
            set { m_context = value; }
        }

        #region Parameters
        ///// <summary>
        ///// \~english Limit trading to options of given type (call, put, both)
        ///// \~russian Ограничение на тип торгуемого инструмента (колл, пут, котируются одновременно оба)
        ///// </summary>
        //[HelperName("Option type", Constants.En)]
        //[HelperName("Тип опциона", Constants.Ru)]
        //[Description("Ограничение на тип торгуемого инструмента (колл, пут, котируются одновременно оба)")]
        //[HelperDescription("Limit trading to options of given type (call, put, both)", Language = Constants.En)]
        //[HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Any")]
        //public StrikeType OptionType
        //{
        //    get { return m_optionType; }
        //    set { m_optionType = value; }
        //}

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
        [HelperName("Entry shift", Constants.En)]
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

        /// <summary>
        /// \~english Strike step to extract the most important options
        /// \~russian Шаг страйков для выделения главных подсерий
        /// </summary>
        [HelperName("Strike step", Constants.En)]
        [HelperName("Шаг страйков", Constants.Ru)]
        [HandlerParameter(Name = "Strike Step", NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "0", Max = "10000000", Step = "1")]
        [Description("Шаг страйков для выделения главных подсерий")]
        [HelperDescription("Strike step to extract the most important options", Constants.En)]
        public double StrikeStep
        {
            get
            {
                return m_strikeStep;
            }
            set
            {
                m_strikeStep = Math.Max(0, value);
                if (Double.IsNaN(m_strikeStep) || Double.IsInfinity(m_strikeStep))
                    m_strikeStep = 0;
            }
        }

        /// <summary>
        /// \~english The lowest working delta we allow to quote (as percents)
        /// \~russian Минимальная рабочая дельта, которую мы согласны котировать (в процентах)
        /// </summary>
        [HelperName("Min delta, %", Constants.En)]
        [HelperName("Мин. дельта, %", Constants.Ru)]
        [HandlerParameter(NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-1000", Max = "1000", Step = "1")]
        [Description("Минимальная рабочая дельта, которую мы согласны котировать (в процентах)")]
        [HelperDescription("The lowest working delta we allow to quote (as percents)", Constants.En)]
        public int MinDeltaPct
        {
            get
            {
                return (int)Math.Round(m_minDelta * Constants.PctMult);
            }
            set
            {
                m_minDelta = ((double)value) / Constants.PctMult;
            }
        }

        /// <summary>
        /// \~english The highest working delta we allow to quote (as percents)
        /// \~russian Максимальная рабочая дельта, которую мы согласны котировать (в процентах)
        /// </summary>
        [HelperName("Max delta, %", Constants.En)]
        [HelperName("Макс. дельта, %", Constants.Ru)]
        [HandlerParameter(NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-1000", Max = "1000", Step = "1")]
        [Description("Максимальная рабочая дельта, которую мы согласны котировать (в процентах)")]
        [HelperDescription("The highest working delta we allow to quote (as percents)", Constants.En)]
        public int MaxDeltaPct
        {
            get
            {
                return (int)Math.Round(m_maxDelta * Constants.PctMult);
            }
            set
            {
                m_maxDelta = ((double)value) / Constants.PctMult;
            }
        }

        /// <summary>
        /// \~english Check absolute value of option's delta
        /// \~russian Флаг того, что дельту опционов следует проверять по модулю (без учета знака)
        /// </summary>
        [HelperName("Check absolute delta", Constants.En)]
        [HelperName("Проверять модуль дельты", Constants.Ru)]
        [HandlerParameter(NotOptimized = false, IsVisibleInBlock = true, Default = "true")]
        [Description("Флаг того, что дельту опционов следует проверять по модулю (без учета знака)")]
        [HelperDescription("Check absolute value of option's delta", Constants.En)]
        public bool CheckAbsDelta
        {
            get
            {
                return m_checkAbsDelta;
            }
            set
            {
                m_checkAbsDelta = value;
            }
        }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public double Execute(double entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer, InteractiveSeries callDelta, int barNum)
        {
            double res = Process(entryPermission, strike, risk, maxRisk, smile, optSer, callDelta, 0, 0, barNum);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public double Execute(bool entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer, InteractiveSeries callDelta, int barNum)
        {
            double permission = entryPermission ? 1 : -1;
            double res = Process(permission, strike, risk, maxRisk, smile, optSer, callDelta, 0, 0, barNum);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// (при этом можно указать риск покупки одного кола)
        /// </summary>
        public double Execute(double entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer, InteractiveSeries callDelta,
            double callRisk, int barNum)
        {
            double res = Process(entryPermission, strike, risk, maxRisk, smile, optSer, callDelta, callRisk, 0, barNum);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// (при этом можно указать риск покупки одного кола)
        /// </summary>
        public double Execute(bool entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer, InteractiveSeries callDelta,
            double callRisk, int barNum)
        {
            double permission = entryPermission ? 1 : -1;
            double res = Process(permission, strike, risk, maxRisk, smile, optSer, callDelta, callRisk, 0, barNum);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// (при этом можно указать риск покупки одного кола и риск покупки одного пута)
        /// </summary>
        public double Execute(double entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer, InteractiveSeries callDelta,
            double callRisk, double putRisk, int barNum)
        {
            double res = Process(entryPermission, strike, risk, maxRisk, smile, optSer, callDelta, callRisk, putRisk, barNum);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// (при этом можно указать риск покупки одного кола и риск покупки одного пута)
        /// </summary>
        public double Execute(bool entryPermission,
            double strike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer, InteractiveSeries callDelta,
            double callRisk, double putRisk, int barNum)
        {
            double permission = entryPermission ? 1 : -1;
            double res = Process(permission, strike, risk, maxRisk, smile, optSer, callDelta, callRisk, putRisk, barNum);
            return res;
        }

        /// <summary>
        /// Основной метод, который выполняет всю торговую логику по котированию и следит за риском
        /// </summary>
        protected double Process(double entryPermission,
            double centralStrike, double risk, double maxRisk, InteractiveSeries smile, IOptionSeries optSer, InteractiveSeries callDelta,
            double callRisk, double putRisk, int barNum)
        {
            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if ((barNum < barsCount - 1) || (optSer == null) || (smile == null))
                return Constants.NaN;

            {
                IOptionStrikePair testPair;
                if (!optSer.TryGetStrikePair(centralStrike, out testPair))
                    return Constants.NaN;
            }

            // Если риск не был измерен говорить вообще не о чем!
            if (Double.IsNaN(risk) || Double.IsInfinity(risk))
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

            SmileInfo callDeltaInfo = callDelta.GetTag<SmileInfo>();
            if ((callDeltaInfo == null) || (callDeltaInfo.ContinuousFunction == null))
                return Constants.NaN;

            // Функция для вычисления дельты кола
            IFunction cDf = callDeltaInfo.ContinuousFunction;

            // Набираем риск
            if (risk < maxRisk)
            {
                List<IOptionStrikePair> orderedPairs = GetFilteredPairs(optSer, centralStrike, cDf, 
                    m_strikeStep, m_minDelta, m_maxDelta, m_checkAbsDelta);

                // Сколько лотов уже выставлено в рынок
                double pendingQty = 0;
                if (orderedPairs.Count > 0)
                {
                    foreach (IOptionStrikePair candidPair in orderedPairs)
                    {
                        double ivAtm;
                        double strike = candidPair.Strike;
                        if ((!sInfo.ContinuousFunction.TryGetValue(strike, out ivAtm)) || Double.IsNaN(ivAtm) || (ivAtm < Double.Epsilon))
                        {
                            string msg = String.Format("[{0}.{1}] Unable to get IV at strike {2}. ivAtm:{3}",
                                Context.Runtime.TradeName, GetType().Name, candidPair.Strike, ivAtm);
                            m_context.Log(msg, MessageType.Error, true);
                            //return Constants.NaN;
                            continue;
                        }

                        double theorPutPx = FinMath.GetOptionPrice(futPx, strike, dT, ivAtm, 0, false);
                        double theorCallPx = FinMath.GetOptionPrice(futPx, strike, dT, ivAtm, 0, true);

                        double cd, pd;
                        // Вычисляю дельту кола и с ее помощью -- дельту пута
                        if (!cDf.TryGetValue(strike, out cd))
                        {
                            // Этого не может быть по правилу отбора страйков!
                            Contract.Assert(false, "Почему мы не смогли вычислить дельту кола???");
                            continue;
                        }

                        // Типа, колл-пут паритет для вычисления дельты путов
                        pd = 1 - cd;
                        if (m_checkAbsDelta)
                        {
                            // Берем дельты по модулю
                            cd = Math.Abs(cd);
                            pd = Math.Abs(pd);
                        }

                        double putPx, callPx;
                        {
                            double putQty, callQty;
                            DateTime putTime, callTime;
                            putPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Put, OptionPxMode.Ask, 0, 0, out putQty, out putTime);
                            callPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Call, OptionPxMode.Ask, 0, 0, out callQty, out callTime);
                        }

                        #region Набираем риск
                        int executedQty = 0;
                        // Если дельта пута влезает в диапазон -- выставляем котировку в путы
                        if ((m_minDelta <= pd) && (pd <= m_maxDelta))
                        {
                            #region Набираем риск в путах
                            ISecurity sec = candidPair.Put.Security;
                            double px = SellOptions.SafeMinPrice(theorPutPx + m_entryShift * sec.Tick, putPx, sec);
                            double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, false);
                            //double qty = Math.Max(1, Math.Abs(m_fixedQty / 2));
                            double qty = Math.Abs(m_fixedQty);
                            // TODO: Немного грубая оценка, но пока сойдет
                            qty = BuyOptions.GetSafeQty(risk + pendingQty * putRisk, maxRisk, qty, putRisk);
                            if (qty > 0)
                            {
                                string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                posMan.BuyAtPrice(m_context, sec, qty, px, "Open BUY", sigName);
                                pendingQty += qty;

                                m_context.Log(sigName, MessageType.Info, false);

                                executedQty += (int)qty;
                            }
                            #endregion Набираем риск в путах
                        }

                        // Если дельта кола влезает в диапазон -- выставляем котировку в колы
                        if (Math.Abs(executedQty) < Math.Abs(m_fixedQty) &&
                            (m_minDelta <= cd) && (cd <= m_maxDelta))
                        {
                            #region Набираем риск в колах
                            ISecurity sec = candidPair.Call.Security;
                            double px = SellOptions.SafeMinPrice(theorCallPx + m_entryShift * sec.Tick, callPx, sec);
                            double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, true);
                            double qty = Math.Abs(m_fixedQty) - Math.Abs(executedQty);
                            // TODO: Немного грубая оценка, но пока сойдет
                            // Делаю оценку изменения текущего риска, если нам зафилят заявку в путах
                            //qty = GetSafeQty(risk + Math.Abs(executedQty) * putRisk, maxRisk, qty, callRisk);
                            // Причем здесь уже не нужно отдельно учитывать executedQty, потому что он входит в pendingQty
                            qty = BuyOptions.GetSafeQty(risk + pendingQty * callRisk, maxRisk, qty, callRisk);
                            if (qty > 0)
                            {
                                string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                posMan.BuyAtPrice(m_context, sec, qty, px, "Open BUY", sigName);
                                pendingQty += qty;

                                m_context.Log(sigName, MessageType.Info, false);

                                //executedQty += (int)qty;
                            }
                            #endregion Набираем риск в колах
                        }
                        #endregion Набираем риск
                    } // End foreach (IOptionStrikePair candidPair in orderedPairs)
                }
                else
                {
                    string msg = String.Format("[{0}] Strike not found. risk:{1}; maxRisk:{2}; orderedPairs.Count:{3}",
                        Context.Runtime.TradeName, risk, maxRisk, orderedPairs.Count);
                    m_context.Log(msg, MessageType.Warning, true);
                }
            }
            else if (risk > maxRisk)
            {
                string msg;
                //string msg = String.Format("[DEBUG:{0}] risk:{1}; maxRisk:{2}", Context.Runtime.TradeName, risk, maxRisk);
                //m_context.Log(msg, MessageType.Info, true);

                // Здесь мы работаем от центрального страйка, поэтому никаких циклов не надо!
                // Надо взять пары, начиная от центральной и далее по возрастанию расстояния
                var orderedPairs = (from p in optSer.GetStrikePairs()
                                    orderby Math.Abs(p.Strike - centralStrike) ascending
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
                            msg = String.Format("[{0}:{1}] Strike:{2}; putOpenQty:{3}; callOpenQty:{4}",
                                Context.Runtime.TradeName, GetType().Name, candidPair.Strike, putOpenQty, callOpenQty);
                            m_context.Log(msg, MessageType.Info, true);
                        }
                        #endregion Проверяю, что в страйке есть ДЛИННАЯ позиция

                        double theorPutPx, theorCallPx;
                        {
                            double iv;
                            if ((!sInfo.ContinuousFunction.TryGetValue(candidPair.Strike, out iv)) || Double.IsNaN(iv) ||
                                (iv < Double.Epsilon))
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
                            double putQty, callQty;
                            DateTime putTime, callTime;
                            putPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Put, OptionPxMode.Bid, 0, 0, out putQty, out putTime);
                            callPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Call, OptionPxMode.Bid, 0, 0, out callQty, out callTime);
                        }

                        //if (m_optionType == StrikeType.Put)
                        //{
                        //    #region В путах
                        //    if (putOpenQty > 0) // Это означает, что в страйке есть длинные путы
                        //    {
                        //        ISecurity sec = candidPair.Put.Security;
                        //        double px = SellOptions.SafeMaxPrice(theorPutPx + m_exitShift * sec.Tick, putPx, sec);
                        //        double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, false);
                        //        double qty = Math.Min(Math.Abs(m_fixedQty), Math.Abs(putOpenQty));
                        //        string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                        //        posMan.SellAtPrice(m_context, sec, qty, px, "Close SELL", sigName);

                        //        m_context.Log(sigName, MessageType.Info, false);

                        //        // Выход из foreach (IOptionStrikePair candidPair in orderedPairs)
                        //        break;
                        //    }
                        //    #endregion В путах
                        //}
                        //else if (m_optionType == StrikeType.Call)
                        //{
                        //    #region В колах
                        //    if (callOpenQty > 0) // Это означает, что в страйке есть длинные колы
                        //    {
                        //        ISecurity sec = candidPair.Call.Security;
                        //        double px = SellOptions.SafeMaxPrice(theorCallPx + m_exitShift * sec.Tick, callPx, sec);
                        //        double iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, 0, true);
                        //        double qty = Math.Min(Math.Abs(m_fixedQty), Math.Abs(callOpenQty));
                        //        string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                        //        posMan.SellAtPrice(m_context, sec, qty, px, "Close SELL", sigName);

                        //        m_context.Log(sigName, MessageType.Info, false);

                        //        // Выход из foreach (IOptionStrikePair candidPair in orderedPairs)
                        //        break;
                        //    }
                        //    #endregion В колах
                        //}
                        //else
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

                                m_context.Log(sigName, MessageType.Info, false);

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
                    } // End foreach (IOptionStrikePair candidPair in orderedPairs)
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
        /// Надо взять пары, начиная от центральной и далее по возрастанию расстояния с учетом шага страйков и ограничений на дельту
        /// </summary>
        /// <param name="optSer">серия опционов (обязательный аргумент)</param>
        /// <param name="centralStrike">центральный страйк</param>
        /// <param name="callDeltaFunction">функция для вычисления дельты кола (обязательный аргумент)</param>
        /// <param name="strikeStep">шаг страйков (допустим 0)</param>
        /// <param name="minDelta">минимальная рабочая дельта для котирования</param>
        /// <param name="maxDelta">максимальная рабочая дельта для котирования</param>
        /// <param name="checkAbsDelta">проверять дельту по модулю</param>
        internal static List<IOptionStrikePair> GetFilteredPairs(IOptionSeries optSer, double centralStrike, IFunction callDeltaFunction,
            double strikeStep, double minDelta, double maxDelta, bool checkAbsDelta)
        {
            List<IOptionStrikePair> orderedPairs;
            #region Отбор страйков (пар опционов) в качестве кандидатов для котирования
            if (strikeStep < Double.Epsilon)
            {
                // Просто сортируем страйки по расстоянию до Центра
                var buf = (from p in optSer.GetStrikePairs()
                           orderby Math.Abs(p.Strike - centralStrike) ascending
                           select p).ToList();
                orderedPairs = new List<IOptionStrikePair>();
                foreach (var p in buf)
                {
                    double cd, pd;
                    // Вычисляю дельту кола и с ее помощью -- дельту пута
                    if (callDeltaFunction.TryGetValue(p.Strike, out cd))
                    {
                        // Типа, колл-пут паритет для вычисления дельты путов
                        pd = 1 - cd;
                        if (checkAbsDelta)
                        {
                            // Берем дельты по модулю
                            cd = Math.Abs(cd);
                            pd = Math.Abs(pd);
                        }

                        // Если дельта кола ИЛИ дельта пута влезают в диапазон -- добавляем в список
                        if ((minDelta <= cd) && (cd <= maxDelta) ||
                            (minDelta <= pd) && (pd <= maxDelta))
                        {
                            orderedPairs.Add(p);
                        }
                    }
                }
            }
            else
            {
                // Сортировка по возрастанию до Центра + обязательно условие кратности параметру m_strikeStep
                var buf = (from p in optSer.GetStrikePairs()
                           let dK = Math.Abs(p.Strike - centralStrike)
                           let dKStep = (int)Math.Round(dK / strikeStep)
                           where DoubleUtil.AreClose(dK, strikeStep * dKStep) // проверяем, что расстояние от страйка до центра кратно m_strikeStep
                           orderby Math.Abs(p.Strike - centralStrike) ascending
                           select p).ToArray();
                orderedPairs = new List<IOptionStrikePair>();
                foreach (var p in buf)
                {
                    double cd, pd;
                    // Вычисляю дельту кола и с ее помощью -- дельту пута
                    if (callDeltaFunction.TryGetValue(p.Strike, out cd))
                    {
                        // Типа, колл-пут паритет для вычисления дельты путов
                        pd = 1 - cd;
                        if (checkAbsDelta)
                        {
                            // Берем дельты по модулю
                            cd = Math.Abs(cd);
                            pd = Math.Abs(pd);
                        }

                        // Если дельта кола ИЛИ дельта пута влезают в диапазон -- добавляем в список
                        if ((minDelta <= cd) && (cd <= maxDelta) ||
                            (minDelta <= pd) && (pd <= maxDelta))
                        {
                            orderedPairs.Add(p);
                        }
                    }
                }
            } // End if (m_strikeStep < Double.Epsilon)
            #endregion Отбор страйков (пар опционов) в качестве кандидатов для котирования

            Contract.Assert(orderedPairs != null, "Каким-то странным образом (orderedPairs == null)?");

            return orderedPairs;
        }
    }
}
