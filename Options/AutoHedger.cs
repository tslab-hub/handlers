using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.Optimization;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Hedge delta by time (on every script execution)
    /// \~russian Автоматический хедж дельты позиции по времени (на каждом исполнении скрипта)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Auto Hedge Delta", Language = Constants.En)]
    [HelperName("Автохеджер дельты", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Delta)]
    [Input(2, TemplateTypes.OPTION_SERIES | TemplateTypes.SECURITY, Name = Constants.OptionSeries)]
    [Input(3, TemplateTypes.BOOL, Name = Constants.Permission)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Автоматический хедж дельты позиции по времени (на каждом исполнении скрипта)")]
    [HelperDescription("Hedge delta by time (on every script execution)", Constants.En)]
    public class AutoHedger : BaseContextWithNumber<double>
    {
        private const string MsgId = "HEDGER";
        private const string Limit = "1000000.0";
        private const string LastExecutionKey = "lastExecution";

        /// <summary>Период хеджирования (секунды)</summary>
        private double m_minPeriod;
        private bool m_hedgeDelta;
        private bool m_workWithoutOptions;
        private double m_sensitivity = 0.66;
        private double m_buyShift = 0, m_sellShift = 0;
        private OptimProperty m_doDelta = new OptimProperty(-1, false, double.MinValue, double.MaxValue, 1, 3);
        private OptimProperty m_targetDelta = new OptimProperty(0, false, double.MinValue, double.MaxValue, 1, 3);
        private OptimProperty m_upDelta = new OptimProperty(1, false, double.MinValue, double.MaxValue, 1, 3);
        private OptimProperty m_buyPrice = new OptimProperty(0, true, double.MinValue, double.MaxValue, 1, 4);
        private OptimProperty m_sellPrice = new OptimProperty(0, true, double.MinValue, double.MaxValue, 1, 4);

        private DateTime LastExecution
        {
            get
            {
                object tmp = m_context.LoadObject(VariableId + "_" + LastExecutionKey);
                DateTime res = (tmp is DateTime) ? (DateTime)tmp : new DateTime();
                return res;
            }
        }

        #region Parameters
        /// <summary>
        /// \~english Align delta to target level
        /// \~russian Привести дельту к целевому уровню
        /// </summary>
        [HelperName("Align Delta", Constants.En)]
        [HelperName("Ровнять дельту", Constants.Ru)]
        [Description("Привести дельту к целевому уровню")]
        [HelperDescription("Align delta to target level", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "False")]
        public bool HedgeDelta
        {
            get { return m_hedgeDelta; }
            set { m_hedgeDelta = value; }
        }

        /// <summary>
        /// \~english Hedge period (seconds)
        /// \~russian Период хеджирования (секунды)
        /// </summary>
        [HelperName("Period", Constants.En)]
        [HelperName("Период", Constants.Ru)]
        [Description("Период хеджирования (секунды)")]
        [HelperDescription("Hedge period (seconds)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "0", Max = Limit, Step = "1.0")]
        public double MinPeriod
        {
            get { return m_minPeriod; }
            set { m_minPeriod = value; }
        }

        /// <summary>
        /// \~english Possible delta decrease from target level
        /// \~russian Допуск по дельте вниз
        /// </summary>
        [HelperName("Down Delta", Constants.En)]
        [HelperName("Допуск вниз", Constants.Ru)]
        [Description("Допуск по дельте вниз")]
        [HelperDescription("Possible delta decrease from target level", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "-1.0",
            Name = "Down Delta", Min = ("-" + Limit), Max = "0", Step = "1.0")]
        public OptimProperty DownDelta
        {
            get { return m_doDelta; }
            set { m_doDelta = value; }
        }

        /// <summary>
        /// \~english Possible delta increase from target level
        /// \~russian Допуск по дельте вверх
        /// </summary>
        [HelperName("Up Delta", Constants.En)]
        [HelperName("Допуск вверх", Constants.Ru)]
        [Description("Допуск по дельте вверх")]
        [HelperDescription("Possible delta increase from target level", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "-1.0",
            Name = "Up Delta", Min = "0", Max = Limit, Step = "1.0")]
        public OptimProperty UpDelta
        {
            get { return m_upDelta; }
            set { m_upDelta = value; }
        }

        /// <summary>
        /// \~english Target delta
        /// \~russian Целевая дельта
        /// </summary>
        [HelperName("Target Delta", Constants.En)]
        [HelperName("Целевая дельта", Constants.Ru)]
        [Description("Целевая дельта")]
        [HelperDescription("Target delta", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0.0",
            Name = "Target Delta", Min = ("-" + Limit), Max = Limit, Step = "1.0")]
        public OptimProperty TargetDelta
        {
            get { return m_targetDelta; }
            set { m_targetDelta = value; }
        }

        /// <summary>
        /// \~english Delta should pass this percent of distance to next integer value to align
        /// \~russian Процент движения к следующему целому числу для выравнивания
        /// </summary>
        [HelperName("Sensitivity Pct", Constants.En)]
        [HelperName("Чувствительность", Constants.Ru)]
        [HandlerParameter(Name = "Sensitivity Pct", NotOptimized = false, IsVisibleInBlock = true,
            Default = "66", Min = "50", Max = "100", Step = "1")]
        [Description("Процент движения к следующему целому числу для выравнивания")]
        [HelperDescription("Delta should pass this percent of distance to next integer value to align", Constants.En)]
        public double SensitivityPct
        {
            get
            {
                return m_sensitivity * Constants.PctMult;
            }
            set
            {
                double t = Math.Max(50, value);
                t = Math.Min(100, t);
                m_sensitivity = t / Constants.PctMult;
            }
        }

        /// <summary>
        /// \~english Buy shift (price steps)
        /// \~russian Сдвиг покупок (шаги цены)
        /// </summary>
        [HelperName("Buy shift", Constants.En)]
        [HelperName("Сдвиг покупок", Constants.Ru)]
        [Description("Сдвиг покупок (шаги цены)")]
        [HelperDescription("Buy shift (price steps)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-" + Limit, Max = Limit, Step = "1.0")]
        public double BuyShift
        {
            get { return m_buyShift; }
            set { m_buyShift = value; }
        }

        /// <summary>
        /// \~english Sell shift (price steps)
        /// \~russian Сдвиг продаж (шаги цены)
        /// </summary>
        [HelperName("Sell shift", Constants.En)]
        [HelperName("Сдвиг продаж", Constants.Ru)]
        [Description("Сдвиг продаж (шаги цены)")]
        [HelperDescription("Sell shift (price steps)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-" + Limit, Max = Limit, Step = "1.0")]
        public double SellShift
        {
            get { return m_sellShift; }
            set { m_sellShift = value; }
        }

        /// <summary>
        /// \~english Price of hedge order to buy
        /// \~russian Цена хеджа при покупке
        /// </summary>
        [HelperName("Buy price", Constants.En)]
        [HelperName("Цена покупки", Constants.Ru)]
        [Description("Цена хеджа при покупке")]
        [HelperDescription("Price of hedge order to buy", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = false, IsCalculable = true,
            Name = "Buy price", Default = "0", Min = "0", Max = Limit, Step = "1.0")]
        public OptimProperty BuyPrice
        {
            get { return m_buyPrice; }
            set { m_buyPrice = value; }
        }

        /// <summary>
        /// \~english Price of hedge order to sell
        /// \~russian Цена хеджа при продаже
        /// </summary>
        [HelperName("Sell price", Constants.En)]
        [HelperName("Цена продажи", Constants.Ru)]
        [Description("Цена хеджа при продаже")]
        [HelperDescription("Price of hedge order to sell", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = false, IsCalculable = true,
            Name = "Sell price", Default = "0", Min = "0", Max = Limit, Step = "1.0")]
        public OptimProperty SellPrice
        {
            get { return m_sellPrice; }
            set { m_sellPrice = value; }
        }

        /// <summary>
        /// \~english Permission to work without options
        /// \~russian Разрешена работа, даже если в позиции нет опционов
        /// </summary>
        [HelperName("Work without options", Constants.En)]
        [HelperName("Работать без опционов", Constants.Ru)]
        [Description("Разрешена работа, даже если в позиции нет опционов")]
        [HelperDescription("Permission to work without options", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "False")]
        public bool WorkWithoutOptions
        {
            get { return m_workWithoutOptions; }
            set { m_workWithoutOptions = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Метод для передачи на вход серии опционов
        /// </summary>
        public double Execute(double price, double rawDelta, IOptionSeries optSer, int barNum)
        {
            // PROD-4568 - По дефолту блоку разрешено работать.
            // См. также обсуждение на форуме: http://forum.tslab.ru/ubb/ubbthreads.php?ubb=showflat&Number=80364#Post80364
            double res = Execute(price, rawDelta, optSer, true, barNum);
            return res;
        }

        /// <summary>
        /// Метод для передачи на вход серии опционов и также в явном виде на вход передаётся разрешение на выполнение дельта-хеджа
        /// </summary>
        public double Execute(double price, double rawDelta, IOptionSeries optSer, bool permissionToWork, int barNum)
        {
            if ((optSer == null) || (optSer.UnderlyingAsset == null))
                return Constants.NaN;

            int len = m_context.BarsCount;
            if (len <= 0)
                return Constants.NaN;

            if (len <= barNum)
            {
                string msg = String.Format("[{0}] (BarsCount <= barNum)! BarsCount:{1}; barNum:{2}",
                    GetType().Name, m_context.BarsCount, barNum);
                m_context.Log(msg, MessageType.Info, true);
                barNum = len - 1;
            }
            ISecurity under = optSer.UnderlyingAsset;
            DateTime now = under.Bars[barNum].Date;
            var strikes = optSer.GetStrikes();
            double res = CommonExecute(m_variableId + "_rawDeltas", now, true, true, false, barNum,
                new object[]
                {
                    price, rawDelta, under.Tick, optSer.ExpirationDate, under.Symbol,
                    under.FinInfo.LastUpdate, under.SecurityDescription, permissionToWork,
                    strikes, under.LotTick
                });
            return res;
        }

        /// <summary>
        /// Метод для передачи на вход отдельного торгового инструмента
        /// </summary>
        public double Execute(double price, double rawDelta, ISecurity sec, int barNum)
        {
            // PROD-4568 - По дефолту блоку разрешено работать.
            // См. также обсуждение на форуме: http://forum.tslab.ru/ubb/ubbthreads.php?ubb=showflat&Number=80364#Post80364
            double res = Execute(price, rawDelta, sec, true, barNum);
            return res;
        }

        /// <summary>
        /// Метод для передачи на вход отдельного торгового инструмента и также в явном виде на вход передаётся разрешение на выполнение дельта-хеджа
        /// </summary>
        public double Execute(double price, double rawDelta, ISecurity sec, bool permissionToWork, int barNum)
        {
            if (sec == null)
                return Constants.NaN;

            int len = m_context.BarsCount;
            if (len <= 0)
                return Constants.NaN;

            if (len <= barNum)
            {
                string msg = String.Format("[{0}] (BarsCount <= barNum)! BarsCount:{1}; barNum:{2}",
                    GetType().Name, m_context.BarsCount, barNum);
                m_context.Log(msg, MessageType.Info, true);
                barNum = len - 1;
            }
            ISecurity under = sec;
            DateTime now = under.Bars[barNum].Date;
            double res = CommonExecute(m_variableId + "_rawDeltas", now, true, true, false, barNum,
                new object[]
                {
                    price, rawDelta, under.Tick, sec.SecurityDescription.ExpirationDate, under.Symbol,
                    under.FinInfo.LastUpdate, under.SecurityDescription, permissionToWork, null, under.LotTick
                });
            return res;
        }

        protected override bool TryCalculate(Dictionary<DateTime, double> history, DateTime now, int barNum, object[] args, out double val)
        {
            int col = 0;
            double price = (double)args[col++];
            double rawDelta = (double)args[col++];
            //IOptionSeries optSer = (IOptionSeries)args[col++];
            double priceStep = (double)args[col++];
            DateTime expirationDate = (DateTime)args[col++];
            string underlyingSymbol = (string)args[col++];
            DateTime lastUpdate = (DateTime)args[col++];
            var baseSecDesc = (DataSource.IDataSourceSecurity)args[col++];
            bool permissionToWork = (bool)args[col++];
            // Здесь работаем не с парами, а со списком, чтобы потом было легче обобщить на весь опцион целиком.
            var strikes = (IEnumerable<IOptionStrike>)args[col++];
            // Шаг лотности БАЗОВОГО АКТИВА (нужен для Дерибит в первую очередь)
            double lotTick = (double)args[col++];

            val = rawDelta;

            #region Validate args
            double futPx = price;
            // Входные параметры не проинициализированы
            if (Double.IsNaN(rawDelta) || (!DoubleUtil.IsPositive(futPx)))
                return true;

            // Входные параметры не проинициализированы
            //if ((optSer == null) || (optSer.UnderlyingAsset == null) || (optSer.UnderlyingAsset.SecurityDescription == null))
            //    return true;
            if (!DoubleUtil.IsPositive(priceStep))
                return true;

            if (!DoubleUtil.IsPositive(lotTick))
                return true;

            m_buyPrice.Value = futPx + m_buyShift * priceStep;
            m_sellPrice.Value = futPx + m_sellShift * priceStep;

            // PROD-3687: Если выйти из метода слишком рано, то не будут заполнены контролы на UI
            if (!m_hedgeDelta)
                return true;

            if (!permissionToWork)
            {
                // PROD-4568 - Если параметр блока говорит "можно", а на вход блока из скрипта подан запрет,
                // то нужно записать в лог значения этих параметров, чтобы не было вопросов потом.
                // См. также обсуждение на форуме: http://forum.tslab.ru/ubb/ubbthreads.php?ubb=showflat&Number=80364#Post80364

                if (Context.Runtime.IsAgentMode)
                {
                    string tName = (Context.Runtime.TradeName ?? "").Replace(Constants.HtmlDot, ".");
                    string expiry = expirationDate.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture);
                    string msg = String.Format("[{0}.{1}] Execution is blocked. Symbol:{2}; Expiry:{3};   HedgeDelta:{4}; Permission:{5}",
                        MsgId, tName, underlyingSymbol, expiry, m_hedgeDelta, permissionToWork);
                    m_context.Log(msg, MessageType.Warning, false);
                }

                return true;
            }

            DateTime lastExec = LastExecution;
            //DateTime lastUpdate = optSer.UnderlyingAsset.FinInfo.LastUpdate;
            // В некоторых случаях слишком частое исполнение нам не подходит
            if ((lastUpdate - lastExec).TotalSeconds < m_minPeriod)
            {
                // Но надо проверять изменения в составе портфеля, чтобы вовремя ровнять дельту
                // НО КАК???
                return true;
            }
            #endregion Validate args

            // Если дельта уже лежит в заданных границах - просто выходим
            double dnEdge = m_targetDelta.Value - Math.Abs(m_doDelta.Value) + (1.0 - m_sensitivity);
            double upEdge = m_targetDelta.Value + Math.Abs(m_upDelta.Value) - (1.0 - m_sensitivity);
            // Пересчитываем пороги срабатывания в истинные лоты
            dnEdge *= lotTick;
            upEdge *= lotTick;
            if ((dnEdge < rawDelta) && (rawDelta < upEdge))
            {
                //string msg = String.Format(CultureInfo.InvariantCulture, "[{0}] Delta is too low to hedge. Delta: {1}; TargetDelta: {2}; Sensitivity: {3}",
                //  MsgId, rawDelta, m_targetDelta.Value, m_sensitivity);
                //context.Log(msg, logColor, true);

                return true;
            }

            //var baseSecDesc = optSer.UnderlyingAsset.SecurityDescription;
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            ISecurity sec = (from s in m_context.Runtime.Securities
                             where (s.SecurityDescription != null) && baseSecDesc.Equals(s.SecurityDescription)
                             select s).SingleOrDefault();
            if (sec == null)
            {
                string msg = String.Format("[{0}] There is no security. Symbol: {1}", MsgId, underlyingSymbol);
                m_context.Log(msg, MessageType.Warning, true);
                return true;
            }

            // PROD-6000 - Если в позиции вообще нет опционов, значит произошла какая-то ошибка
            if (!m_workWithoutOptions)
            {
                bool hasAnyOption = HasAnyOptionPosition(posMan, strikes);
                if (!hasAnyOption)
                {
                    // В режиме агента обязательно логгируем, чтобы потом вопросов не было
                    if (Context.Runtime.IsAgentMode)
                    {
                        string tName = (Context.Runtime.TradeName ?? "").Replace(Constants.HtmlDot, ".");
                        string expiry = expirationDate.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture);
                        string msg = String.Format("[{0}.{1}] Execution is blocked. Symbol:{2}; Expiry:{3};   WorkWithoutOptions:{4}; HasAnyOption:{5}",
                            MsgId, tName, underlyingSymbol, expiry, m_workWithoutOptions, hasAnyOption);
                        m_context.Log(msg, MessageType.Warning, false);
                    }

                    return true;
                }
            }

            // Здесь rawDelta и dnEdge ОБА в истинных лотах
            if (rawDelta <= dnEdge)
            {
                double diffLots = m_targetDelta.Value * lotTick - rawDelta;
                // Переводим в штуки LotTick-ов
                double diffLotTicks = diffLots / lotTick;

                int roundedLo = Math.Sign(diffLotTicks) * (int)Math.Floor(Math.Abs(diffLotTicks));
                int roundedHi = Math.Sign(diffLotTicks) * (1 + (int)Math.Floor(Math.Abs(diffLotTicks)));
                int rounded;
                //if (Math.Abs(rawDelta + roundedLo - m_targetDelta.Value) <= Math.Abs(rawDelta + roundedHi - m_targetDelta.Value))
                if (Math.Abs(roundedLo - diffLotTicks) <= Math.Abs(roundedHi - diffLotTicks))
                    rounded = roundedLo;
                else
                    rounded = roundedHi;

                if (rounded > 0)
                {
                    // [2015-07-15] Сдвиг заявки хеджа относительно рынка на заданную величину
                    double px = futPx + m_buyShift * sec.Tick;

                    string signalName;
                    if (DoubleUtil.IsOne(lotTick))
                    {
                        signalName = String.Format(CultureInfo.InvariantCulture, "F:{0}; Delta:{1}; TargetDelta:{2}",
                            px, rawDelta, m_targetDelta.Value);
                    }
                    else
                    {
                        signalName = String.Format(CultureInfo.InvariantCulture, "F:{0}; Delta:{1}; TargetDelta:{2}; LotTick:{3}",
                            px, rawDelta, m_targetDelta.Value * lotTick, lotTick);
                    }
                    m_context.Log(signalName, MessageType.Warning, false);
                    posMan.BuyAtPrice(m_context, sec, Math.Abs(rounded * lotTick), px, "Delta BUY", signalName);

                    m_context.StoreObject(VariableId + "_" + LastExecutionKey, now);
                }
            }
            // Здесь upEdge и rawDelta ОБА в истинных лотах
            else if (upEdge <= rawDelta)
            {
                double diffLots = m_targetDelta.Value * lotTick - rawDelta; // в этой ветке diff обычно отрицательный...
                // Переводим в штуки LotTick-ов
                double diffLotTicks = diffLots / lotTick;

                int roundedLo = Math.Sign(diffLotTicks) * (int)Math.Floor(Math.Abs(diffLotTicks));
                int roundedHi = Math.Sign(diffLotTicks) * (1 + (int)Math.Floor(Math.Abs(diffLotTicks)));
                int rounded;
                if (Math.Abs(roundedLo - diffLotTicks) <= Math.Abs(roundedHi - diffLotTicks))
                    rounded = roundedLo;
                else
                    rounded = roundedHi;

                if (rounded < 0)
                {
                    // [2015-07-15] Сдвиг заявки хеджа относительно рынка на заданную величину
                    double px = futPx + m_sellShift * sec.Tick;

                    string signalName;
                    if (DoubleUtil.IsOne(lotTick))
                    {
                        signalName = String.Format(CultureInfo.InvariantCulture, "F:{0}; Delta:{1}; TargetDelta:{2}",
                            px, rawDelta, m_targetDelta.Value);
                    }
                    else
                    {
                        signalName = String.Format(CultureInfo.InvariantCulture, "F:{0}; Delta:{1}; TargetDelta:{2}; LotTick:{3}",
                            px, rawDelta, m_targetDelta.Value * lotTick, lotTick);
                    }
                    m_context.Log(signalName, MessageType.Warning, false);
                    posMan.SellAtPrice(m_context, sec, Math.Abs(rounded * lotTick), px, "Delta SELL", signalName);

                    m_context.StoreObject(VariableId + "_" + LastExecutionKey, now);
                }
            }

            return true;
        }

        /// <summary>
        /// Для каждой пары опционов создаётся Tuple.
        /// В первом элементе живут позиции путов, во втором -- колов.
        /// Индексы синхронизированы с индексами массива pairs.
        /// </summary>
        internal static bool HasAnyOptionPosition(PositionsManager posMan, IEnumerable<IOptionStrike> strikes)
        {
            if (strikes == null)
                return false;

            foreach (var ops in strikes)
            {
                ISecurity sec = ops.Security;
                var optPositions = posMan.GetClosedOrActiveForBar(sec);
                if (optPositions.Count > 0)
                {
                    // Нашли позицию именно в каком-то опционе (вернуть его наверх для логгирования?)
                    return true;
                }
            }

            // Если до сих пор ничего не нашли, значит опционов в позиции вообще нет
            return false;
        }
    }
}
