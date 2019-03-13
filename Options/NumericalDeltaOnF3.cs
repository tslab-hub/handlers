using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Optimization;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Numerical estimate of delta at-the-money (only one point is processed using delta profile)
    /// \~russian Численный расчет дельты позиции в текущей точке БА (вычисляется одна точка по профилю дельты)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Numerical Delta ATM (IntSer)", Language = Constants.En)]
    [HelperName("Численная дельта на деньгах (IntSer)", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = "DeltaProfile")]
    [Input(1, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Численный расчет дельты позиции в текущей точке БА (вычисляется одна точка по профилю дельты)")]
    [HelperDescription("Numerical estimate of delta at-the-money (only one point is processed using delta profile)", Constants.En)]
    public class NumericalDeltaOnF3 : BaseContextHandler, IValuesHandlerWithNumber
    {
        private const string MsgId = "DELTA3";

        private bool m_hedgeDelta = false;
        private OptimProperty m_delta = new OptimProperty(0, false, double.MinValue, double.MaxValue, 1.0, 3);

        #region Parameters
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

        /// <summary>
        /// \~english Print delta in main log
        /// \~russian Выводить дельту в главный лог приложения
        /// </summary>
        [ReadOnly(true)]
        [HelperName("Print in Log", Constants.En)]
        [HelperName("Выводить в лог", Constants.Ru)]
        [Description("Выводить дельту в главный лог приложения")]
        [HelperDescription("Print delta in main log", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "False")]
        public bool PrintDeltaInLog { get; set; }
        #endregion Parameters

        public double Execute(InteractiveSeries deltaProfile, IOptionSeries optSer, int barNum)
        {
            List<double> positionDeltas = m_context.LoadObject(VariableId + "positionDeltas") as List<double>;
            if (positionDeltas == null)
            {
                positionDeltas = new List<double>();
                m_context.StoreObject(VariableId + "positionDeltas", positionDeltas);
            }

            int len = m_context.BarsCount;
            if (len <= barNum)
            {
                string msg = String.Format("[{0}] (BarsCount <= barNum)! BarsCount:{1}; barNum:{2}",
                    GetType().Name, m_context.BarsCount, barNum);
                m_context.Log(msg, MessageType.Info, true);
            }
            for (int j = positionDeltas.Count; j < Math.Max(len, barNum + 1); j++)
                positionDeltas.Add(Constants.NaN);

            {
                int barsCount = m_context.BarsCount;
                if (!m_context.IsLastBarUsed)
                    barsCount--;
                if ((barNum < barsCount - 1) || (optSer == null))
                    return positionDeltas[barNum];
            }

            if (deltaProfile == null)
                return Constants.NaN;

            SmileInfo deltaInfo = deltaProfile.GetTag<SmileInfo>();
            if ((deltaInfo == null) || (deltaInfo.ContinuousFunction == null))
            {
                positionDeltas[barNum] = Double.NaN; // заполняю индекс barNumber
                return Constants.NaN;
            }

            int lastBarIndex = optSer.UnderlyingAsset.Bars.Count - 1;
            DateTime now = optSer.UnderlyingAsset.Bars[Math.Min(barNum, lastBarIndex)].Date;
            bool wasInitialized = HandlerInitializedToday(now);

            double f = deltaInfo.F;
            double dT = deltaInfo.dT;
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

            double rawDelta;
            if (!deltaInfo.ContinuousFunction.TryGetValue(f, out rawDelta))
            {
                rawDelta = Constants.NaN;
            }

            positionDeltas[barNum] = rawDelta; // заполняю индекс barNumber

            m_delta.Value = rawDelta;
            if (PrintDeltaInLog)
                m_context.Log(MsgId + ": " + m_delta.Value, MessageType.Info, PrintDeltaInLog);

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
                        len = optSer.UnderlyingAsset.Bars.Count;
                        ISecurity sec = (from s in m_context.Runtime.Securities
                                         where (s.Symbol == optSer.UnderlyingAsset.Symbol)
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
                                PositionsManager posMan = PositionsManager.GetManager(m_context);
                                string signalName = String.Format("\r\nDelta BUY\r\nF:{0}; dT:{1}; Delta:{2}\r\n", f, dT, rawDelta);
                                m_context.Log(signalName, MessageType.Warning, true);
                                posMan.BuyAtPrice(m_context, sec, Math.Abs(rounded), f, signalName, null);
                            }
                            else if (rounded > 0)
                            {
                                PositionsManager posMan = PositionsManager.GetManager(m_context);
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

            return rawDelta;
        }
    }
}
