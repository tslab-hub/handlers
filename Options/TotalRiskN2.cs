using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Optimization;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Total risk of position as number of semistraddles
    /// \~russian Суммарный риск позиции 'сумма полустреддлов на всех страйках' (функция риска N2 в терминах Твардовского)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Total Risk N2", Language = Constants.En)]
    [HelperName("Суммарный риск N2", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION, Name = "Position Manager")]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Суммарный риск позиции 'сумма полустреддлов на всех страйках' (функция риска N2 в терминах Твардовского)")]
    [HelperDescription("Total risk of position as number of semistraddles", Constants.En)]
    public class TotalRiskN2 : BaseContextWithNumber<double>
    {
        private bool m_repeatLastValue;
        private FixedValueMode m_valueMode = FixedValueMode.AsIs;
        private OptimProperty m_displayRisk = new OptimProperty(0, false, double.MinValue, double.MaxValue, 1.0, 3);

        #region Parameters
        /// <summary>
        /// \~english Handler should repeat last known value to avoid further logic errors
        /// \~russian При true будет находить и использовать последнее известное значение
        /// </summary>
        [HelperName("Repeat Last Value", Constants.En)]
        [HelperName("Повтор значения", Constants.Ru)]
        [Description("При true будет находить и использовать последнее известное значение")]
        [HelperDescription("Handler should repeat last known value to avoid further logic errors", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool RepeatLastValue
        {
            get
            {
                return m_repeatLastValue;
            }
            set
            {
                m_repeatLastValue = value;
            }
        }

        /// <summary>
        /// \~english Display units (hundreds, thousands, as is)
        /// \~russian Единицы отображения (сотни, тысячи, как есть)
        /// </summary>
        [HelperName("Display Units", Constants.En)]
        [HelperName("Единицы отображения", Constants.Ru)]
        [Description("Единицы отображения (сотни, тысячи, как есть)")]
        [HelperDescription("Display units (hundreds, thousands, as is)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Name = "Display Units", Default = "AsIs")]
        public FixedValueMode DisplayUnits
        {
            get
            {
                return m_valueMode;
            }
            set
            {
                m_valueMode = value;
            }
        }

        /// <summary>
        /// \~english Risk (just to display at UI)
        /// \~russian Риск (только для отображения на UI)
        /// </summary>
        [HelperName("Risk", Constants.En)]
        [HelperName("Риск", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Риск (только для отображения на UI)")]
        [HelperDescription("Risk (just to display at UI)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, IsCalculable = true,
            Default = "0", Min = "-1000000", Max = "1000000", Step = "1", NumberDecimalDigits = 3)]
        public OptimProperty DisplayRisk
        {
            get
            {
                return m_displayRisk;
            }
            set
            {
                m_displayRisk = value;
            }
        }
        #endregion Parameters

        protected override bool IsValid(double val)
        {
            return !Double.IsNaN(val);
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION
        /// </summary>
        public double Execute(IOption opt, int barNum)
        {
            if ((opt == null) || (opt.UnderlyingAsset == null))
                return Double.NaN; // В данном случае намеренно возвращаю Double.NaN

            int len = m_context.BarsCount;
            if (len <= 0)
                return Double.NaN; // В данном случае намеренно возвращаю Double.NaN

            if (len <= barNum)
            {
                string msg = String.Format("[{0}:{1}] (BarsCount <= barNum)! BarsCount:{2}; barNum:{3}",
                    m_context.Runtime.TradeName, GetType().Name, m_context.BarsCount, barNum);
                m_context.Log(msg, MessageType.Warning, true);
                barNum = len - 1;
            }
            DateTime now = opt.UnderlyingAsset.Bars[barNum].Date;
            double risk = CommonExecute(m_variableId + "_RiskN2", now, true, true, false, barNum, new object[] { opt });

            //// [2015-07-15] Отключаю вывод отладочных сообщений в лог агента.
            //if (barNum >= 0.9 * len)
            //{
            //    string msg = String.Format("[{0}:{1}] barNum:{2}; risk:{3}; now:{4}",
            //        m_context.Runtime.TradeName, GetType().Name, barNum, risk, now.ToString("dd-MM-yyyy HH:mm:ss.fff"));
            //    m_context.Log(msg, MessageType.Info, false);
            //}

            // Просто заполнение свойства для отображения на UI
            int barsCount = ContextBarsCount;
            if (barNum >= barsCount - 1)
            {
                double displayValue = FixedValue.ConvertToDisplayUnits(m_valueMode, risk);
                m_displayRisk.Value = displayValue;
            }

            return risk;
        }

        protected override bool TryCalculate(Dictionary<DateTime, double> history, DateTime now, int barNum, object[] args, out double val)
        {
            IOption opt = (IOption)args[0];

            double risk = 0;
            DateTime today = now.Date;
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            foreach (IOptionSeries optSer in opt.GetSeries())
            {
                if (optSer.ExpirationDate.Date < today)
                    continue;

                IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
                for (int j = 0; j < pairs.Length; j++)
                {
                    IOptionStrikePair pair = pairs[j];
                    double putQty, callQty;
                    SingleSeriesPositionGrid.GetPairQty(posMan, pair, out putQty, out callQty);

                    risk += Math.Abs(putQty + callQty);
                }
            }

            val = risk;

            return true;
        }
    }
}
