using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CompareOfFloatsByEqualityOperator
namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("StochK", Language = Constants.En)]
    [HelperName("StochK", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Стохастический осциллятор (Stochastic Oscillator) = (Цена закрытия текущего бара - Минимальное значение за период от минимума бара) / (Максимальное значение за период от максимума бара - Минимальное значение за период от минимума бара)  * 100. " +
        "Стохастический осциллятор измеряет насколько цена близка к своим верхним или нижним границам. Индикатор изменяется в диапазоне от 0 до 100.")]
    [HelperDescription("The Stochastic Oscillator = (Current bar closing price - Period minimum value calculated on bar minimum) / (Bar maximum value calculated on bar maximum - Bar minimum value based on bar minimum) * 100. " +
        "This indicator shows how close price is to its upper and lower borders.The indicator varies in the range from 0 to 100.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/StochK.xml", "Пример по индикатору Stochastic K", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/StochK.xml", "Example of Stochastic K", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class StochK : BasePeriodIndicatorHandler, IBar2DoubleHandler, IContextUses
    {
        public IList<double> Execute(ISecurity source)
        {
            var high = Context.GetData("Highest", new[] { Period.ToString(CultureInfo.InvariantCulture), source.CacheName },
                                       () => Series.Highest(source.GetHighPrices(Context), Period));
            var low = Context.GetData("Lowest", new[] { Period.ToString(CultureInfo.InvariantCulture), source.CacheName },
                                       () => Series.Lowest(source.GetLowPrices(Context), Period));
            var bars = source.Bars;
            var list = Context?.GetArray<double>(bars.Count) ?? new double[bars.Count];
            for (int i = 0; i < bars.Count; i++)
            {
                var hl = high[i] - low[i];
                var stochK = hl == 0 ? 0 : 100 * (bars[i].Close - low[i]) / hl;
                list[i] = stochK;
            }
            return list;
        }

        public IContext Context { get; set; }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    //[HandlerName("StochRSI")]
    [HelperName("Stoch RSI", Language = Constants.En)]
    [HelperName("Stoch RSI", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Стохастик от индекса относительной силы. StochRSI = (Текущее значение RSI - Минимальное значение RSI за период) / (Максимальное значение RSI за период - Минимальное значение RSI за период) * 100.")]
    [HelperDescription("Stochastics based on relative strength index. StochRSI = (Current RSI - period minimum RSI) / (Period maximum RSI - period minimum RSI) * 100.", Constants.En)]
    [SuppressMessage("StyleCopPlus.StyleCopPlusRules", "SP0100:AdvancedNamingRules", Justification = "Reviewed. Suppression is OK here.")]
    public sealed class StochRSI : BasePeriodIndicatorHandler, IBar2DoubleHandler, IContextUses
    {
        public IList<double> Execute(ISecurity source)
        {
            var rsi = Context.GetData("RSI", new[] { Period.ToString(CultureInfo.InvariantCulture), source.CacheName },
                                       () => Series.RSI(source.GetClosePrices(Context), Period));
            var high = Series.Highest(rsi, Period, Context);
            var low = Series.Lowest(rsi, Period, Context);
            var list = Context?.GetArray<double>(rsi.Count) ?? new double[rsi.Count];
            for (int i = 0; i < rsi.Count; i++)
            {
                var hl = high[i] - low[i];
                var stochRSI = hl == 0 ? 0 : 100 * (rsi[i] - low[i]) / hl;
                list[i] = stochRSI;
            }
            Context?.ReleaseArray((Array)high);
            Context?.ReleaseArray((Array)low);
            return list;
        }

        public IContext Context { get; set; }
    }
}
