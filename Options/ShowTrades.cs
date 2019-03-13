using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;

using TSLab.DataSource;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Script.Realtime;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Show all security trades on the Chart Pane
    /// \~russian Показать все сделки инструмента на Панели Графика
    /// </summary>
#if !DEBUG
    [HandlerInvisible]
#endif
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Show all trades", Language = Constants.En)]
    [HelperName("Показать все сделки", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(2)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION, Name = "Security or Option")]
    [Input(1, TemplateTypes.GRAPHPANE, Name = "Chart Pane")]
    [OutputsCount(0)]
    [Description("Показать все сделки инструмента на Панели Графика")]
    [HelperDescription("Show all security trades on the Chart Pane", Constants.En)]
    public sealed class ShowTrades : IGraphPaneHandler, IValuesHandlerWithNumber, IStreamHandler, IContextUses
    {
        private const string ActiveOrdPrefix = "ActiveOrd_";
        private const string ExecutedOrdPrefix = "ExecutedOrd_";

        public IContext Context { get; set; }

        private bool m_executed = false;
        // TODO: Добавить настройку "Тип линии" (отрезок, луч, прямая)
        //private bool GraphPane.InteractiveLineMode.Finite

        private static readonly Color s_red = new Color(200, 0, 0);
        private static readonly Color s_green = new Color(0, 200, 0);

        #region Parameters
        #endregion Parameters

        public void Execute(IOption opt, IGraphPane pane)
        {
            Execute(opt.UnderlyingAsset, pane);
        }

        public void Execute(ISecurity sec, IGraphPane pane)
        {
            DrawTrades(sec, pane);
        }

        public void Execute(IOption opt, IGraphPane pane, int barNum)
        {
            Execute(opt.UnderlyingAsset, pane, barNum);
        }

        public void Execute(ISecurity sec, IGraphPane pane, int barNum)
        {
            int barsCount = Context.BarsCount;
            if (!Context.IsLastBarUsed)
                barsCount--;

            if (barNum < barsCount - 1)
                return;

            // Если все уже нарисовано -- выходим
            if (m_executed)
                return;

            m_executed = true;
            DrawTrades(sec, pane);
        }

        private void DrawTrades(ISecurity sec, IGraphPane pane)
        {
            // TODO: нужна такая проверка?
            //if (!sec.IsRealtime)
            //{
            //    string msg = RM.GetStringFormat("OptHandlerMsg.PositionsManager.RunAsAgent",
            //        Context.Runtime.TradeName + ":" + MsgId, sec.IsRealtime);
            //    Context.Log(msg, MessageType.Warning, true);
            //    return;
            //}

            //// Позиций нет? Выходим. Ловить нечего.
            //if (sec.Positions.HavePositions)
            //    return;

            // В оптимизации ничего рисовать не надо
            if (Context.IsOptimization)
                return;

            // 0. Чистим за собой мусор от предыдущих прогонов?
            var oldObjects = pane.GetInteractiveObjects(); // Клонирование делается внутри метода
            foreach (var oldObj in oldObjects)
            {
                var oldPoint = oldObj as GraphPane.IInteractivePoint;
                if (oldPoint != null)
                {
                    if (oldPoint.Id.StartsWith(ExecutedOrdPrefix))
                        pane.RemoveInteractiveObject(oldPoint.Id);
                    continue;
                }

                var oldLine = oldObj as GraphPane.IInteractiveLine;
                if (oldLine != null)
                {
                    if (oldLine.Id.StartsWith(ActiveOrdPrefix))
                        pane.RemoveInteractiveObject(oldLine.Id);
                    continue;
                }
            }

            ISecurityRt secRt;
            if (sec is ISecurityRt)
                secRt = (ISecurityRt)sec;
            else
            {
                secRt = (from s in Context.Runtime.Securities
                         where s.SecurityDescription.Equals(sec) && (s is ISecurityRt)
                         select (ISecurityRt)s).SingleOrDefault();
            }

            if (secRt == null)
                return;

            // ОТЛИЧНО! Эта коллекция позволит мне нарисовать свои заявки и сделки
            var orders = secRt.Orders.ToList();
            var activeOrders = new List<IOrder>();
            var executedOrders = new List<IOrder>();
            for (int j = 0; j < orders.Count; j++)
            {
                IOrder ord = orders[j];
                if (ord.IsActive)
                {
                    activeOrders.Add(ord);
                }
                else if (ord.Status == OrderStatus.Executed)
                {
                    executedOrders.Add(ord);
                }
                else
                {
                    //string str = "Все остальное игнорируем?";
                }
            }

            int id = 0;
            IDataBar lastBar = sec.Bars.LastOrDefault();
            int intervalSec = Context.Runtime.IntervalInstance.ToSeconds();
            DateTime now = (lastBar != null) ? lastBar.Date : (sec.FinInfo != null) ? sec.FinInfo.LastUpdate : DateTimeUtils.Now;

            // 1. Рисуем активные заявки в виде линий
            foreach (IOrder ord in activeOrders)
            {
                string itemId = ActiveOrdPrefix + id;
                var end = new GraphPane.MarketPoint(now, ord.Price);
                var beg = new GraphPane.MarketPoint(now.AddSeconds(-10 * intervalSec), ord.Price);
                Color actualCol = ord.IsBuy ? s_green : s_red;
                pane.AddInteractiveLine(itemId, PaneSides.RIGHT, true, actualCol,
                    GraphPane.InteractiveLineMode.Finite, beg, end);

                id++;
            }

            // 2. Рисуем исполненные заявки в виде точек
            foreach (IOrder ord in executedOrders)
            {
                string itemId = ExecutedOrdPrefix + id;
                Color actualCol = ord.IsBuy ? s_green : s_red;
                var point = new GraphPane.MarketPoint(ord.Date, ord.Price);
                pane.AddInteractivePoint(itemId, PaneSides.RIGHT, true, actualCol, point);

                id++;
            }

            //var point = new GraphPane.MarketPoint(DateTime.Now, 50000);
            //pane.AddInteractivePoint("0", PaneSides.RIGHT, true, new Color(255, 0, 0), point);
        }
    }
}