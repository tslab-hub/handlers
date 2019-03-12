using System.Collections.Generic;
using System.ComponentModel;
using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;

namespace TSLab.Script.Handlers
{
    /// <summary>
    /// Базовый класс для группы кубиков, которые вычисляют сколько прошло баров с предыдущего экстремума
    /// </summary>
    [HandlerCategory(HandlerCategories.TradeMath)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public abstract class ExtremePos : IDouble2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        /// <summary>
        /// \~english Indicator period (processing window)
        /// \~russian Период индикатора (окно расчетов)
        /// </summary>
        [HelperName("Period", Constants.En)]
        [HelperName("Период", Constants.Ru)]
        [Description("Период индикатора (окно расчетов)")]
        [HelperDescription("Indicator period (processing window)", Constants.En)]
        [HandlerParameter(true, "20", Min = "10", Max = "100", Step = "5", EditorMin = "1")]
        public int Period { get; set; }

        public IList<double> Execute(IList<double> source)
        {
            var extremeValues = GetExtremeValues(source);
            var result = Context?.GetArray<double>(source.Count) ?? new double[source.Count];

            for (var i = 1; i < result.Length; i++)
            {
                var k = 0;
                while (i - k >= 0 && extremeValues[i] != source[i - k])
                    k++;

                result[i] = k;
            }
            return result;
        }

        protected abstract IList<double> GetExtremeValues(IList<double> source);
    }

    //[HandlerCategory(HandlerCategories.TradeMath)]
    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Bars since last high", Language = Constants.En)]
    [HelperName("Баров с последнего максимума", Language = Constants.Ru)]
    [Description("Количество баров, прошедшее с момента последнего обновления максимума.")]
    [HelperDescription("The number of bars since the latest high.", Constants.En)]
    public sealed class HighestPos : ExtremePos
    {
        protected override IList<double> GetExtremeValues(IList<double> source)
        {
            return Series.Highest(source, Period, Context);
        }
    }

    //[HandlerCategory(HandlerCategories.TradeMath)]
    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Bars since last low", Language = Constants.En)]
    [HelperName("Баров с последнего минимума", Language = Constants.Ru)]
    [Description("Количество баров, прошедшее с момента последнего обновления минимума.")]
    [HelperDescription("The number of bars since the latest low.", Constants.En)]
    public sealed class LowestPos : ExtremePos
    {
        protected override IList<double> GetExtremeValues(IList<double> source)
        {
            return Series.Lowest(source, Period, Context);
        }
    }
}
