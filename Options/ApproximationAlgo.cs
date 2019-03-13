using System;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Points interpolation algorythm
    /// \~russian Алгоритм интерполяции точек
    /// </summary>
    public enum ApproximationAlgo
    {
        /// <summary> \~english No interpolation (plain table) \~russian Без интерполирования (плоская таблица)</summary>
        None,
        /// <summary> \~english Linear interpolation \~russian Линейная интерполяция</summary>
        Linear,
        /// <summary> \~english Natural Cubic Spline \~russian Натуральный кубический сплайн</summary>
        NaturalCubicSpline,
        /// <summary> \~english Not-a-knot Cubic Spline \~russian Кубический сплайн 'без двух узлов'</summary>
        NotAKnotCubicSpline,
    }
}
