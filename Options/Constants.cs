using System.Collections.Generic;
using System.Collections.ObjectModel;
using TSLab.Script.CanvasPane;

namespace TSLab.Script.Handlers.Options
{
    public static class Constants
    {
        /// <summary>Замена NaN на предопределенное число</summary>
        public const double NaN = 0;

        /// <summary>100.0</summary>
        public const double PctMult = 100.0;
        /// <summary>1 000.0</summary>
        public const double PromilleMult = 1000.0;
        /// <summary>1 000 000.0</summary>
        public const double MillionMult = 1000000.0;

        public const string En = "en-US";
        public const string Ru = "ru-RU";

        /// <summary>HTML dot (&point;)</summary>
        public const string HtmlDot = @"&point;";

        /// <summary>На фортс большинство фьючерсов де-юро умирают в 18:45</summary>
        public const string DefaultFortsExpiryTimeStr = "18:45";

        /// <summary>Опционный источник для задания в качестве имени атрибута Input</summary>
        public const string OptionSource = "OPTIONSource";
        /// <summary>Опционная серия для задания в качестве имени атрибута Input</summary>
        public const string OptionSeries = "OptionSeries";
        /// <summary>Любой инструмент (Security or Option Series or OPTION) для задания в качестве имени атрибута Input</summary>
        public const string AnyOption = "AnyOption";
        /// <summary>Обычный линейный источник для задания в качестве имени атрибута Input</summary>
        public const string SecuritySource = "SECURITYSource";
        /// <summary>Источник типа 'Позиция' для задания в качестве имени атрибута Input</summary>
        public const string PositionSource = "POSITIONSource";
        /// <summary>Цена базового актива для задания в качестве имени атрибута Input</summary>
        public const string FutPx = "FutPx";
        /// <summary>Дельта позиции для задания в качестве имени атрибута Input</summary>
        public const string Delta = "Delta";
        /// <summary>Время для задания в качестве имени атрибута Input</summary>
        public const string Time = "Time";
        /// <summary>Улыбка для задания в качестве имени атрибута Input</summary>
        public const string Smile = "Smile";
        /// <summary>Страйк для задания в качестве имени атрибута Input</summary>
        public const string Strike = "Strike";
        /// <summary>Безрисковая ставка для задания в качестве имени атрибута Input</summary>
        public const string RiskFreeRate = "RiskFreeRate";
        /// <summary>Разрешение на работу блока для задания в качестве имени атрибута Input</summary>
        public const string Permission = "Permission";

        /// <summary>
        /// Статическое поле с пустым неизменяемым листом, чтобы избежать бессмысленных 'new' с последующей сборкой мусора.
        /// </summary>
        internal static readonly ReadOnlyCollection<double> EmptyListDouble = new ReadOnlyCollection<double>(new List<double>());

        /// <summary>
        /// Статическое поле с пустым неизменяемым листом, чтобы избежать бессмысленных 'new' с последующей сборкой мусора.
        /// </summary>
        internal static readonly ReadOnlyCollection<Double2> EmptyListDouble2 = new ReadOnlyCollection<Double2>(new List<Double2>());

        /// <summary>
        /// Статическое поле с пустой серией, чтобы избежать бессмысленных 'new' с последующей сборкой мусора.
        /// </summary>
        public static readonly ImmutableInteractiveSeries EmptySeries = new ImmutableInteractiveSeries();

        /// <summary>
        /// Статическое поле с пустым неизменяемым листом, чтобы избежать бессмысленных 'new' с последующей сборкой мусора.
        /// </summary>
        internal static readonly ReadOnlyCollection<IPosition> EmptyListPositions = new ReadOnlyCollection<IPosition>(new List<IPosition>());

        /// <summary>
        /// Неизменяемый класс-наследник для гарантирования того, что коллекция останется пустой
        /// </summary>
        public sealed class ImmutableInteractiveSeries : InteractiveSeries
        {
            #region Overrides of InteractiveSeries
            public override IReadOnlyList<InteractiveObject> ControlPoints
            {
                get
                {
                    var cp = base.ControlPoints;
                    if (cp.Count <= 0)
                        return cp;
                    else
                    {
                        cp = new ReadOnlyCollection<InteractiveObject>(new InteractiveObject[] { });
                        base.ControlPoints = cp;
                        return cp;
                    }
                }
                set
                {
                    /* игнорирую присвоение. по-хорошему, здесь надо кинуть исключение... */
                }
            }
            #endregion
        }
    }
}
