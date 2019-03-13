using System;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Profit calculation algorythm (position type)
    /// \~russian Алгоритм подсчета профита (тип позиций)
    /// </summary>
    public enum TotalProfitAlgo
    {
        /// <summary> \~english All positions (both real and virtual) \~russian Все позиции (вместе реальные и виртуальные)</summary>
        //[LocalizeDisplayName("TotalProfitAlgo.")]
        [LocalizeDescription("TotalProfitAlgo.AllPositions")]
        AllPositions = 0,
        /// <summary> \~english Only real positions \~russian Только реальные позиции</summary>
        //[LocalizeDisplayName("TotalProfitAlgo.")]
        [LocalizeDescription("TotalProfitAlgo.RealPositions")]
        RealPositions = 1,
        /// <summary> \~english Only virtual positions \~russian Только виртуальные позиции</summary>
        //[LocalizeDisplayName("TotalProfitAlgo.")]
        [LocalizeDescription("TotalProfitAlgo.VirtualPositions")]
        VirtualPositions = 2,
    }
}
