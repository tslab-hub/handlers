using System;
using System.Collections.Generic;
using System.ComponentModel;
using TSLab.DataSource;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Current Session Bar", Language = Constants.En)]
    [HelperName("Текущий бар сессии", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Показывает текущий порядковый номер бара c начала сессии.")]
    [HelperDescription("A current bar index from start of current trading day.", Constants.En)]
    public sealed class SessionHeld : IBar2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        public IList<double> Execute(ISecurity source)
        {
            var bars = source.Bars;
            var result = Context?.GetArray<double>(bars.Count) ?? new double[bars.Count];
            var currentHeld = 0;

            for (var i = 1; i < result.Length; i++)
            {
                if (bars[i - 1].Date.Day != bars[i].Date.Day)
                    currentHeld = 0;
                else
                    currentHeld++;

                result[i] = currentHeld;
            }
            return result;
        }
    }

    /// <summary>
    /// Базовый класс для группы кубиков, которые вычисляют экстремумы торговой сессии на каждом баре
    /// </summary>
    [HandlerCategory(HandlerCategories.TradeMath)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    public abstract class SessionBase : IBar2DoubleHandler, IContextUses
    {
        public IContext Context { get; set; }

        // TODO: смысл параметра Session неочевиден из кода. Номер торговой сессии о которой идет речь?
        /// <summary>
        /// \~english Session
        /// \~russian Сессия
        /// </summary>
        [HelperName("Session", Constants.En)]
        [HelperName("Сессия", Constants.Ru)]
        [Description("Сессия")]
        [HelperDescription("Session", Constants.En)]
        [HandlerParameter(true, "1", Min = "0", Max = "10", Step = "1", EditorMin = "0")]
        public int Session { get; set; }

        public IList<double> Execute(ISecurity source)
        {
            var bars = source.Bars;
            var result = Context?.GetArray<double>(bars.Count) ?? new double[bars.Count];

            if (result.Length > 0)
            {
                var currentResult = new List<double>(Session + 1);
                var initialValue = GetInitialValue(bars[0]);

                for (var i = 0; i <= Session; i++)
                    currentResult.Add(initialValue);

                for (var i = 1; i < result.Length; i++)
                {
                    var bar = bars[i];
                    if (bars[i - 1].Date.Day != bar.Date.Day)
                    {
                        currentResult.RemoveAt(0);
                        currentResult.Add(GetValue(bar));
                    }
                    currentResult[Session] = GetValue(bar, currentResult);
                    result[i] = currentResult[0];
                }
            }
            return result;
        }

        protected abstract double GetInitialValue(IDataBar bar);

        protected abstract double GetValue(IDataBar bar);

        protected abstract double GetValue(IDataBar bar, IReadOnlyList<double> currentResult);
    }

    //[HandlerCategory(HandlerCategories.TradeMath)]
    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Session open", Language = Constants.En)]
    [HelperName("Открытие сессии", Language = Constants.Ru)]
    [Description("Цена открытия торговой сессии.")]
    [HelperDescription("Shows a session opening trade price.", Constants.En)]
    public sealed class SessionOpen : SessionBase
    {
        protected override double GetInitialValue(IDataBar bar)
        {
            return bar.Open;
        }

        protected override double GetValue(IDataBar bar)
        {
            return bar.Open;
        }

        protected override double GetValue(IDataBar bar, IReadOnlyList<double> currentResult)
        {
            return currentResult[Session];
        }
    }

    //[HandlerCategory(HandlerCategories.TradeMath)]
    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Session close", Language = Constants.En)]
    [HelperName("Закрытие сессии", Language = Constants.Ru)]
    [Description("Цена закрытия торговой сессии.")]
    [HelperDescription("Shows a session close trade price.", Constants.En)]
    public sealed class SessionClose : SessionBase
    {
        protected override double GetInitialValue(IDataBar bar)
        {
            return bar.Open;
        }

        protected override double GetValue(IDataBar bar)
        {
            return bar.Close;
        }

        protected override double GetValue(IDataBar bar, IReadOnlyList<double> currentResult)
        {
            return bar.Close;
        }
    }

    //[HandlerCategory(HandlerCategories.TradeMath)]
    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Session high", Language = Constants.En)]
    [HelperName("Максимум сессии", Language = Constants.Ru)]
    [Description("Максимальное значение сессии.")]
    [HelperDescription("Shows the session highest trade price.", Constants.En)]
    public sealed class SessionHigh : SessionBase
    {
        protected override double GetInitialValue(IDataBar bar)
        {
            return bar.High;
        }

        protected override double GetValue(IDataBar bar)
        {
            return bar.High;
        }

        protected override double GetValue(IDataBar bar, IReadOnlyList<double> currentResult)
        {
            return Math.Max(bar.High, currentResult[Session]);
        }
    }

    //[HandlerCategory(HandlerCategories.TradeMath)]
    // Категория и описание входов/выходов идет через базовый класс.
    [HelperName("Session low", Language = Constants.En)]
    [HelperName("Минимум сессии", Language = Constants.Ru)]
    [Description("Минимальное значение сессии.")]
    [HelperDescription("Shows the session lowest trade price.", Constants.En)]
    public sealed class SessionLow : SessionBase
    {
        protected override double GetInitialValue(IDataBar bar)
        {
            return bar.Low;
        }

        protected override double GetValue(IDataBar bar)
        {
            return bar.Low;
        }

        protected override double GetValue(IDataBar bar, IReadOnlyList<double> currentResult)
        {
            return Math.Min(bar.Low, currentResult[Session]);
        }
    }
}
