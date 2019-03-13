using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.DataSource;
using TSLab.Script.Optimization;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Base asset price. Different algorythms are available (fixed price, last trade, quote midpoint, etc). Stream handler.
    /// \~russian Цена базового актива. Заложены различные алгоритмы (фиксированная цена, последний трейд, между котировками, на основании опционов, и т.п.). Потоковый обработчик.
    /// </summary>
    [HandlerCategory(HandlerCategories.Options)]
    [HelperName("Base Price", Language = Constants.En)]
    [HelperName("Цена БА", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION | TemplateTypes.OPTION_SERIES)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Цена базового актива. В расчет заложены различные алгоритмы (фиксированная цена, последний трейд, между котировками, на основании опционов, и т.п.). Потоковый обработчик.")]
    [HelperDescription("A base asset price. Various algorithms are applied in calculation (fixed price, the last trade, quote midpoint, etc). It is a stream handler.", Constants.En)]
    public class BasePx : BaseContextWithBlock<double>
    {
        private const string DefaultPx = "0";

        private bool m_repeatLastPx = false;
        private BasePxMode m_pxMode = BasePxMode.LastTrade;
        private FixedValueMode m_valueMode = FixedValueMode.AsIs;
        private double m_fixedPx = Double.Parse(DefaultPx);
        private OptimProperty m_displayPrice = new OptimProperty(double.Parse(DefaultPx, CultureInfo.InvariantCulture), false, double.MinValue, double.MaxValue, 1.0, 0);

        #region Parameters
        /// <summary>
        /// \~english Algorythm to get base asset's price (FixedPx, LastTrade, etc)
        /// \~russian Алгоритм расчета цены БА (фиксированная, последний трейд и т.п.)
        /// </summary>
        [HelperName("Algorythm", Constants.En)]
        [HelperName("Алгоритм", Constants.Ru)]
        [Description("Алгоритм расчета цены БА (фиксированная, последний трейд и т.п.)")]
        [HelperDescription("Algorythm to get base asset's price (FixedPx, LastTrade, etc)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "LastTrade")]
        public BasePxMode PxMode
        {
            get { return m_pxMode; }
            set { m_pxMode = value; }
        }

        /// <summary>
        /// \~english Display units (hundreds, thousands, as is)
        /// \~russian Единицы отображения (сотни, тысячи, как есть)
        /// </summary>
        [HelperName("Display Units", Constants.En)]
        [HelperName("Единицы отображения", Constants.Ru)]
        [Description("Единицы отображения (сотни, тысячи, как есть)")]
        [HelperDescription("Display units (hundreds, thousands, as is)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "AsIs")]
        public FixedValueMode DisplayUnits
        {
            get { return m_valueMode; }
            set { m_valueMode = value; }
        }

        /// <summary>
        /// \~english Price for algorythm FixedPx
        /// \~russian Цена для алгоритма FixedPx (фиксированная)
        /// </summary>
        [HelperName("Price", Constants.En)]
        [HelperName("Цена", Constants.Ru)]
        [Description("Цена для алгоритма FixedPx (фиксированная)")]
        [HelperDescription("Price for algorythm FixedPx", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = DefaultPx)]
        public double FixedPx
        {
            get { return m_fixedPx; }
            set { m_fixedPx = value; }
        }

        /// <summary>
        /// \~english Base asset price (only to display at UI)
        /// \~russian Цена БА (только для отображения в интерфейсе)
        /// </summary>
        [ReadOnly(true)]
        [HelperName("Display Price", Constants.En)]
        [HelperName("Цена для интерфейса", Constants.Ru)]
        [Description("Цена БА (только для отображения в интерфейсе)")]
        [HelperDescription("Base asset price (only to display at UI)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultPx, IsCalculable = true)]
        public OptimProperty DisplayPrice
        {
            get { return m_displayPrice; }
            set { m_displayPrice = value; }
        }

        /// <summary>
        /// \~english Handler should repeat last known value to avoid further logic errors
        /// \~russian При true будет находить и использовать последнее известное значение
        /// </summary>
        [HelperName("Repeat Last Px", Constants.En)]
        [HelperName("Повтор значения", Constants.Ru)]
        [Description("При true будет находить и использовать последнее известное значение")]
        [HelperDescription("Handler should repeat last known value to avoid further logic errors", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false", Name = "Repeat Last Px")]
        public bool RepeatLastPx
        {
            get { return m_repeatLastPx; }
            set { m_repeatLastPx = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION, чтобы подключаться к источнику-опциону
        /// </summary>
        public IList<double> Execute(IOption opt)
        {
            if (opt == null)
                return Constants.EmptyListDouble;

            IList<double> res = ExecuteAll(opt.UnderlyingAsset, null);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public IList<double> Execute(IOptionSeries optSer)
        {
            if (optSer == null)
                return Constants.EmptyListDouble;

            IList<double> res = ExecuteAll(optSer.UnderlyingAsset, optSer);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.SECURITY, чтобы подключаться к источнику-БА
        /// </summary>
        public IList<double> Execute(ISecurity sec)
        {
            if (sec == null)
                return Constants.EmptyListDouble;

            IList<double> res = ExecuteAll(sec, null);
            return res;
        }

        private IList<double> ExecuteAll(ISecurity sec, IOptionSeries optSer)
        {
            if (sec == null) 
                return Constants.EmptyListDouble;

            IList<double> basePrices = CommonStreamExecute(m_variableId + "_basePrices", m_variableId + "_basePriceHistory",
                sec, m_repeatLastPx, true, false, new object[] { sec, optSer });

            if (basePrices.Count > 0)
            {
                double px = basePrices[basePrices.Count - 1];
                double displayValue = FixedValue.ConvertToDisplayUnits(m_valueMode, px);
                m_displayPrice.Value = displayValue;
            }

            return new ReadOnlyCollection<double>(basePrices);
        }

        protected override bool TryCalculate(Dictionary<DateTime, double> history, DateTime now, int barNum, object[] args, out double val)
        {
            ISecurity sec = (ISecurity)args[0];
            IOptionSeries optSer = (IOptionSeries)args[1];

            val = Double.NaN;

            double px;
            #region switch(m_pxMode)
            switch (m_pxMode)
            {
                case BasePxMode.FixedPx:
                    px = m_fixedPx;
                    break;

                case BasePxMode.LastTrade:
                    if (sec.FinInfo.LastPrice.HasValue)
                        px = sec.FinInfo.LastPrice.Value;
                    else
                        return false;
                    break;

                case BasePxMode.BidAskMidPoint:
                    {
                        FinInfo info = sec.FinInfo;
                        if (info.Ask.HasValue && info.Bid.HasValue && info.AskSize.HasValue
                            && info.BidSize.HasValue && (info.AskSize.Value > 0) && (info.BidSize.Value > 0))
                        {
                            px = (info.Ask.Value + info.Bid.Value) / 2;
                        }
                        else if (info.Ask.HasValue && info.AskSize.HasValue && (info.AskSize.Value > 0))
                        {
                            px = info.Ask.Value;
                        }
                        else if (info.Bid.HasValue && info.BidSize.HasValue && (info.BidSize.Value > 0))
                        {
                            px = info.Bid.Value;
                        }
                        else
                        {
                            // Приемлемо ли такое решение?
                            if (info.LastPrice.HasValue)
                                px = info.LastPrice.Value;
                            else
                                return false;
                        }
                    }
                    break;

                case BasePxMode.TheorPxBased:
                    {
                        if (optSer == null)
                            return false;

                        IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
                        IOptionStrikePair pair = pairs[pairs.Length / 2];

                        if ((pair.PutFinInfo.TheoreticalPrice == null) ||
                            (pair.CallFinInfo.TheoreticalPrice == null))
                            return false;

                        double putPx = pair.PutFinInfo.TheoreticalPrice.Value;
                        double callPx = pair.CallFinInfo.TheoreticalPrice.Value;
                        px = callPx - putPx + pair.Strike;
                    }
                    break;

                default:
                    throw new NotImplementedException("pxMode:" + m_pxMode);
            }
            #endregion switch(m_pxMode)

            val = px;

            return true;
        }
    }
}
