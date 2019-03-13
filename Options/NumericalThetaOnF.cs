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
    /// \~english Numerical estimate of theta at-the-money (only one point is processed)
    /// \~russian Численный расчет теты позиции в текущей точке БА (вычисляется одна точка)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Numerical Theta ATM", Language = Constants.En)]
    [HelperName("Численная тета на деньгах", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Численный расчет теты позиции в текущей точке БА (вычисляется одна точка)")]
    [HelperDescription("Numerical estimate of theta at-the-money (only one point is processed)", Constants.En)]
    public class NumericalThetaOnF : BaseContextHandler, IValuesHandlerWithNumber
    {
        private const string MsgId = "THETA";

        private double m_tStep = 0.00001;
        private NumericalGreekAlgo m_greekAlgo = NumericalGreekAlgo.FrozenSmile;
        private TimeRemainMode m_tRemainMode = TimeRemainMode.PlainCalendar;
        private OptimProperty m_theta = new OptimProperty(0, false, double.MinValue, double.MaxValue, 1.0, 3);

        #region Parameters
        /// <summary>
        /// \~english Time step for numerical derivative
        /// \~russian Шаг варьирования времени при дифференцировании
        /// </summary>
        [HelperName("Time Step", Constants.En)]
        [HelperName("Шаг времени", Constants.Ru)]
        [Description("Шаг варьирования времени при дифференцировании")]
        [HelperDescription("Time step for numerical derivative", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0.00001", Min = "0", Max = "1000000", Step = "0.00001")]
        public double TStep
        {
            get { return m_tStep; }
            set { m_tStep = value; }
        }

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
        /// \~english Algorythm to estimate time-to-expiry
        /// \~russian Алгоритм расчета времени до экспирации
        /// </summary>
        [HelperName("Estimation algo", Constants.En)]
        [HelperName("Алгоритм расчета", Constants.Ru)]
        [Description("Алгоритм расчета времени до экспирации")]
        [HelperDescription("Algorythm to estimate time-to-expiry", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "PlainCalendar")]
        public TimeRemainMode DistanceMode
        {
            get { return m_tRemainMode; }
            set { m_tRemainMode = value; }
        }

        /// <summary>
        /// \~english Current theta (just to show it on ControlPane)
        /// \~russian Текущая тета всей позиции (для отображения в интерфейсе агента)
        /// </summary>
        [HelperName("Theta", Constants.En)]
        [HelperName("Тета", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Текущая тета всей позиции (для отображения в интерфейсе агента)")]
        [HelperDescription("Current theta (just to show it on ControlPane)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0", IsCalculable = true)]
        public OptimProperty Theta
        {
            get { return m_theta; }
            set { m_theta = value; }
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

            double futPx = price;
            double dT = time;
            if (!DoubleUtil.IsPositive(dT))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.NaN;
            }

            if (!DoubleUtil.IsPositive(futPx))
            {
                // [{0}] Base asset price must be positive value. F:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.FutPxMustBePositive", GetType().Name, futPx);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.NaN;
            }

            double rawTheta, res;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (SingleSeriesNumericalTheta.TryEstimateTheta(posMan, pairs, smile, m_greekAlgo, futPx, dT, m_tStep, out rawTheta))
            {
                // Переводим тету в дифференциал 'изменение цены за 1 сутки'.
                rawTheta = SingleSeriesNumericalTheta.RescaleThetaToDays(m_tRemainMode, rawTheta);

                res = rawTheta;
            }
            else
            {
                res = Constants.NaN;
            }

            m_theta.Value = res;
            //context.Log(String.Format("[{0}] Delta: {1}; rawDelta:{2}", MsgId, delta.Value, rawDelta), logColor, true);

            SetHandlerInitialized(now);

            return res;
        }
    }
}
