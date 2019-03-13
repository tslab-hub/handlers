using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TSLab.DataSource;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Base asset price calculated using theretical option prices
    /// \~russian Цена базового актива, вычисленная через теоретические (биржевые) цены опционов
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsIndicators)]
    [HelperName("Forward Price", Language = Constants.En)]
    [HelperName("Форвардная цена", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Цена базового актива, вычисленная через теоретические (биржевые) цены опционов")]
    [HelperDescription("Base asset price calculated using theretical option prices", Constants.En)]
    public class ForwardTheorPx : BaseContextHandler, IStreamHandler
    {
        protected double m_strike = 120000;

        #region Parameters
        /// <summary>
        /// \~english Strike to calculate forward price
        /// \~russian Страйк, который будет использован для расчета форвардной цены
        /// </summary>
        [HelperName("Strike", Constants.En)]
        [HelperName("Страйк", Constants.Ru)]
        [Description("Страйк, который будет использован для расчета форвардной цены")]
        [HelperDescription("Strike to calculate forward price", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "120000")]
        public double Strike
        {
            get { return m_strike; }
            set { m_strike = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public IList<double> Execute(IOptionSeries optSer)
        {
            List<double> res = m_context.LoadObject(VariableId + "Prices") as List<double>;
            if (res == null)
            {
                res = new List<double>();
                m_context.StoreObject(VariableId + "Prices", res);
            }

            int oldCount = res.Count;
            int len = m_context.BarsCount;
            for (int j = oldCount; j < len; j++)
                res.Add(Constants.NaN);

            IOptionStrikePair pair = (from p in optSer.GetStrikePairs()
                                      where DoubleUtil.AreClose(p.Strike, m_strike)
                                      select p).FirstOrDefault();
            if (pair == null)
                return res;
            var putBars = pair.Put.Security.Bars;
            var callBars = pair.Call.Security.Bars;

            //if ((putBars.Count <= 0) || (callBars.Count <= 0))
            //    return res;

            ISecurity sec = optSer.UnderlyingAsset;
            // кеширование
            for (int j = oldCount; j < len; j++)
            {
                int putIndex = Math.Min(putBars.Count - 1, j);
                int callIndex = Math.Min(callBars.Count - 1, j);
                if ((putIndex >= 0) && (callIndex >= 0) &&
                    (putBars[putIndex] is IBar) && (callBars[callIndex] is IBar))
                {
                    double putPx = ((IBar)putBars[j]).TheoreticalPrice;
                    double callPx = ((IBar)callBars[j]).TheoreticalPrice;
                    double px = callPx - putPx + pair.Strike;
                    res.Add(px);
                }
                else
                    res.Add(Constants.NaN);
            }

            // актуализирую текущее значение
            if ((len > 0) &&
                pair.PutFinInfo.TheoreticalPrice.HasValue && pair.CallFinInfo.TheoreticalPrice.HasValue)
            {
                double putPx = pair.PutFinInfo.TheoreticalPrice.Value;
                double callPx = pair.CallFinInfo.TheoreticalPrice.Value;
                double px = callPx - putPx + pair.Strike;

                res[res.Count - 1] = px;
            }

            return res;
        }
    }
}
