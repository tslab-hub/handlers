using System;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Numerical algorythms to estimate greeks
    /// \~russian Алгоритмы численного расчета греков
    /// </summary>
    public enum NumericalGreekAlgo
    {
        /// <summary>
        /// \~english Smile is frozen and does not change with market
        /// \~russian Улыбка заморожена и никак не меняется при изменении рыночных параметров
        /// </summary>
        //[LocalizeDisplayName("NumericalGreekAlgo.FrozenSmile")]
        [LocalizeDescription("NumericalGreekAlgo.FrozenSmile")]
        FrozenSmile,

        /// <summary>
        /// \~english Smile follows base asset as solid body; no vertical shift
        /// \~russian Улыбка без искажений сдвигается по горизонтали вслед за БА
        /// </summary>
        //[LocalizeDisplayName("NumericalGreekAlgo.ShiftingSmile")]
        [LocalizeDescription("NumericalGreekAlgo.ShiftingSmile")]
        ShiftingSmile,

        ///// <summary>
        ///// \~english Smile follows base asset and slides on itself as solid body
        ///// \~russian Улыбка без искажений скользит сама по себе вслед за БА
        ///// </summary>
        //SlidingSmile,

        ///// <summary>
        ///// \~english Smile follows base asset and changes its shape when market moves
        ///// \~russian При движении БА улыбка скользит и деформируется
        ///// </summary>
        //AdaptingSmile,
    }
}
