using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TSLab.MessageType;
using TSLab.Script.CanvasPane;
using TSLab.Script.Optimization;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Numerical estimate value at-the-money (only one point is processed using provided profile)
    /// \~russian Вычитание двух волатильностей с проверкой корректности аргументов
    /// </summary>
    //[HandlerCategory(HandlerCategories.OptionsIndicators
    //[HandlerName("Get Value ATM (IntSer)", Language = Constants.En)]
    //[HandlerName("Значение на деньгах (IntSer)", Language = Constants.Ru)]
    //[HandlerAlwaysKeep]
    //[InputsCount(1)]
    //[Input(0, TemplateTypes.INTERACTIVESPLINE, Name = "Profile")]
    //[OutputType(TemplateTypes.DOUBLE)]
    //[Description("Численный расчет значения точки на-деньгах  (вычисляется одна точка из профиля)")]
    //[HelperDescription("Numerical estimate value at-the-money (only one point is processed using provided profile)", Constants.En)]
    public class SubtractVolatilities : BaseContextHandler, IValuesHandlerWithNumber, IDoubleReturns
    {
        private const string MsgId = "GETVAL";

        private double m_moneyness = 0;
        private OptimProperty m_result = new OptimProperty(0, Double.MinValue, Double.MaxValue, 1.0, 3);

        #region Parameters
        /// <summary>
        /// \~english Moneyness: LN(K/F)/SQRT(T)
        /// \~russian Денежность: LN(K/F)/SQRT(T)
        /// </summary>
        [ReadOnly(true)]
        [HelperParameterName("Moneyness", Constants.En)]
        [HelperParameterName("Денежность", Constants.Ru)]
        [Description("Денежность: LN(K/F)/SQRT(T)")]
        [HelperDescription("Moneyness: LN(K/F)/SQRT(T)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-10000000", Max = "10000000", Step = "1")]
        public double Moneyness
        {
            get { return m_moneyness; }
            set { m_moneyness = value; }
        }

        /// <summary>
        /// \~english Value ATM
        /// \~russian Значение на-деньгах
        /// </summary>
        [ReadOnly(true)]        
        [HelperParameterName("Result", Constants.En)]
        [HelperParameterName("Результат", Constants.Ru)]
        [Description("Значение на-деньгах")]
        [HelperDescription("Value ATM", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true)]
        public OptimProperty Result
        {
            get { return m_result; }
            set { m_result = value; }
        }

        /// <summary>
        /// \~english Print in main log
        /// \~russian Выводить в главный лог приложения
        /// </summary>
        [ReadOnly(true)]
        [HelperParameterName("Print in Log", Constants.En)]
        [HelperParameterName("Выводить в лог", Constants.Ru)]
        [Description("Выводить в главный лог приложения")]
        [HelperDescription("Print in main log", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "False")]
        public bool PrintInLog { get; set; }
        #endregion Parameters

        public double Execute(InteractiveSeries profile, int barNum)
        {
            List<double> results = m_context.LoadObject(VariableId + "_results") as List<double>;
            if (results == null)
            {
                results = new List<double>();
                m_context.StoreObject(VariableId + "_results", results);
            }

            int len = m_context.BarsCount;
            for (int j = results.Count; j < len; j++)
                results.Add(Double.NaN);

            {
                int barsCount = m_context.BarsCount;
                if (!m_context.IsLastBarUsed)
                    barsCount--;
                if (barNum < barsCount - 1)
                    return results[barNum];
            }

            if (profile == null)
                return Double.NaN;

            SmileInfo profInfo = profile.GetTag<SmileInfo>();
            if ((profInfo == null) || (profInfo.ContinuousFunction == null))
            {
                results[barNum] = Double.NaN; // заполняю индекс barNumber
                return Double.NaN;
            }

            double f = profInfo.F;
            double dT = profInfo.dT;
            if (Double.IsNaN(f) || (f < Double.Epsilon))
            {
                // [{0}] Base asset price must be positive value. F:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.FutPxMustBePositive", GetType().Name, futPx);
                m_context.Log(msg, MessageType.Error, true);
                return Double.NaN;
            }

            if (!DoubleUtil.IsZero(m_moneyness))
            {
                if (Double.IsNaN(dT) || (dT < Double.Epsilon))
                {
                    // [{0}] Time to expiry must be positive value. dT:{1}
                    string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                    m_context.Log(msg, MessageType.Error, true);
                    return Double.NaN;
                }
            }

            double rawRes;
            double effectiveF;
            if (DoubleUtil.IsZero(m_moneyness))
                effectiveF = f;
            else
                effectiveF = f * Math.Exp(m_moneyness * Math.Sqrt(profInfo.dT));
            if (!profInfo.ContinuousFunction.TryGetValue(effectiveF, out rawRes))
            {
                rawRes = Double.NaN;
            }

            results[barNum] = rawRes; // заполняю индекс barNumber

            m_result.Value = rawRes;
            m_context.Log(MsgId + ": " + m_result.Value, MessageType.Info, PrintInLog);

            return rawRes;
        }
    }
}
