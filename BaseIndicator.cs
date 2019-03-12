using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TSLab.DataSource;
using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;
using TSLab.Utils;

namespace TSLab.Script.Handlers
{
    /// <summary>
    /// Базовый класс для индикаторов и кубиков с параметром Период (целочисленным)
    /// </summary>
    public abstract class BasePeriodIndicatorHandler
    {
        private int m_period = 1;

        /// <summary>
        /// \~english Indicator period (processing window)
        /// \~russian Период индикатора (окно расчетов)
        /// </summary>
        [HelperName("Period", Constants.En)]
        [HelperName("Период", Constants.Ru)]
        [Description("Период индикатора (окно расчетов)")]
        [HelperDescription("Indicator period (processing window)", Constants.En)]
        [HandlerParameter(true, "20", Min = "10", Max = "100", Step = "5", EditorMin = "1")]
        public int Period
        {
            get { return m_period; }
            set { m_period = Math.Max(value, 1); }
        }
    }

    public abstract class BaseRSI<THandler> : DoubleStreamAndValuesHandlerWithPeriod
        where THandler : DoubleStreamAndValuesHandlerWithPeriod, new()
    {
        private double m_lastSource;
        private THandler m_uHandler;
        private THandler m_dHandler;

        public override bool IsGapTolerant
        {
            get { return false; }
        }

        protected override void InitExecuteContext()
        {
            m_lastSource = 0;
            m_uHandler = new THandler { Context = Context, Period = Period };
            m_dHandler = new THandler { Context = Context, Period = Period };
        }

        protected override void ClearExecuteContext()
        {
            m_lastSource = 0;
            m_uHandler = null;
            m_dHandler = null;
        }

        protected override void InitForGap()
        {
            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - GapTolerancePeriodMultiplier * Period);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                var source = m_executeContext.GetSourceForGap(i);
                double uEma, dEma;
                Precalc(source, i, out uEma, out dEma);
            }
        }

        protected override double Execute()
        {
            double uEma, dEma;
            Precalc(m_executeContext.Source, m_executeContext.Index, out uEma, out dEma);
            double result;

            if (dEma == 0.0)
                result = 100;
            else if (uEma / dEma == 1.0)
                result = 0;
            else
                result = 100 - 100 / (1 + uEma / dEma);

            return result;
        }

        private void Precalc(double source, int index, out double uEma, out double dEma)
        {
            double u = 0, d = 0;
            if (index > 0)
            {
                if (m_lastSource < source)
                    u = source - m_lastSource;
                else if (m_lastSource > source)
                    d = m_lastSource - source;
            }
            m_lastSource = source;
            uEma = m_uHandler.Execute(u, index);
            dEma = m_dHandler.Execute(d, index);
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("RSI", Language = Constants.En)]
    [HelperName("RSI", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индекс относительной силы (Relative strength index), следующий за ценами осциллятор, который колеблется в диапазоне от 0 до 100.")]
    [HelperDescription("The Relative Strength Index is a momentum oscillator, measuring velocity and magnitude of directional price movements.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/script_AND.xml", "Пример по RSI и Bollinger", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/script_AND.xml", "Example of RSI and Bollinger", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class RSI : BaseRSI<EMA>
    {
        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.RSI(source, Period, Context);
            return result;
        }
    }

    //[HandlerName("Cutler's RSI")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Cutler's RSI", Language = Constants.En)]
    [HelperName("RSI Катлера", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индекс относительной силы (Cutler's Relative strength index) это версия индикатора, использующая экспоненциональное сглаживание. Следующий за ценами осциллятор, который колеблется в диапазоне от 0 до 100.")]
    [HelperDescription("A variation called the Cutler's RSI, based on a simple moving average. This is a momentum oscillator, measuring velocity and magnitude of directional price movements.", Constants.En)]
    [SuppressMessage("StyleCopPlus.StyleCopPlusRules", "SP0100:AdvancedNamingRules", Justification = "Reviewed. Suppression is OK here.")]
    public sealed class CuttlerRSI : BaseRSI<SMA>
    {
        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.CuttlerRSI(source, Period, Context);
            return result;
        }
    }

    public abstract class SumBase : DoubleStreamAndValuesHandlerWithPeriod
    {
        private readonly bool m_isSma;
        private Queue<double> m_queue;
        private double m_sum;

        protected SumBase(bool isSma)
        {
            m_isSma = isSma;
        }

        public override bool IsGapTolerant
        {
            get { return true; }
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                return;

            m_queue = new Queue<double>(Period);
            m_sum = 0;
        }

        protected override void ClearExecuteContext()
        {
            m_queue = null;
            m_sum = 0;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                return;

            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - Period + 1);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                var source = m_executeContext.GetSourceForGap(i);
                var result = Calc(source);
            }
        }

        protected override double Execute()
        {
            if (IsSimple)
                return m_executeContext.Source;

            var result = Calc(m_executeContext.Source);
            return result;
        }

        private double Calc(double source)
        {
            if (m_queue.Count == Period)
                m_sum -= m_queue.Dequeue();

            m_queue.Enqueue(source);
            m_sum += source;
            return m_isSma ? m_sum / m_queue.Count : m_sum;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Sum in", Language = Constants.En)]
    [HelperName("Сумма за", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Рассчитывается путем сложения входящих значений, например, цен закрытия инструмента за определенный период.")]
    [HelperDescription("Sums up incoming values, for example, this indicator can sum up instrument close prices in some period.", Constants.En)]
    public sealed class SummFor : SumBase
    {
        public SummFor()
            : base(false)
        {
        }

        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.SummFor(source, Period, Context);
            return result;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("SMA", Language = Constants.En)]
    [HelperName("SMA", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Простое (арифметическое) скользящее среднее. Рассчитывается путем сложения входящих значений, например, цен закрытия инструмента за определенный период, затем полученная сумма делится на значение периода.")]
    [HelperDescription("The Simple Moving Average is calculated by summing up incoming values, for example, instrument close prices of some period. The result is divided by a period value.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/StochK.xml", "Пример по индикатору Stochastic K", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/StochK.xml", "Example of Stochastic K", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class SMA : SumBase
    {
        public SMA()
            : base(true)
        {
        }

        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.SMA(source, Period, Context);
            return result;
        }
    }

    public abstract class StDevVolatility : DoubleStreamAndValuesHandlerWithPeriod
    {
        protected ShrinkedList<double> m_source;
        protected ShrinkedList<double> m_smaValues;
        private SMA m_smaHandler;

        public override bool IsGapTolerant
        {
            get { return true; }
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                return;

            m_source = new ShrinkedList<double>(Period);
            m_smaValues = new ShrinkedList<double>(Period);
            m_smaHandler = new SMA { Context = Context, Period = Period };
        }

        protected override void ClearExecuteContext()
        {
            m_source = null;
            m_smaValues = null;
            m_smaHandler = null;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                return;

            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - Period + 1);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                var source = m_executeContext.GetSourceForGap(i);
                m_source.Add(source);
                m_smaValues.Add(m_smaHandler.Execute(source, i));
            }
        }

        protected override double Execute()
        {
            if (IsSimple)
                return 0;

            m_source.Add(m_executeContext.Source);
            m_smaValues.Add(m_smaHandler.Execute(m_executeContext.Source, m_executeContext.Index));
            var result = Calc();
            return result;
        }

        protected abstract double Calc();
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("StDev", Language = Constants.En)]
    [HelperName("StDev", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Standard Deviation, стандартное отклонение (среднеквадратическое отклонение).")]
    [HelperDescription("The Standard Deviation indicator (RMS deviation).", Constants.En)]
    public sealed class StDev : StDevVolatility
    {
        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.StDev(source, Period, Context);
            return result;
        }

        protected override double Calc()
        {
            var result = Indicators.StDev(m_source, m_smaValues, m_source.Count - 1, Period);
            return result;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Variation", Language = Constants.En)]
    [HelperName("Variation", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Квадратичная вариация: вычисляется SMA(Period), вычисляется отклонение значений от среднего, разность возводится в квадрат. Затем вычисляется среднее значение этих отклонений. Корень НЕ ИЗВЛЕКАЕТСЯ.")]
    [HelperDescription("Variation: average of squared difference of values and corresponding SMA.", Constants.En)]
    [HandlerInvisible]
    public sealed class Volatility : StDevVolatility
    {
        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.Volatility(source, Period, Context);
            return result;
        }

        protected override double Calc()
        {
            var result = Indicators.Volatility(m_source, m_smaValues, m_source.Count - 1, Period);
            return result;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("EMA", Language = Constants.En)]
    [HelperName("EMA", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Экспоненциально сглаженное скользящее среднее. Определяется путем прибавления к предыдущему значению скользящего среднего определенной доли текущей цены закрытия.")]
    [HelperDescription("The Exponential Moving Average. Calculated by summing up a previous moving average value and some part of a current closing price.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/2MA.xml", "Пример 2МА", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/2MA.xml", "Example of 2МА", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class EMA : DoubleStreamAndValuesHandlerWithPeriod
    {
        private SMA m_sma;
        private double m_k;
        private double m_lastResult;

        public override bool IsGapTolerant
        {
            get { return IsSimple; }
        }

        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.EMA(source, Period, Context);
            return result;
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                return;

            m_sma = new SMA { Context = Context, Period = Period };
            m_k = 2D / (1D + Period);
            m_lastResult = 0;
        }

        protected override void ClearExecuteContext()
        {
            m_sma = null;
            m_lastResult = 0;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                return;

            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - GapTolerancePeriodMultiplier * Period);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                var source = m_executeContext.GetSourceForGap(i);
                var result = Calc(source, i);
            }
        }

        protected override double Execute()
        {
            if (IsSimple)
                return m_executeContext.Source;

            var result = Calc(m_executeContext.Source, m_executeContext.Index);
            return result;
        }

        private double Calc(double source, int index)
        {
            if (index < Period)
                m_lastResult = m_sma.Execute(source, index);
            else
                m_lastResult = m_k * (source - m_lastResult) + m_lastResult;

            return m_lastResult;
        }
    }

    //[HandlerName("TR")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("TR", Language = Constants.En)]
    [HelperName("TR", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Истинный диапазон (True Range - TR) - это наибольшая из следующих трех величин: разница между текущими максимумом и минимумом; разница между предыдущей ценой закрытия и текущим максимумом; разница между предыдущей ценой закрытия и текущим минимумом.")]
    [HelperDescription("The True Range (TR) is the biggest one from these three values: difference between current maximum and minimum; difference between a previous closing price and a current maximum; difference between a previous closing price and a current minimum.", Constants.En)]
    public sealed class TrueRange : IBar2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        public IList<double> Execute(ISecurity source)
        {
            return Series.TrueRange(source.Bars, Context);
        }
    }

    //[HandlerName("ATR")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("ATR", Language = Constants.En)]
    [HelperName("ATR", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индикатор среднего истинного диапазона (Average True Range - ATR). Представляет собой скользящее среднее значений истинного диапазона (TR).")]
    [HelperDescription("The average True Range (ATR). This indicator shows a moving average value of the True Range values (TR).", Constants.En)]
    public sealed class AverageTrueRange : BasePeriodIndicatorHandler, IBar2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        public IList<double> Execute(ISecurity source)
        {
            return Series.AverageTrueRange(source.Bars, Period, Context);
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("CCI", Language = Constants.En)]
    [HelperName("CCI", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индекс товарного канала (Commodity Channel Index - CCI). Измеряет отклонение цены инструмента от его среднестатистической цены.")]
    [HelperDescription("The Commodity Channel Index (CCI) measures deviation of instrument price from its average price.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/IndexTrade.xml", "Пример по ряду приемов проектирования и 2ум источникам данных", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/IndexTrade.xml", "Examples of some building strategies and using 2 sources", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class CCI : BasePeriodIndicatorHandler, IBar2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        public IList<double> Execute(ISecurity source)
        {
            return Series.CCI(source.Bars, Period, Context);
        }
    }

    //[HandlerName("Typical Price")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Typical Price", Language = Constants.En)]
    [HelperName("Типичная цена", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Типичная цена представляет из себя среднее от High, Low и Close. Результат называют средней или типичной ценой.")]
    [HelperDescription("The Typical Price is an average price based on High, Low and Close. The result is called an average or typical price.", Constants.En)]
    public sealed class TypicalPrice : IBar2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        public IList<double> Execute(ISecurity source)
        {
            return Series.TypicalPrice(source.Bars, Context);
        }
    }

    public abstract class BollingerBandsBase : DoubleStreamAndValuesHandlerWithPeriod
    {
        private readonly bool m_isTopLine;
        private SMA m_sma;
        private StDev m_stDev;

        protected BollingerBandsBase(bool isTopLine)
        {
            m_isTopLine = isTopLine;
        }

        /// <summary>
        /// \~english Width of a Bollinger band
        /// \~russian Ширина полосы Боллинджера
        /// </summary>
        [HelperName("Width", Constants.En)]
        [HelperName("Ширина", Constants.Ru)]
        [Description("Ширина полосы Боллинджера")]
        [HelperDescription("Width of a Bollinger band", Constants.En)]
        [HandlerParameter(true, "2", Min = "0.5", Max = "3", Step = "0.5")]
        public double Coef { get; set; }

        public override bool IsGapTolerant
        {
            get { return true; }
        }

        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.BollingerBands(source, Period, Coef, m_isTopLine, Context);
            return result;
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                return;

            m_sma = new SMA { Context = Context, Period = Period };
            m_stDev = new StDev { Context = Context, Period = Period };
        }

        protected override void ClearExecuteContext()
        {
            m_sma = null;
            m_stDev = null;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                return;

            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - Period + 1);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
            {
                var source = m_executeContext.GetSourceForGap(i);
                var sma = m_sma.Execute(source, i);
                var stDev = m_stDev.Execute(source, i);
            }
        }

        protected override double Execute()
        {
            if (IsSimple)
                return m_executeContext.Source;

            var sma = m_sma.Execute(m_executeContext.Source, m_executeContext.Index);
            var stDev = m_stDev.Execute(m_executeContext.Source, m_executeContext.Index);
            var sign = m_isTopLine ? 1 : -1;
            var result = sma + sign * Coef * stDev;
            return result;
        }
    }

    //[HandlerName("Bollinger Bands (+)")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Bollinger Bands (+)", Language = Constants.En)]
    [HelperName("Полоса Боллинджера (+)", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Верхняя граница коридора Болинжера.")]
    [HelperDescription("The upper Bollinger band.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/script_AND.xml", "Пример по RSI и Bollinger", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/script_AND.xml", "Example of RSI and Bollinger", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class BollingerBands1 : BollingerBandsBase
    {
        public BollingerBands1()
            : base(true)
        {
        }
    }

    //[HandlerName("Bollinger Bands (-)")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Bollinger Bands (-)", Language = Constants.En)]
    [HelperName("Полоса Боллинджера (-)", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Нижняя граница коридора Болинжера.")]
    [HelperDescription("The lower Bollinger band.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/script_AND.xml", "Пример по RSI и Bollinger", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/script_AND.xml", "Example of RSI and Bollinger", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class BollingerBands2 : BollingerBandsBase
    {
        public BollingerBands2()
            : base(false)
        {
        }
    }

    [HandlerDecimals(2)]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Relative", Language = Constants.En)]
    [HelperName("Относительный", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Относительное изменение цены от начала диапазона графика. За единицу принимается значение закрытия первого бара и дальше строятся значения относительно него (в процентах).")]
    [HelperDescription("Relative change of price starting from the beginning of some period applied in the chart. The first bar closing price is considered to be one unit and other values are based on this unit. Indicator values are presented as percents.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/RelativeIndex.xml", "Пример по индикатору Относительный", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/RelativeIndex.xml", "Example of Relative", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class Relative : IDouble2DoubleHandler
    {
        public IList<double> Execute(IList<double> source)
        {
            double v = source.Count == 0 ? 0 : source[0];
            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (v == 0)
                v = double.MaxValue;
            // ReSharper restore CompareOfFloatsByEqualityOperator
            IList<double> list = new List<double>(source.Count);
            foreach (double t in source)
                list.Add((t - v) / v * 100);
            return list;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Relative for period", Language = Constants.En)]
    [HelperName("Относительный за период", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Относительное изменение цены от начала выбранного таймфрейма. Значения указываются относительно начала периода (в процентах).")]
    [HelperDescription("Relative change of price starting from the beginning of configured timeframe. Indicator values are presented as percents.", Constants.En)]
    public sealed class RelativeForPeriod : IDouble2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        /// <summary>
        /// \~english Timeframe (format D.HH:MM:SS)
        /// \~russian Интервал (формат Д.ЧЧ:ММ:СС)
        /// </summary>
        [HelperName("Timeframe", Constants.En)]
        [HelperName("Интервал", Constants.Ru)]
        [Description("Интервал (формат Д.ЧЧ:ММ:СС)")]
        [HelperDescription("Timeframe (format D.HH:MM:SS)", Constants.En)]
        [HandlerParameter(true, Default = "1.0:0:0", Min = "0:0:1", Max = "365.0:0:0", Step = "0:0:1", EditorMin = "0:0:1", EditorMax = "365.0:0:0")]
        public TimeSpan TimeFrame { get; set; }

        public IList<double> Execute(IList<double> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var security = Context.Runtime.Securities.First();
            var bars = security.Bars;
            var minCount = Math.Min(source.Count, bars.Count);

            if (minCount == 0)
                return EmptyArrays.Double;

            TimeFrameUtils.GetFirstBounds(TimeFrame, bars[0].Date, out var firstDateTime, out var lastDateTime);
            var results = Context.GetArray<double>(minCount);

            for (int i = 0, firstIndex = 0; i <= results.Length; i++)
            {
                if (i == results.Length || bars[i].Date >= lastDateTime)
                {
                    var count = i - firstIndex;
                    if (count > 0)
                    {
                        var firstValue = source[firstIndex];
                        if (firstValue == 0)
                            firstValue = double.MaxValue;

                        results[firstIndex] = 0;
                        for (int j = 1, index = firstIndex + 1; j < count; j++, index++)
                            results[index] = (source[index] - firstValue) / firstValue * 100;
                    }
                    if (i == results.Length)
                        break;

                    TimeFrameUtils.GetBounds(TimeFrame, bars[i].Date, ref firstDateTime, ref lastDateTime);
                    firstIndex = i;
                }
            }
            return results;
        }
    }

    public abstract class HighestLowest : DoubleStreamAndValuesHandlerWithPeriod
    {
        protected ShrinkedList<double> m_source;

        public override bool IsGapTolerant
        {
            get { return true; }
        }

        protected override void InitExecuteContext()
        {
            if (IsSimple)
                return;

            m_source = new ShrinkedList<double>(Period);
        }

        protected override void ClearExecuteContext()
        {
            m_source = null;
        }

        protected override void InitForGap()
        {
            if (IsSimple)
                return;

            var firstIndex = Math.Max(m_executeContext.LastIndex + 1, m_executeContext.Index - Period + 1);
            for (var i = firstIndex; i < m_executeContext.Index; i++)
                m_source.Add(m_executeContext.GetSourceForGap(i));
        }

        protected override double Execute()
        {
            if (IsSimple)
                return m_executeContext.Source;

            m_source.Add(m_executeContext.Source);
            var result = Calc();
            return result;
        }

        protected abstract double Calc();
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Highest for", Language = Constants.En)]
    [HelperName("Максимум за", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Наибольшая цена инструмента за выбранный период (в барах).")]
    [HelperDescription("The highest price of an instrument in a period.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo.xml", "Пример стратегии Hi - Low", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo.xml", "Example of Hi - Low", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class Highest : HighestLowest
    {
        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.Highest(source, Period, Context);
            return result;
        }

        protected override double Calc()
        {
            var result = Indicators.Highest(m_source, m_source.Count - 1, Period);
            return result;
        }
    }

    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Lowest for", Language = Constants.En)]
    [HelperName("Минимум за", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Наименьшая цена инструмента за выбранный период (в барах).")]
    [HelperDescription("The lowest price of an instrument in a period.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo.xml", "Пример стратегии Hi - Low", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo.xml", "Example of Hi - Low", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class Lowest : HighestLowest
    {
        public override IList<double> Execute(IList<double> source)
        {
            var result = Series.Lowest(source, Period, Context);
            return result;
        }

        protected override double Calc()
        {
            var result = Indicators.Lowest(m_source, m_source.Count - 1, Period);
            return result;
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    //[HandlerName("Multiply by Coef", Language = "en-US")]
    //[HandlerName("Умножить на число", Language = "ru-RU")]
    [HelperName("Multiply by", Language = Constants.En)]
    [HelperName("Умножить на", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Умножение каждого элемента входной серии на заданный коэффициент.")]
    [HelperDescription("Multiplies each item of input by a constant factor.", Constants.En)]
    public sealed class Multiply : IDouble2DoubleHandler, IValuesHandlerWithNumber
    {
        /// <summary>
        /// \~english Every item of input is multiplied by this coefficient ( Mult*x )
        /// \~russian Каждый элемент входной серии умножается на указанный коэффициент ( Mult*x )
        /// </summary>
        [HelperName("Multiply", Constants.En)]
        [HelperName("Множитель", Constants.Ru)]
        [Description("Каждый элемент входной серии умножается на указанный коэффициент ( Mult*x )")]
        [HelperDescription("Every item of input is multiplied by this coefficient ( Mult*x )", Constants.En)]
        [HandlerParameter(true, "2", Min = "0.5", Max = "5", Step = "0.5")]
        public double Coef
        {
            get;
            set;
        }

        /// <summary>
        /// Обработчик для интерфейса IStreamHandler
        /// </summary>
        public IList<double> Execute(IList<double> bars)
        {
            IList<double> list = new List<double>(bars.Count);
            foreach (double t in bars)
                list.Add(t * Coef);
            return list;
        }

        /// <summary>
        /// Обработчик для интерфейса IValuesHandlerWithNumber
        /// </summary>
        public double Execute(double barVal, int barNum)
        {
            double res = barVal * Coef;
            return res;
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Ln", Language = Constants.En)]
    [HelperName("Ln", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Натуральный логарифм (Ln) для серии значений.")]
    [HelperDescription("A natural logarithm (Ln) for a values series.", Constants.En)]
    public sealed class Ln : IDouble2DoubleHandler, IValuesHandlerWithNumber
    {
        private double m_mult = 1, m_add = 0;

        /// <summary>
        /// \~english A result of logarithm may be multiplied by this coefficient ( Mult*LN(x) + Add )
        /// \~russian Результат логарифмирования можно сразу умножить на этот коэффициент ( Mult*LN(x) + Add )
        /// </summary>
        [HelperName("Multiply", Constants.En)]
        [HelperName("Множитель", Constants.Ru)]
        [Description("Результат логарифмирования можно сразу умножить на этот коэффициент ( Mult*LN(x) + Add )")]
        [HelperDescription("A result of logarithm may be multiplied by this coefficient ( Mult*LN(x) + Add )", Constants.En)]
        [HandlerParameter(true, Default = "1")]
        public double Mult
        {
            get { return m_mult; }
            set { m_mult = value; }
        }

        /// <summary>
        /// \~english A result of logarithm (after multiplication) may be shifted by this value ( Mult*LN(x) + Add )
        /// \~russian Результат логарифмирования (после домножения) можно увеличить на этот сдвиг ( Mult*LN(x) + Add )
        /// </summary>
        [HelperName("Add", Constants.En)]
        [HelperName("Прибавить", Constants.Ru)]
        [Description("Результат логарифмирования (после домножения) можно увеличить на этот сдвиг ( Mult*LN(x) + Add )")]
        [HelperDescription("A result of logarithm (after multiplication) may be shifted by this value ( Mult*LN(x) + Add )", Constants.En)]
        [HandlerParameter(true, Default = "0")]
        public double Add
        {
            get { return m_add; }
            set { m_add = value; }
        }

        /// <summary>
        /// Обработчик для интерфейса IStreamHandler
        /// </summary>
        public IList<double> Execute(IList<double> bars)
        {
            IList<double> list = new List<double>(bars.Count);
            for (int j = 0; j < bars.Count; j++)
            {
                double res = m_mult * Math.Log(bars[j]) + m_add;
                list.Add(res);
            }
            return list;
        }

        /// <summary>
        /// Обработчик для интерфейса IValuesHandlerWithNumber
        /// </summary>
        public double Execute(double barVal, int barNum)
        {
            double res = m_mult * Math.Log(barVal) + m_add;
            return res;
        }
    }
}
