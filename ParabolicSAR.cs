using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using TSLab.Script.Handlers.Options;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace TSLab.Script.Handlers
{
    //[HandlerName("ParabolicSAR")]
    [HandlerCategory(HandlerCategories.Indicators)]
    [HelperName("Parabolic SAR", Language = Constants.En)]
    [HelperName("Parabolic SAR", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Parabolic Time/Price System (Параболическая система цены/времени).")]
    [HelperDescription("The Parabolic Time/Price System.", Constants.En)]
    [HelperLink(@"http://forum.tslab.ru/ubb/ubbthreads.php?ubb=showflat&Number=31728#Post31728", "Описание индикатора на форуме TSLab", Constants.Ru)]
    [SuppressMessage("StyleCopPlus.StyleCopPlusRules", "SP0100:AdvancedNamingRules", Justification = "Reviewed. Suppression is OK here.")]
    public sealed class ParabolicSAR : IBar2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        /// <summary>
        /// \~english Initial acceleration
        /// \~russian Начальное ускорение
        /// </summary>
        [HelperName("Acceleration start", Constants.En)]
        [HelperName("Начальное ускорение", Constants.Ru)]
        [Description("Начальное ускорение")]
        [HelperDescription("Initial acceleration", Constants.En)]
        [HandlerParameter(true, "0.02", Min = "0.01", Max = "0.1", Step = "0.01", Name = "Acceleration start")]
        public double AccelerationStart { get; set; }

        /// <summary>
        /// \~english Step to increase acceleration
        /// \~russian Шаг увеличения ускорения
        /// </summary>
        [HelperName("Acceleration step", Constants.En)]
        [HelperName("Шаг увеличения ускорения", Constants.Ru)]
        [Description("Шаг увеличения ускорения")]
        [HelperDescription("Step to increase acceleration", Constants.En)]
        [HandlerParameter(true, "0.02", Min = "0.01", Max = "0.1", Step = "0.01", Name = "Acceleration step")]
        public double AccelerationStep { get; set; }

        /// <summary>
        /// \~english Acceleration limit
        /// \~russian Максимальное ускорение
        /// </summary>
        [HelperName("Acceleration max", Constants.En)]
        [HelperName("Максимальное ускорение", Constants.Ru)]
        [Description("Максимальное ускорение")]
        [HelperDescription("Acceleration limit", Constants.En)]
        [HandlerParameter(true, "0.2", Min = "0.1", Max = "0.4", Step = "0.1", Name = "Acceleration max")]
        public double AccelerationMax { get; set; }

//| Parabolic Sell And Reverse system                                |
        public IList<double> Execute(ISecurity source)
        {
            var bars = source.Bars;
            int count = bars.Count;
            var sarBuffer = Context?.GetArray<double>(count) ?? new double[count];

            if (count > 1)
            {
                bool dirlong = bars[1].High > bars[0].High || bars[1].Low > bars[0].Low;
                sarBuffer[0] = sarBuffer[1] = dirlong ? bars[0].Low : bars[0].High;
                double start = AccelerationStart;
                double lastLow = bars[1].Low;
                double lastHigh = bars[1].High;
                double ep = dirlong ? lastHigh : lastLow;
                
                for (int i = 2; i < count; i++)
                {
                    var priceLow = bars[i].Low;
                    var priceHigh = bars[i].High;
                    //--- check for reverse from long to short
                    if (dirlong && priceLow < sarBuffer[i - 1])
                    {
                        start = AccelerationStart;
                        dirlong = false;
                        ep = priceLow;
                        lastLow = priceLow;
                        sarBuffer[i] = lastHigh;
                        continue;
                    }
                    //--- check for reverse from short to long  
                    if (!dirlong && priceHigh > sarBuffer[i - 1])
                    {
                        start = AccelerationStart;
                        dirlong = true;
                        ep = priceHigh;
                        lastHigh = priceHigh;
                        sarBuffer[i] = lastLow;
                        continue;
                    }
                    //sar(i) = sar(i-1)+start*(ep-sar(i-1))
                    var price = sarBuffer[i - 1];
                    var sar = price + start * (ep - price);
                    //----
                    if (dirlong)
                    {
                        if (ep < priceHigh && (start + AccelerationStep) <= AccelerationMax)
                            start += AccelerationStep;
                        //----
                        if (priceHigh < bars[i - 1].High && i == 2)
                            sar = sarBuffer[i - 1];
                        price = bars[i - 1].Low;
                        //----
                        if (sar > price)
                            sar = price;
                        price = bars[i - 2].Low;
                        //----
                        if (sar > price)
                            sar = price;
                        //----
                        if (sar > priceLow)
                        {
                            start = AccelerationStart;
                            dirlong = false;
                            ep = priceLow;
                            lastLow = priceLow;
                            sarBuffer[i] = lastHigh;
                            continue;
                        }
                        //----
                        if (ep < priceHigh)
                        {
                            lastHigh = priceHigh;
                            ep = priceHigh;
                        }
                    }     //dir-long
                    else
                    {
                        if (ep > priceLow && (start + AccelerationStep) <= AccelerationMax)
                            start += AccelerationStep;
                        //----
                        if (priceLow < bars[i - 1].Low && i == 2)
                            sar = sarBuffer[i - 1];
                        price = bars[i - 1].High;
                        //----
                        if (sar < price)
                            sar = price;
                        price = bars[i - 2].High;
                        //----
                        if (sar < price)
                            sar = price;
                        //----
                        if (sar < priceHigh)
                        {
                            start = AccelerationStart;
                            dirlong = true;
                            ep = priceHigh;
                            lastHigh = priceHigh;
                            sarBuffer[i] = lastLow;
                            continue;
                        }
                        //----
                        if (ep > priceLow)
                        {
                            lastLow = priceLow;
                            ep = priceLow;
                        }
                    }     //dir-short
                    sarBuffer[i] = sar;
                }
            }
            return sarBuffer;
        }
    }
}
