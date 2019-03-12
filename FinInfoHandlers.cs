using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.DataSource;
using TSLab.Script.Handlers.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers
{
    /// <summary>
    /// Базовый класс для группы кубиков, которые используют FinInfo
    /// </summary>
    [HandlerCategory(HandlerCategories.TradeMath)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public abstract class FinInfoHandler : IBar2DoubleHandler//, IContextUses
    {
        //public IContext Context { get; set; }

        public IList<double> Execute(ISecurity source)
        {
            return new ConstList<double>(source.Bars.Count, GetValue(source.FinInfo) ?? 0);
        }

        protected abstract double? GetValue(FinInfo finInfo);
    }

    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Number of purchases", Language = Constants.En)]
    [HelperName("СумСпрос", Language = Constants.Ru)]
    [Description("Суммарное текущее количество заявок на покупку. Эта же величина показывается в таблице 'Котировки'.")]
    [HelperDescription("Shows the number of purchases (bids). This value is shown also in 'Quotes' table.", Constants.En)]
    public sealed class BuyCount : FinInfoHandler
    {
        protected override double? GetValue(FinInfo finInfo)
        {
            return finInfo.BuyCount;
        }
    }

    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Number of sales", Language = Constants.En)]
    [HelperName("СумПредл", Language = Constants.Ru)]
    [Description("Суммарное текущее количество заявок на продажу. Эта же величина показывается в таблице 'Котировки'.")]
    [HelperDescription("Shows the number of sales (asks). This value is shown also in 'Quotes' table.", Constants.En)]
    public sealed class SellCount : FinInfoHandler
    {
        protected override double? GetValue(FinInfo finInfo)
        {
            return finInfo.SellCount;
        }
    }

    // Гарантийные обязательства покупателя
    public sealed class BuyDeposit : FinInfoHandler
    {
        protected override double? GetValue(FinInfo finInfo)
        {
            return finInfo.BuyDeposit;
        }
    }

    // Гарантийные обязательства продавца
    public sealed class SellDeposit : FinInfoHandler
    {
        protected override double? GetValue(FinInfo finInfo)
        {
            return finInfo.SellDeposit;
        }
    }

    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Price step", Language = Constants.En)]
    [HelperName("Шаг цены", Language = Constants.Ru)]
    [Description("Шаг цены инструмента. Эта же величина показывается в таблице 'Котировки'.")]
    [HelperDescription("Price step of a security. This value is shown also in 'Quotes' table.", Constants.En)]
    public sealed class Tick : FinInfoHandler
    {
        protected override double? GetValue(FinInfo finInfo)
        {
            if (finInfo == null || finInfo.Security == null)
                return 1;

            var lastPrice = finInfo.LastPrice ?? 0.0;
            var tick = finInfo.Security.GetTick(lastPrice);
            if (!DoubleUtil.IsPositive(tick))
                tick = Math.Pow(10, -finInfo.Security.Decimals);

            return tick;
        }
    }
}
