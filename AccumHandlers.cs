using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;
using TSLab.Utils;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Max", Language = Constants.En)]
    [HelperName("Наибольшее", Language = Constants.Ru)]
    [InputsCount(2, 6)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Максимальное значение из нескольких (от 2 до 6 входов)")]
    [HelperDescription("Maximum of a few input values (from 2 to 6 inputs)", Constants.En)]
    public sealed class Max : IDoubleAccumHandler, IDouble2CalculatorHandler, IContextUses
    {
        public IContext Context { get; set; }

        #region IDouble2CalculatorHandler Members

        public double Execute(double source1, double source2)
        {
            return Math.Max(source1, source2);
        }
        public double Execute(double source1, double source2, double source3)
        {
            return Execute(Math.Max(source1, source2), source3);
        }

        public double Execute(double source1, double source2, double source3, double source4)
        {
            return Execute(Math.Max(source1, source2), source3, source4);
        }

        public double Execute(double source1, double source2, double source3, double source4, double source5)
        {
            return Execute(Math.Max(source1, source2), source3, source4, source5);
        }

        public double Execute(double source1, double source2, double source3, double source4, double source5, double source6)
        {
            return Execute(Math.Max(source1, source2), source3, source4, source5, source6);
        }

        #endregion

        #region IDoubleAccumHandler Members

        public IList<double> Execute(IList<double> source1, IList<double> source2)
        {
            return Series.Max(source1, source2, Context);
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3)
        {
            var source = Execute(source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3, IList<double> source4)
        {
            var source = Execute(source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3, IList<double> source4, IList<double> source5)
        {
            var source = Execute(source5, source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3, IList<double> source4, IList<double> source5, IList<double> source6)
        {
            var source = Execute(source6, source5, source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        #endregion
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Min", Language = Constants.En)]
    [HelperName("Наименьшее", Language = Constants.Ru)]
    [InputsCount(2, 6)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Минимальное значение из нескольких (от 2 до 6 входов)")]
    [HelperDescription("Minimum of a few input values (from 2 to 6 inputs)", Constants.En)]
    public sealed class Min : IDoubleAccumHandler, IDouble2CalculatorHandler, IContextUses
    {
        public IContext Context { get; set; }

        #region IDouble2CalculatorHandler Members

        public double Execute(double source1, double source2)
        {
            return Math.Min(source1, source2);
        }
        public double Execute(double source1, double source2, double source3)
        {
            return Execute(Math.Min(source1, source2), source3);
        }

        public double Execute(double source1, double source2, double source3, double source4)
        {
            return Execute(Math.Min(source1, source2), source3, source4);
        }

        public double Execute(double source1, double source2, double source3, double source4, double source5)
        {
            return Execute(Math.Min(source1, source2), source3, source4, source5);
        }

        public double Execute(double source1, double source2, double source3, double source4, double source5, double source6)
        {
            return Execute(Math.Min(source1, source2), source3, source4, source5, source6);
        }

        #endregion

        #region IDoubleAccumHandler Members

        public IList<double> Execute(IList<double> source1, IList<double> source2)
        {
            return Series.Min(source1, source2, Context);
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3)
        {
            var source = Execute(source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3, IList<double> source4)
        {
            var source = Execute(source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3, IList<double> source4, IList<double> source5)
        {
            var source = Execute(source5, source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3, IList<double> source4, IList<double> source5, IList<double> source6)
        {
            var source = Execute(source6, source5, source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        #endregion
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Sum up", Language = Constants.En)]
    [HelperName("Сложить", Language = Constants.Ru)]
    [InputsCount(2, 6)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Сложить несколько чисел (от 2 до 6 входов)")]
    [HelperDescription("Sum a few input values (from 2 to 6 inputs)", Constants.En)]
    public sealed class Add : IDoubleAccumHandler, IDouble2CalculatorHandler, IContextUses
    {
        public IContext Context { get; set; }

        #region IDouble2CalculatorHandler Members

        public double Execute(double source1, double source2)
        {
            return source1 + source2;
        }

        public double Execute(double source1, double source2, double source3)
        {
            return source1 + source2 + source3;
        }

        public double Execute(double source1, double source2, double source3, double source4)
        {
            return source1 + source2 + source3 + source4;
        }

        public double Execute(double source1, double source2, double source3, double source4, double source5)
        {
            return source1 + source2 + source3 + source4 + source5;
        }

        public double Execute(double source1, double source2, double source3, double source4, double source5, double source6)
        {
            return source1 + source2 + source3 + source4 + source5 + source6;
        }

        #endregion

        #region IDoubleAccumHandler Members

        public IList<double> Execute(IList<double> source1, IList<double> source2)
        {
            return Series.Add(source1, source2, Context);
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3)
        {
            var source = Execute(source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3, IList<double> source4)
        {
            var source = Execute(source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3, IList<double> source4, IList<double> source5)
        {
            var source = Execute(source5, source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<double> Execute(IList<double> source1, IList<double> source2, IList<double> source3, IList<double> source4, IList<double> source5, IList<double> source6)
        {
            var source = Execute(source6, source5, source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        #endregion
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Subtract", Language = Constants.En)]
    [HelperName("Вычесть", Language = Constants.Ru)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Вычесть второе число из первого")]
    [HelperDescription("Subtract second number from the first one", Constants.En)]
    public sealed class Sub : IDoubleAccumHandler, IDouble2CalculatorHandler, IContextUses
    {
        public IContext Context { get; set; }

        #region IDouble2CalculatorHandler Members

        public double Execute(double source1, double source2)
        {
            return source1 - source2;
        }

        #endregion

        #region IDoubleAccumHandler Members

        public IList<double> Execute(IList<double> source1, IList<double> source2)
        {
            return Series.Sub(source1, source2, Context);
        }

        #endregion
    }

    [HandlerCategory(HandlerCategories.TradeMath, "LogicalNotTemplate", false)]
    [HelperName("Not", Language = Constants.En)]
    [HelperName("НЕ", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Логическое отрицание. Меняет входящее логическое значение на противоположное. Если на входе true то на выходе false, если на входе false, то на выходе true.")]
    [HelperDescription("Negation. Incoming logical value is changed to the opposite. TRUE changes to FALSE, FALSE changes to TRUE.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo_1pos.xml", "Пример модифицированной стратегии Hi Low", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo_1pos.xml", "Example of Hi Low startegy (modified)", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class Not : IBoolConvertor, IContextUses
    {
        public IContext Context { get; set; }

        #region IBoolConvertor Members

        public IList<bool> Execute(IList<bool> src)
        {
            var dst = Context?.GetArray<bool>(src.Count) ?? new bool[src.Count];
            for (int i = 0; i < src.Count; i++)
            {
                dst[i] = !src[i];
            }
            return dst;
        }

        public bool Execute(bool source, int num)
        {
            return !source;
        }

        #endregion
    }

    [HandlerCategory(HandlerCategories.TradeMath, "LogicalAndTemplate", false)]
    [HelperName("And", Language = Constants.En)]
    [HelperName("И", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(2, 6)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Операция логическое 'И' нескольких значений (от 2 до 6 входов). На выходе true, только если все входы одновременно имеют значение true.")]
    [HelperDescription("Logical conjunction of a few input values (from 2 to 6 inputs). Output is TRUE only if all inputs are TRUE at the same time.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/script_AND.xml", "Пример по RSI и Bollinger", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/script_AND.xml", "Example of RSI and Bollinger", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class And : IBoolAccum
    {
        public IContext Context { get; set; }

        #region IBoolAccum Members

        public IList<bool> Execute(IList<bool> source1, IList<bool> source2)
        {
            return Series.And(source1, source2, Context);
        }

        public IList<bool> Execute(IList<bool> source1, IList<bool> source2, IList<bool> source3)
        {
            var source = Execute(source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<bool> Execute(IList<bool> source1, IList<bool> source2, IList<bool> source3, IList<bool> source4)
        {
            var source = Execute(source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<bool> Execute(IList<bool> source1, IList<bool> source2, IList<bool> source3, IList<bool> source4, IList<bool> source5)
        {
            var source = Execute(source5, source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<bool> Execute(IList<bool> source1, IList<bool> source2, IList<bool> source3, IList<bool> source4, IList<bool> source5, IList<bool> source6)
        {
            var source = Execute(source6, source5, source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<bool> Execute(params IList<bool>[] srcs)
        {
            if (srcs == null)
                throw new ArgumentNullException(nameof(srcs));

            if (srcs.Length < 2)
                throw new ArgumentOutOfRangeException(nameof(srcs));

            var result = Series.And(srcs[0], srcs[1], Context);
            for (var i = 2; i < srcs.Length; i++)
            {
                var lastResult = result;
                result = Series.And(result, srcs[i], Context);
                Context?.ReleaseArray((Array)lastResult);
            }
            return result;
        }

        public bool Execute(bool source1, bool source2)
        {
            return source1 && source2;
        }

        public bool Execute(bool source1, bool source2, bool source3)
        {
            return source1 && source2 && source3;
        }

        public bool Execute(bool source1, bool source2, bool source3, bool source4)
        {
            return source1 && source2 && source3 && source4;
        }

        public bool Execute(bool source1, bool source2, bool source3, bool source4, bool source5)
        {
            return source1 && source2 && source3 && source4 && source5;
        }

        public bool Execute(bool source1, bool source2, bool source3, bool source4, bool source5, bool source6)
        {
            return source1 && source2 && source3 && source4 && source5 && source6;
        }

        public bool Execute(params bool[] sources)
        {
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));

            if (sources.Length < 2)
                throw new ArgumentOutOfRangeException(nameof(sources));

            return sources.All(item => item);
        }

        #endregion
    }

    [HandlerCategory(HandlerCategories.TradeMath, "LogicalOrTemplate", false)]
    [HelperName("Or", Language = Constants.En)]
    [HelperName("ИЛИ", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(2, 6)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Операция логическое 'ИЛИ' нескольких значений (от 2 до 6 входов). На выходе true, если хотя бы один вход имеют значение true.")]
    [HelperDescription("Logical disjunction of a few input values (from 2 to 6 inputs). Output is TRUE if at least one input is TRUE.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/script_AND.xml", "Пример по RSI и Bollinger", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/script_AND.xml", "Example of RSI and Bollinger", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class Or : IBoolAccum
    {
        public IContext Context { get; set; }

        #region IBoolAccum Members

        public IList<bool> Execute(IList<bool> source1, IList<bool> source2)
        {
            return Series.Or(source1, source2, Context);
        }

        public IList<bool> Execute(IList<bool> source1, IList<bool> source2, IList<bool> source3)
        {
            var source = Execute(source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<bool> Execute(IList<bool> source1, IList<bool> source2, IList<bool> source3, IList<bool> source4)
        {
            var source = Execute(source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<bool> Execute(IList<bool> source1, IList<bool> source2, IList<bool> source3, IList<bool> source4, IList<bool> source5)
        {
            var source = Execute(source5, source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<bool> Execute(IList<bool> source1, IList<bool> source2, IList<bool> source3, IList<bool> source4, IList<bool> source5, IList<bool> source6)
        {
            var source = Execute(source6, source5, source4, source3, source2);
            var results = Execute(source, source1);
            Context?.ReleaseArray((Array)source);
            return results;
        }

        public IList<bool> Execute(params IList<bool>[] srcs)
        {
            if (srcs == null)
                throw new ArgumentNullException(nameof(srcs));

            if (srcs.Length < 2)
                throw new ArgumentOutOfRangeException(nameof(srcs));

            var result = Series.Or(srcs[0], srcs[1], Context);
            for (var i = 2; i < srcs.Length; i++)
            {
                var lastResult = result;
                result = Series.Or(result, srcs[i], Context);
                Context?.ReleaseArray((Array)lastResult);
            }
            return result;
        }

        public bool Execute(bool source1, bool source2)
        {
            return source1 || source2;
        }

        public bool Execute(bool source1, bool source2, bool source3)
        {
            return source1 || source2 || source3;
        }

        public bool Execute(bool source1, bool source2, bool source3, bool source4)
        {
            return source1 || source2 || source3 || source4;
        }

        public bool Execute(bool source1, bool source2, bool source3, bool source4, bool source5)
        {
            return source1 || source2 || source3 || source4 || source5;
        }

        public bool Execute(bool source1, bool source2, bool source3, bool source4, bool source5, bool source6)
        {
            return source1 || source2 || source3 || source4 || source5 || source6;
        }

        public bool Execute(params bool[] sources)
        {
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));

            if (sources.Length < 2)
                throw new ArgumentOutOfRangeException(nameof(sources));

            return sources.Any(item => item);
        }

        #endregion
    }

    //[HandlerName("Cross Over")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Cross over", Language = Constants.En)]
    [HelperName("Пересечение сверху", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Функция определяет моменты, когда второй вход (сигнал) пересекает опорную линию (первый вход) сверху вниз.")]
    [HelperDescription("Handler returns true, if second input (signal) crosses down a reference line (first input).", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD.xml", "Пример по индикатору MACD", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD.xml", "Example of MACD", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class CrossOver : ValueSupportedComparer, IDoubleCompaperHandler
    {
        #region IDoubleCompaperHandler Members

        public IList<bool> Execute(IList<double> src1, IList<double> src2)
        {
            int count = Math.Max(src1.Count, src2.Count);
            int mcount = Math.Min(src1.Count, src2.Count);
            var dst = Context?.GetArray<bool>(count) ?? new bool[count];
            for (int i = 1; i < mcount; i++)
            {
                dst[i] = (src1[i] > src2[i] && src1[i - 1] <= src2[i - 1]);
            }
            return dst;
        }

        #endregion

        protected override bool Execute(IList<double> src1, IList<double> src2, IList<bool> dst, int i)
        {
            if (i > 1)
            {
                dst[i] = (src1[i] > src2[i] && src1[i - 1] <= src2[i - 1]);
            }
            return dst[i];
        }
    }

    //[HandlerName("Cross Under")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Cross under", Language = Constants.En)]
    [HelperName("Пересечение снизу", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Функция определяет моменты, когда второй вход (сигнал) пересекает опорную линию (первый вход) снизу вверх.")]
    [HelperDescription("Handler returns true, if second input (signal) crosses up a reference line (first input).", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD.xml", "Пример по индикатору MACD", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD.xml", "Example of MACD", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/2MA.xml", "Пример 2MA", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/2MA.xml", "Example of 2MA", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class CrossUnder : ValueSupportedComparer, IDoubleCompaperHandler
    {
        #region IDoubleCompaperHandler Members

        public IList<bool> Execute(IList<double> src1, IList<double> src2)
        {
            int count = Math.Max(src1.Count, src2.Count);
            int mcount = Math.Min(src1.Count, src2.Count);
            var dst = Context?.GetArray<bool>(count) ?? new bool[count];
            for (int i = 1; i < mcount; i++)
            {
                dst[i] = (src1[i] < src2[i] && src1[i - 1] >= src2[i - 1]);
            }
            return dst;
        }

        #endregion

        protected override bool Execute(IList<double> src1, IList<double> src2, IList<bool> dst, int i)
        {
            if (i > 1)
            {
                dst[i] = (src1[i] < src2[i] && src1[i - 1] >= src2[i - 1]);
            }
            return dst[i];
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Equal", Language = Constants.En)]
    [HelperName("Равно", Language = Constants.Ru)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Кубик возвращает true, если на вход переданы равные значения (в пределах точности вычислений)")]
    [HelperDescription("Handler returns true, if input values are equal (with respect to double values precision)", Constants.En)]
    public sealed class EqualHandler : ValueSupportedComparer, IDoubleCompaperHandler
    {
        public IList<bool> Execute(IList<double> source1, IList<double> source2)
        {
            var maxCount = Math.Max(source1.Count, source2.Count);
            var minCount = Math.Min(source1.Count, source2.Count);
            var results = Context?.GetArray<bool>(maxCount) ?? new bool[maxCount];

            for (var i = 0; i < minCount; i++)
            {
                // [23-5-2018] По совещанию в скайпе принято решение переделать на DoubleUtil.AreClose
                // results[i] = source1[i] == source2[i];
                results[i] = DoubleUtil.AreClose(source1[i], source2[i]);
            }

            return results;
        }

        protected override bool Execute(IList<double> source1, IList<double> source2, IList<bool> results, int index)
        {
            return results[index] = source1[index] == source2[index];
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Greater", Language = Constants.En)]
    [HelperName("Больше", Language = Constants.Ru)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Функция определяет моменты, когда первый вход строго больше второго.")]
    [HelperDescription("Handler returns true, if the first input is strictly greater than the second input.", Constants.En)]
    public sealed class GreaterHandler : ValueSupportedComparer, IDoubleCompaperHandler
    {
        public IList<bool> Execute(IList<double> source1, IList<double> source2)
        {
            var maxCount = Math.Max(source1.Count, source2.Count);
            var minCount = Math.Min(source1.Count, source2.Count);
            var results = Context?.GetArray<bool>(maxCount) ?? new bool[maxCount];

            for (var i = 0; i < minCount; i++)
                results[i] = source1[i] > source2[i];

            return results;
        }

        protected override bool Execute(IList<double> source1, IList<double> source2, IList<bool> results, int index)
        {
            return results[index] = source1[index] > source2[index];
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Less", Language = Constants.En)]
    [HelperName("Меньше", Language = Constants.Ru)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Функция определяет моменты, когда первый вход строго меньше второго.")]
    [HelperDescription("Handler returns true, if the first input is strictly less than the second input.", Constants.En)]
    public sealed class LessHandler : ValueSupportedComparer, IDoubleCompaperHandler
    {
        public IList<bool> Execute(IList<double> source1, IList<double> source2)
        {
            var maxCount = Math.Max(source1.Count, source2.Count);
            var minCount = Math.Min(source1.Count, source2.Count);
            var results = Context?.GetArray<bool>(maxCount) ?? new bool[maxCount];

            for (var i = 0; i < minCount; i++)
                results[i] = source1[i] < source2[i];

            return results;
        }

        protected override bool Execute(IList<double> source1, IList<double> source2, IList<bool> results, int index)
        {
            return results[index] = source1[index] < source2[index];
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Greater or equal", Language = Constants.En)]
    [HelperName("Больше или равно", Language = Constants.Ru)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Функция определяет моменты, когда первый вход больше или равен второму.")]
    [HelperDescription("Handler returns true, if the first input is greater or equal than the second input.", Constants.En)]
    public sealed class GreaterOrEqualHandler : ValueSupportedComparer, IDoubleCompaperHandler
    {
        public IList<bool> Execute(IList<double> source1, IList<double> source2)
        {
            var maxCount = Math.Max(source1.Count, source2.Count);
            var minCount = Math.Min(source1.Count, source2.Count);
            var results = Context?.GetArray<bool>(maxCount) ?? new bool[maxCount];

            for (var i = 0; i < minCount; i++)
                results[i] = source1[i] >= source2[i];

            return results;
        }

        protected override bool Execute(IList<double> source1, IList<double> source2, IList<bool> results, int index)
        {
            return results[index] = source1[index] >= source2[index];
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Less or equal", Language = Constants.En)]
    [HelperName("Меньше или равно", Language = Constants.Ru)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Функция определяет моменты, когда первый вход меньше или равен второму.")]
    [HelperDescription("Handler returns true, if the first input is less or equal than the second input.", Constants.En)]
    public sealed class LessOrEqualHandler : ValueSupportedComparer, IDoubleCompaperHandler
    {
        public IList<bool> Execute(IList<double> source1, IList<double> source2)
        {
            var maxCount = Math.Max(source1.Count, source2.Count);
            var minCount = Math.Min(source1.Count, source2.Count);
            var results = Context?.GetArray<bool>(maxCount) ?? new bool[maxCount];

            for (var i = 0; i < minCount; i++)
                results[i] = source1[i] <= source2[i];

            return results;
        }

        protected override bool Execute(IList<double> source1, IList<double> source2, IList<bool> results, int index)
        {
            return results[index] = source1[index] <= source2[index];
        }
    }
}
