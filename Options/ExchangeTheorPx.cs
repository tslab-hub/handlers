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
    /// \~english Theoretical option price provided by exchange. Additional linear transformation is allowed.
    /// \~russian Теоретические биржевые цены опционов. Возможно, с каким-то линейным преобразованием.
    /// </summary>
    [HandlerCategory(HandlerCategories.Options)]
    [HelperName("Exchange Theor Px", Language = Constants.En)]
    [HelperName("Биржевая теор. цена", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION_SERIES)]
    [OutputType(TemplateTypes.DOUBLE2)]
    [Description("Теоретические биржевые цены опционов. Возможно, с каким-то линейным преобразованием.")]
    [HelperDescription("Theoretical option price provided by exchange. Additional linear transformation is allowed.", Constants.En)]
    public class ExchangeTheorPx : IContextUses, IStreamHandler
    {
        private IContext m_context;
        private double m_multPx = 1, m_addPx = 0;

        public IContext Context
        {
            get { return m_context; }
            set { m_context = value; }
        }

        #region Parameters
        /// <summary>
        /// \~english Price multiplier
        /// \~russian Мультипликатор цен
        /// </summary>
        [HelperName("Multiplier", Constants.En)]
        [HelperName("Мультипликатор", Constants.Ru)]
        [Description("Мультипликатор цен")]
        [HelperDescription("Price multiplier", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "1", Min = "-1000000", Max = "1000000", Step = "1")]
        public double Multiplier
        {
            get { return m_multPx; }
            set { m_multPx = value; }
        }

        /// <summary>
        /// \~english Price shift (price steps)
        /// \~russian Сдвиг цен (в шагах цены)
        /// </summary>
        [HelperName("Shift", Constants.En)]
        [HelperName("Сдвиг цен", Constants.Ru)]
        [Description("Сдвиг цен (в шагах цены)")]
        [HelperDescription("Price shift (price steps)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "0", Min = "-1000000", Max = "1000000", Step = "1")]
        public double ShiftPx
        {
            get { return m_addPx; }
            set { m_addPx = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES
        /// </summary>
        public IList<Double2> Execute(IOptionSeries optSer)
        {
            List<Double2> res = new List<Double2>();

            IOptionStrike[] strikes = (from strike in optSer.GetStrikes()
                                       orderby strike.Strike ascending
                                       select strike).ToArray();
            for (int j = 0; j < strikes.Length; j++)
            {
                IOptionStrike sInfo = strikes[j];
                if ((sInfo.FinInfo == null) || (!sInfo.FinInfo.TheoreticalPrice.HasValue))
                    continue;

                double optPx = sInfo.FinInfo.TheoreticalPrice.Value;
                optPx *= m_multPx;
                optPx += m_addPx * sInfo.Security.SecurityDescription.GetTick(sInfo.FinInfo.TheoreticalPrice.Value);

                res.Add(new Double2(sInfo.Strike, optPx));
            }

            return res;
        }
    }
}
