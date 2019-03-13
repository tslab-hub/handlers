using System;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Optimization;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Numerical estimate of gamma at-the-money (only one point is processed; bar handler)
    /// \~russian Численный расчет гаммы позиции в текущей точке БА (вычисляется одна точка; побарный обработчик)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Numerical Gamma ATM", Language = Constants.En)]
    [HelperName("Численная гамма на деньгах", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Численный расчет гаммы позиции в текущей точке БА (вычисляется одна точка; побарный обработчик)")]
    [HelperDescription("Numerical estimate of gamma at-the-money (only one point is processed; bar handler)", Constants.En)]
    public class NumericalGammaOnF : BaseContextHandler, IValuesHandlerWithNumber
    {
        private const string MsgId = "GAMMA";

        private NumericalGreekAlgo m_greekAlgo = NumericalGreekAlgo.ShiftingSmile;
        private OptimProperty m_gamma = new OptimProperty(0, false, double.MinValue, double.MaxValue, 1.0, 3);

        #region Parameters
        /// <summary>
        /// \~english FrozenSmile - smile is frozen; ShiftingSmile - smile shifts horizontally without modification
        /// \~russian FrozenSmile - улыбка заморожена; ShiftingSmile - улыбка без искажений сдвигается по горизонтали вслед за БА
        /// </summary>
        [HelperName("Greek Algo", Constants.En)]
        [HelperName("Алгоритм улыбки", Constants.Ru)]
        [Description("FrozenSmile -- улыбка заморожена; ShiftingSmile -- улыбка без искажений сдвигается по горизонтали вслед за БА")]
        [HelperDescription("FrozenSmile - smile is frozen; ShiftingSmile - smile shifts horizontally without modification", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "ShiftingSmile")]
        public NumericalGreekAlgo GreekAlgo
        {
            get { return m_greekAlgo; }
            set { m_greekAlgo = value; }
        }

        /// <summary>
        /// \~english Current gamma (just to show it on ControlPane)
        /// \~russian Текущая гамма всей позиции (для отображения в интерфейсе агента)
        /// </summary>
        [HelperName("Gamma", Constants.En)]
        [HelperName("Гамма", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Текущая гамма всей позиции (для отображения в интерфейсе агента)")]
        [HelperDescription("Current gamma (just to show it on ControlPane)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0", IsCalculable = true)]
        public OptimProperty Gamma
        {
            get { return m_gamma; }
            set { m_gamma = value; }
        }
        #endregion Parameters

        public double Execute(double price, double time, InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if ((barNum < barsCount - 1) || (optSer == null))
            {
                return Constants.NaN;
            }

            int lastBarIndex = optSer.UnderlyingAsset.Bars.Count - 1;
            DateTime now = optSer.UnderlyingAsset.Bars[Math.Min(barNum, lastBarIndex)].Date;
            bool wasInitialized = HandlerInitializedToday(now);

            double f = price;
            double dT = time;
            if (!DoubleUtil.IsPositive(dT))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.NaN;
            }

            if (!DoubleUtil.IsPositive(f))
            {
                // [{0}] Base asset price must be positive value. F:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.FutPxMustBePositive", GetType().Name, f);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.NaN;
            }

            double rawGamma, res;
            double dF = optSer.UnderlyingAsset.Tick;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (SingleSeriesNumericalGamma.TryEstimateGamma(posMan, optSer, pairs, smile, m_greekAlgo, f, dF, dT, out rawGamma))
                res = rawGamma;
            else
                res = Constants.NaN;

            //SmileInfo sInfo = smile.GetTag<SmileInfo>();
            //if ((sInfo != null) && (sInfo.ContinuousFunction != null))
            //{
            //    double iv = sInfo.ContinuousFunction.Value(sInfo.F);
            //    string msg = String.Format("[DEBUG:{0}] F:{1}; dT:{2};   smile.F:{3}; smile.dT:{4}; smile.IV:{5}",
            //        GetType().Name, F, dT, sInfo.F, sInfo.dT, iv);
            //    context.Log(msg, MessageType.Info, true);
            //}

            m_gamma.Value = res;
            //context.Log(String.Format("[{0}] Delta: {1}; rawDelta:{2}", MsgId, delta.Value, rawDelta), logColor, true);

            SetHandlerInitialized(now);

            return res;
        }
    }
}
