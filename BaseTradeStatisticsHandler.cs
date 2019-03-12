using System.ComponentModel;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    //[HandlerCategory(HandlerCategories.ClusterAnalysis)]
    // Не стоит навешивать на абстрактные классы атрибуты категорий и описания входов/выходов. Это снижает гибкость управления в перспективе.
    public abstract class BaseTradeStatisticsHandler<TTradeStatisticsWithKind> : IBaseTradeStatisticsHandler<TTradeStatisticsWithKind>
        where TTradeStatisticsWithKind : IBaseTradeStatisticsWithKind
    {
        public string VariableId { get; set; } = string.Empty;

        public IContext Context { get; set; }

        /// <summary>
        /// \~english How many price steps should be grouped together.
        /// \~russian Осуществление выбора шага цены, используемого для группировки цен.
        /// </summary>
        [HelperName("Combine price steps", Constants.En)]
        [HelperName("Объединять шаги цены", Constants.Ru)]
        [Description("Осуществление выбора шага цены, используемого для группировки цен.")]
        [HelperDescription("How many price steps should be grouped together.", Constants.En)]
        [HandlerParameter(true, "1", Min = "1", Max = "10", Step = "1", EditorMin = "1")]
        public int CombinePricesCount { get; set; }

        /// <summary>
        /// \~english Kind of a trade statistics (trades count, trade volume, buy count, sell count, buy and sell difference, relative buy and sell difference).
        /// \~russian Вид торговой статистики (количество сделок, объем торгов, количество покупок, количество продаж, разница количества покупок и продаж, относительная разница количества покупок и продаж).
        /// </summary>
        [HelperName("Kind", Constants.En)]
        [HelperName("Вид", Constants.Ru)]
        [Description("Вид торговой статистики (количество сделок, объем торгов, количество покупок, количество продаж, разница количества покупок и продаж, относительная разница количества покупок и продаж).")]
        [HelperDescription("Kind of a trade statistics (trades count, trade volume, buy count, sell count, buy and sell difference, relative buy and sell difference).", Constants.En)]
        [HandlerParameter(true, nameof(TradeStatisticsKind.TradesCount))]
        public TradeStatisticsKind Kind { get; set; }

        /// <summary>
        /// \~english A width of a hystogram relative to a width of a chart pane.
        /// \~russian Ширина гистограммы в процентах относительно ширины панели графика.
        /// </summary>
        [HelperName("Width, %", Constants.En)]
        [HelperName("Ширина, %", Constants.Ru)]
        [Description("Ширина гистограммы в процентах относительно ширины панели графика.")]
        [HelperDescription("A width of a hystogram relative to a width of a chart pane.", Constants.En)]
        [HandlerParameter(true, "100", Min = "1", Max = "100", Step = "1", EditorMin = "1", EditorMax = "100")]
        public virtual double WidthPercent { get; set; }

        public abstract TTradeStatisticsWithKind Execute(ISecurity security);

        protected ITradeHistogramsCache GetTradeHistogramsCache(ISecurity security)
        {
            var result = TradeHistogramsCaches.Instance.GetTradeHistogramsCache(Context, security, CombinePricesCount);
            return result;
        }
    }
}
