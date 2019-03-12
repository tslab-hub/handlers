using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.DataSource;
using TSLab.Script.GraphPane;
using TSLab.Script.Handlers.Options;
using TSLab.Script.Optimization;
using TSLab.Utils;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(BlockCategories.GraphPaneHandler)]
    [HelperName("Interactive line", Language = Constants.En)]
    [HelperName("Интерактивная линия", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.GRAPHPANE, Name = "TemplateLibrary.Pane")]
    [Input(1, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Создает интерактивную линию на панели графика.")]
    [HelperDescription("Creates an interactive line on a chart pane.", Constants.En)]
    public sealed partial class InteractiveLineGen : IInteractiveLineGen
    {
        private static readonly IList<double> s_defaultResult1 = new[] { double.NaN };
        private IInteractiveLine m_interactiveLine;
        private IReadOnlyList<IDataBar> m_bars;

        public IContext Context { get; set; }

        public string VariableId { get; set; }

        /// <summary>
        /// \~english Pane side (vertical axis)
        /// \~russian Сторона графика (вертикальная ось)
        /// </summary>
        [HelperName("Pane side", Constants.En)]
        [HelperName("Сторона графика", Constants.Ru)]
        [Description("Сторона графика (вертикальная ось)")]
        [HelperDescription("Pane side (vertical axis)", Constants.En)]
        [HandlerParameter(Default = "RIGHT", IsShown = false, NotOptimized = true)]
        public PaneSides PaneSide { get; set; }

        /// <summary>
        /// \~english Color in hexadecimal RGB format (i.e. #ff0000 - red, #00ff00 - green, #0000ff - blue)
        /// \~russian Цвет в шестнадцатеричном формате RGB (например, #ff0000 - красный, #00ff00 - зеленый, #0000ff - синий)
        /// </summary>
        [HelperName("Color", Constants.En)]
        [HelperName("Цвет", Constants.Ru)]
        [Description("Цвет в шестнадцатеричном формате RGB (например, #ff0000 - красный, #00ff00 - зеленый, #0000ff - синий)")]
        [HelperDescription("Color in hexadecimal RGB format (i.e. #ff0000 - red, #00ff00 - green, #0000ff - blue)", Constants.En)]
        [HandlerParameter(Default = "#ff0000", IsShown = false, NotOptimized = true, Editor = "StringToColorTemplate")]
        public string Color { get; set; }

        /// <summary>
        /// \~english Thickness of a line
        /// \~russian Толщина линии
        /// </summary>
        [HelperName("Thickness", Constants.En)]
        [HelperName("Толщина", Constants.Ru)]
        [Description("Толщина линии")]
        [HelperDescription("Thickness of a line", Constants.En)]
        [HandlerParameter(true, "1", Min = "1", Max = "10", Step = "1", EditorMin = "1", EditorMax = "10")]
        public double Thickness { get; set; }

        /// <summary>
        /// \~english Line type (finit, infinit, ray)
        /// \~russian Вид линии (отрезок, луч, прямая)
        /// </summary>
        [HelperName("Line type", Constants.En)]
        [HelperName("Вид линии", Constants.Ru)]
        [Description("Вид линии (отрезок, луч, прямая)")]
        [HelperDescription("Line type (finit, infinit, ray)", Constants.En)]
        [HandlerParameter(Default = "Infinite", IsShown = false, NotOptimized = true)]
        public InteractiveLineMode Mode { get; set; }

        /// <summary>
        /// \~english Date of a first point
        /// \~russian Дата первой точки
        /// </summary>
        [HelperName("First date", Constants.En)]
        [HelperName("Первая дата", Constants.Ru)]
        [Description("Дата первой точки")]
        [HelperDescription("Date of a first point", Constants.En)]
        [HandlerParameter(Name = "FirstDateTime", Default = "0001-01-01T00:00:00.0000000", IsShown = false)]
        public DateTimeOptimProperty FirstDateTime { get; set; }

        /// <summary>
        /// \~english Y value of a first point
        /// \~russian Координата Y первой точки
        /// </summary>
        [HelperName("First value", Constants.En)]
        [HelperName("Первое значение", Constants.Ru)]
        [Description("Координата Y первой точки")]
        [HelperDescription("Y value of a first point", Constants.En)]
        [HandlerParameter(Name = "FirstValue", Default = "0", IsShown = false)]
        public OptimProperty FirstValue { get; set; }

        /// <summary>
        /// \~english Date of a second point
        /// \~russian Дата второй точки
        /// </summary>
        [HelperName("Second date", Constants.En)]
        [HelperName("Вторая дата", Constants.Ru)]
        [Description("Дата второй точки")]
        [HelperDescription("Date of a second point", Constants.En)]
        [HandlerParameter(Name = "SecondDateTime", Default = "9999-12-31T23:59:59.9999999", IsShown = false)]
        public DateTimeOptimProperty SecondDateTime { get; set; }

        /// <summary>
        /// \~english Y value of a second point
        /// \~russian Координата Y второй точки
        /// </summary>
        [HelperName("Second value", Constants.En)]
        [HelperName("Второе значение", Constants.Ru)]
        [Description("Координата Y второй точки")]
        [HelperDescription("Y value of a second point", Constants.En)]
        [HandlerParameter(Name = "SecondValue", Default = "0", IsShown = false)]
        public OptimProperty SecondValue { get; set; }

        /// <summary>
        /// \~english Recalculate agent when line changes its parameters
        /// \~russian Пересчет агента, если изменяются параметры линии
        /// </summary>
        [HelperName("Recalculate agent?", Constants.En)]
        [HelperName("Пересчитать агент?", Constants.Ru)]
        [Description("Пересчет агента, если изменяются параметры линии")]
        [HelperDescription("Recalculate agent when line changes its parameters", Constants.En)]
        [HandlerParameter(Default = "true", IsShown = false, NotOptimized = true)]
        public bool IsNeedRecalculate { get; set; }

        public IList<double> Execute(IGraphPane pane, ISecurity security)
        {
            if (pane == null)
                throw new ArgumentNullException(nameof(pane));

            if (security == null)
                throw new ArgumentNullException(nameof(security));

            var id = typeof(IInteractiveLineGen).Name + "." + VariableId;
            var container = (NotClearableContainer<InteractiveLineGen>)Context.LoadObject(id);
            container?.Content.Unsubscribe();
            var result = GetLine(pane, m_bars = security.Bars);
            Subscribe();
            Context.StoreObject(id, new NotClearableContainer<InteractiveLineGen>(this));
            return result;
        }

        private IList<double> GetLine(IGraphPane pane, IReadOnlyList<IDataBar> bars)
        {
            m_interactiveLine = null;
            if (bars == null || bars.Count == 0)
                return EmptyArrays.Double;

            if (bars.Count == 1)
                return s_defaultResult1;

            var interactiveLine = GetInteractiveLine(pane, bars);
            var firstPosition = interactiveLine.FirstPoint.MarketPosition;
            var secondPosition = interactiveLine.SecondPoint.MarketPosition;

            if (firstPosition.X == secondPosition.X)
                return new ConstList(double.NaN, bars.Count);

            var firstBarIndex = GetBarIndex(bars, firstPosition.X);
            var secondBarIndex = GetBarIndex(bars, secondPosition.X);

            if (firstBarIndex == null || secondBarIndex == null || firstBarIndex == secondBarIndex)
                return new ConstList(double.NaN, bars.Count);

            var a = (secondPosition.Y - firstPosition.Y) / (secondBarIndex.Value - firstBarIndex.Value);
            var b = firstPosition.Y - a * firstBarIndex.Value;

            int minIndex;
            int maxIndex;

            switch (Mode)
            {
                case InteractiveLineMode.Finite:
                    if (firstBarIndex < secondBarIndex)
                    {
                        minIndex = firstBarIndex.Value;
                        maxIndex = secondBarIndex.Value;
                    }
                    else
                    {
                        minIndex = secondBarIndex.Value;
                        maxIndex = firstBarIndex.Value;
                    }
                    break;

                case InteractiveLineMode.Infinite:
                    minIndex = 0;
                    maxIndex = bars.Count - 1;
                    break;

                case InteractiveLineMode.Ray:
                    if (firstBarIndex < secondBarIndex)
                    {
                        minIndex = firstBarIndex.Value;
                        maxIndex = bars.Count - 1;
                    }
                    else
                    {
                        minIndex = 0;
                        maxIndex = firstBarIndex.Value;
                    }
                    break;

                default:
                    throw new InvalidOperationException();
            }
            return a == 0 ? (IList<double>)new ConstList(b, bars.Count, minIndex, maxIndex) : new LineList(a, b, bars.Count, minIndex, maxIndex);
        }

        private IInteractiveLine GetInteractiveLine(IGraphPane pane, IReadOnlyList<IDataBar> bars)
        {
            var minDate = bars.First().Date;
            var maxDate = bars.Last().Date;
            var interactiveLine = (IInteractiveLine)pane.GetInteractiveObject(VariableId);
            var intColor = ColorParser.Parse(Color);
            MarketPoint firstMarketPosition;
            MarketPoint secondMarketPosition;

            if (interactiveLine != null)
            {
                interactiveLine.ExecutionDataBars = bars;
                firstMarketPosition = interactiveLine.FirstPoint.MarketPosition;
                secondMarketPosition = interactiveLine.SecondPoint.MarketPosition;
                CorrectMarketPointsEx(ref firstMarketPosition, ref secondMarketPosition, minDate, maxDate);

                if (interactiveLine.PaneSides == PaneSide && interactiveLine.Color == intColor && interactiveLine.Mode == Mode)
                {
                    pane.AddUnremovableInteractiveObjectId(VariableId);
                    interactiveLine.Thickness = Thickness;
                    interactiveLine.FirstPoint.MarketPosition = firstMarketPosition;
                    interactiveLine.SecondPoint.MarketPosition = secondMarketPosition;
                    return m_interactiveLine = interactiveLine;
                }
                pane.RemoveInteractiveObject(VariableId);
            }
            else
            {
                firstMarketPosition = new MarketPoint(FirstDateTime.Value, FirstValue.Value);
                secondMarketPosition = new MarketPoint(SecondDateTime.Value, SecondValue.Value);
                CorrectMarketPointsEx(ref firstMarketPosition, ref secondMarketPosition, minDate, maxDate);
            }
            m_interactiveLine = pane.AddInteractiveLine(VariableId, PaneSide, false, intColor, Mode, firstMarketPosition, secondMarketPosition);
            m_interactiveLine.Thickness = Thickness;
            m_interactiveLine.ExecutionDataBars = bars;
            return m_interactiveLine;
        }

        private void CorrectMarketPointsEx(ref MarketPoint firstMarketPosition, ref MarketPoint secondMarketPosition, DateTime minDate, DateTime maxDate)
        {
            CorrectMarketPoints(ref firstMarketPosition, ref secondMarketPosition, minDate, maxDate);
            SetValue(FirstDateTime, firstMarketPosition.X);
            SetValue(FirstValue, firstMarketPosition.Y);
            SetValue(SecondDateTime, secondMarketPosition.X);
            SetValue(SecondValue, secondMarketPosition.Y);
        }

        private static void CorrectMarketPoints(ref MarketPoint firstMarketPosition, ref MarketPoint secondMarketPosition, DateTime minDate, DateTime maxDate)
        {
            var firstDate = firstMarketPosition.X;
            var secondDate = secondMarketPosition.X;

            if (firstDate < secondDate)
            {
                if (firstDate < minDate || firstDate >= maxDate)
                    firstDate = minDate;

                if (secondDate <= minDate || secondDate > maxDate)
                    secondDate = maxDate;
            }
            else if (firstDate > secondDate)
            {
                if (secondDate < minDate || secondDate >= maxDate)
                    secondDate = minDate;

                if (firstDate <= minDate || firstDate > maxDate)
                    firstDate = maxDate;
            }
            else
            {
                firstDate = minDate;
                secondDate = maxDate;
            }
            if (double.IsNaN(firstMarketPosition.Y) && double.IsNaN(secondMarketPosition.Y))
            {
                firstMarketPosition = new MarketPoint(firstMarketPosition.X, 0);
                secondMarketPosition = new MarketPoint(secondMarketPosition.X, 0);
            }
            else if (double.IsNaN(firstMarketPosition.Y))
                firstMarketPosition = new MarketPoint(firstMarketPosition.X, secondMarketPosition.Y);
            else if (double.IsNaN(secondMarketPosition.Y))
                secondMarketPosition = new MarketPoint(secondMarketPosition.X, firstMarketPosition.Y);

            if (firstMarketPosition.X != firstDate || secondMarketPosition.X != secondDate)
            {
                var a = (secondMarketPosition.Y - firstMarketPosition.Y) / (secondMarketPosition.X.Ticks - firstMarketPosition.X.Ticks);
                var b = firstMarketPosition.Y - a * firstMarketPosition.X.Ticks;

                if (firstMarketPosition.X != firstDate)
                    firstMarketPosition = new MarketPoint(firstDate, a * firstDate.Ticks + b);

                if (secondMarketPosition.X != secondDate)
                    secondMarketPosition = new MarketPoint(secondDate, a * secondDate.Ticks + b);
            }
        }

        private static int? GetBarIndex(IReadOnlyList<IDataBar> bars, DateTime date)
        {
            if (date < bars.First().Date || date > bars.Last().Date)
                return null;

            for (var i = 1; i < bars.Count; i++)
                if (date < bars[i].Date)
                    return i - 1;

            return bars.Count - 1;
        }

        private void Unsubscribe()
        {
            if (m_interactiveLine != null)
            {
                FirstUnsubscribe();
                SecondUnsubscribe();
            }
        }

        private void Subscribe()
        {
            if (m_interactiveLine != null)
            {
                FirstSubscribe();
                SecondSubscribe();
            }
        }

        private void FirstUnsubscribe()
        {
            FirstDateTime.PropertyChanged -= OnFirstDateTimePropertyChanged;
            FirstValue.PropertyChanged -= OnFirstValuePropertyChanged;
            m_interactiveLine.FirstPoint.PropertyChanged -= OnFirstPointPropertyChanged;
        }

        private void FirstSubscribe()
        {
            FirstDateTime.PropertyChanged += OnFirstDateTimePropertyChanged;
            FirstValue.PropertyChanged += OnFirstValuePropertyChanged;
            m_interactiveLine.FirstPoint.PropertyChanged += OnFirstPointPropertyChanged;
        }

        private void OnFirstDateTimePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IOptimPropertyBase.Value))
            {
                FirstUnsubscribe();
                var dateTimeOptimProperty = (DateTimeOptimProperty)sender;
                var firstMarketPosition = new MarketPoint(dateTimeOptimProperty.Value, m_interactiveLine.FirstPoint.MarketPosition.Y);
                var secondMarketPosition = m_interactiveLine.SecondPoint.MarketPosition;
                CorrectMarketPoints(ref firstMarketPosition, ref secondMarketPosition, m_bars.First().Date, m_bars.Last().Date);
                SetValue(dateTimeOptimProperty, firstMarketPosition.X);
                SetValue(FirstValue, firstMarketPosition.Y);
                m_interactiveLine.FirstPoint.MarketPosition = firstMarketPosition;
                FirstSubscribe();
            }
        }

        private void OnFirstValuePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IOptimPropertyBase.Value))
            {
                FirstUnsubscribe();
                var point = m_interactiveLine.FirstPoint;
                point.MarketPosition = new MarketPoint(point.MarketPosition.X, ((OptimProperty)sender).Value);
                FirstSubscribe();
            }
        }

        private void OnFirstPointPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var interactivePoint = (IInteractivePoint)sender;
            if (e.PropertyName == nameof(IInteractivePoint.MarketPosition))
            {
                FirstUnsubscribe();
                var marketPosition = interactivePoint.MarketPosition;
                SetValue(FirstDateTime, marketPosition.X);
                SetValue(FirstValue, marketPosition.Y);
                FirstSubscribe();
            }
            if (IsNeedRecalculate && !interactivePoint.IsMoving && (e.PropertyName == nameof(IInteractivePoint.MarketPosition) || e.PropertyName == nameof(IInteractivePoint.IsMoving)))
                Context.Recalc();
        }

        private void SecondUnsubscribe()
        {
            SecondDateTime.PropertyChanged -= OnSecondDateTimePropertyChanged;
            SecondValue.PropertyChanged -= OnSecondValuePropertyChanged;
            m_interactiveLine.SecondPoint.PropertyChanged -= OnSecondPointPropertyChanged;
        }

        private void SecondSubscribe()
        {
            SecondDateTime.PropertyChanged += OnSecondDateTimePropertyChanged;
            SecondValue.PropertyChanged += OnSecondValuePropertyChanged;
            m_interactiveLine.SecondPoint.PropertyChanged += OnSecondPointPropertyChanged;
        }

        private void OnSecondDateTimePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IOptimPropertyBase.Value))
            {
                SecondUnsubscribe();
                var dateTimeOptimProperty = (DateTimeOptimProperty)sender;
                var firstMarketPosition = m_interactiveLine.FirstPoint.MarketPosition;
                var secondMarketPosition = new MarketPoint(dateTimeOptimProperty.Value, m_interactiveLine.SecondPoint.MarketPosition.Y);
                CorrectMarketPointsEx(ref firstMarketPosition, ref secondMarketPosition, m_bars.First().Date, m_bars.Last().Date);
                SetValue(dateTimeOptimProperty, secondMarketPosition.X);
                SetValue(SecondValue, secondMarketPosition.Y);
                m_interactiveLine.SecondPoint.MarketPosition = secondMarketPosition;
                SecondSubscribe();
            }
        }

        private void OnSecondValuePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IOptimPropertyBase.Value))
            {
                SecondUnsubscribe();
                var point = m_interactiveLine.SecondPoint;
                point.MarketPosition = new MarketPoint(point.MarketPosition.X, ((OptimProperty)sender).Value);
                SecondSubscribe();
            }
        }

        private void OnSecondPointPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var interactivePoint = (IInteractivePoint)sender;
            if (e.PropertyName == nameof(IInteractivePoint.MarketPosition))
            {
                SecondUnsubscribe();
                var marketPosition = interactivePoint.MarketPosition;
                SetValue(SecondDateTime, marketPosition.X);
                SetValue(SecondValue, marketPosition.Y);
                SecondSubscribe();
            }
            if (IsNeedRecalculate && !interactivePoint.IsMoving && (e.PropertyName == nameof(IInteractivePoint.MarketPosition) || e.PropertyName == nameof(IInteractivePoint.IsMoving)))
                Context.Recalc();
        }

        private static void SetValue<TValue>(IOptimPropertyBase<TValue> optimPropertyBase, TValue value)
        {
            ((OptimDataBase)optimPropertyBase.Data).Value = optimPropertyBase.Value = value;
        }
    }
}
