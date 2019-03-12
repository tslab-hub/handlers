using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TSLab.DataSource;
using TSLab.Script.Handlers.Options;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable CompareOfFloatsByEqualityOperator
namespace TSLab.Script.Handlers
{
    #region Base

    // TODO: если мы хотим кубики Market Position удержать отдельной категорией, тогда надо завести новую константу
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.FOOTPRINT)]
    public abstract class MarketPositionBase : IFootPrintMaker
    {
        public IContext Context { get; set; }

        [HandlerParameter(true, "3", Min = "1", Max = "10", Step = "1")]
        public double ColorPower { get; set; }

        [HandlerParameter(true, "1", Min = "1", Max = "10", Step = "1")]
        public double CombineSteps { get; set; }

        private FootPrint m_fp;

        protected ITrade m_lastTrade;

        public FootPrint Execute(ISecurity source)
        {
            var count = source.Bars.Count;
            m_fp = new FootPrint(source, CombineSteps, AddTick);
            Func<int, IReadOnlyList<ITrade>> getTradesFunc;

            if (Context != null)
            {
                var tradesCache = Context.GetTradesCache(source);
                getTradesFunc = tradesCache.GetTrades;
            }
            else
                getTradesFunc = source.GetTrades;

            for (int i = 0; i < count; i++)
            {
                var bar = m_fp[i];
                var trades = getTradesFunc(i);
                ProcessTrades(bar, trades);
            }
            for (int i = 0; i < count; i++)
            {
                var bar = m_fp[i];
                UpdateColor(bar);
            }
            return m_fp;
        }

        private void ProcessTrades(FootPrint.FpBar bar, IEnumerable<ITrade> trades)
        {
            foreach (var trade in trades)
            {
                ProcessTrade(bar, trade);
                m_lastTrade = trade;
            }
        }

        private void AddTick(FootPrint.FpBar bar, IEnumerable<ITrade> barTrades)
        {
            bar.Lines.Clear();
            ProcessTrades(bar, barTrades);
            UpdateColor(bar);
        }

        protected double ConvertPriceValue(double v)
        {
            return ColorPower <= 0 ? v : Math.Log(v, ColorPower);
        }

        protected abstract void ProcessTrade(FootPrint.FpBar bar, ITrade trade);

        protected abstract void UpdateColor(FootPrint.FpBar bar);

        protected abstract FootPrint.Line GetLine(FootPrint.FpBar bar, ITrade trade);
    }

    #endregion

    #region Volume

    //[HandlerName("Volume Footprint")]
    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Volume cluster", Language = Constants.En)]
    [HelperName("Кластер объёма", Language = Constants.Ru)]
    [Description("Отображает сумму покупок и продаж для каждого шага или диапазона цены, в зависимости от настройки параметра 'Объединять шагов'.")]
    [HelperDescription("Volume footprint shows the sum of purchases and sales for every price step or price range, depending on value of a 'Combine Steps' parameter.", Constants.En)]
    public class VolumeMarketPosition : MarketPositionBase
    {
        public class VolumeLine : FootPrint.Line
        {
            public double Volume { get; set; }

            public override string ToString()
            {
                return Volume.ToString("F0");
            }
        }

        protected override void ProcessTrade(FootPrint.FpBar bar, ITrade trade)
        {
            var line = (VolumeLine)GetLine(bar, trade);
            line.Volume += trade.Quantity;
        }

        protected override void UpdateColor(FootPrint.FpBar bar)
        {
            double maxVolume = bar.Lines.Cast<VolumeLine>().Aggregate<VolumeLine, double>(0, (current, line) => Math.Max(current, line.Volume));
            var logMax = ConvertPriceValue(maxVolume);
            foreach (var line1 in bar.Lines)
            {
                var line = (VolumeLine)line1;
                var coef = line.Volume == 0 ? 0 : ConvertPriceValue(line.Volume) / logMax;
                var intCoef = Math.Min(120, (int)(coef * 120));
                line.Color = new Color(50, 50, 255 - intCoef);
            }
        }

        protected override FootPrint.Line GetLine(FootPrint.FpBar bar, ITrade trade)
        {
            return bar.GetLine<VolumeLine>(trade.Price);
        }
    }

    #endregion

    #region Bid/Ask

    //[HandlerName("Bid/Ask Footprint")]
    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Buy/Sell cluster", Language = Constants.En)]
    [HelperName("Кластер пок./прод.", Language = Constants.Ru)]
    [Description("Отображает количество покупок и продаж отдельно для каждого шага или диапазона цены, в зависимости от настройки параметра 'Объединять шагов'. В случае если покупок больше, строка окрашивается зеленым цветом, если меньше - красным. В случае равенства числа покупок и числа продаж строка окрашивается желтым цветом.")]
    [HelperDescription("Bid/Ask footprint shows the number of purchases and sales separately for every price step or price range, depending on value of a 'Combine Steps' parameter. If there are more purchases, the line turns green, if less, the line turns red. In case the numbers of purchases and sales are equal, the line turns yellow.", Constants.En)]
    public class BidAskMarketPosition : MarketPositionBase
    {
        public class BidAskLine : FootPrint.Line
        {
            public double AskVolume { get; set; }
            public double BidVolume { get; set; }

            public override string ToString()
            {
                return String.Format("{0:F0}x{1:F0}", AskVolume, BidVolume);
            }
        }

        #region ProcessTrade

        private bool m_lastWasAsk;

        protected override void ProcessTrade(FootPrint.FpBar bar, ITrade trade)
        {
            var line = (BidAskLine)GetLine(bar, trade);
            bool isProcessed = false;
            if (trade.Direction != TradeDirection.Unknown)
            {
                if (trade.Direction == TradeDirection.Buy)
                {
                    line.AskVolume += trade.Quantity;
                }
                else
                {
                    line.BidVolume += trade.Quantity;
                }
                isProcessed = true;
            }
            else if (trade is ITradeWithBidAsk)
            {
                var trd = trade as ITradeWithBidAsk;
                if (trd.AskPrice > 0 && trd.BidPrice > 0)
                {
                    if (Math.Abs(trd.AskPrice - trade.Price) < Math.Abs(trd.BidPrice - trade.Price))
                    {
                        line.AskVolume += trade.Quantity;
                    }
                    else
                    {
                        line.BidVolume += trd.Quantity;
                    }
                    isProcessed = true;
                }
                else if (trd.AskPrice > 0)
                {
                    line.AskVolume += trade.Quantity;
                    isProcessed = true;
                }
                else if (trd.BidPrice > 0)
                {
                    line.BidVolume += trd.Quantity;
                    isProcessed = true;
                }
            }
            if (!isProcessed)
            {
                var lastPrice = m_lastTrade == null ? 0.0 : m_lastTrade.Price;
                if (trade.Price > lastPrice)
                {
                    line.AskVolume += trade.Quantity;
                    m_lastWasAsk = true;
                }
                else if (trade.Price < lastPrice)
                {
                    line.BidVolume += trade.Quantity;
                    m_lastWasAsk = false;
                }
                else
                {
                    if (m_lastWasAsk)
                    {
                        line.AskVolume += trade.Quantity;
                    }
                    else
                    {
                        line.BidVolume += trade.Quantity;
                    }
                }
            }
        }

        #endregion

        #region UpdateColor

        protected override void UpdateColor(FootPrint.FpBar bar)
        {
            double maxVolume = 0;
            foreach (var line1 in bar.Lines)
            {
                var line = (BidAskLine)line1;
                maxVolume = Math.Max(maxVolume, line.AskVolume);
                maxVolume = Math.Max(maxVolume, line.BidVolume);
            }
            var logMax = ConvertPriceValue(maxVolume);
            foreach (var line1 in bar.Lines)
            {
                var line = (BidAskLine)line1;
                int r = 50;
                int g = 50;
                if (line.AskVolume >= line.BidVolume)
                {
                    var coef = line.AskVolume == 0 ? 0 : ConvertPriceValue(line.AskVolume) / logMax;
                    g = 255 - Math.Min(120, (int)(coef * 120));
                }
                if (line.AskVolume <= line.BidVolume)
                {
                    var coef = line.BidVolume == 0 ? 0 : ConvertPriceValue(line.BidVolume) / logMax;
                    r = 255 - Math.Min(120, (int)(coef * 120));
                }
                line.Color = new Color(r, g, 50);
            }
        }

        #endregion

        protected override FootPrint.Line GetLine(FootPrint.FpBar bar, ITrade trade)
        {
            return bar.GetLine<BidAskLine>(trade.Price);
        }
    }

    #endregion

    #region Delta

    //[HandlerName("Delta Footprint")]
    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Delta cluster", Language = Constants.En)]
    [HelperName("Дельта кластер", Language = Constants.Ru)]
    [Description("Отображает разность числа покупок и числа продаж отдельно для каждого шага или диапазона цены, в зависимости от настройки параметра 'Объединять шагов'. В случае если покупок больше, строка окрашивается зеленым цветом, если меньше - красным цветом. В случае равенства числа покупок и числа продаж строка окрашивается желтым.")]
    [HelperDescription("Delta Footprint shows difference in the number of purchases and sales separately for every price step or price range, depending on value of a 'Combine Steps' parameter. If there are more purchases, the line turns green, if less, the line turns red. In case the numbers of sales and purchases are equal, the line turns yellow.", Constants.En)]
    public class DeltaMarketPosition : BidAskMarketPosition
    {
        public class DeltaLine : BidAskLine
        {
            public override string ToString()
            {
                return String.Format("{0:F0}", AskVolume - BidVolume);
            }
        }

        protected override FootPrint.Line GetLine(FootPrint.FpBar bar, ITrade trade)
        {
            return bar.GetLine<DeltaLine>(trade.Price);
        }
    }

    #endregion
}
