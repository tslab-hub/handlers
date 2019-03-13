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
    /// \~english Buy and sell options while position risk is 'small' (strike range is set in units of delta)
    /// \~region Покупка и продажа опционов до заданного уровня риска (диапазон страйков задается в единицах дельты)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Market maker (delta range)", Language = Constants.En)]
    [HelperName("Маркетмейкер (диапазон по дельте)", Language = Constants.Ru)]
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
    [Description("Покупка и продажа опционов до заданного уровня риска (диапазон страйков задается в единицах дельты)")]
    [HelperDescription("Buy and sell options while position risk is 'small' (strike range is set in units of delta)", Constants.En)]
    // ReSharper disable once UnusedMember.Global
    public class MarketMakerDelta : IContextUses, IValuesHandlerWithNumber
    {
        private const string DefaultQty = "1";

        private IContext m_context;

        ///// <summary>Тип опционов для котирования (Any - автоматический выбор)</summary>
        //private StrikeType m_optionType = StrikeType.Any;
        /// <summary>Объем одной заявки (неотрицательное число)</summary>
        private int m_fixedQty = Int32.Parse(DefaultQty);
        /// <summary>Сдвиг котировки при входе в позицию (в шагах цены)</summary>
        private int m_entryShift;
        ///// <summary>Сдвиг котировки при выходе из позиции (в шагах цены)</summary>
        //private int m_exitShift;
        /// <summary>Сдвиг котировки при входе в позицию (в безразмерных единицах волатильности)</summary>
        private double m_entryShiftIv;
        ///// <summary>Сдвиг котировки при выходе из позиции (в безразмерных единицах волатильности)</summary>
        //private double m_exitShiftIv;

        /// <summary>Шаг страйков для выделения главных подсерий</summary>
        private double m_strikeStep = 0;
        /// <summary>Минимальная допустимая дельта с которой мы еще согласны работать</summary>
        private double m_minDelta = 0;
        /// <summary>Максимальная допустимая дельта с которой мы еще согласны работать</summary>
        private double m_maxDelta = 0;

        /// <summary>Флаг того, что дельту опционов следует проверять по модулю, а не в абсолютном выражении</summary>
        private bool m_checkAbsDelta = true;
        /// <summary>Флаг того, что котировки надо выставлять СРАЗУ ВСЕ (в расчете на работу с сервисом типа Liquid.Pro)</summary>
        private bool m_liquidProAlgo = false;

        /// <summary>Максимальное допустимое количество контрактов на одном страйке (без учета знака)</summary>
        private int m_maxContractsOnStrike = 0;

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
        //[HelperName("Option Type", Constants.En)]
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
            set { m_fixedQty = Math.Max(0, value); }
        }

        /// <summary>
        /// \~english Shift to get better buy or sell price (price steps)
        /// \~russian Сдвиг цены для получения лучшей цены покупки или продажи (в шагах цены)
        /// </summary>
        [HelperName("Shift, p.s.", Constants.En)]
        [HelperName("Сдвиг, ш.ц.", Constants.Ru)]
        [Description("Сдвиг цены для получения лучшей цены покупки или продажи (в шагах цены)")]
        [HelperDescription("Shift to get better buy or sell price (price steps)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "0", Max = "1000000", Step = "1")]
        public int EntryShift
        {
            get { return m_entryShift; }
            set { m_entryShift = Math.Max(0, value); }
        }

        ///// <summary>
        ///// \~english Exit shift to get quick execution (price step)
        ///// \~russian Сдвиг выхода для быстрого закрытия позиции (шаг цены)
        ///// </summary>
        //[HelperName("Exit Shift", Constants.En)]
        //[HelperName("Сдвиг выхода", Constants.Ru)]
        //[Description("Сдвиг выхода для быстрого закрытия позиции (шаг цены)")]
        //[HelperDescription("Exit shift to get quick execution (price step)", Language = Constants.En)]
        //[HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
        //    Default = "0", Min = "-1000000", Max = "1000000", Step = "1")]
        //public int ExitShift
        //{
        //    get { return m_exitShift; }
        //    set { m_exitShift = value; }
        //}

        /// <summary>
        /// \~english Shift to get better buy or sell price (percents of volatility)
        /// \~russian Сдвиг цены для получения лучшей цены покупки или продажи (в процентах волатильности)
        /// </summary>
        [HelperName("Shift, %", Constants.En)]
        [HelperName("Сдвиг, %", Constants.Ru)]
        [Description("Сдвиг цены для получения лучшей цены покупки или продажи (в процентах волатильности)")]
        [HelperDescription("Shift to get better buy or sell price (percents of volatility)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "0", Max = "1000000", Step = "0.5")]
        public double EntryShiftIvPct
        {
            get { return m_entryShiftIv * Constants.PctMult; }
            set { m_entryShiftIv = Math.Max(0, value / Constants.PctMult); }
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

        /// <summary>
        /// \~english Set all quotes at all strikes (this mode is convinient with the Liquid.Pro service)
        /// \~russian Флаг того, что котировки надо выставлять СРАЗУ ВСЕ (в расчете на работу с сервисом типа Liquid.Pro)
        /// </summary>
        [HelperName("Force all quotes", Constants.En)]
        [HelperName("Выставлять все котировки", Constants.Ru)]
        [HandlerParameter(NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        [Description("Флаг того, что котировки надо выставлять СРАЗУ ВСЕ (в расчете на работу с сервисом типа Liquid.Pro)")]
        [HelperDescription("Set all quotes at all strikes (this mode is convinient with the Liquid.Pro service)", Constants.En)]
        public bool LiquidProAlgo
        {
            get
            {
                return m_liquidProAlgo;
            }
            set
            {
                m_liquidProAlgo = value;
            }
        }

        /// <summary>
        /// \~english Maximum amount of contracts at the single strike (absolute value)
        /// \~russian Максимальное допустимое количество контрактов на одном страйке (без учета знака)
        /// </summary>
        [HelperName("Max contracts at strike", Constants.En)]
        [HelperName("Макс. контрактов на страйке", Constants.Ru)]
        [HandlerParameter(NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "0", Max = "10000000", Step = "10")]
        [Description("Максимальное допустимое количество контрактов на одном страйке (без учета знака)")]
        [HelperDescription("Maximum amount of contracts at the single strike (absolute value)", Constants.En)]
        public int MaxContractsOnStrike
        {
            get
            {
                return m_maxContractsOnStrike;
            }
            set
            {
                m_maxContractsOnStrike = Math.Max(0, value);
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
                List<IOptionStrikePair> orderedPairs = BuyOptionGroupDelta.GetFilteredPairs(optSer, centralStrike, cDf, 
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

                        double theorPutPx = FinMath.GetOptionPrice(futPx, strike, dT, ivAtm, sInfo.RiskFreeRate, false);
                        double theorCallPx = FinMath.GetOptionPrice(futPx, strike, dT, ivAtm, sInfo.RiskFreeRate, true);

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
                            double putQty2, callQty2;
                            DateTime putTime, callTime;
                            putPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Put, OptionPxMode.Ask, 0, 0, out putQty2, out putTime);
                            callPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Call, OptionPxMode.Ask, 0, 0, out callQty2, out callTime);
                        }

                        // Текущие объемы на одном страйке
                        double putQty, callQty;
                        {
                            SingleSeriesPositionGrid.GetPairQty(posMan, candidPair, out putQty, out callQty);
                            //double currentStrikeRisk = Math.Abs(putQty + callQty);
                            //if (currentStrikeRisk >= m_maxContractsOnStrike)
                            //{
                            //    // TODO: Если на одном страйке лимиты уже выбраны -- пропускаем его. Или пытаемся выставить заявку на выход?..
                            //    continue;
                            //}
                        }

                        #region Набираем риск (ПОКУПКА)
                        {
                            int executedQty = 0;
                            // Если дельта пута влезает в диапазон -- выставляем котировку в путы
                            if ((m_minDelta <= pd) && (pd <= m_maxDelta))
                            {
                                #region Набираем риск в путах (ПОКУПКА)
                                ISecurity sec = candidPair.Put.Security;
                                //double px = SellOptions.SafeMinPrice(theorPutPx - m_entryShift * sec.Tick, putPx, sec);
                                // Собираюсь покупать, а не продавать. Поэтому мне _НЕ_ нужна SafeMinPrice
                                double px = Math.Max(0, theorPutPx - m_entryShift * sec.Tick);
                                double iv = Double.NaN;
                                if (px > 0)
                                    iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, sInfo.RiskFreeRate, false);
                                // Сдвигаем покупку вниз по волатильности ЕСЛИ НУЖНО!
                                if ((!Double.IsNaN(iv)) && (!DoubleUtil.IsZero(m_entryShiftIv)))
                                {
                                    iv -= m_entryShiftIv;
                                    // Пересчитываем новую цену после сдвига по волатильности
                                    if ((!Double.IsNaN(iv)) && (iv > 0))
                                    {
                                        double theorOptPx = FinMath.GetOptionPrice(futPx, candidPair.Strike, dT, iv, sInfo.RiskFreeRate, false);
                                        px = Math.Floor(theorOptPx / candidPair.Tick) * candidPair.Tick;
                                    }
                                    else
                                        px = Double.NaN;
                                }
                                
                                if ((!Double.IsNaN(px)) && (px > 0))
                                {
                                    double qty = Math.Max(1, Math.Abs(m_fixedQty / 2));
                                    // TODO: Немного грубая оценка, но пока сойдет
                                    qty = BuyOptions.GetSafeQty(risk + pendingQty * putRisk, maxRisk, qty, putRisk);
                                    // Работа в режиме Liquid.Pro
                                    if (m_liquidProAlgo)
                                        qty = m_fixedQty;
                                    // Безопасный объем заявки с учетом ограничения риска на страйк
                                    qty = GetSafeQtyToBuyStrike(putQty, callQty, qty, m_maxContractsOnStrike);
                                    if (qty > 0)
                                    {
                                        string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                        posMan.BuyAtPrice(m_context, sec, qty, px, "Open BUY", sigName);
                                        pendingQty += qty;

                                        m_context.Log(sigName, MessageType.Info, false);

                                        executedQty += (int)qty;
                                    }
                                }
                                #endregion Набираем риск в путах (ПОКУПКА)
                            }

                            // Если дельта кола влезает в диапазон -- выставляем котировку в колы
                            if (((Math.Abs(executedQty) < Math.Abs(m_fixedQty)) || m_liquidProAlgo) &&
                                (m_minDelta <= cd) && (cd <= m_maxDelta))
                            {
                                #region Набираем риск в колах (ПОКУПКА)
                                ISecurity sec = candidPair.Call.Security;
                                //double px = SellOptions.SafeMinPrice(theorCallPx - m_entryShift * sec.Tick, callPx, sec);
                                // Собираюсь покупать, а не продавать. Поэтому мне _НЕ_ нужна SafeMinPrice
                                double px = Math.Max(0, theorCallPx - m_entryShift * sec.Tick);
                                double iv = Double.NaN;
                                if (px > 0)
                                    iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, sInfo.RiskFreeRate, true);
                                // Сдвигаем покупку вниз по волатильности ЕСЛИ НУЖНО!
                                if ((!Double.IsNaN(iv)) && (!DoubleUtil.IsZero(m_entryShiftIv)))
                                {
                                    iv -= m_entryShiftIv;
                                    // Пересчитываем новую цену после сдвига по волатильности
                                    if ((!Double.IsNaN(iv)) && (iv > 0))
                                    {
                                        double theorOptPx = FinMath.GetOptionPrice(futPx, candidPair.Strike, dT, iv, sInfo.RiskFreeRate, true);
                                        //theorOptPx -= ivTarget.EntryShiftPrice * candidPair.Tick;
                                        px = Math.Floor(theorOptPx / candidPair.Tick) * candidPair.Tick;
                                    }
                                    else
                                        px = Double.NaN;
                                }

                                if ((!Double.IsNaN(px)) && (px > 0))
                                {
                                    double qty = Math.Abs(m_fixedQty) - Math.Abs(executedQty);
                                    // TODO: Немного грубая оценка, но пока сойдет
                                    qty = BuyOptions.GetSafeQty(risk + pendingQty * callRisk, maxRisk, qty, callRisk);
                                    // Работа в режиме Liquid.Pro
                                    if (m_liquidProAlgo)
                                        qty = m_fixedQty;
                                    // Безопасный объем заявки с учетом ограничения риска на страйк
                                    qty = GetSafeQtyToBuyStrike(putQty, callQty, qty, m_maxContractsOnStrike);
                                    if (qty > 0)
                                    {
                                        string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                        posMan.BuyAtPrice(m_context, sec, qty, px, "Open BUY", sigName);
                                        pendingQty += qty;

                                        m_context.Log(sigName, MessageType.Info, false);

                                        //executedQty += (int)qty;
                                    }
                                }
                                #endregion Набираем риск в колах
                            }
                        }
                        #endregion Набираем риск (ПОКУПКА)

                        #region Набираем риск (ПРОДАЖА)
                        {
                            int executedQty = 0;
                            // Если дельта пута влезает в диапазон -- выставляем котировку в путы
                            if ((m_minDelta <= pd) && (pd <= m_maxDelta))
                            {
                                #region Набираем риск в путах (ПРОДАЖА)
                                ISecurity sec = candidPair.Put.Security;
                                //double px = SellOptions.SafeMaxPrice(theorPutPx + m_entryShift * sec.Tick, putPx, sec);
                                // Собираюсь продавать, а не покупать. Поэтому мне _НЕ_ нужна SafeMaxPrice
                                double px = Math.Max(0, theorPutPx + m_entryShift * sec.Tick);
                                double iv = Double.NaN;
                                if (px > 0)
                                    iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, sInfo.RiskFreeRate, false);
                                // Сдвигаем покупку ВВЕРХ по волатильности ЕСЛИ НУЖНО!
                                if ((!Double.IsNaN(iv)) && (!DoubleUtil.IsZero(m_entryShiftIv)))
                                {
                                    iv += m_entryShiftIv;
                                    // Пересчитываем новую цену после сдвига по волатильности
                                    if ((!Double.IsNaN(iv)) && (iv > 0))
                                    {
                                        double theorOptPx = FinMath.GetOptionPrice(futPx, candidPair.Strike, dT, iv, sInfo.RiskFreeRate, false);
                                        //theorOptPx += ivTarget.EntryShiftPrice * candidPair.Tick;
                                        px = Math.Ceiling(theorOptPx / candidPair.Tick) * candidPair.Tick;
                                    }
                                    else
                                        px = Double.NaN;
                                }

                                if ((!Double.IsNaN(px)) && (px > 0))
                                {
                                    double qty = Math.Max(1, Math.Abs(m_fixedQty / 2));
                                    // TODO: Немного грубая оценка, но пока сойдет
                                    qty = BuyOptions.GetSafeQty(risk + pendingQty * putRisk, maxRisk, qty, putRisk);
                                    // Работа в режиме Liquid.Pro
                                    if (m_liquidProAlgo)
                                        qty = m_fixedQty;
                                    // Безопасный объем заявки с учетом ограничения риска на страйк
                                    qty = GetSafeQtyToSellStrike(putQty, callQty, qty, m_maxContractsOnStrike);
                                    if (qty > 0)
                                    {
                                        string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                        posMan.SellAtPrice(m_context, sec, qty, px, "Open SELL", sigName);
                                        pendingQty += qty;

                                        m_context.Log(sigName, MessageType.Info, false);

                                        executedQty += (int)qty;
                                    }
                                }
                                #endregion Набираем риск в путах (ПРОДАЖА)
                            }

                            // Если дельта кола влезает в диапазон -- выставляем котировку в колы
                            if (((Math.Abs(executedQty) < Math.Abs(m_fixedQty)) || m_liquidProAlgo) &&
                                (m_minDelta <= cd) && (cd <= m_maxDelta))
                            {
                                #region Набираем риск в колах (ПРОДАЖА)
                                ISecurity sec = candidPair.Call.Security;
                                //double px = SellOptions.SafeMaxPrice(theorCallPx + m_entryShift * sec.Tick, callPx, sec);
                                // Собираюсь продавать, а не покупать. Поэтому мне _НЕ_ нужна SafeMaxPrice
                                double px = Math.Max(0, theorCallPx + m_entryShift * sec.Tick);
                                double iv = Double.NaN;
                                if (px > 0)
                                    iv = FinMath.GetOptionSigma(futPx, candidPair.Strike, dT, px, sInfo.RiskFreeRate, true);
                                // Сдвигаем покупку ВВЕРХ по волатильности ЕСЛИ НУЖНО!
                                if ((!Double.IsNaN(iv)) && (!DoubleUtil.IsZero(m_entryShiftIv)))
                                {
                                    iv += m_entryShiftIv;
                                    // Пересчитываем новую цену после сдвига по волатильности
                                    if ((!Double.IsNaN(iv)) && (iv > 0))
                                    {
                                        double theorOptPx = FinMath.GetOptionPrice(futPx, candidPair.Strike, dT, iv, sInfo.RiskFreeRate, true);
                                        //theorOptPx += ivTarget.EntryShiftPrice * candidPair.Tick;
                                        px = Math.Ceiling(theorOptPx / candidPair.Tick) * candidPair.Tick;
                                    }
                                    else
                                        px = Double.NaN;
                                }

                                if ((!Double.IsNaN(px)) && (px > 0))
                                {
                                    double qty = Math.Abs(m_fixedQty) - Math.Abs(executedQty);
                                    // TODO: Немного грубая оценка, но пока сойдет
                                    qty = BuyOptions.GetSafeQty(risk + pendingQty * callRisk, maxRisk, qty, callRisk);
                                    // Работа в режиме Liquid.Pro
                                    if (m_liquidProAlgo)
                                        qty = m_fixedQty;
                                    // Безопасный объем заявки с учетом ограничения риска на страйк
                                    qty = GetSafeQtyToSellStrike(putQty, callQty, qty, m_maxContractsOnStrike);
                                    if (qty > 0)
                                    {
                                        string sigName = String.Format("Risk:{0}; MaxRisk:{1}; Px:{2}; Qty:{3}; IV:{4:P2}; dT:{5}", risk, maxRisk, px, qty, iv, dT);
                                        posMan.SellAtPrice(m_context, sec, qty, px, "Open SELL", sigName);
                                        pendingQty += qty;

                                        m_context.Log(sigName, MessageType.Info, false);

                                        //executedQty += (int)qty;
                                    }
                                }
                                #endregion Набираем риск в колах (ПРОДАЖА)
                            }
                        }
                        #endregion Набираем риск (ПРОДАЖА)
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

                // Надо взять пары, начиная от центральной и далее по возрастанию расстояния
                var orderedPairs = (from p in optSer.GetStrikePairs()
                                    orderby Math.Abs(p.Strike - centralStrike) ascending
                                    select p).ToArray();
                if (orderedPairs.Length > 0)
                {
                    foreach (IOptionStrikePair candidPair in orderedPairs)
                    {
                        bool orderIsSentToMarket = false;
                        double putOpenQty = posMan.GetTotalQty(candidPair.Put.Security, barNum);
                        double callOpenQty = posMan.GetTotalQty(candidPair.Call.Security, barNum);

                        if (!DoubleUtil.IsZero(putOpenQty) || !DoubleUtil.IsZero(callOpenQty))
                        {
                            msg = String.Format("[{0}:{1}] Strike:{2}; putOpenQty:{3}; callOpenQty:{4}",
                                Context.Runtime.TradeName, GetType().Name, candidPair.Strike, putOpenQty, callOpenQty);
                            m_context.Log(msg, MessageType.Info, true);
                        }

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

                        #region Проверяю, что в страйке есть ДЛИННАЯ позиция
                        if ((putOpenQty > 0) || (callOpenQty > 0))
                        {
                            #region Сдаём риск (один квант объёма за раз)
                            double putPx, callPx;
                            {
                                double putQty, callQty;
                                DateTime putTime, callTime;
                                putPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Put, OptionPxMode.Bid, 0, 0, out putQty, out putTime);
                                callPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Call, OptionPxMode.Bid, 0, 0, out callQty, out callTime);
                            }

                            #region В оба вида опционов сразу встаю
                            int executedQty = 0;
                            if (putOpenQty > 0) // Это означает, что в страйке есть длинные путы
                            {
                                ISecurity sec = candidPair.Put.Security;
                                double px = SellOptions.SafeMaxPrice(theorPutPx + /* m_exitShift */ m_entryShift * sec.Tick, putPx, sec);
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
                                double px = SellOptions.SafeMaxPrice(theorCallPx + /* m_exitShift */ m_entryShift * sec.Tick, callPx, sec);
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
                                //break;
                                orderIsSentToMarket = true;
                            }
                            #endregion В оба вида опционов сразу встаю
                            #endregion Сдаём риск (один квант объёма за раз)
                        }
                        #endregion Проверяю, что в страйке есть ДЛИННАЯ позиция

                        #region Проверяю, что в страйке есть КОРОТКАЯ позиция
                        if ((putOpenQty < 0) || (callOpenQty < 0))
                        {
                            #region Сдаём риск (один квант объёма за раз)
                            double putPx, callPx;
                            {
                                DateTime putTime, callTime;
                                double putAskQty, callAskQty;
                                putPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Put, OptionPxMode.Ask, 0, 0, out putAskQty, out putTime);
                                callPx = IvSmile.GetOptPrice(m_context, futPx, candidPair.Call, OptionPxMode.Ask, 0, 0, out callAskQty, out callTime);
                            }

                            #region В оба вида опционов сразу встаю
                            int executedQty = 0;
                            if (putOpenQty < 0) // Это означает, что в страйке есть короткие путы
                            {
                                ISecurity sec = candidPair.Put.Security;
                                double px = SellOptions.SafeMinPrice(theorPutPx - /* m_exitShift */ m_entryShift * sec.Tick, putPx, sec);
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
                                double px = SellOptions.SafeMinPrice(theorCallPx - /* m_exitShift */ m_entryShift * sec.Tick, callPx, sec);
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
                                //break;
                                orderIsSentToMarket = true;
                            }
                            #endregion В оба вида опционов сразу встаю
                            #endregion Сдаём риск (один квант объёма за раз)

                            // Выход из foreach (IOptionStrikePair candidPair in orderedPairs)
                            if (orderIsSentToMarket)
                                break;
                        }
                        #endregion Проверяю, что в страйке есть КОРОТКАЯ позиция
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
        /// Проверить, что при ПОКУПКЕ данного количество лотов итоговая позиция не превысит уровень максимального риска в данном страйке.
        /// В противном случае попробовать уменьшить количество лотов для зафила.
        /// </summary>
        /// <param name="putQty">текущее количество опционов пут в данном страйке (со знаком!)</param>
        /// <param name="callQty">текущее количество опционов колл в данном страйке (со знаком!)</param>
        /// <param name="qty">предполагаемый объём заявки НА ПОКУПКУ</param>
        /// <param name="maxContractsOnStrike">лимит риска на одном страйке (сумма количества лотов без знака)</param>
        /// <returns>максимальное безопасное qty при котором ещё не будет превышен лимит maxContractsOnStrike</returns>
        internal static double GetSafeQtyToBuyStrike(double putQty, double callQty, double qty, int maxContractsOnStrike)
        {
            int actualQty = 0;
            while ((actualQty < qty) && (Math.Abs(putQty + callQty + actualQty) < maxContractsOnStrike))
                actualQty++;

            // Защита от перезафила
            if (actualQty > qty)
                actualQty--;
            if (actualQty <= 0)
                return 0;

            if (Math.Abs(putQty + callQty + actualQty) > maxContractsOnStrike)
                actualQty--;
            if (actualQty <= 0)
                return 0;

            // Гарантированно неотрицательное число не больше qty и не выводящее нас за пределы лимита maxContractsOnStrike
            return actualQty;
        }

        /// <summary>
        /// Проверить, что при ПРОДАЖЕ данного количество лотов итоговая позиция не превысит уровень максимального риска в данном страйке.
        /// В противном случае попробовать уменьшить количество лотов для зафила.
        /// </summary>
        /// <param name="putQty">текущее количество опционов пут в данном страйке (со знаком!)</param>
        /// <param name="callQty">текущее количество опционов колл в данном страйке (со знаком!)</param>
        /// <param name="qty">предполагаемый объём заявки НА ПРОДАЖУ</param>
        /// <param name="maxContractsOnStrike">лимит риска на одном страйке (сумма количества лотов без знака)</param>
        /// <returns>максимальное безопасное qty при котором ещё не будет превышен лимит maxContractsOnStrike</returns>
        internal static double GetSafeQtyToSellStrike(double putQty, double callQty, double qty, int maxContractsOnStrike)
        {
            int actualQty = 0;
            while ((actualQty < qty) && (Math.Abs(putQty + callQty - actualQty) < maxContractsOnStrike))
                actualQty++;

            // Защита от перезафила
            if (actualQty > qty)
                actualQty--;
            if (actualQty <= 0)
                return 0;

            if (Math.Abs(putQty + callQty - actualQty) > maxContractsOnStrike)
                actualQty--;
            if (actualQty <= 0)
                return 0;

            // Гарантированно неотрицательное число не больше qty и не выводящее нас за пределы лимита maxContractsOnStrike
            return actualQty;
        }
    }
}
