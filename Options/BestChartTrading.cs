using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Trading with mouse on CanvasPane. Best quotes are accented automatically
    /// \~russian Торговля кликами мыши по чарту CanvasPane (автоматически выделяются лучшие точки)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Best Chart Trading", Language = Constants.En)]
    [HelperName("Торговля по лучшим заявкам", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = "Quotes")]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Торговля кликами мыши по чарту CanvasPane (автоматически выделяются лучшие точки)")]
    [HelperDescription("Trading with mouse on CanvasPane. Best quotes are accented automatically", Constants.En)]
    public class BestChartTrading : BaseContextHandler, IValuesHandlerWithNumber, IDisposable
    {
        private int m_qty = 10;
        private StrikeType m_optionType = StrikeType.Call;
        private OptionPxMode m_optionPxMode = OptionPxMode.Ask;

        private double m_outlet = 0.02;
        private int m_widthPx = 100;
        /// <summary>Размер узла в пикселах</summary>
        private double m_outletSize = 16;

        private InteractiveSeries m_clickableSeries;

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
            Default = "10", Min = "-1000000", Max = "1000000", Step = "1")]
        public int Qty
        {
            get { return m_qty; }
            set { m_qty = value; }
        }

        /// <summary>
        /// \~english Width of neutral band (price steps)
        /// \~russian Ширина нейтральной полосы (в шагах цены)
        /// </summary>
        [HelperName("Width", Constants.En)]
        [HelperName("Ширина", Constants.Ru)]
        [Description("Ширина нейтральной полосы (в шагах цены)")]
        [HelperDescription("Width of neutral band (price steps)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "100", Min = "0", Max = "10000000", Step = "1")]
        public double WidthPx
        {
            get { return m_widthPx; }
            set { m_widthPx = (int)Math.Round(value); }
        }

        /// <summary>
        /// \~english Outlet distance to 'profitable' market order (units of volatility)
        /// \~russian Величина выноса торгового маркера, которым подсвечивается 'выгодная' заявка (в единицах волатильности)
        /// </summary>
        [HelperName("Outlet distance", Constants.En)]
        [HelperName("Величина выноса ", Constants.Ru)]
        [Description("Величина выноса торгового маркера, которым подсвечивается 'выгодная' заявка (в единицах волатильности)")]
        [HelperDescription("Outlet distance to 'profitable' market order (units of volatility)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0.02", Min = "-10000000", Max = "10000000", Step = "1")]
        public double OutletDistance
        {
            get { return m_outlet; }
            set { m_outlet = value; }
        }

        /// <summary>
        /// \~english Outlet size (pixel)
        /// \~russian Величина торгового маркера, которым подсвечивается 'выгодная' заявка (в пикселях)
        /// </summary>
        [HelperName("Outlet size", Constants.En)]
        [HelperName("Величина маркера ", Constants.Ru)]
        [Description("Величина торгового маркера, которым подсвечивается 'выгодная' заявка (в пикселях)")]
        [HelperDescription("Outlet size (pixel)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "16", Min = "1", Max = "10000000", Step = "1")]
        public double OutletSize
        {
            get { return m_outletSize; }
            set
            {
                // Не меньше 1 пиксела
                m_outletSize = Math.Max(1, value);
            }
        }
        #endregion

        /// <summary>
        /// Метод под флаг TemplateTypes.INTERACTIVESPLINE
        /// </summary>
        public InteractiveSeries Execute(InteractiveSeries quotes, InteractiveSeries smile, int barNum)
        {
            if ((quotes == null) || (smile == null))
                return Constants.EmptySeries;

            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return quotes;

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if ((oldInfo == null) || (oldInfo.ContinuousFunction == null))
                return Constants.EmptySeries;

            InteractiveSeries res = quotes;
            double futPx = oldInfo.F;
            double dT = oldInfo.dT;
            foreach (InteractiveObject obj in res.ControlPoints)
            {
                SmileNodeInfo nodeInfo = obj.Anchor.Tag as SmileNodeInfo;
                if (nodeInfo == null)
                    continue;

                double realOptPx = nodeInfo.OptPx;
                bool isCall = (nodeInfo.OptionType == StrikeType.Call);

                double sigma = oldInfo.ContinuousFunction.Value(nodeInfo.Strike);
                if (Double.IsNaN(sigma) || (sigma < Double.Epsilon))
                {
                    //string msg = String.Format("[DEBUG:{0}] Invalid sigma:{1} for strike:{2}", GetType().Name, sigma, nodeInfo.Strike);
                    //m_context.Log(msg, MessageType.Warning, true);
                    continue;
                }

                double theorOptPx = FinMath.GetOptionPrice(futPx, nodeInfo.Strike, dT, sigma, oldInfo.RiskFreeRate, isCall);
                if (Double.IsNaN(theorOptPx) || (theorOptPx < Double.Epsilon))
                {
                    //string msg = String.Format("[DEBUG:{0}] Invalid theorOptPx:{1} for strike:{2}", GetType().Name, theorOptPx, nodeInfo.Strike);
                    //m_context.Log(msg, MessageType.Warning, true);
                    continue;
                }

                if (nodeInfo.PxMode == OptionPxMode.Ask)
                {
                    double doLevel = theorOptPx - m_widthPx * nodeInfo.Pair.Tick;
                    if (realOptPx <= doLevel)
                    {
                        var anchor = (InteractivePointActive)obj.Anchor;

                        // ReSharper disable once UseObjectOrCollectionInitializer
                        var tmp = new InteractivePointActive();

                        tmp.IsActive = true;
                        tmp.Tag = anchor.Tag;
                        tmp.Tooltip = null; // anchor.Tooltip;
                        int decimals = (nodeInfo.Security != null) ? nodeInfo.Security.Decimals : 0;
                        string pot = Math.Abs(theorOptPx - realOptPx).ToString("N" + decimals, CultureInfo.InvariantCulture);
                        tmp.Label = anchor.Tooltip + " (" + pot + ")";
                        tmp.ValueX = anchor.ValueX;
                        tmp.ValueY = anchor.ValueY + m_outlet;
                        tmp.DragableMode = DragableMode.None;
                        tmp.Size = m_outletSize;

                        //tmp.Color = Colors.White;
                        //tmp.Geometry = m_outletGeometry; // Geometries.Ellipse;

                        obj.ControlPoint1 = tmp;
                    }
                }

                if (nodeInfo.PxMode == OptionPxMode.Bid)
                {
                    double upLevel = theorOptPx + m_widthPx * nodeInfo.Pair.Tick;
                    if (realOptPx >= upLevel)
                    {
                        var anchor = (InteractivePointActive)obj.Anchor;

                        // ReSharper disable once UseObjectOrCollectionInitializer
                        var tmp = new InteractivePointActive();

                        tmp.IsActive = true;
                        tmp.Tag = anchor.Tag;
                        tmp.Tooltip = null; // anchor.Tooltip;
                        int decimals = (nodeInfo.Security != null) ? nodeInfo.Security.Decimals : 0;
                        string pot = Math.Abs(theorOptPx - realOptPx).ToString("N" + decimals, CultureInfo.InvariantCulture);
                        tmp.Label = anchor.Tooltip + " (" + pot + ")";
                        tmp.ValueX = anchor.ValueX;
                        tmp.ValueY = anchor.ValueY - m_outlet;
                        tmp.DragableMode = DragableMode.None;
                        tmp.Size = m_outletSize;

                        //tmp.Color = Colors.White;
                        //tmp.Geometry = m_outletGeometry; // Geometries.Ellipse;

                        obj.ControlPoint1 = tmp;
                    }
                }
            }

            res.ClickEvent -= InteractiveSplineOnClickEvent;
            res.ClickEvent += InteractiveSplineOnClickEvent;

            m_clickableSeries = res;

            return res;
        }

        private void InteractiveSplineOnClickEvent(object sender, InteractiveActionEventArgs eventArgs)
        {
            // [2016-03-01] PROD-2452: Запрещаю торговлю по узлу, если нет "подсветки"
            {
                InteractiveObject obj = eventArgs.InteractiveObject;
                if ((obj == null) || (obj.Anchor == null) || (obj.ControlPoint1 == null) ||
                    (!(obj.Anchor is InteractivePointActive)) || (!(obj.ControlPoint1 is InteractivePointActive)))
                {
                    string msg = String.Format("[{0}.ClickEvent] Denied #1", GetType().Name);
                    m_context.Log(msg, MessageType.Warning, false);
                    return;
                }

                var cp1 = (InteractivePointActive)obj.ControlPoint1;
                // PROD-4967 - Необязательно проверять активность якоря.
                // Потому что эта настройка делается на более позднем этапе в момент создания графического объекта
                // методом smilePanePane.AddList("SmilePane_pane_TradeAsks_chart", <...>)
                //var anchor = (InteractivePointActive)obj.Anchor;
                //if ((anchor.IsActive == null) || (!anchor.IsActive.Value) ||
                if ((cp1.IsActive == null) || (!cp1.IsActive.Value))
                {
                    string msg = String.Format("[{0}.ClickEvent] Denied #3 (ControlPoint1 is not active)", GetType().Name);
                    m_context.Log(msg, MessageType.Warning, false);
                    return;
                }
            }

            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (posMan.BlockTrading)
            {
                //string msg = String.Format("[{0}] Trading is blocked. Please, change 'Block Trading' parameter.", m_optionPxMode);
                string msg = String.Format(RM.GetString("OptHandlerMsg.PositionsManager.TradingBlocked"), m_optionPxMode);
                m_context.Log(msg, MessageType.Info, true);
                return;
            }

            // Здесь нет проверки знака m_qty, потому что в данном кубике мы действительно можем и продавать и покупать

            SmileNodeInfo nodeInfo = eventArgs.Point.Tag as SmileNodeInfo;
            if (nodeInfo == null)
            {
                //string msg = String.Format("[{0}] There is no nodeInfo. Quote type: {1}; Strike: {2}",
                string msg = RM.GetStringFormat(CultureInfo.InvariantCulture, "OptHandlerMsg.ThereIsNoNodeInfo", 
                    m_context.Runtime.TradeName, m_optionPxMode, eventArgs.Point.ValueX);
                m_context.Log(msg, MessageType.Error, true);
                return;
            }

            {
                string msg = String.Format(CultureInfo.InvariantCulture, "[{0}.ClickEvent] Strike: {1}; Security: {2}",
                    GetType().Name, eventArgs.Point.ValueX, nodeInfo.FullName);
                m_context.Log(msg, MessageType.Info, false);
            }

            nodeInfo.ClickTime = DateTime.Now;

            // [2015-10-02] Подписка на данный инструмент, чтобы он появился в коллекции Context.Runtime.Securities
            ISecurity testSec = (from s in Context.Runtime.Securities
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
                    m_optionPxMode, nodeInfo.DSName, nodeInfo.Symbol, nodeInfo.FullName, bc);
                Context.Log(msg, MessageType.Info, false);

                // Пересчитываю целочисленный параметр Qty в фактические лоты конкретного инструмента
                double actQty = m_qty * sec.LotTick; // Здесь нет модуля, потому что направление сделки несет в себе знак Qty
                nodeInfo.Qty = actQty;
            }
            else if ((nodeInfo.Pair != null) && (nodeInfo.Pair.Put != null))
            {
                // Аварийная ветка
                // Пересчитываю целочисленный параметр Qty в фактические лоты конкретного инструмента
                double actQty = m_qty * nodeInfo.Pair.Put.LotTick; // Здесь нет модуля, потому что направление сделки несет в себе знак Qty
                nodeInfo.Qty = actQty;
            }
            else
            {
                // Аварийная ветка
                // Не могу пересчитать целочисленный параметр Qty в фактические лоты конкретного инструмента!
                //double actQty = Math.Abs(m_qty * nodeInfo.Pair.Put.LotTick);
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
