using System;
using System.Linq;
using TSLab.DataSource;
using TSLab.Script;
using TSLab.Script.Handlers;

namespace TSLab.Helper.Handlers
{
    /// <summary>
    /// Для каждого бара берет сделки и проверяет их на то чтобы:
    /// цены не выходили за границу бара,
    /// чтобы время не выходило за границу бара.
    /// </summary>
#if DEBUG
    [HandlerCategory("RusAlgo")]
#pragma warning disable CS0612
    [HandlerName("Ticks Test")]
#pragma warning restore CS0612
    [Input(0, TemplateTypes.SECURITY, Name = "Инструмент")]
#endif
#if !DEBUG
    [HandlerInvisible]
#endif
    public class TickTestHandler : IOneSourceHandler, ISecurityInputs, ISecurityReturns, IStreamHandler, IContextUses
    {
        public IContext Context { set; private get; }

        [HandlerParameter(Name = "Макс Ошибок", Default = "10", NotOptimized = true)]
        public uint MaxError { get; set; }

        [HandlerParameter(Name = "Цена тиков", Default = "true", NotOptimized = true)]
        public bool TickPrice { get; set; }

        [HandlerParameter(Name = "Время тиков", Default = "true", NotOptimized = true)]
        public bool TickDate { get; set; }

        [HandlerParameter(Name = "Объем тиков", Default = "true", NotOptimized = true)]
        public bool TickVolume { get; set; }

        public ISecurity Execute(ISecurity sec)
        {
            if (sec.IntervalBase == DataIntervals.TICK)
                throw new InvalidOperationException("Кубик не работает на тиковом таймфрейме");

            var ctx = Context;
            var errors = 0;

            for (int i = 0; i < ctx.BarsCount - 1; i++)
            {
                var bar = sec.Bars[i];

                var trades = sec.GetTrades(i);
                if (!trades.Any())
                    continue;

                // время не должно выходить за пределы времени бара
                if (TickDate)
                {
                    #region время тика

                    var barOpenDate = bar.Date;
                    var barCloseDate = bar.Date + sec.IntervalInstance.Shift;

                    foreach (var trade in trades)
                    {
                        if (trade.Date < barOpenDate || trade.Date >= barCloseDate)
                        {
                            var msg =
                                string.Format(
                                    "Для сделки с id:{0} дата {1:dd.MM.yyyy HH:mm:ss.fff} вылазит за границы свечи {2} [{3:dd.MM.yyyy HH:mm:ss.fff} - {4:dd.MM.yyyy HH:mm:ss.fff}].",
                                    trade.TradeNo, trade.Date, i, barOpenDate, barCloseDate);
                            ctx.Log(msg, MessageType.Error, true);

                            errors++;
                        }

                        if (errors > MaxError)
                        {
                            ctx.Log($"Число ошибок превысило {MaxError}. Прерываю проверку.", MessageType.Error, true);
                            return sec;
                        }
                    }

                    #endregion
                }

                if (TickPrice)
                {
                    #region цена сделки

                    var tradeHigh = 0.0;
                    var tradeLow = double.MaxValue;

                    // выход цены за границы бара вообще
                    foreach (var trade in trades)
                    {
                        tradeHigh = Math.Max(trade.Price, tradeHigh);
                        tradeLow = Math.Min(trade.Price, tradeLow);

                        // цена не должна вылетать за границы хай лоу бара
                        if (trade.Price > bar.High || trade.Price < bar.Low)
                        {
                            var msg =
                                string.Format("Для сделки с id:{0} цена {1} вылазит за границы свечи {2} [{3} - {4}].",
                                    trade.TradeNo, trade.Price, i, bar.High, bar.Low);
                            ctx.Log(msg, MessageType.Error, true);

                            errors++;
                        }

                        if (errors > MaxError)
                        {
                            ctx.Log($"Число ошибок превысило {MaxError}. Прерываю проверку.", MessageType.Error, true);
                            return sec;
                        }
                    }

                    // проверим OHLC бара и тиков
                    if (tradeHigh != bar.High)
                    {
                        ctx.Log($"High {tradeHigh} в тиках не равен таковому в баре {bar.High}.", MessageType.Error, true);
                        errors++;
                    }

                    if (tradeLow != bar.Low)
                    {
                        ctx.Log($"Low {tradeLow} в тиках не равен таковому в баре {bar.Low}.", MessageType.Error, true);
                        errors++;
                    }

                    if (trades[0].Price != bar.Open)
                    {
                        ctx.Log($"Open {trades[0].Price} в тиках не равен таковому в баре {bar.Open}.", MessageType.Error, true);
                        errors++;
                    }

                    if (trades.Last().Price != bar.Close)
                    {
                        ctx.Log($"Close {trades.Last().Price} в тиках не равен таковому в баре {bar.Close}.", MessageType.Error, true);
                        errors++;
                    }

                    #endregion
                }

                if (TickVolume)
                {
                    #region объем тиков

                    // объем сделок должен соответствовать объему бара
                    var tradeVol = trades.Sum(t => t.Quantity);
                    if (tradeVol != bar.Volume)
                    {
                        ctx.Log($"Объем свечи в тиках {tradeVol} не равен таковому в баре {bar.Volume}.", MessageType.Error, true);
                        errors++;
                    }

                    #endregion
                }

                if (errors > MaxError)
                {
                    ctx.Log($"Число ошибок превысило {MaxError}. Прерываю проверку.", MessageType.Error, true);
                    return sec;
                }
            }

            return sec;
        }
    }
}
