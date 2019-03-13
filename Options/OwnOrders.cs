using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Script.Realtime;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Own active orders
    /// \~russian Свои активные заявки
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Own orders", Language = Constants.En)]
    [HelperName("Свои заявки", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(3, TemplateTypes.DOUBLE, Name = Constants.RiskFreeRate)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Свои активные заявки")]
    [HelperDescription("Own active orders", Constants.En)]
    public class OwnOrders : BaseContextHandler, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0";

        /// <summary>Показывать длинные заявки или короткие?</summary>
        private bool m_showLongOrders = false;
        //private string m_tooltipFormat = DefaultTooltipFormat;

        #region Parameters
        /// <summary>
        /// \~english Show long or short orders
        /// \~russian Показывать длинные заявки или короткие
        /// </summary>
        [HelperName("Show long", Constants.En)]
        [HelperName("Показывать длинные", Constants.Ru)]
        [Description("Показывать длинные заявки или короткие?")]
        [HelperDescription("Show long or short orders", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool ShowLongOrders
        {
            get { return m_showLongOrders; }
            set { m_showLongOrders = value; }
        }

        ///// <summary>
        ///// \~english Tooltip format (i.e. '0.00', '0.0##' etc)
        ///// \~russian Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.
        ///// </summary>
        //[HelperName("Tooltip Format", Constants.En)]
        //[HelperName("Формат подсказки", Constants.Ru)]
        //[Description("Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.")]
        //[HelperDescription("Tooltip format (i.e. '0.00', '0.0##' etc)", Constants.En)]
        //[HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultTooltipFormat)]
        //public string TooltipFormat
        //{
        //    get { return m_tooltipFormat; }
        //    set
        //    {
        //        if (!String.IsNullOrWhiteSpace(value))
        //        {
        //            try
        //            {
        //                string yStr = Math.PI.ToString(value);
        //                m_tooltipFormat = value;
        //            }
        //            catch
        //            {
        //                m_context.Log("Tooltip format error. I'll keep old one: " + m_tooltipFormat, MessageType.Warning, true);
        //            }
        //        }
        //    }
        //}
        #endregion Parameters

        public InteractiveSeries Execute(double price, double time, IOptionSeries optSer, int barNum)
        {
            var res = Execute(price, time, optSer, 0, barNum);
            return res;
        }

        public InteractiveSeries Execute(double price, double time, IOptionSeries optSer, double riskFreeRatePct, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return Constants.EmptySeries;

            // В оптимизации ничего рисовать не надо
            if (Context.IsOptimization)
                return Constants.EmptySeries;

            double futPx = price;
            double dT = time;

            if (!DoubleUtil.IsPositive(futPx))
            {
                // [{0}] Base asset price must be positive value. F:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.FutPxMustBePositive", GetType().Name, futPx);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (!DoubleUtil.IsPositive(dT))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            // TODO: Нужно ли писать отдельный код для лаборатории? Чтобы показывать позиции из симуляции?
            // if (!Context.Runtime.IsAgentMode)

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();

            var allRealtimeSecs = Context.Runtime.Securities;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            for (int j = 0; j < pairs.Length; j++)
            {
                IOptionStrikePair pair = pairs[j];

                #region Process put
                {
                    var put = pair.Put.Security;
                    // TODO: Нужно ли тут проверить наличие позиций???
                    //if (put.Positions.HavePositions)

                    ISecurityRt secRt;
                    if (put is ISecurityRt)
                        secRt = (ISecurityRt)put;
                    else
                    {
                        secRt = (from s in allRealtimeSecs
                                 where s.SecurityDescription.Equals(put) && (s is ISecurityRt)
                                 select (ISecurityRt)s).SingleOrDefault();
                    }

                    if ((secRt != null) && secRt.HasActiveOrders)
                    {
                        //secRt.SecurityDescription.TradePlace.DataSource
                        // ОТЛИЧНО! Эта коллекция позволит мне нарисовать свои заявки (это коллекция реальных заявок агента из таблицы My Orders)
                        var orders = secRt.Orders.ToList();
                        foreach (IOrder ord in orders)
                        {
                            if (!ord.IsActive)
                                continue;

                            // Объект ord является RealtimeOrder. Его идентификатор совпадает с OrderNumber в таблице MyOrders

                            if ((m_showLongOrders && ord.IsBuy) ||
                                ((!m_showLongOrders) && (!ord.IsBuy)))
                            {
                                // Почему-то InteractivePointLight хоть и давал себя настроить, но не отображался толком.
                                double sigma = FinMath.GetOptionSigma(futPx, pair.Strike, dT, ord.Price, riskFreeRatePct, false);
                                var ip = new InteractivePointActive(pair.Strike, sigma);
                                ip.Tooltip = String.Format(CultureInfo.InvariantCulture,
                                    " F: {0}\r\n K: {1}; IV: {2:P2}\r\n {3} px {4} qty {5}",
                                    futPx, pair.Strike, sigma, pair.Put.StrikeType, ord.Price, ord.RestQuantity);
                                controlPoints.Add(new InteractiveObject(ip));
                            }
                        }
                    }
                }
                #endregion Process put

                #region Process call
                {
                    var call = pair.Call.Security;
                    // TODO: Нужно ли тут проверить наличие позиций???
                    //if (call.Positions.HavePositions)

                    ISecurityRt secRt;
                    if (call is ISecurityRt)
                        secRt = (ISecurityRt)call;
                    else
                    {
                        secRt = (from s in allRealtimeSecs
                                 where s.SecurityDescription.Equals(call) && (s is ISecurityRt)
                                 select (ISecurityRt)s).SingleOrDefault();
                    }

                    if ((secRt != null) && secRt.HasActiveOrders)
                    {
                        // ОТЛИЧНО! Эта коллекция позволит мне нарисовать свои заявки (это коллекция реальных заявок агента из таблицы My Orders)
                        var orders = secRt.Orders.ToList();
                        foreach (IOrder ord in orders)
                        {
                            if (!ord.IsActive)
                                continue;

                            // Объект ord является RealtimeOrder. Его идентификатор совпадает с OrderNumber в таблице MyOrders

                            if ((m_showLongOrders && ord.IsBuy) ||
                                ((!m_showLongOrders) && (!ord.IsBuy)))
                            {
                                // Почему-то InteractivePointLight хоть и давал себя настроить, но не отображался толком.
                                double sigma = FinMath.GetOptionSigma(futPx, pair.Strike, dT, ord.Price, riskFreeRatePct, true);
                                var ip = new InteractivePointActive(pair.Strike, sigma);
                                ip.Tooltip = String.Format(CultureInfo.InvariantCulture,
                                    " F: {0}\r\n K: {1}; IV: {2:P2}\r\n {3} px {4} qty {5}",
                                    futPx, pair.Strike, sigma, pair.Call.StrikeType, ord.Price, ord.RestQuantity);
                                controlPoints.Add(new InteractiveObject(ip));
                            }
                        }
                    }
                }
                #endregion Process call
            } // End for (int j = 0; j < pairs.Length; j++)

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            return res;
        }
    }
}
