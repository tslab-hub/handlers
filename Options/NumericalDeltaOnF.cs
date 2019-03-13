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
    /// \~english Numerical estimate of delta at-the-money (only one point is processed; bar handler)
    /// \~russian Численный расчет дельты позиции в текущей точке БА (вычисляется одна точка; побарный обработчик)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Numerical Delta ATM", Language = Constants.En)]
    [HelperName("Численная дельта на деньгах", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Численный расчет дельты позиции в текущей точке БА (вычисляется одна точка; побарный обработчик)")]
    [HelperDescription("Numerical estimate of delta at-the-money (only one point is processed; bar handler)", Constants.En)]
    public class NumericalDeltaOnF : BaseContextHandler, IValuesHandlerWithNumber
    {
        private const string MsgId = "DELTA";

        private bool m_hedgeDelta = false;
        private NumericalGreekAlgo m_greekAlgo = NumericalGreekAlgo.ShiftingSmile;
        private OptimProperty m_delta = new OptimProperty(0, false, double.MinValue, double.MaxValue, 1.0, 3);

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
        /// \~english Current delta (just to show it on ControlPane)
        /// \~russian Текущая дельта всей позиции (для отображения в интерфейсе агента)
        /// </summary>
        [HelperName("Delta", Constants.En)]
        [HelperName("Дельта", Constants.Ru)]
        [ReadOnly(true)]
        [Description("Текущая дельта всей позиции (для отображения в интерфейсе агента)")]
        [HelperDescription("Current delta (just to show it on ControlPane)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0", IsCalculable = true)]
        public OptimProperty Delta
        {
            get { return m_delta; }
            set { m_delta = value; }
        }

        /// <summary>
        /// \~english Align delta (just to make button on ControlPane)
        /// \~russian Выровнять дельту (даёт возможность сделать в интерфейсе кнопку)
        /// </summary>
        [HelperName("Align Delta", Constants.En)]
        [HelperName("Выровнять дельту", Constants.Ru)]
        [Description("Выровнять дельту (даёт возможность сделать в интерфейсе кнопку)")]
        [HelperDescription("Align delta (just to make button on ControlPane)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "False")]
        public bool HedgeDelta
        {
            get { return m_hedgeDelta; }
            set { m_hedgeDelta = value; }
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

            if (smile == null)
            {
                string msg = String.Format("[{0}] Argument 'smile' must be filled with InteractiveSeries.", GetType().Name);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.NaN;
            }

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if (oldInfo == null)
            {
                string msg = String.Format("[{0}] Property Tag of object smile must be filled with SmileInfo. Tag:{1}", GetType().Name, smile.Tag);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.NaN;
            }

            double rawDelta, res;
            double dF = optSer.UnderlyingAsset.Tick;
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            if (SingleSeriesNumericalDelta.TryEstimateDelta(posMan, optSer, pairs, smile, m_greekAlgo, f, dF, dT, out rawDelta))
            {
                res = rawDelta;
            }
            else
            {
                res = Constants.NaN;
            }

            SmileInfo sInfo = smile.GetTag<SmileInfo>();
            if ((sInfo != null) && (sInfo.ContinuousFunction != null))
            {
                double iv = sInfo.ContinuousFunction.Value(sInfo.F);
                string msg = String.Format("[{0}] F:{1}; dT:{2};   smile.F:{3}; smile.dT:{4}; smile.IV:{5}",
                    GetType().Name, f, dT, sInfo.F, sInfo.dT, iv);
                m_context.Log(msg, MessageType.Info, false);
            }

            m_delta.Value = res;
            //context.Log(String.Format("[{0}] Delta: {1}; rawDelta:{2}", MsgId, delta.Value, rawDelta), logColor, true);

            if (m_hedgeDelta)
            {
                #region Hedge logic
                try
                {
                    int rounded = Math.Sign(rawDelta) * ((int)Math.Floor(Math.Abs(rawDelta)));
                    if (rounded == 0)
                    {
                        string msg = String.Format("[{0}] Delta is too low to hedge. Delta: {1}", MsgId, rawDelta);
                        m_context.Log(msg, MessageType.Info, true);
                    }
                    else
                    {
                        int len = optSer.UnderlyingAsset.Bars.Count;
                        ISecurity sec = (from s in m_context.Runtime.Securities
                                         where (s.SecurityDescription.Equals(optSer.UnderlyingAsset.SecurityDescription))
                                         select s).SingleOrDefault();
                        if (sec == null)
                        {
                            string msg = String.Format("[{0}] There is no security. Symbol: {1}", MsgId, optSer.UnderlyingAsset.Symbol);
                            m_context.Log(msg, MessageType.Warning, true);
                        }
                        else
                        {
                            if (rounded < 0)
                            {
                                string signalName = String.Format("\r\nDelta BUY\r\nF:{0}; dT:{1}; Delta:{2}\r\n", f, dT, rawDelta);
                                m_context.Log(signalName, MessageType.Warning, true);
                                posMan.BuyAtPrice(m_context, sec, Math.Abs(rounded), f, signalName, null);
                            }
                            else if (rounded > 0)
                            {
                                string signalName = String.Format("\r\nDelta SELL\r\nF:{0}; dT:{1}; Delta:+{2}\r\n", f, dT, rawDelta);
                                m_context.Log(signalName, MessageType.Warning, true);
                                posMan.SellAtPrice(m_context, sec, Math.Abs(rounded), f, signalName, null);
                            }
                        }
                    }
                }
                finally
                {
                    m_hedgeDelta = false;
                }
                #endregion Hedge logic
            }

            SetHandlerInitialized(now);

            return res;
        }
    }
}
