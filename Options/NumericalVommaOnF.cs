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
    /// \~english Numerical estimate of vega at-the-money (only one point is processed)
    /// \~russian Численный расчет веги позиции в текущей точке БА (вычисляется одна точка)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Numerical Vomma ATM", Language = Constants.En)]
    [HelperName("Численная вомма на деньгах", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Численный расчет воммы позиции в текущей точке БА (вычисляется одна точка)")]
    [HelperDescription("Numerical estimate of vomma at-the-money (only one point is processed)", Constants.En)]
    public class NumericalVommaOnF : BaseContextHandler, IValuesHandlerWithNumber
    {
        private const string MsgId = "VOMMA";
        /// <summary>
        /// Минимальный шаг сигмы для численного дифференцирования (1e-6)
        /// </summary>
        private const double MinSigmaStep = 0.000001;

        private double m_sigmaStep = 0.0001;
        private NumericalGreekAlgo m_greekAlgo = NumericalGreekAlgo.ShiftingSmile;
        private OptimProperty m_vomma = new OptimProperty(0, false, double.MinValue, double.MaxValue, 1.0, 3);

        #region Parameters
        /// <summary>
        /// \~english Sigma step for numerical derivative
        /// \~russian Шаг варьирования сигмы при дифференцировании
        /// </summary>
        [HelperName("Sigma step", Constants.En)]
        [HelperName("Шаг сигмы", Constants.Ru)]
        [Description("Шаг варьирования сигмы при дифференцировании")]
        [HelperDescription("Sigma step for numerical derivative", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0.0001", Min = "0.000001", Max = "1000000", Step = "0.0001")]
        public double SigmaStep
        {
            get
            {
                return m_sigmaStep;
            }
            set
            {
                if (DoubleUtil.IsPositive(value))
                {
                    m_sigmaStep = Math.Max(MinSigmaStep, value);
                }
                else
                {
                    m_sigmaStep = MinSigmaStep;
                }
            }
        }

        /// <summary>
        /// \~english FrozenSmile - smile is frozen; ShiftingSmile - smile shifts horizontally without modification
        /// \~russian FrozenSmile - улыбка заморожена; ShiftingSmile - улыбка без искажений сдвигается по горизонтали вслед за БА
        /// </summary>
        [HelperName("Greek algo", Constants.En)]
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
        /// \~english Current vomma (just to show it on ControlPane)
        /// \~russian Текущая вомма всей позиции (для отображения в интерфейсе агента)
        /// </summary>
        [HelperName("Vomma", Constants.En)]
        [HelperName("Вомма", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Текущая вомма всей позиции (для отображения в интерфейсе агента)")]
        [HelperDescription("Current vomma (just to show it on ControlPane)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0", IsCalculable = true)]
        public OptimProperty Vomma
        {
            get { return m_vomma; }
            set { m_vomma = value; }
        }
        #endregion Parameters

        public double Execute(double price, double time, InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return Constants.NaN;

            if ((smile == null) || (optSer == null))
                return Constants.NaN;

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
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.NaN;
            }

            if (!DoubleUtil.IsPositive(f))
            {
                // [{0}] Base asset price must be positive value. F:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.FutPxMustBePositive", GetType().Name, f);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.NaN;
            }

            double rawVega, res;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (SingleSeriesNumericalVega.TryEstimateVomma(posMan, optSer, pairs, smile, m_greekAlgo, f, m_sigmaStep, dT, out rawVega))
            {
                // Переводим вомму в дифференциал 'изменение цены за 1% волы'.
                // В знаменателе стоит dSigma^2, поэтому и делить нужно 2 раза на 100%.
                rawVega /= (Constants.PctMult * Constants.PctMult);

                res = rawVega;
            }
            else
            {
                res = Constants.NaN;
            }

            m_vomma.Value = res;
            //context.Log(String.Format("[{0}] Delta: {1}; rawDelta:{2}", MsgId, delta.Value, rawDelta), logColor, true);

            SetHandlerInitialized(now);

            return res;
        }
    }
}
