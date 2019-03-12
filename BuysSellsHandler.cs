using System;
using System.Collections.Generic;
using System.ComponentModel;
using TSLab.Script.Handlers.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers
{
    //[HandlerCategory(HandlerCategories.VolumeAnalysis)]
    // Не стоит навешивать на абстрактные классы атрибуты категорий и описания входов/выходов. Это снижает гибкость управления в перспективе.
    public abstract class BuysSellsHandler : IBuysSellsHandler
    {
        public IContext Context { get; set; }

        /// <summary>
        /// \~english Market volume quantity units (shares, lots, trades count)
        /// \~russian Режим отображения рыночной статистики (объем, лоты, количество сделок)
        /// </summary>
        [HelperName("Quantity units", Constants.En)]
        [HelperName("Единицы объема", Constants.Ru)]
        [Description("Режим отображения рыночной статистики (объем, лоты, количество сделок)")]
        [HelperDescription("Market volume quantity units (shares, lots, trades count)", Constants.En)]
        [HandlerParameter(true, nameof(QuantityMode.Quantity))]
        public QuantityMode QuantityMode { get; set; }

        public IList<double> Execute(ISecurity security)
        {
            if (security == null)
                throw new ArgumentNullException(nameof(security));

            var quantityMode = QuantityMode;
            if (quantityMode != QuantityMode.Quantity && quantityMode != QuantityMode.QuantityInLots && quantityMode != QuantityMode.TradesCount)
                throw new InvalidEnumArgumentException(nameof(QuantityMode), (int)quantityMode, quantityMode.GetType());

            var barsCount = security.Bars.Count;
            if (barsCount == 0)
                return EmptyArrays.Double;

            var results = Context?.GetArray<double>(barsCount) ?? new double[barsCount];
            var tradeHistogramsCache = TradeHistogramsCaches.Instance.GetTradeHistogramsCache(Context, security, 0);

            if (quantityMode == QuantityMode.Quantity)
                for (var i = 0; i < barsCount; i++)
                    results[i] = GetValue(tradeHistogramsCache.GetHistogram(i));
            else if (quantityMode == QuantityMode.QuantityInLots)
                for (var i = 0; i < barsCount; i++)
                    results[i] = GetValue(tradeHistogramsCache.GetHistogram(i)) / security.LotSize;
            else
                for (var i = 0; i < barsCount; i++)
                    results[i] = GetCount(tradeHistogramsCache.GetHistogram(i));

            return results;
        }

        protected abstract double GetValue(ICachedTradeHistogram histogram);

        protected abstract int GetCount(ICachedTradeHistogram histogram);
    }
}
