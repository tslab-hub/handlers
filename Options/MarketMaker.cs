using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.MessageType;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Market-maker. It can repeat quotes from one market to another
    /// \~russian Маркет-мейкер. Умеет переносить котировки идентичных активов между площадками
    /// </summary>
    //[HandlerCategory(Constants.GeneralByTick)]
    //[HandlerName("Market Maker", Language = Constants.En)]
    //[HandlerName("Маркетмейкер", Language = Constants.Ru)]
    //[InputsCount(2)]
    //[Input(0, TemplateTypes.OPTION_SERIES, Name = "Source Option Series")]
    //[Input(1, TemplateTypes.OPTION_SERIES, Name = "Trading Option Series")]
    //[OutputType(TemplateTypes.DOUBLE)]
    //[Description("Маркет-мейкер. Умеет переносить котировки идентичных активов между площадками")]
    //[HelperDescription("Market-maker. It can repeat quotes from one market to another", Constants.En)]
    public class MarketMaker : BaseContextHandler, IValuesHandlerWithNumber, IDoubleReturns, IDisposable
    {
        private int m_qty = 1;
        private StrikeType m_optionType = StrikeType.Call;
        private OptionPxMode m_optionPxMode = OptionPxMode.Ask;

        private double m_outlet = 0.02;
        private double m_widthPx = 5000;
        private double m_vertiShiftPx = 0;

        private InteractiveSeries m_clickableSeries = null;

        #region Parameters
        /// <summary>
        /// \~english Option type (when Any, the handler will choose the best quote)
        /// \~russian Тип опционов (Any предполагает автоматический выбор лучшей котировки)
        /// </summary>
        [HelperParameterName("Option Type", Constants.En)]
        [HelperParameterName("Вид опционов", Constants.Ru)]
        [Description("Тип опционов (Any предполагает автоматический выбор лучшей котировки)")]
        [HelperDescription("Option type (when Any, the handler will choose the best quote)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "Call")]
        public StrikeType OptionType
        {
            get { return m_optionType; }
            set { m_optionType = value; }
        }

        /// <summary>
        /// \~english Quote type (ask or bid)
        /// \~russian Тип котировки (аск или бид)
        /// </summary>
        [HelperParameterName("Quote Type", Constants.En)]
        [HelperParameterName("Тип котировки", Constants.Ru)]
        [Description("Тип котировки (аск или бид)")]
        [HelperDescription("Quote type (ask or bid)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "Ask")]
        public OptionPxMode OptPxMode
        {
            get { return m_optionPxMode; }
            set { m_optionPxMode = value; }
        }

        /// <summary>
        /// \~english Quote size
        /// \~russian Объём для котирования
        /// </summary>
        [HelperParameterName("Size", Constants.En)]
        [HelperParameterName("Кол-во", Constants.Ru)]
        [Description("Объём для котирования")]
        [HelperDescription("Quote size", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "1", Min = "1", Max = "1000000", Step = "1")]
        public int Qty
        {
            get { return m_qty; }
            set { m_qty = value; }
        }

        [Description("Ширина нейтральной полосы (цена)")]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "5000")]
        public double WidthPx
        {
            get { return m_widthPx; }
            set { m_widthPx = value; }
        }
        #endregion

        /// <summary>
        /// Метод под флаг TemplateTypes.INTERACTIVESPLINE
        /// </summary>
        public double Execute(IOptionSeries src, IOptionSeries dest, int barNum)
        {
            if ((src == null) || (dest == null))
                return Constants.NaN;

            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return Constants.NaN;

            if (src.UnderlyingAsset.FinInfo.LastPrice == null)
                return Constants.NaN;

            double f = src.UnderlyingAsset.FinInfo.LastPrice.Value;
            IOptionStrikePair[] srcPairs = src.GetStrikePairs().ToArray();
            IOptionStrikePair[] destPairs = dest.GetStrikePairs().ToArray();

            double counter = 0;
            for (int j = 0; j < srcPairs.Length; j++)
            {
                IOptionStrikePair srcPair = srcPairs[j];
                if (srcPair.Strike < f - m_widthPx)
                    continue;

                if (srcPair.Strike < f)
                {
                }
            }

            return counter;
        }

        private void InteractiveSplineOnClickEvent(object sender, InteractiveActionEventArgs eventArgs)
        {
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (posMan.BlockTrading)
            {
                //string msg = String.Format("[{0}] Trading is blocked. Please, change 'Block Trading' parameter.", m_optionPxMode);
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked", m_optionPxMode);
                m_context.Log(msg, MessageType.Info, true);
                return;
            }

            SmileNodeInfo nodeInfo = eventArgs.Point.Tag as SmileNodeInfo;
            if (nodeInfo == null)
            {
                //string msg = String.Format("[{0}] There is no nodeInfo. Quote type: {1}; Strike: {2}", m_optionPxMode);
                string msg = RM.GetStringFormat(CultureInfo.InvariantCulture, "OptHandlerMsg.ThereIsNoNodeInfo",
                    m_context.Runtime.TradeName, m_optionPxMode, eventArgs.Point.ValueX);
                m_context.Log(msg, MessageType.Error, true);
                return;
            }

            nodeInfo.Qty = m_qty;
            nodeInfo.ClickTime = DateTime.Now;

            // Передаю событие в PositionsManager дополнив его инфой о количестве лотов
            posMan.InteractiveSplineOnClickEvent(m_context, sender, eventArgs);
        }

        public void Dispose()
        {
            if (m_clickableSeries != null)
            {
                m_clickableSeries.ClickEvent -= InteractiveSplineOnClickEvent;

                m_clickableSeries = null;
            }
        }
    }
}
