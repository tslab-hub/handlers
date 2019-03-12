using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

using TSLab.Script.Handlers.Options;
using TSLab.Script.Helpers;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CompareOfFloatsByEqualityOperator
namespace TSLab.Script.Handlers
{
    public static class ADXHelper
    {
        public static IList<double> CalcDIP(ISecurity source, int period, IMemoryContext context = null)
        {
            var bars = source.Bars;
            int count = bars.Count;

            var trueRange = Series.TrueRange(bars, context);
            var atr = Series.EMA(trueRange, period, context);
            context?.ReleaseArray((Array)trueRange);
            Debug.Assert(atr != null, "atr != null");
            IList<double> diP1 = context?.GetArray<double>(count) ?? new double[count];

            for (int i = 1; i < count; i++)
            {
                var dmP = bars[i].High - bars[i - 1].High;
                var dmM = bars[i - 1].Low - bars[i].Low;
                if ((dmP < 0 && dmM < 0) || dmP == dmM)
                {
                    dmP = dmM = 0;
                }
                if (dmM > dmP)
                {
                    dmP = 0;
                }
                diP1[i] = dmP;
            }
            var diP2 = Series.EMA(diP1, period, context);
            context?.ReleaseArray((Array)diP1);
            for (int i = 1; i < count; i++)
            {
                diP2[i] = atr[i] == 0 ? 0 : diP2[i] / atr[i];
            }
            context?.ReleaseArray((Array)atr);
            return diP2;
        }

        public static IList<double> CalcDIM(ISecurity source, int period, IMemoryContext context = null)
        {
            var bars = source.Bars;
            int count = bars.Count;

            var trueRange = Series.TrueRange(bars, context);
            var atr = Series.EMA(trueRange, period, context);
            context?.ReleaseArray((Array)trueRange);
            IList<double> diM1 = context?.GetArray<double>(count) ?? new double[count];

            for (int i = 1; i < count; i++)
            {
                var dmP = bars[i].High - bars[i - 1].High;
                var dmM = bars[i - 1].Low - bars[i].Low;
                if ((dmP < 0 && dmM < 0) || dmP == dmM)
                {
                    dmP = dmM = 0;
                }
                if (dmP > dmM)
                {
                    dmM = 0;
                }
                diM1[i] = dmM;
            }
            var diM2 = Series.EMA(diM1, period, context);
            context?.ReleaseArray((Array)diM1);
            for (int i = 1; i < count; i++)
            {
                diM2[i] = atr[i] == 0 ? 0 : diM2[i] / atr[i];
            }
            context?.ReleaseArray((Array)atr);
            return diM2;
        }

        public static IList<double> CalcADX(IList<double> source1, IList<double> source2, int period, IMemoryContext context = null)
        {
            int count = source1.Count;
            IList<double> dx1 = context?.GetArray<double>(count) ?? new double[count];

            for (int i = 1; i < count; i++)
            {
                dx1[i] = source1[i] == 0 && source2[i] == 0
                             ? 0
                             : Math.Abs(source1[i] - source2[i]) / (source1[i] + source2[i]) * 100;
            }
            var dx2 = Series.EMA(dx1, period, context);
            context?.ReleaseArray((Array)dx1);
            return dx2;
        }
    }

    //[HandlerName("+DI")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("+DI", Language = Constants.En)]
    [HelperName("+DI", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индикатор положительного направления системы индикаторов Average Directional Movement Index.")]
    [HelperDescription("A positive direction indicator of the Average Directional Movement Index.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/ADX.xml", "Пример по индикатору ADX", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/ADX.xml", "Example of ADX", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class DIP : BasePeriodIndicatorHandler, IBar2DoubleHandler, IContextUses
    {
        public IList<double> Execute(ISecurity source)
        {
            return ADXHelper.CalcDIP(source, Period, Context);
        }

        public IContext Context { get; set; }
    }

    //[HandlerName("-DI")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("-DI", Language = Constants.En)]
    [HelperName("-DI", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индикатор отрицательного направления системы индикаторов Average Directional Movement Index.")]
    [HelperDescription("A negative direction indicator of the Average Directional Movement Index.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/ADX.xml", "Пример по индикатору ADX", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/ADX.xml", "Example of ADX", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class DIM : BasePeriodIndicatorHandler, IBar2DoubleHandler, IContextUses
    {
        public IList<double> Execute(ISecurity source)
        {
            return ADXHelper.CalcDIM(source, Period, Context);
        }

        public IContext Context { get; set; }
    }

    //[HandlerName("ADX")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("ADX", Language = Constants.En)]
    [HelperName("ADX", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Индикатор ADX или индикатор средней направленности (Average Directional Movement Index). Служит для определения вероятного направления основного тренда.")]
    [HelperDescription("An indicator of the Average Directional Movement Index (ADX). Helps to define a possible trend direction.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/ADX.xml", "Пример по индикатору ADX", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/ADX.xml", "Example of ADX", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class ADXFull : BasePeriodIndicatorHandler, IBar2DoubleHandler, IContextUses
    {
        public IList<double> Execute(ISecurity source)
        {
            var dip = ADXHelper.CalcDIP(source, Period, Context);
            var dim = ADXHelper.CalcDIM(source, Period, Context);
            var results = ADXHelper.CalcADX(dip, dim, Period, Context);
            Context?.ReleaseArray((Array)dip);
            Context?.ReleaseArray((Array)dim);
            return results;
        }

        public IContext Context { get; set; }
    }

    // TODO: чем старый отличается от нового?
    //[HandlerName("ADX (Old)")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("ADX (Old)", Language = Constants.En)]
    [HelperName("ADX (cтарый)", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(2)]
    [Input(0, TemplateTypes.DOUBLE)]
    [Input(1, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Нестандартный (старый) расчет ADX: разность первого и второго входа делится на их сумму и затем усредняется по алгоритму EMA")]
    [HelperDescription("Old algo to calculate ADX: EMA of (Input1-Input2)/(Input1+Input2)", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class ADX : BasePeriodIndicatorHandler, IDoubleAccumHandler, IContextUses
    {
        public IContext Context { get; set; }

        public IList<double> Execute(IList<double> source1, IList<double> source2)
        {
            return ADXHelper.CalcADX(source1, source2, Period, Context);
        }
    }
}
