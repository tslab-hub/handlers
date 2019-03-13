using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Vertical line for CanvasPane. It is possible to trade base asset using this control.
    /// \~russian Рисование вертикальной линии на чарте. Также даёт возможность совершать сделки в БА.
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Vertical Line", Language = Constants.En)]
    [HelperName("Вертикальная линия", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION, Name = Constants.AnyOption)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Рисование вертикальной линии на чарте. Также даёт возможность совершать сделки в БА.")]
    [HelperDescription("Vertical line for CanvasPane. It is possible to trade base asset using this control.", Constants.En)]
    public class VerticalLine2 : BaseContextHandler, IValuesHandlerWithNumber, IDisposable
    {
        private const string MsgId = "FUT";

        private double m_sigmaLow = 0.10, m_sigmaHigh = 0.50;

        private int m_qty = 1;

        private InteractiveSeries m_clickableSeries = null;

        #region Parameters
        /// <summary>
        /// \~english Low level of this marker (in percents)
        /// \~russian Нижний уровень маркера (в процентах)
        /// </summary>
        [HelperName("Sigma low, %", Constants.En)]
        [HelperName("Нижний уровень, %", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Нижний уровень маркера (в процентах)")]
        [HelperDescription("Low level of this marker (in percents)", Language = Constants.En)]
        [HandlerParameter(true, "10", Min = "0", Max = "10000000", Step = "0.01", NotOptimized = true)]
        public double SigmaLowPct
        {
            get { return m_sigmaLow * Constants.PctMult; }
            set
            {
                if (value > 0)
                    m_sigmaLow = value / Constants.PctMult;
            }
        }

        /// <summary>
        /// \~english High level of this marker (in percents)
        /// \~russian Верхний уровень маркера (в процентах)
        /// </summary>
        [HelperName("Sigma high, %", Constants.En)]
        [HelperName("Верхний уровень, %", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Верхний уровень маркера (в процентах)")]
        [HelperDescription("High level of this marker (in percents)", Language = Constants.En)]
        [HandlerParameter(true, "50", Min = "0", Max = "10000000", Step = "0.01", NotOptimized = true)]
        public double SigmaHighPct
        {
            get { return m_sigmaHigh * Constants.PctMult; }
            set
            {
                if (value > 0)
                    m_sigmaHigh = value / Constants.PctMult;
            }
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
        #endregion Parameters

        public InteractiveSeries Execute(double price, IOption opt, int barNum)
        {
            return Execute(price, opt.UnderlyingAsset, barNum);
        }

        public InteractiveSeries Execute(double price, IOptionSeries optSer, int barNum)
        {
            return Execute(price, optSer.UnderlyingAsset, barNum);
        }

        public InteractiveSeries Execute(double price, ISecurity sec, int barNum)
        {
            InteractiveSeries res = new InteractiveSeries();
            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return res;

            res.ClickEvent -= InteractiveSplineOnClickEvent;
            res.ClickEvent += InteractiveSplineOnClickEvent;

            m_clickableSeries = res;

            double f = price;

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();

            {
                InteractivePointActive ip = new InteractivePointActive(f, m_sigmaLow);
                ip.IsActive = true;
                ip.DragableMode = DragableMode.None;
                ip.Geometry = Geometries.Ellipse;
                ip.Color = AlphaColors.BlueViolet;
                ip.Tooltip = String.Format("F:{0}; Y:{1:0.00}%", f, m_sigmaLow * Constants.PctMult);

                SmileNodeInfo node = new SmileNodeInfo();
                node.OptPx = f;
                node.Security = sec;
                node.PxMode = OptionPxMode.Bid;

                node.Symbol = node.Security.Symbol;
                node.DSName = node.Security.SecurityDescription.DSName;
                node.Expired = node.Security.SecurityDescription.Expired;
                node.FullName = node.Security.SecurityDescription.FullName;

                ip.Tag = node;

                controlPoints.Add(new InteractiveObject(ip));
            }

            {
                InteractivePointActive ip = new InteractivePointActive(f, m_sigmaHigh);
                ip.IsActive = true;
                ip.DragableMode = DragableMode.None;
                ip.Geometry = Geometries.Triangle;
                ip.Color = AlphaColors.Green;
                ip.Tooltip = String.Format("F:{0}; Y:{1:0.00}%", f, m_sigmaHigh * Constants.PctMult);

                SmileNodeInfo node = new SmileNodeInfo();
                node.OptPx = f;
                node.Security = sec;
                node.PxMode = OptionPxMode.Ask;

                node.Symbol = node.Security.Symbol;
                node.DSName = node.Security.SecurityDescription.DSName;
                node.Expired = node.Security.SecurityDescription.Expired;
                node.FullName = node.Security.SecurityDescription.FullName;

                ip.Tag = node;

                controlPoints.Add(new InteractiveObject(ip));
            }

            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            return res;
        }

        private void InteractiveSplineOnClickEvent(object sender, InteractiveActionEventArgs eventArgs)
        {
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (posMan.BlockTrading)
            {
                //string msg = String.Format("[{0}] Trading is blocked. Please, change 'Block Trading' parameter.", MsgId);
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlocked", MsgId);
                m_context.Log(msg, MessageType.Warning, true);
                return;
            }

            if (m_qty == 0)
            {
                //string msg = String.Format("[{0}] Trading is blocked for zero quantity. Qty: {1}", MsgId, qty);
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlockedForZeroQty", MsgId, m_qty);
                m_context.Log(msg, MessageType.Warning, true);
                return;
            }
            else if (m_qty < 0)
            {
                // GLSP-252 - Запрет на совершение сделок при отрицательном Qty
                //string msg = String.Format("[{0}] Trading is blocked for negative quantity. Qty: {1}", MsgId, qty);
                string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.TradingBlockedForNegativeQty", MsgId, m_qty);
                m_context.Log(msg, MessageType.Warning, true);
                return;
            }

            SmileNodeInfo nodeInfo = eventArgs.Point.Tag as SmileNodeInfo;
            if (nodeInfo == null)
            {
                //string msg = String.Format("[{0}] There is no nodeInfo. Base asset price: {1}", MsgId);
                string msg = RM.GetStringFormat(CultureInfo.InvariantCulture, "OptHandlerMsg.CurrentFutPx.ThereIsNoNodeInfo",
                    MsgId, eventArgs.Point.ValueX);
                m_context.Log(msg, MessageType.Error, true);
                return;
            }

            nodeInfo.ClickTime = DateTime.Now;

            // [2015-10-02] Подписка на данный инструмент, чтобы он появился в коллекции Context.Runtime.Securities
            ISecurity testSec = (from s in m_context.Runtime.Securities
                                 let secDesc = s.SecurityDescription
                                 where secDesc.FullName.Equals(nodeInfo.FullName, StringComparison.InvariantCultureIgnoreCase) &&
                                       secDesc.DSName.Equals(nodeInfo.DSName, StringComparison.InvariantCultureIgnoreCase) &&
                                       secDesc.Name.Equals(nodeInfo.Symbol, StringComparison.InvariantCultureIgnoreCase)
                                 select s).SingleOrDefault();
            if (testSec == null)
            {
                ISecurity sec = nodeInfo.Security;
                int bc = sec.Bars.Count;
                string msg = String.Format("[{0}] There is security DsName: {1}; Symbol: {2}; Security: {3} with {4} bars available.",
                    nodeInfo.PxMode, nodeInfo.DSName, nodeInfo.Symbol, nodeInfo.FullName, bc);
                Context.Log(msg, MessageType.Info, false);

                // Пересчитываю целочисленный параметр Qty в фактические лоты конкретного инструмента
                double actQty = m_qty * sec.LotTick; // Здесь нет модуля, потому что направление сделки несет в себе знак Qty
                nodeInfo.Qty = actQty;
            }
            else if ((nodeInfo.Security != null) && (nodeInfo.Security != null))
            {
                // Аварийная ветка
                // Пересчитываю целочисленный параметр Qty в фактические лоты конкретного инструмента
                double actQty = m_qty * nodeInfo.Security.LotTick; // Здесь нет модуля, потому что направление сделки несет в себе знак Qty
                nodeInfo.Qty = actQty;
            }
            else
            {
                // Аварийная ветка
                // Не могу пересчитать целочисленный параметр Qty в фактические лоты конкретного инструмента!
                nodeInfo.Qty = m_qty; // Здесь нет модуля, потому что направление сделки несет в себе знак Qty

                string msg = String.Format(CultureInfo.InvariantCulture, "[{0}.ClickEvent] LotTick is set to 1.", GetType().Name);
                m_context.Log(msg, MessageType.Warning, false);
            }

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
