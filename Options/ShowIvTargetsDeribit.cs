using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Show volatility limit orders (special for Deribit)
    /// \~russian Показать котировки по волатильности
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTickDeribit)]
    [HelperName("Show IV Targets (Deribit)", Language = Constants.En)]
    [HelperName("Заявки по волатильности (Deribit)", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(1, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = "Quote IV")]
    [Input(3, TemplateTypes.DOUBLE, Name = "ScaleMultiplier" /* Constants.Scale */)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Показать котировки по волатильности (c учетом необходимости домножить цены на курс конвертации)")]
    [HelperDescription("Show volatility limit orders (special for Deribit)", Constants.En)]
    public class ShowIvTargetsDeribit : BaseContextHandler, IValuesHandlerWithNumber, IDisposable
    {
        /// <summary>Показывать длинные заявки</summary>
        private bool m_isLong = false;

        private InteractiveSeries m_clickableSeries;

        #region Parameters
        /// <summary>
        /// \~english Show long orders
        /// \~russian Показывать длинные заявки
        /// </summary>
        [HelperName("Long", Constants.En)]
        [HelperName("Длинные", Constants.Ru)]
        [Description("Показывать длинные заявки")]
        [HelperDescription("Show long orders", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool IsLong
        {
            get { return m_isLong; }
            set { m_isLong = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.INTERACTIVESPLINE
        /// </summary>
        public InteractiveSeries Execute(InteractiveSeries smile, IOptionSeries optSer, InteractiveSeries quoteIv, double scaleMult, int barNum)
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

            // 1. Формируем маркеры заявок
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            IList<PositionsManager.IvTargetInfo> ivTargets = posMan.GetIvTargets(m_isLong, true);
            for (int j = 0; j < ivTargets.Count; j++)
            {
                var ivTarget = ivTargets[j];

                // PROD-6102 - Требуется точное совпадение опционной серии
                if (optSer.ExpirationDate.Date != ivTarget.SecInfo.Expiry.Date)
                {
                    // Вывести предупреждение???
                    continue;
                }

                IOptionStrikePair pair;
                double k = ivTarget.SecInfo.Strike;
                if (!optSer.TryGetStrikePair(k, out pair))
                {
                    // Вывести предупреждение???
                    continue;
                }

                double sigma;
                QuoteIvMode quoteMode = ivTarget.QuoteMode;
                if (quoteMode == QuoteIvMode.Absolute)
                    sigma = ivTarget.EntryIv;
                else
                {
                    sigma = oldInfo.ContinuousFunction.Value(k) + ivTarget.EntryIv;
                    if (!DoubleUtil.IsPositive(sigma))
                    {
                        //string msg = String.Format("[DEBUG:{0}] Invalid sigma:{1} for strike:{2}", GetType().Name, sigma, nodeInfo.Strike);
                        //m_context.Log(msg, MessageType.Warning, true);
                        continue;
                    }
                }

                bool isCall = (futPx <= k);
                if ((ivTarget.SecInfo.StrikeType != null) &&
                    (ivTarget.SecInfo.StrikeType.Value != StrikeType.Any))
                {
                    isCall = (ivTarget.SecInfo.StrikeType.Value == StrikeType.Call);
                }
                StrikeType optionType = isCall ? StrikeType.Call : StrikeType.Put;
                Contract.Assert(pair.Tick < 1, $"На тестовом контуре Дерибит присылает неправильный шаг цены! Tick:{pair.Tick}; Decimals:{pair.Put.Security.Decimals}");
                double theorOptPxDollars = FinMath.GetOptionPrice(futPx, pair.Strike, dT, sigma, oldInfo.RiskFreeRate, isCall);
                // Сразу(!!!) переводим котировку из баксов в битки
                double theorOptPxBitcoins = theorOptPxDollars / scaleMult;

                // Сдвигаем цену в долларах (с учетом ш.ц. в баксах)
                theorOptPxDollars += ivTarget.EntryShiftPrice * pair.Tick * scaleMult;
                theorOptPxDollars = Math.Round(theorOptPxDollars / (pair.Tick * scaleMult)) * (pair.Tick * scaleMult);

                // Сдвигаем цену в биткойнах (с учетом ш.ц. в битках)
                theorOptPxBitcoins += ivTarget.EntryShiftPrice * pair.Tick;
                theorOptPxBitcoins = Math.Round(theorOptPxBitcoins / pair.Tick) * pair.Tick;
                if ((!DoubleUtil.IsPositive(theorOptPxBitcoins)) || (!DoubleUtil.IsPositive(theorOptPxDollars)))
                {
                    //string msg = String.Format("[DEBUG:{0}] Invalid theorOptPx:{1} for strike:{2}", GetType().Name, theorOptPx, nodeInfo.Strike);
                    //m_context.Log(msg, MessageType.Warning, true);
                    continue;
                }

                // Пересчитываем сигму обратно, ЕСЛИ мы применили сдвиг цены в абсолютном выражении
                if (ivTarget.EntryShiftPrice != 0)
                {
                    sigma = FinMath.GetOptionSigma(futPx, pair.Strike, dT, theorOptPxDollars, oldInfo.RiskFreeRate, isCall);
                    if (!DoubleUtil.IsPositive(sigma))
                    {
                        //string msg = String.Format("[DEBUG:{0}] Invalid sigma:{1} for strike:{2}", GetType().Name, sigma, nodeInfo.Strike);
                        //m_context.Log(msg, MessageType.Warning, true);
                        continue;
                    }
                }

                double totalQty;
                if (isCall)
                    totalQty = posMan.GetTotalQty(pair.Call.Security, m_context.BarsCount, TotalProfitAlgo.AllPositions, ivTarget.IsLong);
                else
                    totalQty = posMan.GetTotalQty(pair.Put.Security, m_context.BarsCount, TotalProfitAlgo.AllPositions, ivTarget.IsLong);
                double targetQty = Math.Abs(ivTarget.TargetShares) - totalQty;

                // ReSharper disable once UseObjectOrCollectionInitializer
                InteractivePointActive tmp = new InteractivePointActive();

                // Попробуем по-простому?
                tmp.Tag = ivTarget;

                tmp.IsActive = true;
                tmp.ValueX = k;
                tmp.ValueY = sigma;
                tmp.DragableMode = DragableMode.None;
                if (ivTarget.EntryShiftPrice == 0)
                {
                    tmp.Tooltip = String.Format(CultureInfo.InvariantCulture,
                        " F: {0}\r\n K: {1}; IV: {2:P2}\r\n {3} px {4} rIV {5:P2} @ {6}",
                        futPx, k, sigma, optionType, theorOptPxBitcoins, ivTarget.EntryIv, targetQty);
                }
                else
                {
                    string shiftStr = (ivTarget.EntryShiftPrice > 0) ? "+" : "-";
                    shiftStr = shiftStr + Math.Abs(ivTarget.EntryShiftPrice) + "ps";
                    tmp.Tooltip = String.Format(CultureInfo.InvariantCulture,
                        " F: {0}\r\n K: {1}; IV: {2:P2}\r\n {3} px {4} rIV {5:P2} {6} @ {7}",
                        futPx, k, sigma, optionType, theorOptPxBitcoins, ivTarget.EntryIv, shiftStr, targetQty);
                }

                //tmp.Color = Colors.White;
                //if (m_qty > 0)
                //    tmp.Geometry = Geometries.Triangle;
                //else if (m_qty < 0)
                //    tmp.Geometry = Geometries.TriangleDown;
                //else
                //    tmp.Geometry = Geometries.None;

                InteractiveObject obj = new InteractiveObject();
                obj.Anchor = tmp;

                controlPoints.Add(obj);
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            if (controlPoints.Count > 0)
            {
                res.ClickEvent -= InteractiveSplineOnClickEvent;
                res.ClickEvent += InteractiveSplineOnClickEvent;

                m_clickableSeries = res;
            }

            return res;
        }

        private void InteractiveSplineOnClickEvent(object sender, InteractiveActionEventArgs eventArgs)
        {
            OptionPxMode pxMode = m_isLong ? OptionPxMode.Ask : OptionPxMode.Bid;

            PositionsManager posMan = PositionsManager.GetManager(m_context);
            // Из практики торговли часто бывает ситуация, что торговля заблокирована, а снять задачу котирования УЖЕ хочется.
            //if (posMan.BlockTrading)
            //{
            //    //string msg = String.Format("[{0}] Trading is blocked. Please, change 'Block Trading' parameter.", m_optionPxMode);
            //    string msg = String.Format(RM.GetString("OptHandlerMsg.PositionsManager.TradingBlocked"), pxMode);
            //    m_context.Log(msg, MessageType.Info, true);
            //    return;
            //}

            InteractivePointActive tmp = eventArgs.Point;
            if ((tmp.Tag == null) || (!(tmp.Tag is PositionsManager.IvTargetInfo)))
            {
                string msg = String.Format("[{0}.ClickEvent] Denied #1", GetType().Name);
                m_context.Log(msg, MessageType.Warning, false);
                return;
            }

            {
                string msg = String.Format(CultureInfo.InvariantCulture, "[{0}.ClickEvent] Strike: {1}", GetType().Name, eventArgs.Point.ValueX);
                m_context.Log(msg, MessageType.Warning, false);
            }

            var ivTarget = tmp.Tag as PositionsManager.IvTargetInfo;

            // Передаю событие в PositionsManager
            //posMan.InteractiveSplineOnQuoteIvEvent(m_context, sender, eventArgs);
            // ОЧЕНЬ БОЛЬШОЙ ВОПРОС ЧТО БУДЕТ С КОНТЕКСТОМ ПРИ ЭТОМ???
            int res = posMan.CancelVolatility(m_context, ivTarget, "Left-click");
            if (res > 0)
            {
                string msg = String.Format(RM.GetString("OptHandlerMsg.PositionsManager.IvTargetCancelled"), pxMode, ivTarget);
                m_context.Log(msg, MessageType.Info, true, new Dictionary<string, object> { { "VOLATILITY_ORDER_CANCELLED", msg } });

                // Вызываем принудительный пересчет агента, чтобы немедленно убрать заявку из стакана
                m_context.Recalc();
            }
        }

        public void Dispose()
        {
            if (m_clickableSeries != null)
            {
                m_clickableSeries.ClickEvent -= InteractiveSplineOnClickEvent;
                //m_clickableSeries.EndDragEvent -= res_EndDragEvent;

                m_clickableSeries = null;
            }
        }
    }
}
