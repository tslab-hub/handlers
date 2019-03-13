using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Optimization;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Numerical estimate of delta at-the-money (only one point is processed; stream handler)
    /// \~russian Численный расчет дельты позиции в текущей точке БА (вычисляется одна точка; потоковый обработчик)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Numerical Delta ATM v2", Language = Constants.En)]
    [HelperName("Численная дельта на деньгах в2", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Численный расчет дельты позиции в текущей точке БА (вычисляется одна точка; потоковый обработчик)")]
    [HelperDescription("Numerical estimate of delta at-the-money (only one point is processed; stream handler)", Constants.En)]
    public class NumericalDeltaOnF2 : BaseContextHandler, IStreamHandler
    {
        private const string MsgId = "DELTA2";

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

        public IList<double> Execute(IList<double> prices, IList<double> times, InteractiveSeries smile, IOptionSeries optSer)
        {
            List<double> positionDeltas = m_context.LoadObject(VariableId + "positionDeltas") as List<double>;
            if (positionDeltas == null)
            {
                positionDeltas = new List<double>();
                m_context.StoreObject(VariableId + "positionDeltas", positionDeltas);
            }

            int len = optSer.UnderlyingAsset.Bars.Count;
            for (int j = positionDeltas.Count; j < len; j++)
                positionDeltas.Add(Constants.NaN);

            double f = prices[prices.Count - 1];
            double dT = times[times.Count - 1];
            if ((dT < Double.Epsilon) || Double.IsNaN(dT) || Double.IsNaN(f))
            {
                return positionDeltas;
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

            positionDeltas[positionDeltas.Count - 1] = res;

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

            return positionDeltas;
        }
    }
}
