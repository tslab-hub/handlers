using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.DataSource;
using TSLab.Script.GraphPane;
using TSLab.Script.Handlers.Options;
using TSLab.Script.Optimization;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(BlockCategories.GraphPaneHandler)]
    [HelperName("Interactive constant", Language = Constants.En)]
    [HelperName("Интерактивная константа", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.GRAPHPANE, Name = "TemplateLibrary.Pane")]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Создает интерактивную константу на панели графика (горизонтальная линия).")]
    [HelperDescription("Creates an interactive constant on a chart pane (a horizontal line).", Constants.En)]
    public sealed class InteractiveConstGen : ConstGenBase<double>, IInteractiveConstGen
    {
        private IInteractiveSimpleLine m_interactiveSimpleLine;

        public IContext Context { get; set; }

        /// <summary>
        /// \~english Value of a constant
        /// \~russian Значение константы
        /// </summary>
        [HelperName("Value", Constants.En)]
        [HelperName("Значение", Constants.Ru)]
        [Description("Значение константы")]
        [HelperDescription("Value of a constant", Constants.En)]
        [HandlerParameter(Name = "Value", Default = "100", IsShown = false)]
        public OptimProperty Value { get; set; }

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
        /// \~english Recalculate agent when line changes its parameters
        /// \~russian Пересчет агента, если изменяются параметры линии
        /// </summary>
        [HelperName("Recalculate agent?", Constants.En)]
        [HelperName("Пересчитать агент?", Constants.Ru)]
        [Description("Пересчет агента, если изменяются параметры линии")]
        [HelperDescription("Recalculate agent when line changes its parameters", Constants.En)]
        [HandlerParameter(Default = "true", IsShown = false, NotOptimized = true)]
        public bool IsNeedRecalculate { get; set; }

        public IList<double> Execute(IGraphPane pane)
        {
            if (pane == null)
                throw new ArgumentNullException(nameof(pane));

            var id = typeof(IInteractiveConstGen).Name + "." + Value.Data.GetId();
            var container = (NotClearableContainer<InteractiveConstGen>)Context.LoadObject(id);
            container?.Content.Unsubscribe();
            InitInteractiveSimpleLine(pane);
            Subscribe();
            MakeList(Context.BarsCount, Value.Value);
            Context.StoreObject(id, new NotClearableContainer<InteractiveConstGen>(this));
            return this;
        }

        private void InitInteractiveSimpleLine(IGraphPane pane)
        {
            var id = Value.Data.GetId();
            m_interactiveSimpleLine = (IInteractiveSimpleLine)pane.GetInteractiveObject(id);
            var intColor = ColorParser.Parse(Color);
            MarketPoint marketPosition;

            if (m_interactiveSimpleLine != null)
            {
                marketPosition = new MarketPoint(m_interactiveSimpleLine.MarketPosition.X, Value.Value);
                if (m_interactiveSimpleLine.PaneSides == PaneSide && m_interactiveSimpleLine.Color == intColor)
                {
                    m_interactiveSimpleLine.Thickness = Thickness;
                    m_interactiveSimpleLine.MarketPosition = marketPosition;
                    pane.AddUnremovableInteractiveObjectId(id);
                    return;
                }
                pane.RemoveInteractiveObject(id);
            }
            else
                marketPosition = new MarketPoint(DateTime.UtcNow, Value.Value);

            m_interactiveSimpleLine = pane.AddInteractiveSimpleLine(id, PaneSide, false, intColor, InteractiveSimpleLineMode.Horizontal, marketPosition);
            m_interactiveSimpleLine.Thickness = Thickness;
        }

        private void Unsubscribe()
        {
            Value.PropertyChanged -= OnValuePropertyChanged;
            m_interactiveSimpleLine.PropertyChanged -= OnInteractiveSimpleLinePropertyChanged;
        }

        private void Subscribe()
        {
            Value.PropertyChanged += OnValuePropertyChanged;
            m_interactiveSimpleLine.PropertyChanged += OnInteractiveSimpleLinePropertyChanged;
        }

        private void OnValuePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IOptimPropertyBase.Value))
            {
                Unsubscribe();
                m_interactiveSimpleLine.MarketPosition = new MarketPoint(m_interactiveSimpleLine.MarketPosition.X, Value.Value);
                Subscribe();
            }
        }

        private void OnInteractiveSimpleLinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IInteractivePoint.MarketPosition))
            {
                Unsubscribe();
                ((OptimDataBase)Value.Data).Value = Value.Value = m_interactiveSimpleLine.MarketPosition.Y;
                Subscribe();
            }
            if (IsNeedRecalculate && !m_interactiveSimpleLine.IsMoving && (e.PropertyName == nameof(IInteractivePoint.MarketPosition) || e.PropertyName == nameof(IInteractivePoint.IsMoving)))
                Context.Recalc();
        }
    }
}
