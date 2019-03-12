using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
namespace TSLab.Script.Handlers
{
    public class MACDBase : IContextUses
    {
        protected IList<double> CalcMACD(IList<double> source, int p1, int p2)
        {
            var ema1 = Series.EMA(source, p1, Context);
            var ema2 = Series.EMA(source, p2, Context);
            var res = Context?.GetArray<double>(ema1.Count) ?? new double[ema1.Count];
            for (int i = 0; i < ema1.Count; i++)
            {
                res[i] = ema1[i] - ema2[i];
            }
            return res;
        }

        public IContext Context { get; set; }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("MACD", Language = Constants.En)]
    [HelperName("MACD", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Технический индикатор 'Схождение / Расхождение скользящих средних' - это следующий за тенденцией динамический индикатор. Показывает соотношение между двумя скользящими средними цены. Параметры мувингов фиксированы (12 и 26).")]
    [HelperDescription("The Moving Average Convergence/Divergence. This indicator spots trend changes. Shows a ratio of two moving average values of price. Parameters of movings are hardcoded (12 and 26).", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD.xml", "Пример по индикатору MACD", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD.xml", "Example of MACD", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class MACD : MACDBase, IDouble2DoubleHandler
    {
        public IList<double> Execute(IList<double> source)
        {
            return CalcMACD(source, 12, 26);
        }
    }

    //[HandlerName("MACD Ext")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("MACD Ext", Language = Constants.En)]
    [HelperName("MACD расшир.", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("MACD с возможностью менять параметры мувингов и использовать их в оптимизаторе (Первая EMA - Вторая EMA).")]
    [HelperDescription("The Moving Average Convergence/Divergence with calculation periods unlocked for optimization (First EMA - Second EMA).", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD.xml", "Пример по индикатору MACD", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD.xml", "Example of MACD", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class MACDEx : MACDBase, IDouble2DoubleHandler
    {
        /// <summary>
        /// \~english First EMA period
        /// \~russian Период первого мувинга (типа EMA)
        /// </summary>
        [HelperName("First period", Constants.En)]
        [HelperName("Первый период", Constants.Ru)]
        [Description("Период первого мувинга (типа EMA)")]
        [HelperDescription("First EMA period", Constants.En)]
        [HandlerParameter(true, "12", Min = "5", Max = "40", Step = "1")]
        public int Period1 { get; set; }

        /// <summary>
        /// \~english Second EMA period
        /// \~russian Период второго мувинга (типа EMA)
        /// </summary>
        [HelperName("Second period", Constants.En)]
        [HelperName("Второй период", Constants.Ru)]
        [Description("Период второго мувинга (типа EMA)")]
        [HelperDescription("Second EMA period", Constants.En)]
        [HandlerParameter(true, "26", Min = "10", Max = "40", Step = "1")]
        public int Period2 { get; set; }

        public IList<double> Execute(IList<double> source)
        {
            return CalcMACD(source, Period1, Period2);
        }
    }

    //[HandlerName("MACD Signal")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("MACD Signal", Language = Constants.En)]
    [HelperName("MACD сигнал", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Сигнальная линия MACD.")]
    [HelperDescription("The MACD signal line.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD.xml", "Пример по индикатору MACD", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD.xml", "Example of MACD", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class MACDSig : IDouble2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        /// <summary>
        /// \~english Signal EMA period
        /// \~russian Период сигнального мувинга (типа EMA)
        /// </summary>
        [HelperName("Period", Constants.En)]
        [HelperName("Период", Constants.Ru)]
        [Description("Период сигнального мувинга (типа EMA)")]
        [HelperDescription("Signal EMA period", Constants.En)]
        [HandlerParameter(true, "9", Min = "3", Max = "20", Step = "1", EditorMin = "1")]
        public int Period
        {
            get;
            set;
        }

        public IList<double> Execute(IList<double> source)
        {
            return Series.EMA(source, Period, Context);
        }
    }
}
// ReSharper restore MemberCanBePrivate.Global
// ReSharper restore UnusedAutoPropertyAccessor.Global
// ReSharper restore UnusedMember.Global
