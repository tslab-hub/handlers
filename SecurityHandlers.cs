using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.DataSource;
using TSLab.Script.Handlers.Options;
using TSLab.Utils;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
namespace TSLab.Script.Handlers
{
    public abstract class SecurityBase : IBar2DoubleHandler, IContextUses, IDoubleHandlerWithUpdate
    {
        protected abstract double GetData(IDataBar bar);

        protected virtual IEnumerable<double> GetData(IEnumerable<IDataBar> source)
        {
            return source.Select(GetData);
        }

        public IList<double> Execute(ISecurity source)
        {
            var data = GetData(source.Bars);
            if (Context != null && Context.IsOptimization)
            {
                return data.ToArray();
            }
            var size = (source.Bars.Count / BarUtils.ListSizeStep + 1) * BarUtils.ListSizeStep;
            var list = new List<double>(size);
            list.AddRange(data);
            return list;
        }

        public void Update(IList<double> data, IDataBar bar, bool isNewBar)
        {
            var ndt = GetData(bar);
            if (isNewBar)
            {
                data.Add(ndt);
            }
            else if (data.Count > 0)
            {
                data[data.Count - 1] = ndt;
            }
        }

        public IContext Context { get; set; }
    }

    // TODO: Что-то у меня сомнения в точности описания. Может быть, не "количество операций", а просто "объем бара"???
    [HandlerDecimals(0)]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Volume", Language = Constants.En)]
    [HelperName("Объем", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Объем заключенных контрактов - количество операций с фьючерсными контрактами или опционами, совершенными за определенный период времени.")]
    [HelperDescription("Volume of the bar", Constants.En)]
    public class Volume : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar.Volume;
        }

        protected override IEnumerable<double> GetData(IEnumerable<IDataBar> source)
        {
            return source.Select(b => b.Volume);
        }
    }

    [HandlerDecimals(0)]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Open Interest", Language = Constants.En)]
    [HelperName("Открытый интерес", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Открытый интерес (объём открытых позиций) инструмента в том виде, как его присылает брокер. Эта же величина показывается в таблице 'Котировки'.")]
    [HelperDescription("Open interest as recieved from data feed. This value is shown also in 'Quotes' table.", Constants.En)]
    //[HandlerName("Open Interest")]
    public class OpenInterest : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar.Interest;
        }

        protected override IEnumerable<double> GetData(IEnumerable<IDataBar> source)
        {
            return source.Select(b => b.Interest);
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Time", Language = Constants.En)]
    [HelperName("Время", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Время каждого бара преобразуется в число в формате ЧЧММСС. Внимание! Блок 'Время' рассчитывается в момент пересчета агента. Соотвественно, выполнение входа в позицию и выполнение выхода из позиции возможны в указанный период времени работы агента + интервал пересчета агента. Если время выполнения заявки запланировано на момент позже времени закрытия торговой сессии, то данная заявка будет исполнена на следующий день.")]
    [HelperDescription("Time of every bar is converted to number as hhmmss.", Constants.En)]
    public sealed class Time : IBar2DoubleHandler
    {
        public IList<double> Execute(ISecurity source)
        {
            return source.Bars.Select(b => b.Date.Hour * 10000.0 + b.Date.Minute * 100.0 + b.Date.Second).ToArray();
        }
    }

    //[HandlerName("Time In Mins")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Time in minutes", Language = Constants.En)]
    [HelperName("Время в минутах", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Время бара в минутах от начала дня. Например, время бара 10:31 превратится в число 631.")]
    [HelperDescription("Time of the bar from midnight in minutes. I.e. time 10:31 converts to a number 631.", Constants.En)]
    public sealed class TimeInMins : IBar2DoubleHandler
    {
        public IList<double> Execute(ISecurity source)
        {
            return source.Bars.Select(b => b.Date.Hour * 60.0 + b.Date.Minute).ToArray();
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Date", Language = Constants.En)]
    [HelperName("Дата", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Блок принимает на вход финансовый инструмент и возвращает дату каждого бара в виде числа в формате ггммдд. Например, дата 31-12-2018 превратится в число 181231.")]
    [HelperDescription("Date of every bar is converted to number as yymmdd. I.e. date 12.31.2018 converts to a number 181231.", Constants.En)]
    public sealed class Date : IBar2DoubleHandler
    {
        public IList<double> Execute(ISecurity source)
        {
            return source.Bars.Select(b => (b.Date.Year % 100) * 10000.0 + b.Date.Month * 100.0 + b.Date.Day).ToArray();
        }
    }

    //[HandlerName("Day Of Week")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Day of week", Language = Constants.En)]
    [HelperName("День недели", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Блок принимает на вход финансовый инструмент и возвращает день недели в виде значений от 1 (понедельник) до 7 (воскресенье).")]
    [HelperDescription("Handler accepts an instrument at the entry and returns a week day as number from 1 (Monday) to 7 (Sunday).", Constants.En)]
    public sealed class DayOfWeek : IBar2DoubleHandler
    {
        public IList<double> Execute(ISecurity source)
        {
            return source.Bars.Select(b => (b.Date.DayOfWeek == System.DayOfWeek.Sunday
                                                  ? 7.0
                                                  : (double)b.Date.DayOfWeek)).ToArray();
        }
    }

    //[HandlerName("Day Of Month")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Day of month", Language = Constants.En)]
    [HelperName("День месяца", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Блок принимает на вход финансовый инструмент и возвращает день месяца в виде значений от 1 до 31.")]
    [HelperDescription("Handler accepts an instrument at the entry and returns a month day as number from 1 31.", Constants.En)]
    public sealed class DayOfMonth : IBar2DoubleHandler
    {
        public IList<double> Execute(ISecurity source)
        {
            return source.Bars.Select(b => (double)b.Date.Day).ToArray();
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Open", Language = Constants.En)]
    [HelperName("Открытие", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Цена открытия бара.")]
    [HelperDescription("Opening price of the bar.", Constants.En)]
    public class Open : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar.Open;
        }

        protected override IEnumerable<double> GetData(IEnumerable<IDataBar> source)
        {
            return source.Select(b => b.Open);
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Close", Language = Constants.En)]
    [HelperName("Закрытие", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Цена закрытия бара.")]
    [HelperDescription("Closing price of the bar.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/2MA.xml", "Пример 2MA", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/2MA.xml", "Example of 2MA", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class Close : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar.Close;
        }

        protected override IEnumerable<double> GetData(IEnumerable<IDataBar> source)
        {
            return source.Select(b => b.Close);
        }
    }

    // TODO: дописать в названием 'Минимум БАРА'?
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Low", Language = Constants.En)]
    [HelperName("Минимум", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Минимальная цена бара.")]
    [HelperDescription("A minimum price of a bar.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo.xml", "Пример стратегии Hi - Low", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo.xml", "Example of Hi - Low strategy", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class Low : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar.Low;
        }

        protected override IEnumerable<double> GetData(IEnumerable<IDataBar> source)
        {
            return source.Select(b => b.Low);
        }
    }

    // TODO: дописать в названием 'Максимум БАРА'?
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("High", Language = Constants.En)]
    [HelperName("Максимум", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Максимальная цена бара.")]
    [HelperDescription("A maximum price of a bar.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo.xml", "Пример стратегии Hi - Low", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/Hi_Lo.xml", "Example of Hi - Low strategy", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public class High : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar.High;
        }

        protected override IEnumerable<double> GetData(IEnumerable<IDataBar> source)
        {
            return source.Select(b => b.High);
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Best buy price", Language = Constants.En)]
    [HelperName("Цена лучшей покупки", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Цена лучшей покупки, запомненная в конце каждого бара (если доступно).")]
    [HelperDescription("Best buy price recorded at the end of every bar (if available).", Constants.En)]
    public class Bid : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar is IBar bar1 ? bar1.Bid : 0;
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Best sell price", Language = Constants.En)]
    [HelperName("Цена лучшей продажи", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Цена лучшей продажи, запомненная в конце каждого бара (если доступно).")]
    [HelperDescription("Best sell price recorded at the end of every bar (if available).", Constants.En)]
    public class Ask : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar is IBar bar1 ? bar1.Ask : 0;
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Step price", Language = Constants.En)]
    [HelperName("Стоимость шага цены", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Стоимость шага цены, запомненная в конце каждого бара (если доступно).")]
    [HelperDescription("A step price recorded at the end of every bar (if available).", Constants.En)]
    //[HandlerName("Step Price")]
    public class StepPrice : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar is IBar bar1 ? bar1.StepPrice : 0;
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Theoretical price", Language = Constants.En)]
    [HelperName("Теор. цена опциона", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Теоретическая цена инструмента (если есть) в том виде, как её присылает брокер. Эта же величиная показывается в таблице 'Котировки'.")]
    [HelperDescription("Theoretical instrument price received from the Exchange. This value is also shown at the Quotes window.", Constants.En)]
    //[HandlerName("Theoretical Price")]
    public class TheoreticalPrice : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar is IBar bar1 ? bar1.TheoreticalPrice : 0;
        }
    }

    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Option Volatility", Language = Constants.En)]
    [HelperName("Опционная волатильность", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Теоретическая волатильность опциона (если есть) в том виде, как её присылает биржа. Эта же величиная показывается в таблице 'Котировки'.")]
    [HelperDescription("Theoretical option volatility received from the Exchange. This value is also shown at the Quotes window.", Constants.En)]
    //[HandlerName("Option Volatility")]
    public class OptVolatility : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar is IBar bar1 ? bar1.Volatility : 1;
        }
    }

    // TODO: Что-то у меня сомнения в точности описания. Может быть, не "количество бидов", а "суммарный объем бидов"?
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Total demand", Language = Constants.En)]
    [HelperName("Суммарный спрос", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Суммарный спрос (количество бидов), запомненное в конце каждого бара (если доступно)")]
    [HelperDescription("Total demand size recorded at the end of every bar (if available)", Constants.En)]
    public class BidQty : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar is IBar bar1 ? bar1.BidQty : 0;
        }
    }

    // TODO: Что-то у меня сомнения в точности описания. Может быть, не "количество асков", а "суммарный объем асков"?
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Total offer", Language = Constants.En)]
    [HelperName("Суммарное предложение", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Суммарное предложение (количество асков), запомненное в конце каждого бара (если доступно)")]
    [HelperDescription("Total offer size recorded at the end of every bar (if available)", Constants.En)]
    public class AskQty : SecurityBase
    {
        protected override double GetData(IDataBar bar)
        {
            return bar is IBar bar1 ? bar1.AskQty : 0;
        }
    }

    // TODO: Что-то у меня сомнения в точности описания. Может быть, не "Обрезает High и Low", а "округляет цены бара до указанного количества знаков после запятой"?
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Cut off", Language = Constants.En)]
    [HelperName("Обрезать", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Обрезает High и Low на заданную величину.")]
    [HelperDescription("Cuts High and Low up to a selected value.", Constants.En)]
    public class Cutter : IBar2BarHandler
    {
        /// <summary>
        /// \~english Decimals for ceiling
        /// \~russian Количество десятичных знаков после запятой при округлении чисел вверх
        /// </summary>
        [HelperName("Decimals", Constants.En)]
        [HelperName("Десятичных знаков", Constants.Ru)]
        [Description("Количество десятичных знаков после запятой при округлении чисел вверх")]
        [HelperDescription("Decimals for ceiling", Constants.En)]
        [HandlerParameter(true, "2", NotOptimized = true)]
        public int Decimals
        {
            get;
            set;
        }

        public ISecurity Execute(ISecurity source)
        {
            var coef = Math.Pow(10, Decimals);
            var bars = source.Bars.Select(
                delegate(IDataBar bar)
                {
                    var babar = bar as IBar;
                    var nbar = babar == null
                        ? new DataBar()
                        : new BidAskBar
                        {
                            Ask = Math.Ceiling(babar.Ask * coef) / coef,
                            Bid = Math.Ceiling(babar.Bid * coef) / coef,
                            AskQty = babar.AskQty,
                            BidQty = babar.BidQty,
                            StepPrice = babar.StepPrice,
                            Volatility = babar.Volatility,
                            TheoreticalPrice = babar.TheoreticalPrice,
                        };
                    nbar.Date = bar.Date;
                    nbar.Open = Math.Ceiling(bar.Open * coef) / coef;
                    nbar.High = Math.Ceiling(bar.High * coef) / coef;
                    nbar.Low = Math.Ceiling(bar.Low * coef) / coef;
                    nbar.Close = Math.Ceiling(bar.Close * coef) / coef;
                    nbar.Volume = bar.Volume;
                    nbar.Interest = bar.Interest;
                    return nbar;
                });
            return source.CloneAndReplaceBars(bars);
        }
    }

    // TODO: Что-то у меня сомнения в точности описания. Что такое "(CB)"?
    //[HandlerName("Multiply (Security)")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Multiply (CB) by", Language = Constants.En)]
    [HelperName("Умножить (ЦБ) на", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Кубик преобразует бары на входе в синтетический инструмент (все цены исходных баров умножаются на заданный коэффициент).")]
    [HelperDescription("Handler converts bars of an input to a synthetic security (all prices of incoming bars are multiplied by a given coefficient). ", Constants.En)]
    public class SecMultiply : IBar2BarHandler
    {
        /// <summary>
        /// \~english Every bar of input is multiplied by this coefficient ( Mult*x )
        /// \~russian Каждый бар входной серии умножается на указанный коэффициент ( Mult*x )
        /// </summary>
        [HelperName("Multiply", Constants.En)]
        [HelperName("Множитель", Constants.Ru)]
        [Description("Каждый бар входной серии умножается на указанный коэффициент ( Mult*x )")]
        [HelperDescription("Every bar of input is multiplied by this coefficient ( Mult*x )", Constants.En)]
        [HandlerParameter(true, "10")]
        public double Coef
        {
            get;
            set;
        }

        public ISecurity Execute(ISecurity source)
        {
            var bars = source.Bars.Select(
                bar =>
                {
                    var babar = bar as IBar;
                    var nbar = babar == null
                        ? new DataBar()
                        : new BidAskBar
                        {
                            Ask = Math.Ceiling(babar.Ask * Coef),
                            Bid = Math.Ceiling(babar.Bid * Coef),
                            AskQty = babar.AskQty,
                            BidQty = babar.BidQty,
                            StepPrice = babar.StepPrice,
                            Volatility = babar.Volatility,
                            TheoreticalPrice = babar.TheoreticalPrice,
                        };
                    nbar.Date = bar.Date;
                    nbar.Open = Math.Ceiling(bar.Open * Coef);
                    nbar.High = Math.Ceiling(bar.High * Coef);
                    nbar.Low = Math.Ceiling(bar.Low * Coef);
                    nbar.Close = Math.Ceiling(bar.Close * Coef);
                    nbar.Volume = bar.Volume;
                    nbar.Interest = bar.Interest;
                    return nbar;
                });
            return source.CloneAndReplaceBars(bars);
        }
    }

    //[HandlerName("MultiplyWith")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Multiply by", Language = Constants.En)]
    [HelperName("Перемножить с", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [Input(1, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Кубик преобразует бары на входе в синтетический инструмент (каждый исходный бар умножаются на свой вес, взятый из второго входа).")]
    [HelperDescription("Handler converts bars of an input to a synthetic security (each incoming bar is multiplied by an individual weight from a second input).", Constants.En)]
    public class SecMultiplyWith : IBarDouble2BarHandler
    {
        /// <summary>
        /// \~english Decimals for ceiling
        /// \~russian Количество десятичных знаков после запятой при округлении чисел вверх
        /// </summary>
        [HelperName("Decimals", Constants.En)]
        [HelperName("Десятичных знаков", Constants.Ru)]
        [Description("Количество десятичных знаков после запятой при округлении чисел вверх")]
        [HelperDescription("Decimals for ceiling", Constants.En)]
        [HandlerParameter(true, "2", NotOptimized = true)]
        public int Decimals { get; set; }

        /// <summary>
        /// \~english Every bar of input is multiplied by this coefficient ( Mult * Source2 * x )
        /// \~russian Каждый бар входной серии умножается на указанный коэффициент ( Mult * Source2 * x )
        /// </summary>
        [HelperName("Multiply", Constants.En)]
        [HelperName("Множитель", Constants.Ru)]
        [Description("Каждый бар входной серии умножается на указанный коэффициент ( Mult * Source2 * x )")]
        [HelperDescription("Every bar of input is multiplied by this coefficient ( Mult * Source2 * x )", Constants.En)]
        [HandlerParameter(true, "1")]
        public double Coef { get; set; }

        public ISecurity Execute(ISecurity source, IList<double> src2)
        {
            var dcf = Math.Pow(10, Decimals);
            var bars = new List<IDataBar>(source.Bars.Count);
            for (int i = 0; i < source.Bars.Count; i++)
            {
                var bar = source.Bars[i];
                var s2 = src2.Count == 0 ? 0 : src2[Math.Min(i, src2.Count - 1)];
                var coef = Coef * s2 * dcf;
                var babar = bar as IBar;
                var nbar = babar == null
                    ? new DataBar()
                    : new BidAskBar
                    {
                        Ask = Math.Ceiling(babar.Ask * coef) / dcf,
                        Bid = Math.Ceiling(babar.Bid * coef) / dcf,
                        AskQty = babar.AskQty,
                        BidQty = babar.BidQty,
                        StepPrice = babar.StepPrice,
                        Volatility = babar.Volatility,
                        TheoreticalPrice = babar.TheoreticalPrice,
                    };
                nbar.Date = bar.Date;
                nbar.Open = Math.Ceiling(bar.Open * coef) / dcf;
                nbar.High = Math.Ceiling(bar.High * coef) / dcf;
                nbar.Low = Math.Ceiling(bar.Low * coef) / dcf;
                nbar.Close = Math.Ceiling(bar.Close * coef) / dcf;
                nbar.Volume = bar.Volume;
                nbar.Interest = bar.Interest;
                bars.Add(nbar);
            }
            return source.CloneAndReplaceBars(bars);
        }
    }

    //[HandlerName("DivideWith")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Divide with", Language = Constants.En)]
    [HelperName("Поделить с", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [Input(1, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Кубик преобразует бары на входе в синтетический инструмент (каждый исходный бар делится на свой вес, взятый из второго входа).")]
    [HelperDescription("Handler converts bars of an input to a synthetic security (each incoming bar is divided by an individual weight from a second input).", Constants.En)]
    public class SecDivideWith : IBarDouble2BarHandler
    {
        /// <summary>
        /// \~english Decimals for ceiling
        /// \~russian Количество десятичных знаков после запятой при округлении чисел вверх
        /// </summary>
        [HelperName("Decimals", Constants.En)]
        [HelperName("Десятичных знаков", Constants.Ru)]
        [Description("Количество десятичных знаков после запятой при округлении чисел вверх")]
        [HelperDescription("Decimals for ceiling", Constants.En)]
        [HandlerParameter(true, "2", NotOptimized = true)]
        public int Decimals { get; set; }

        /// <summary>
        /// \~english Every bar of input is multiplied by this coefficient ( Mult * x / Source2 )
        /// \~russian Каждый бар входной серии умножается на указанный коэффициент ( Mult * x / Source2 )
        /// </summary>
        [HelperName("Multiply", Constants.En)]
        [HelperName("Множитель", Constants.Ru)]
        [Description("Каждый бар входной серии умножается на указанный коэффициент ( Mult * x / Source2 )")]
        [HelperDescription("Every bar of input is multiplied by this coefficient ( Mult * x / Source2 )", Constants.En)]
        [HandlerParameter(true, "1")]
        public double Coef { get; set; }

        public ISecurity Execute(ISecurity source, IList<double> src2)
        {
            var dcf = Math.Pow(10, Decimals);
            var bars = new List<IDataBar>(source.Bars.Count);
            for (int i = 0; i < source.Bars.Count; i++)
            {
                var bar = source.Bars[i];
                var s2 = src2.Count == 0 ? 0 : src2[Math.Min(i, src2.Count - 1)];
                var coef = Coef * 1 / Math.Max(s2, 1e-10) * dcf;
                var babar = bar as IBar;
                var nbar = babar == null
                    ? new DataBar()
                    : new BidAskBar
                    {
                        Ask = Math.Ceiling(babar.Ask * coef) / dcf,
                        Bid = Math.Ceiling(babar.Bid * coef) / dcf,
                        AskQty = babar.AskQty,
                        BidQty = babar.BidQty,
                        StepPrice = babar.StepPrice,
                        Volatility = babar.Volatility,
                        TheoreticalPrice = babar.TheoreticalPrice,
                    };
                nbar.Date = bar.Date;
                nbar.Open = Math.Ceiling(bar.Open * coef) / dcf;
                nbar.High = Math.Ceiling(bar.High * coef) / dcf;
                nbar.Low = Math.Ceiling(bar.Low * coef) / dcf;
                nbar.Close = Math.Ceiling(bar.Close * coef) / dcf;
                nbar.Volume = bar.Volume;
                nbar.Interest = bar.Interest;
                bars.Add(nbar);
            }
            return source.CloneAndReplaceBars(bars);
        }
    }

    //[HandlerName("Absolute Commission")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Absolute comission", Language = Constants.En)]
    [HelperName("Абсолютная комиссия", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(0)]
    [Description("Значение комиссии на одну сделку (покупка или продажа) в абсолютных величинах.")]
    [HelperDescription("Absolute commission for one trade (long or short)", Constants.En)]
    public class AbsolutCommission : ICommissionHandler
    {
        /// <summary>
        /// \~english Absolute comission per 1 lot of a security
        /// \~russian Абсолютная комиссия на 1 лот инструмента
        /// </summary>
        [HelperName("Comission", Constants.En)]
        [HelperName("Комиссия", Constants.Ru)]
        [Description("Абсолютная комиссия на 1 лот инструмента")]
        [HelperDescription("Absolute comission per 1 lot of a security", Constants.En)]
        [HandlerParameter(true, "0.0002", NotOptimized = true, Editor = "CommissionTemplate")]
        public double Commission
        {
            get;
            set;
        }

        public void Execute(ISecurity source)
        {
            source.Commission = ComissionDelegate;
        }

        private double ComissionDelegate(IPosition pos, double price, double shares, bool isEntry, bool isPart)
        {
            return shares * Commission;
        }
    }

    //[HandlerName("Relative Commission")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Relative comission", Language = Constants.En)]
    [HelperName("Относительная комиссия", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(0)]
    [Description("Значение комиссии на одну сделку (покупка или продажа) в процентах. Стоимость денег: Применяется для расчета цены заемных средств, то есть торговли с плечом. Для коротких позиций считается со всей суммы сделки. Для длинной как число лотов -1 помноженное на цену лота.")]
    [HelperDescription("Relative commission for one trade (long or short) as percents. Cost of money: is used to get a price of a borrowed funds.", Constants.En)]
    public class RelativeCommission : ICommissionHandler
    {
        /// <summary>
        /// \~english Сomission as a percent of a volume
        /// \~russian Комиссия в процентах от объема сделки
        /// </summary>
        [HelperName("Comission, %", Constants.En)]
        [HelperName("Комиссия, %", Constants.Ru)]
        [Description("Комиссия в процентах от объема сделки")]
        [HelperDescription("Сomission as a percent of a volume", Constants.En)]
        [HandlerParameter(true, "0.05", NotOptimized = true, Editor = "CommissionPctTemplate")]
        public double CommissionPct
        {
            get;
            set;
        }

        /// <summary>
        /// \~english Margin to open or to keep position (as percents)
        /// \~russian Обеспечение (доля средств) для поддержания позиции (в процентах)
        /// </summary>
        [HelperName("Margin, %", Constants.En)]
        [HelperName("Маржа, %", Constants.Ru)]
        [Description("Обеспечение (доля средств) для поддержания позиции (в процентах)")]
        [HelperDescription("Margin to open or to keep position (as percents)", Constants.En)]
        [HandlerParameter(true, "10.0", NotOptimized = true)]
        public double MarginPct
        {
            get;
            set;
        }

        public void Execute(ISecurity source)
        {
            source.Commission = CommissionDelegate;
        }

        protected virtual double CommissionDelegate(IPosition pos, double price, double shares, bool isEntry, bool isPart)
        {
            var comm = price * CommissionPct / 100.0 * shares;
            if (!isEntry && !isPart)
            {
                var days = (pos.ExitBar.Date - pos.EntryBar.Date).TotalDays;
                var mshares = pos.MaxShares;
                if (pos.IsLong)
                {
                    var sh = pos.SharesOrigin;
                    if (sh <= 1)
                    {
                        mshares = 0;
                    }
                    else
                    {
                        mshares *= 1 - 1 / sh;
                    }
                }
                if (shares > 0)
                {
                    comm += pos.AverageEntryPrice * MarginPct / 100 * days / 365 * mshares;
                }
            }
            return comm;
        }
    }

    //[HandlerName("Relative Commission With Minimal")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Relative comission with minimum", Language = Constants.En)]
    [HelperName("Относ. комиссия с минимумом", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(0)]
    [Description("Значение комиссии на одну сделку (покупка или продажа) в процентах. Также можно указать минимальную абсолютную комиссию за сделку.")]
    [HelperDescription("Relative commission for one trade (long or short) as percents. Minumum absolute comission for a trade is additionally applied.", Constants.En)]
    public class RelativeCommisionWithMinimal : RelativeCommission
    {
        /// <summary>
        /// \~english Minimal absolute comission for a trade
        /// \~russian Минимальная абсолютная комиссия за сделку
        /// </summary>
        [HelperName("Minimal comission", Constants.En)]
        [HelperName("Минимальная комиссия", Constants.Ru)]
        [Description("Минимальная абсолютная комиссия за сделку")]
        [HelperDescription("Minimal absolute comission for a trade", Constants.En)]
        [HandlerParameter(true, "30.0", NotOptimized = true)]
        public double MinimalCommission
        {
            get;
            set;
        }

        protected override double CommissionDelegate(IPosition pos, double price, double shares, bool isEntry, bool isPart)
        {
            var res = base.CommissionDelegate(pos, price, shares, isEntry, isPart);
            var minimalCommission = MinimalCommission / pos.Security.LotSize;
            if (res < minimalCommission)
            {
                res = minimalCommission;
            }
            return res;
        }
    }

    [HandlerCategory(HandlerCategories.ServiceElements)]
    public sealed class TimestampHandler : IBar2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        public IList<double> Execute(ISecurity source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var bars = source.Bars;
            var barsCount = bars.Count;

            if (barsCount == 0)
                return EmptyArrays.Double;

            var results = Context.GetArray<double>(barsCount);
            for (var i = 0; i < barsCount; i++)
                results[i] = new DateTimeOffset(bars[i].Date).ToUnixTimeMilliseconds();

            return results;
        }
    }
}
// ReSharper restore MemberCanBePrivate.Global
// ReSharper restore UnusedAutoPropertyAccessor.Global
// ReSharper restore UnusedMember.Global
