using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;

using TSLab.MessageType;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Quote volatility with mouse on CanvasPane
    /// \~russian Котирование волатильности мышкой на CanvasPane
    /// </summary>
    [HandlerCategory(Constants.GeneralByTick)]
    [HelperName("Volatility trading", Language = Constants.En)]
    [HelperName("Торговля волатильностью", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(1, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Котирование волатильности мышкой на CanvasPane")]
    [HelperDescription("Quote volatility with mouse on CanvasPane", Constants.En)]
    public class IvChartTrading : BaseContextHandler, IValuesHandlerWithNumber, IDisposable
    {
        private int m_qty = 10;
        private double m_shiftIv = 0;
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

        /// <summary>
        /// \~english Shift quote relative to the smile (in percents of volatility)
        /// \~russian Сдвиг котировки относительно улыбки (в процентах волатильности)
        /// </summary>
        [HelperName("Shift IV", Constants.En)]
        [HelperName("Сдвиг волатильности", Constants.Ru)]
        [Description("Сдвиг котировки относительно улыбки (в процентах волатильности)")]
        [HelperDescription("Shift quote relative to the smile (in percents of volatility)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-10000000", Max = "10000000", Step = "0.1")]
        public double ShiftIvPct
        {
            get { return m_shiftIv * Constants.PctMult; }
            set { m_shiftIv = value / Constants.PctMult; }
        }
        #endregion

        /// <summary>
        /// Метод под флаг TemplateTypes.INTERACTIVESPLINE
        /// </summary>
        public InteractiveSeries Execute(InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            if ((smile == null) || (optSer == null))
                return Constants.EmptySeries;

            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if ((oldInfo == null) || (oldInfo.ContinuousFunction == null))
                return Constants.EmptySeries;

            double futPx = oldInfo.F;
            double dT = oldInfo.dT;

            IOptionStrikePair[] pairs = (from strike in optSer.GetStrikePairs()
                                         //orderby strike.Strike ascending -- уже отсортировано
                                         select strike).ToArray();
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            for (int j = 0; j < pairs.Length; j++)
            {
                var pair = pairs[j];
                double sigma = oldInfo.ContinuousFunction.Value(pair.Strike) + m_shiftIv;
                if (Double.IsNaN(sigma) || (sigma < Double.Epsilon))
                {
                    //string msg = String.Format("[DEBUG:{0}] Invalid sigma:{1} for strike:{2}", GetType().Name, sigma, nodeInfo.Strike);
                    //m_context.Log(msg, MessageType.Warning, true);
                    continue;
                }

                bool isCall = (futPx <= pair.Strike);
                double theorOptPx = FinMath.GetOptionPrice(futPx, pair.Strike, dT, sigma, oldInfo.RiskFreeRate, isCall);
                theorOptPx = Math.Round(theorOptPx / pair.Tick) * pair.Tick;
                if (Double.IsNaN(theorOptPx) || (theorOptPx < Double.Epsilon))
                {
                    //string msg = String.Format("[DEBUG:{0}] Invalid theorOptPx:{1} for strike:{2}", GetType().Name, theorOptPx, nodeInfo.Strike);
                    //m_context.Log(msg, MessageType.Warning, true);
                    continue;
                }

                // ReSharper disable once UseObjectOrCollectionInitializer
                InteractivePointActive tmp = new InteractivePointActive();

                tmp.IsActive = true;
                //tmp.Tag = anchor.Tag;
                tmp.Tooltip = null; // anchor.Tooltip;
                //tmp.Label = anchor.Tooltip;
                tmp.ValueX = pair.Strike;
                tmp.ValueY = sigma;
                tmp.DragableMode = DragableMode.Yonly;
                //tmp.Size = m_outletSize;

                //tmp.Color = Colors.White;
                //tmp.Geometry = m_outletGeometry; // Geometries.Ellipse;

                InteractiveObject obj = new InteractiveObject();
                obj.Anchor = tmp;

                controlPoints.Add(obj);
            }

            // Это надо ОБЯЗАТЕЛЬНО делать ПОСЛЕ восстановления виртуальных позиций
            #region Process clicks
            //{
            //    var onClickEvents = m_context.LoadObject(VariableId + "OnClickEvent") as List<InteractiveActionEventArgs>;
            //    if (onClickEvents == null)
            //    {
            //        onClickEvents = new List<InteractiveActionEventArgs>();
            //        m_context.StoreObject(VariableId + "OnClickEvent", onClickEvents);
            //    }

            //    if (onClickEvents.Count > 0)
            //    {
            //        RouteOnClickEvents(onClickEvents);
            //    }
            //}
            #endregion Process clicks

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            //res.ClickEvent -= InteractiveSplineOnClickEvent;
            //res.ClickEvent += InteractiveSplineOnClickEvent;
            res.EndDragEvent -= res_EndDragEvent;
            res.EndDragEvent += res_EndDragEvent;

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
                        posMan.BuyAtPrice(m_context, sec, Math.Abs(m_qty), nodeInfo.OptPx, signalName, null);
                    }
                    else if ((m_optionPxMode == OptionPxMode.Bid) && (m_qty > 0) ||
                             (m_optionPxMode == OptionPxMode.Ask) && (m_qty < 0))
                    {
                        string signalName = "\r\nLeft-Click SELL \r\n" + eventArg.Point.Tooltip + "\r\n";
                        posMan.SellAtPrice(m_context, sec, Math.Abs(m_qty), nodeInfo.OptPx, signalName, null);
                    }
                    else if (m_optionPxMode == OptionPxMode.Mid)
                    {
                        string msg = String.Format("[{0}] OptionPxMode.Mid is not implemented.", m_optionPxMode);
                        m_context.Log(msg, MessageType.Alert, true);
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
                    m_context.Log(msg, MessageType.Alert, true);
                }
            }

            if (recalc)
                m_context.Recalc();
        }

        private void res_EndDragEvent(object sender, InteractiveActionEventArgs e)
        {
            InteractiveSeries oldLine = sender as InteractiveSeries;
            if (oldLine != null)
            {
                //SmileInfo info = oldLine.GetTag<SmileInfo>();
                //if (info != null)
                //{
                //    List<double> xs = new List<double>();
                //    List<double> ys = new List<double>();

                //    foreach (InteractiveObject oldObj in oldLine.ControlPoints)
                //    {
                //        if (!(oldObj.Anchor.IsVisible ?? oldObj.AnchorGraphPointData.IsVisible))
                //            continue;

                //        double k = oldObj.Anchor.ValueX;
                //        double sigma = oldObj.Anchor.ValueY;

                //        xs.Add(k);
                //        ys.Add(sigma);
                //    }

                //    try
                //    {
                //        NotAKnotCubicSpline spline = new NotAKnotCubicSpline(xs, ys);

                //        info.ContinuousFunction = spline;
                //        info.ContinuousFunctionD1 = spline.DeriveD1();

                //        oldLine.Tag = info;
                //    }
                //    catch (Exception ex)
                //    {
                //        m_context.Log(ex.ToString(), MessageType.Error, true);
                //        //return Constants.EmptySeries;
                //    }
                //}

                // Сразу отписываюсь!
                oldLine.EndDragEvent -= res_EndDragEvent;
            }

            m_context.Recalc();
        }

        public void Dispose()
        {
            if (m_clickableSeries != null)
            {
                //m_clickableSeries.ClickEvent -= InteractiveSplineOnClickEvent;
                m_clickableSeries.EndDragEvent -= res_EndDragEvent;

                m_clickableSeries = null;
            }
        }
    }
}
