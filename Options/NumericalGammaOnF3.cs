using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.CanvasPane;
using TSLab.Script.Optimization;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Numerical estimate of gamma at-the-money (only one point is processed using gamma profile)
    /// \~russian Численный расчет гаммы позиции в текущей точке БА (вычисляется одна точка по профилю гаммы)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Numerical Gamma ATM (IntSer)", Language = Constants.En)]
    [HelperName("Численная гамма на деньгах (IntSer)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = "GammaProfile")]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Численный расчет гаммы позиции в текущей точке БА (вычисляется одна точка по профилю гаммы)")]
    [HelperDescription("Numerical estimate of gamma at-the-money (only one point is processed using gamma profile)", Constants.En)]
    public class NumericalGammaOnF3 : BaseContextHandler, IValuesHandlerWithNumber
    {
        private OptimProperty m_gamma = new OptimProperty(0, false, Double.MinValue, Double.MaxValue, 1, 6);

        #region Parameters
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

        public double Execute(InteractiveSeries gammaProfile, int barNum)
        {
            List<double> positionGammas = m_context.LoadObject(VariableId + "positionGammas") as List<double>;
            if (positionGammas == null)
            {
                positionGammas = new List<double>();
                m_context.StoreObject(VariableId + "positionGammas", positionGammas);
            }

            int len = m_context.BarsCount;
            if (len <= barNum)
            {
                string msg = String.Format("[{0}] (BarsCount <= barNum)! BarsCount:{1}; barNum:{2}",
                    GetType().Name, m_context.BarsCount, barNum);
                m_context.Log(msg, MessageType.Info, true);
            }
            for (int j = positionGammas.Count; j < Math.Max(len, barNum + 1); j++)
                positionGammas.Add(Constants.NaN);

            {
                int barsCount = m_context.BarsCount;
                if (!m_context.IsLastBarUsed)
                    barsCount--;
                if (barNum < barsCount - 1)
                    return positionGammas[barNum];
            }

            if (gammaProfile == null)
                return Constants.NaN;

            SmileInfo gammaInfo = gammaProfile.GetTag<SmileInfo>();
            if ((gammaInfo == null) || (gammaInfo.ContinuousFunction == null))
            {
                positionGammas[barNum] = Constants.NaN; // заполняю индекс barNumber
                return Constants.NaN;
            }

            double f = gammaInfo.F;
            double dT = gammaInfo.dT;

            if ((dT < Double.Epsilon) || Double.IsNaN(dT) || Double.IsNaN(f))
            {
                positionGammas[barNum] = Constants.NaN; // заполняю индекс barNumber
                return Constants.NaN;
            }

            double rawGamma;
            if (!gammaInfo.ContinuousFunction.TryGetValue(f, out rawGamma))
            {
                rawGamma = Constants.NaN;
            }

            positionGammas[barNum] = rawGamma; // заполняю индекс barNumber

            m_gamma.Value = rawGamma;
            //context.Log("Gamma[3]: " + gamma.Value, logColor, true);

            return rawGamma;
        }
    }
}
