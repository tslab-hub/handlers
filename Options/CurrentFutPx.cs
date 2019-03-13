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
    /// \~english Draw vertical line on chart.
    /// Vertical position is aligned to a reference InteractiveSeries.
    /// \~russian Рисование вертикальной линии на графике.
    /// Положение по вертикали привязано к произвольной InteractiveSeries.
    /// Также даёт возможность совершать сделки в БА.
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Current Fut Px", Language = Constants.En)]
    [HelperName("Маркер текущей цены БА", Language = Constants.Ru)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = "ReferenceLine")]
    [Input(2, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION, Name = Constants.AnyOption)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Рисование вертикальной линии на графике. Положение по вертикали привязано к произвольной InteractiveSeries. Также даёт возможность совершать сделки в БА.")]
    [HelperDescription("Draws a vertical line in the chart. A vertical position is connected to any given Interactive Series.", Constants.En)]
    public class CurrentFutPx : BaseContextHandler, IValuesHandlerWithNumber, IDisposable
    {
        private const string MsgId = "CFP";
        private const string DefaultQty = "1";
        private const string DefaultTooltipFormat = "P2";

        /// <summary>Минимальный вынос маркера относительно опорной кривой (по умолчанию 0.03)</summary>
        private double m_minHeight = 0.03;
        /// <summary>Высота (по умолчанию 0.1)</summary>
        private double m_offset = 0.1;

        private int m_qty = Int32.Parse(DefaultQty);
        private string m_tooltipFormat = DefaultTooltipFormat;

        private InteractiveSeries m_clickableSeries = null;

        #region Parameters
        /// <summary>
        /// \~english Minimum height of the marker (absolute units)
        /// \~russian Минимальный вынос маркера относительно опорной кривой в обе стороны (в абсолютных единицах).
        /// </summary>
        [HelperName("Min Height", Constants.En)]
        [HelperName("Минимальный отступ", Constants.Ru)]
        [Description("Минимальный вынос маркера относительно опорной кривой в обе стороны (в абсолютных единицах).")]
        [HelperDescription("Minimum height of the marker (absolute units)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true,
            Default = "0.03", Min = "0", Max = "10000000", Step = "1")]
        public double MinHeight
        {
            get { return m_minHeight; }
            set
            {
                if (value > 0)
                    m_minHeight = value;
            }
        }

        /// <summary>
        /// \~english Height of the marker (percents)
        /// \~russian Вынос вертикального маркера относительно опорной кривой в обе стороны (в процентах).
        /// </summary>
        [HelperName("Offset, %", Constants.En)]
        [HelperName("Высота, %", Constants.Ru)]
        [Description("Вынос вертикального маркера относительно опорной кривой в обе стороны (в процентах).")]
        [HelperDescription("Height of the marker (percents)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true,
            Default = "10", Min = "0", Max = "1000000", Step = "1")]
        public double OffsetPct
        {
            get { return m_offset * Constants.PctMult; }
            set
            {
                if (value > 0)
                    m_offset = value / Constants.PctMult;
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

        /// <summary>
        /// \~english Tooltip format (i.e. '0.00', '0.0##' etc)
        /// \~russian Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.
        /// </summary>
        [HelperName("Tooltip format", Constants.En)]
        [HelperName("Формат подсказки", Constants.Ru)]
        [Description("Формат числа для тултипа. Например, '0.00', '0.0##', 'P2' и т.п.")]
        [HelperDescription("Tooltip format (i.e. '0.00', '0.0##', 'P2' etc)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = DefaultTooltipFormat)]
        public string TooltipFormat
        {
            get { return m_tooltipFormat; }
            set
            {
                if (!String.IsNullOrWhiteSpace(value))
                {
                    try
                    {
                        string yStr = Math.PI.ToString(value);
                        m_tooltipFormat = value;
                    }
                    catch
                    {
                        m_context.Log("Tooltip format error. I'll keep old one: " + m_tooltipFormat, MessageType.Warning, true);
                    }
                }
            }
        }
        #endregion Parameters

        public InteractiveSeries Execute(double price, InteractiveSeries line, IOption opt, int barNum)
        {
            if (opt == null)
                return Constants.EmptySeries;

            return Execute(price, line, opt.UnderlyingAsset, barNum);
        }

        public InteractiveSeries Execute(double price, InteractiveSeries line, IOptionSeries optSer, int barNum)
        {
            if (optSer == null)
                return Constants.EmptySeries;

            return Execute(price, line, optSer.UnderlyingAsset, barNum);
        }

        public InteractiveSeries Execute(double price, InteractiveSeries line, ISecurity sec, int barNum)
        {
            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ClickEvent += InteractiveSplineOnClickEvent;

            m_clickableSeries = res;

            double f = price;
            double? sigmaAtm = null;
            SmileInfo sInfo = line.GetTag<SmileInfo>();
            if ((sInfo != null) && (sInfo.ContinuousFunction != null))
            {
                double tmp;
                if (sInfo.ContinuousFunction.TryGetValue(f, out tmp))
                {
                    if (!Double.IsNaN(tmp))
                        sigmaAtm = tmp;
                }
            }

            if (sigmaAtm == null)
                return res;

            double h = Math.Max(m_minHeight, sigmaAtm.Value * m_offset);
            // Теперь определяем максимальный размах по вертикали
            if (line.ControlPoints.Count > 1)
            {
                var cps = line.ControlPoints;
                double min = Double.MaxValue, max = Double.MinValue;
                for (int j = 0; j < cps.Count; j++)
                {
                    var anchor = cps[j].Anchor;
                    double y = anchor.ValueY;
                    if (y <= min)
                        min = y;
                    if (max <= y)
                        max = y;
                }

                if ((min < max) && (!Double.IsInfinity(max - min)))
                {
                    h = Math.Max(h, (max - min) * m_offset);
                }
            }

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            {
                InteractivePointActive ip = new InteractivePointActive();
                ip.IsActive = true;
                ip.DragableMode = DragableMode.None;
                ip.Geometry = Geometries.Ellipse; // Geometries.TriangleDown;
                ip.Color = AlphaColors.Magenta;
                double y = (sigmaAtm.Value - h);
                ip.Value = new Point(f, y);
                string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                // По умолчанию формат 'P2' -- то есть отображение с точностью 2знака,
                // со знаком процента и с автоматическим умножением на 100.
                ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "F:{0}; Y:{1}", f, yStr);

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
                InteractivePointActive ip = new InteractivePointActive();
                ip.IsActive = true;
                ip.DragableMode = DragableMode.None;
                ip.Geometry = Geometries.Triangle;
                ip.Color = AlphaColors.GreenYellow;
                double y = (sigmaAtm.Value + h);
                ip.Value = new Point(f, y);
                string yStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);
                // По умолчанию формат 'P2' -- то есть отображение с точностью 2знака,
                // со знаком процента и с автоматическим умножением на 100.
                ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "F:{0}; Y:{1}", f, yStr);

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
