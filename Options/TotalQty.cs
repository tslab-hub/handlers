using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.DataSource;
using TSLab.Script.Optimization;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Pure open position in a given security
    /// \~russian Суммарное количество лотов в данном инструменте
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Total Open Qty", Language = Constants.En)]
    [HelperName("Суммарная открытая позиция", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = "Security")]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Суммарное количество лотов в данном инструменте")]
    [HelperDescription("Pure open position in a given security", Constants.En)]
    public class TotalQty : BaseContextHandler, IValuesHandlerWithNumber
    {
        private OptimProperty m_openQty = new OptimProperty(0, false, double.MinValue, double.MaxValue, 1.0, 0);

        #region Parameters
        /// <summary>
        /// \~english Total open quantity
        /// \~russian Суммарный объем в инструменте
        /// </summary>
        [HelperName("Open Qty", Constants.En)]
        [HelperName("Открытый объём", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Суммарный объём в инструменте")]
        [HelperDescription("Total open quantity", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0", IsCalculable = true)]
        public OptimProperty OpenQty
        {
            get { return m_openQty; }
            set { m_openQty = value; }
        }
        #endregion Parameters

        public double Execute(ISecurity sec, int barNum)
        {
            List<double> positionQtys = PreparePositionQtys();

            // Гарантируем совпадение по числу элементов
            int barsCount = m_context.BarsCount;
            if (barsCount <= barNum)
            {
                string msg = String.Format("[{0}] (BarsCount <= barNum)! BarsCount:{1}; barNum:{2}",
                    GetType().Name, m_context.BarsCount, barNum);
                m_context.Log(msg, MessageType.Warning, true);
            }
            for (int j = positionQtys.Count; j < Math.Max(barsCount, barNum + 1); j++)
                positionQtys.Add(Constants.NaN);

            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return positionQtys[barNum];

            double res;
            {
                PositionsManager posMan = PositionsManager.GetManager(m_context);
                if (posMan != null)
                    res = posMan.GetTotalQty(sec, barNum);
                else
                    res = GetTotalQty(sec, barNum);
            }

            if (m_context.IsFixedBarsCount)
            {
                // В этом режиме сдвигаю все значения влево
                // Если мы уже набрали в буфер необходимое число баров
                if (barsCount <= positionQtys.Count)
                {
                    for (int j = 0; j < positionQtys.Count - 1; j++)
                        positionQtys[j] = positionQtys[j + 1];
                }
            }

            positionQtys[barNum] = res;

            m_openQty.Value = res;

            return res;
        }

        /// <summary>
        /// Полный суммарный открытый объём в данном инструменте в позициях указанного направления
        /// (запасной вариант подсчета. По идее, должен быть синхронизирован с таким же методом в классе PositionsManager)
        /// </summary>
        /// <param name="sec">инструмент</param>
        /// <param name="barNum">индекс бара</param>
        /// <returns>суммарный объём (в лотах)</returns>
        public double GetTotalQty(ISecurity sec, int barNum)
        {
            double res = 0;
            var positions = PositionsManager.GetActiveForBar(sec, barNum, TotalProfitAlgo.AllPositions, null);
            for (int j = 0; j < positions.Count; j++)
            {
                IPosition pos = positions[j];
                int sign = pos.IsLong ? 1 : -1;
                double qty = Math.Abs(pos.Shares);
                res += sign * qty;
            }
            return res;
        }

        /// <summary>
        /// Извлечь из локального кеша историю значений данного индикатора.
        /// Если ее нет, создать и сразу поместить туда.
        /// </summary>
        /// <returns>история значений данного индикатора</returns>
        private List<double> PreparePositionQtys()
        {
            // [2019-01-30] Перевожу на использование NotClearableContainer (PROD-6683)
            List<double> positionQtys;
            string key = VariableId + "_positionQtys";
            var container = m_context.LoadObject(key) as NotClearableContainer<List<double>>;
            if (container != null)
                positionQtys = container.Content;
            else
                positionQtys = m_context.LoadObject(key) as List<double>; // Старая ветка на всякий случай

            if (positionQtys == null)
            {
                positionQtys = new List<double>();
                container = new NotClearableContainer<List<double>>(positionQtys);
                m_context.StoreObject(key, container);
            }

            return positionQtys;
        }
    }
}
