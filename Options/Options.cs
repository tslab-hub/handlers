using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using TSLab.DataSource;
using TSLab.Script.Options;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable CompareOfFloatsByEqualityOperator
namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Base asset (stream handler)
    /// \~russian Базовый актив (потоковый обработчик)
    /// </summary>
    [HandlerCategory(HandlerCategories.Options)]
    [HelperName("Base asset", Language = Constants.En)]
    [HelperName("Базовый актив", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION | TemplateTypes.OPTION_SERIES)]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Базовый актив (потоковый обработчик)")]
    [HelperDescription("Base asset (stream handler)", Constants.En)]
    public class OptionBase : IStreamHandler, ISecurityReturns
    {
        public ISecurity Execute(IOption opt)
        {
            return opt.UnderlyingAsset;
        }

        public ISecurity Execute(IOptionSeries opt)
        {
            return opt.UnderlyingAsset;
        }
    }

    /// <summary>
    /// \~english Number of options in the source
    /// \~russian Количество опционов в источнике
    /// </summary>
    [HandlerCategory(HandlerCategories.Options)]
    [HelperName("Count Options", Language = Constants.En)]
    [HelperName("Количество опционов", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION, Name = Constants.OptionSource)]
    [OutputType(TemplateTypes.INT)]
    [Description("Количество опционов в источнике")]
    [HelperDescription("Number of options in the source", Constants.En)]
    public class OptionStrikeCount : ConstGenBase<int>, IStreamHandler
    {
        public IList<int> Execute(IOption source)
        {
            MakeList(source.UnderlyingAsset.Bars.Count, source.GetStrikes().Count());
            return this;
        }
    }
    
    public abstract class BidAskStrikeBase : OptionSeriesBase
    {
        protected class StrikeInfo
        {
            public double ExpDate { get; set; }

            public double BasePrice { get; set; }

            public double Put { get; set; }
            public double Call { get; set; }

            public double CallSigma { get; set; }
            public double PutSigma { get; set; }
        }

        protected override IEnumerable<Double2> Calculate(IOptionStrike[] strikes)
        {
            var bidList = new List<Double2>();

            var finArray = new Dictionary<double, StrikeInfo>();
            foreach (var optionStrike in strikes)
            {
                if (!finArray.ContainsKey(optionStrike.Strike))
                {
                    var lastUpdate = optionStrike.FinInfo.LastUpdate;
                    if (lastUpdate == DateTime.MinValue) continue;
                    var stInfo = new StrikeInfo
                    {
                        ExpDate = OptionUtils.YearsBetweenDates(optionStrike.ExpirationDate, lastUpdate),
                        BasePrice = optionStrike.UnderlyingAsset.FinInfo.LastPrice ?? 0
                    };
                    FillStrikeInfo(optionStrike, stInfo);

                    finArray.Add(optionStrike.Strike, stInfo);
                }
                else
                {
                    var key = optionStrike.Strike;

                    var stInfo = finArray[key];
                    FillStrikeInfo(optionStrike, stInfo);
                }
            }

            // выход, пустой список
            if (finArray.Count == 0)
                return bidList;

            // расчет волатильностей
            foreach (var strikeInfo in finArray)
            {
                double precision;
                var callSigma = (strikeInfo.Value.Call != 0.0)
                    ? FinMath.GetOptionSigma(strikeInfo.Value.BasePrice, strikeInfo.Key, strikeInfo.Value.ExpDate,
                        strikeInfo.Value.Call, 0.0, true, out precision)
                    : 0;

                var putSigma = (strikeInfo.Value.Put != 0.0)
                    ? FinMath.GetOptionSigma(strikeInfo.Value.BasePrice, strikeInfo.Key, strikeInfo.Value.ExpDate,
                        strikeInfo.Value.Put, 0.0, false, out precision)
                    : 0;

                strikeInfo.Value.CallSigma = callSigma;
                strikeInfo.Value.PutSigma = putSigma;

                if (putSigma == 0 && callSigma == 0)
                    continue;

                // добавим значение
                bidList.Add(new Double2 { V1 = strikeInfo.Key, V2 = Math.Max(callSigma, putSigma) * 100.0 });
            }

            return bidList;
        }

        protected abstract void FillStrikeInfo(IOptionStrike optionStrike, StrikeInfo stInfo);
    }

    /// <summary>
    /// \~english Options bid prices. Price is set to 0 if there is no demand in it. Handler returns a list of Double2.
    /// \~russian Цена покупки опционов. Если котировки нет, цена считается нулевой. Результатом блока является лист Double2.
    /// </summary>
    [HelperName("Option Bids", Language = Constants.En)]
    [HelperName("Цены покупки", Language = Constants.Ru)]
    [Description("Цена покупки опционов. Если котировки нет, цена считается нулевой. Результатом блока является лист Double2.")]
    [HelperDescription("Options bid prices. Price is set to 0 if there is no demand in it. Handler returns a list of Double2.", Constants.En)]
    public class BidStrikes : BidAskStrikeBase
    {
        protected override void FillStrikeInfo(IOptionStrike optionStrike, StrikeInfo stInfo)
        {
            switch (optionStrike.StrikeType)
            {
                case StrikeType.Call:
                    stInfo.Call = optionStrike.FinInfo.Bid ?? 0;
                    break;
                case StrikeType.Put:
                    stInfo.Put = optionStrike.FinInfo.Bid ?? 0;
                    break;
                default:
                    return;
            }
        }
    }

    /// <summary>
    /// \~english Options ask prices. Price is set to 0 if there is no offer in it. Handler returns a list of Double2.
    /// \~russian Цена продажи опционов. Если котировки нет, цена считается нулевой. Результатом блока является лист Double2.
    /// </summary>
    [HelperName("Option Asks", Language = Constants.En)]
    [HelperName("Цены продажи", Language = Constants.Ru)]
    [Description("Цена продажи опционов. Если котировки нет, цена считается нулевой. Результатом блока является лист Double2.")]
    [HelperDescription("Options ask prices. Price is set to 0 if there is no offer in it. Handler returns a list of Double2.", Constants.En)]
    public class AskStrikes : BidAskStrikeBase
    {
        protected override void FillStrikeInfo(IOptionStrike optionStrike, StrikeInfo stInfo)
        {
            switch (optionStrike.StrikeType)
            {
                case StrikeType.Call:
                    stInfo.Call = optionStrike.FinInfo.Ask ?? Constants.NaN;
                    break;
                case StrikeType.Put:
                    stInfo.Put = optionStrike.FinInfo.Ask ?? Constants.NaN;
                    break;
                default:
                    return;
            }
        }
    }
}
