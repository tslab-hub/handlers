using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.DataSource;
using TSLab.Script.CanvasPane;
using TSLab.Script.Optimization;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Numerical estimate of profit at-the-money (only one point is processed using position profile)
    /// \~russian Численный расчет профита позиции в текущей точке БА (вычисляется одна точка по профилю позиции)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Profit ATM (IntSer)", Language = Constants.En)]
    [HelperName("Профит на деньгах (IntSer)", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE | TemplateTypes.SECURITY, Name = "Position Profile or Security")]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Численный расчет профита позиции в текущей точке БА (вычисляется одна точка по профилю позиции)")]
    [HelperDescription("Numerical estimate of profit at-the-money (only one point is processed using position profile)", Constants.En)]
    public class TotalProfit : BaseContextHandler, IValuesHandlerWithNumber
    {
        private const string MsgId = "PROFIT";

        private double m_scaleMultiplier = 1;
        private TotalProfitAlgo m_algo = TotalProfitAlgo.AllPositions;
        private OptimProperty m_profit = new OptimProperty(0, false, double.MinValue, double.MaxValue, 1.0, 3);

        #region Parameters
        /// <summary>
        /// \~english Profit calculation algorytm
        /// \~russian Алгоритм расчета
        /// </summary>
        [HelperName("Profit algo", Constants.En)]
        [HelperName("Алгоритм расчета", Constants.Ru)]
        [Description("Алгоритм расчета")]
        [HelperDescription("Profit calculation algorytm", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "AllPositions")]
        public TotalProfitAlgo ProfitAlgo
        {
            get { return m_algo; }
            set { m_algo = value; }
        }

        /// <summary>
        /// \~english Current position profit
        /// \~russian Текущий профит всей позиции
        /// </summary>
        [ReadOnly(true)]
        [HelperName("Profit", Constants.En)]
        [HelperName("Профит", Constants.Ru)]
        [Description("Текущий профит всей позиции")]
        [HelperDescription("Current position profit", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0", IsCalculable = true)]
        public OptimProperty Profit
        {
            get { return m_profit; }
            set { m_profit = value; }
        }

        /// <summary>
        /// \~english Scale multiplier to convert profit from price units to money (i.e. dollars or euros)
        /// \~russian Масштабный множитель для пересчета единиц цен в деньги (например, в рубли или доллары)
        /// </summary>
        [HelperName("Scale multiplier", Constants.En)]
        [HelperName("Масштабный множитель", Constants.Ru)]
        [Description("Масштабный множитель для пересчета единиц цен в деньги (например, в рубли или доллары)")]
        [HelperDescription("Scale multiplier to convert profit from price units to money (i.e. dollars or euros)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "1")]
        public double ScaleMultiplier
        {
            get { return (m_scaleMultiplier > 0) ? m_scaleMultiplier : 1; }
            set { m_scaleMultiplier = (value > 0) ? value : 1; }
        }

        /// <summary>
        /// \~english Print profit in main log
        /// \~russian Выводить профит в главный лог приложения
        /// </summary>
        [HelperName("Print in Log", Constants.En)]
        [HelperName("Выводить в лог", Constants.Ru)]
        [Description("Выводить профит в главный лог приложения")]
        [HelperDescription("Print profit in main log", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "False")]
        public bool PrintProfitInLog { get; set; }
        #endregion Parameters

        public double Execute(InteractiveSeries positionProfile, int barNum)
        {
            List<double> positionProfits = PreparePositionProfits();

            int len = m_context.BarsCount;
            for (int j = positionProfits.Count; j < len; j++)
                positionProfits.Add(Constants.NaN);

            {
                int barsCount = m_context.BarsCount;
                if (!m_context.IsLastBarUsed)
                    barsCount--;
                if (barNum < barsCount - 1)
                    return positionProfits[barNum];
            }

            if (positionProfile == null)
                return Constants.NaN;

            SmileInfo positionInfo = positionProfile.GetTag<SmileInfo>();
            if ((positionInfo == null) || (positionInfo.ContinuousFunction == null))
            {
                positionProfits[barNum] = Double.NaN; // заполняю индекс barNumber
                return Constants.NaN;
            }

            double f = positionInfo.F;
            if (Double.IsNaN(f) || (f < Double.Epsilon))
            {
                string msg = String.Format("[{0}] F must be positive value. F:{1}", GetType().Name, f);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.NaN;
            }

            double rawProfit;
            if (!positionInfo.ContinuousFunction.TryGetValue(f, out rawProfit))
            {
                rawProfit = Constants.NaN;
            }

            if (m_context.IsFixedBarsCount)
            {
                // В этом режиме сдвигаю все значения влево
                // Если мы уже набрали в буфер необходимое число баров
                if (len <= positionProfits.Count)
                {
                    for (int j = 0; j < positionProfits.Count - 1; j++)
                        positionProfits[j] = positionProfits[j + 1];
                }
            }

            // Пересчитываю прибыль в привычные денежные единицы
            rawProfit *= ScaleMultiplier;
            positionProfits[barNum] = rawProfit; // заполняю индекс barNumber

            m_profit.Value = rawProfit;
            if (PrintProfitInLog)
                m_context.Log(MsgId + ": " + m_profit.Value, MessageType.Info, PrintProfitInLog);

            return rawProfit;
        }

        public double Execute(ISecurity sec, int barNum)
        {
            List<double> positionProfits = PreparePositionProfits();

            int len = m_context.BarsCount;
            for (int j = positionProfits.Count; j < len; j++)
                positionProfits.Add(Constants.NaN);

            {
                int barsCount = m_context.BarsCount;
                if (!m_context.IsLastBarUsed)
                    barsCount--;
                if (barNum < barsCount - 1)
                    return positionProfits[barNum];
            }

            if (sec == null)
                return Constants.NaN;

            double cache, pnl;
            IDataBar bar = sec.Bars[barNum];
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            double rawProfit = posMan.GetTotalProfit(sec, barNum, m_algo, bar.Close, out cache, out pnl);

            if (m_context.IsFixedBarsCount)
            {
                // В этом режиме сдвигаю все значения влево
                // Если мы уже набрали в буфер необходимое число баров
                if (len <= positionProfits.Count)
                {
                    for (int j = 0; j < positionProfits.Count - 1; j++)
                        positionProfits[j] = positionProfits[j + 1];
                }
            }

            // Пересчитываю прибыль в привычные денежные единицы
            rawProfit *= ScaleMultiplier;
            positionProfits[barNum] = rawProfit; // заполняю индекс barNumber

            m_profit.Value = rawProfit;
            if (PrintProfitInLog)
                m_context.Log(MsgId + ": " + m_profit.Value, MessageType.Info, PrintProfitInLog);

            return rawProfit;
        }

        /// <summary>
        /// Извлечь из локального кеша историю значений данного индикатора.
        /// Если ее нет, создать и сразу поместить туда.
        /// </summary>
        /// <returns>история значений данного индикатора</returns>
        private List<double> PreparePositionProfits()
        {
            // [2019-01-30] Перевожу на использование NotClearableContainer (PROD-6683)
            List<double> positionProfits;
            string key = VariableId + "_positionProfits";
            var container = m_context.LoadObject(key) as NotClearableContainer<List<double>>;
            if (container != null)
                positionProfits = container.Content;
            else
                positionProfits = m_context.LoadObject(key) as List<double>; // Старая ветка на всякий случай

            if (positionProfits == null)
            {
                positionProfits = new List<double>();
                container = new NotClearableContainer<List<double>>(positionProfits);
                m_context.StoreObject(key, container);
            }

            return positionProfits;
        }
    }
}
