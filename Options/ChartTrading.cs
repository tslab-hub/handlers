using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Trading with mouse on CanvasPane
    /// \~russian Торговля кликами мыши по чарту CanvasPane
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Chart Trading", Language = Constants.En)]
    [HelperName("Торговля на графике", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = "Quotes")]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Торговля кликами мыши по чарту CanvasPane")]
    [HelperDescription("Trading with mouse on CanvasPane", Constants.En)]
    public class ChartTrading : BaseContextHandler, IValuesHandlerWithNumber, IDisposable
    {
        private int m_qty = 10;
        private StrikeType m_optionType = StrikeType.Call;
        private OptionPxMode m_optionPxMode = OptionPxMode.Ask;

        private InteractiveSeries m_clickableSeries = null;

        #region Parameters
        /// <summary>
        /// \~english Option type (when Any, the handler will choose the best quote)
        /// \~russian Тип опционов (Any предполагает автоматический выбор лучшей котировки)
        /// </summary>
        [HelperName("Option Type", Constants.En)]
        [HelperName("Вид опционов", Constants.Ru)]
        [Description("Тип опционов (Any предполагает автоматический выбор лучшей котировки)")]
        [HelperDescription("Option type (when Any, the handler will choose the best quote)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Call")]
        public StrikeType OptionType
        {
            get { return m_optionType; }
            set { m_optionType = value; }
        }

        /// <summary>
        /// \~english Quote type (ask or bid)
        /// \~russian Тип котировки (аск или бид)
        /// </summary>
        [HelperName("Quote Type", Constants.En)]
        [HelperName("Тип котировки", Constants.Ru)]
        [Description("Тип котировки (аск или бид)")]
        [HelperDescription("Quote type (ask or bid)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "Ask")]
        public OptionPxMode OptPxMode
        {
            get { return m_optionPxMode; }
            set { m_optionPxMode = value; }
        }

        /// <summary>
        /// \~english Trade quantity. Negative value reverts the signal.
        /// \~russian Торговый объём. Отрицательное значение приведет к перевороту сигнала.
        /// </summary>
        [HelperName("Size", Constants.En)]
        [HelperName("Кол-во", Constants.Ru)]
        [Description("Торговый объём. Отрицательное значение приведет к перевороту сигнала.")]
        [HelperDescription("Trade quantity. Negative value reverts the signal.", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "1", Min = "-1000000", Max = "1000000", Step = "1")]
        public int Qty
        {
            get { return m_qty; }
            set { m_qty = value; }
        }
        #endregion

        /// <summary>
        /// Метод под флаг TemplateTypes.INTERACTIVESPLINE
        /// </summary>
        public InteractiveSeries Execute(InteractiveSeries quotes, int barNum)
        {
            if (quotes == null)
                return Constants.EmptySeries;

            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return quotes;

            // Это надо ОБЯЗАТЕЛЬНО делать ПОСЛЕ восстановления виртуальных позиций
            #region Process clicks
            {
                var onClickEvents = m_context.LoadObject(VariableId + "OnClickEvent") as List<InteractiveActionEventArgs>;
                if (onClickEvents == null)
                {
                    onClickEvents = new List<InteractiveActionEventArgs>();
                    m_context.StoreObject(VariableId + "OnClickEvent", onClickEvents);
                }

                if (onClickEvents.Count > 0)
                {
                    RouteOnClickEvents(onClickEvents);
                }
            }
            #endregion Process clicks

            InteractiveSeries res = quotes;
            res.ClickEvent -= InteractiveSplineOnClickEvent;
            res.ClickEvent += InteractiveSplineOnClickEvent;
            m_clickableSeries = res;

            return res;
        }

        private void InteractiveSplineOnClickEvent(object sender, InteractiveActionEventArgs eventArgs)
        {
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (posMan.BlockTrading)
            {
                //string msg = String.Format("[{0}] Trading is blocked. Please, change 'Block Trading' parameter.", m_optionPxMode);
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked", m_optionPxMode);
                m_context.Log(msg, MessageType.Warning, true);
                return;
            }

            List<InteractiveActionEventArgs> onClickEvents = m_context.LoadObject(VariableId + "OnClickEvent") as List<InteractiveActionEventArgs>;
            if (onClickEvents == null)
            {
                onClickEvents = new List<InteractiveActionEventArgs>();
                m_context.StoreObject(VariableId + "OnClickEvent", onClickEvents);
            }

            onClickEvents.Add(eventArgs);

            m_context.Recalc();
        }

        private void RouteOnClickEvents(List<InteractiveActionEventArgs> eventArgs)
        {
            if (eventArgs == null)
                return;

            int argLen = eventArgs.Count;
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (posMan.BlockTrading && (argLen > 0))
            {
                eventArgs.Clear();
                string msg = String.Format("[{0}] ERROR! Trading is blocked. Should NOT be here. All events were removed.", m_optionPxMode);
                m_context.Log(msg, MessageType.Warning, true);
                return;
            }

            bool recalc = false;
            for (int j = argLen - 1; j >= 0; j--)
            {
                InteractiveActionEventArgs eventArg = eventArgs[j];
                eventArgs.RemoveAt(j);

                try
                {
                    SmileNodeInfo nodeInfo = eventArg.Point.Tag as SmileNodeInfo;
                    if (nodeInfo == null)
                    {
                        //string msg = String.Format("[{0}] There is no nodeInfo. Quote type: {1}; Strike: {2}", m_optionPxMode);
                        string msg = RM.GetStringFormat(CultureInfo.InvariantCulture, "OptHandlerMsg.ThereIsNoNodeInfo",
                            m_context.Runtime.TradeName, m_optionPxMode, eventArg.Point.ValueX);
                        m_context.Log(msg, MessageType.Error, true);
                        continue;
                    }

                    if ((!posMan.UseVirtualPositions) && nodeInfo.Expired)
                    {
                        string msg = String.Format("[{0}] Security {1} is already expired. Expiry: {2}",
                            m_optionPxMode, nodeInfo.Symbol, nodeInfo.Security.SecurityDescription.ExpirationDate);
                        m_context.Log(msg, MessageType.Error, true);
                        continue;
                    }

                    ISecurity sec = (from s in m_context.Runtime.Securities
                                     where (s.Symbol.Equals(nodeInfo.Symbol, StringComparison.InvariantCultureIgnoreCase)) &&
                                           (String.IsNullOrWhiteSpace(nodeInfo.DSName) || (s.SecurityDescription.DSName == nodeInfo.DSName)) &&
                                           (String.IsNullOrWhiteSpace(nodeInfo.FullName) || (s.SecurityDescription.FullName == nodeInfo.FullName))
                                     select s).SingleOrDefault();
                    if (sec == null)
                    {
                        string msg = String.Format("[{0}] There is no security. Symbol: {1}", m_optionPxMode, nodeInfo.Symbol);
                        m_context.Log(msg, MessageType.Error, true);
                        continue;
                    }

                    double actualPx = nodeInfo.OptPx;
                    if (m_optionType != StrikeType.Any)
                    {
                        // Заменяю инструмент в соответствии с настройками кубика
                        switch (m_optionType)
                        {
                            case StrikeType.Put:
                                if (sec.SecurityDescription.ActiveType != DataSource.ActiveType.OptionPut)
                                {
                                    var oldCall = sec;
                                    sec = nodeInfo.Pair.Put.Security;
                                    // Теперь еще нужно заменить ЦЕНУ. Проще всего это сделать через паритет.
                                    // C - P = F - K ==> P = C + K - F
                                    double putPx = nodeInfo.OptPx + nodeInfo.Strike - nodeInfo.F;
                                    // Но не меньше 1 ш.ц.!
                                    actualPx = Math.Max(putPx, sec.Tick);
                                    string txt = String.Format(CultureInfo.InvariantCulture,
                                        "[{0}] Symbol '{1}' has been replaced to '{2}' and changed price from {3} to {4}",
                                        m_optionPxMode, nodeInfo.Symbol, sec.Symbol, nodeInfo.OptPx, actualPx);
                                    m_context.Log(txt, MessageType.Warning, true);
                                }
                                break;

                            case StrikeType.Call:
                                if (sec.SecurityDescription.ActiveType != DataSource.ActiveType.OptionCall)
                                {
                                    var oldPut = sec;
                                    sec = nodeInfo.Pair.Call.Security;
                                    // Теперь еще нужно заменить ЦЕНУ. Проще всего это сделать через паритет.
                                    // C - P = F - K ==> C = P + F - K
                                    double callPx = nodeInfo.OptPx + nodeInfo.F - nodeInfo.Strike;
                                    // Но не меньше 1 ш.ц.!
                                    actualPx = Math.Max(callPx, sec.Tick);
                                    string txt = String.Format(CultureInfo.InvariantCulture,
                                        "[{0}] Symbol '{1}' has been replaced to '{2}' and changed price from {3} to {4}",
                                        m_optionPxMode, nodeInfo.Symbol, sec.Symbol, nodeInfo.OptPx, actualPx);
                                    m_context.Log(txt, MessageType.Warning, true);
                                }
                                break;
                        }
                    }

                    int len = sec.Bars.Count;
                    // Валидирую наличие правильной котировки
                    if (sec.FinInfo != null)
                    {
                        Debug.WriteLine("AskPx: " + sec.FinInfo.Ask);
                        Debug.WriteLine("BidPx: " + sec.FinInfo.Bid);
                    }

                    recalc = true;
                    if ((m_optionPxMode == OptionPxMode.Ask) && (m_qty > 0) ||
                        (m_optionPxMode == OptionPxMode.Bid) && (m_qty < 0))
                    {
                        string signalName = "\r\nLeft-Click BUY \r\n" + eventArg.Point.Tooltip + "\r\n";
                        posMan.BuyAtPrice(m_context, sec, Math.Abs(m_qty), actualPx, signalName, null);
                    }
                    else if ((m_optionPxMode == OptionPxMode.Bid) && (m_qty > 0) ||
                             (m_optionPxMode == OptionPxMode.Ask) && (m_qty < 0))
                    {
                        string signalName = "\r\nLeft-Click SELL \r\n" + eventArg.Point.Tooltip + "\r\n";
                        posMan.SellAtPrice(m_context, sec, Math.Abs(m_qty), actualPx, signalName, null);
                    }
                    else if (m_optionPxMode == OptionPxMode.Mid)
                    {
                        string msg = String.Format("[{0}] OptionPxMode.Mid is not implemented.", m_optionPxMode);
                        m_context.Log(msg, MessageType.Warning, true);
                    }
                    else if (m_qty == 0)
                    {
                        string msg = String.Format("[{0}] Qty: {1}. Trading is blocked.", m_optionPxMode, m_qty);
                        m_context.Log(msg, MessageType.Warning, true);
                    }
                }
                catch (Exception ex)
                {
                    string msg = String.Format("[{0}] {1} in RouteOnClickEvents. Message: {2}\r\n{3}",
                            m_optionPxMode, ex.GetType().FullName, ex.Message, ex);
                    m_context.Log(msg, MessageType.Warning, true);
                }
            }

            if (recalc)
                m_context.Recalc();
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
