using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    //[HandlerCategory(HandlerCategories.ClusterAnalysis)]
    // Не стоит навешивать на абстрактные классы атрибуты категорий и описания входов/выходов. Это снижает гибкость управления в перспективе.

    /// <summary>
    /// Базовый класс для расчета экстремумов торговой стаистистики.
    /// На вход подается Торговая Статистика, на выход идут числа.
    /// </summary>
    public abstract class TradeStatisticsBaseExtremumPriceHandler : ITradeStatisticsBaseExtremumPriceHandler
    {
        protected sealed class Extremum
        {
            public Extremum(ITradeHistogramBar bar, double value, double price)
            {
                Bar = bar;
                Value = value;
                Price = price;
            }

            public ITradeHistogramBar Bar { get; }
            public double Value { get; }
            public double Price { get; }
        }

        public string VariableId { get; set; }

        public IContext Context { get; set; }

        /// <summary>
        /// \~english Extremum kind (minimum, maximum).
        /// \~russian Вид экстремума (минимум, максимум).
        /// </summary>
        [HelperName("Extremum kind", Constants.En)]
        [HelperName("Вид экстремума", Constants.Ru)]
        [Description("Вид экстремума (минимум, максимум)")]
        [HelperDescription("Extremum kind (minimum, maximum).", Constants.En)]
        [HandlerParameter(true, nameof(ExtremumPriceMode.Minimum))]
        public ExtremumPriceMode PriceMode { get; set; }

        public abstract IList<double> Execute(IBaseTradeStatisticsWithKind tradeStatistics);

        protected Extremum GetExtremum(IBaseTradeStatisticsWithKind tradeStatistics, int barIndex, ref double lastPrice)
        {
            var bars = tradeStatistics.GetAggregatedHistogramBars(barIndex);
            if (bars.Count == 0)
                return new Extremum(null, double.NaN, lastPrice);

            if (bars.Count == 1)
            {
                var bar = bars[0];
                return new Extremum(bar, Math.Abs(tradeStatistics.GetValue(bar)), lastPrice = bar.AveragePrice);
            }
            IEnumerable<ITradeHistogramBar> orderedBars;
            switch (PriceMode)
            {
                case ExtremumPriceMode.Minimum:
                    orderedBars = bars;
                    break;
                case ExtremumPriceMode.Maximum:
                    orderedBars = bars.Reverse();
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(PriceMode), (int)PriceMode, PriceMode.GetType());
            }
            var extremumBar = orderedBars.First();
            var extremumValue = Math.Abs(tradeStatistics.GetValue(extremumBar));

            foreach (var bar in orderedBars.Skip(1))
            {
                var value = Math.Abs(tradeStatistics.GetValue(bar));
                if (extremumValue < value)
                {
                    extremumBar = bar;
                    extremumValue = value;
                }
            }
            return new Extremum(extremumBar, extremumValue, lastPrice = extremumBar.AveragePrice);
        }

        protected virtual string GetParametersStateId()
        {
            return PriceMode.ToString();
        }
    }
}
