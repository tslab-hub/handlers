using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.DataSource;
using TSLab.Script.Handlers.Options;
using TSLab.Script.Optimization;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.OptionsPublic
{
    /// <summary>
    /// \~english Base asset price. Different algorythms are available (fixed price, last trade, quote midpoint, etc). Bar handler.
    /// \~russian Цена базового актива. Заложены различные алгоритмы (фиксированная цена, последний трейд, между котировками, на основании опционов, и т.п.). Тиковый обработчик.
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPublic)]
    [HelperName("Base Price", Language = Constants.En)]
    [HelperName("Цена БА", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION | TemplateTypes.OPTION_SERIES)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Цена базового актива. Заложены различные алгоритмы (фиксированная цена, последний трейд, между котировками, на основании опционов, и т.п.). Тиковый обработчик.")]
    [HelperDescription("Base asset price. Different algorythms are available (fixed price, last trade, quote midpoint, etc). Bar handler.", Constants.En)]
#if !DEBUG
    // Этот атрибут УБИРАЕТ блок из списка доступных в Редакторе Скриптов.
    // В своих блоках можете просто удалить его вместе с директивами условной компилляции.
    [HandlerInvisible]
#endif
    public class BasePxPublic : BaseContextHandler, IValuesHandlerWithNumber 
    {
        private const string DefaultPx = "120000";

        private bool m_repeatLastPx = false;
        private BasePxMode m_pxMode = BasePxMode.LastTrade;
        private FixedValueMode m_valueMode = FixedValueMode.AsIs;
        private double m_fixedPx = Double.Parse(DefaultPx, CultureInfo.InvariantCulture);
        private OptimProperty m_displayPrice = new OptimProperty(double.Parse(DefaultPx, CultureInfo.InvariantCulture), false, double.MinValue, double.MaxValue, 1.0, 0);

        /// <summary>
        /// Локальное кеширующее поле
        /// </summary>
        private double m_prevPx = Double.NaN;

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
        public double Execute(IOption opt, int barNum)
        {
            double failRes = Constants.NaN;
            if (m_repeatLastPx)
                failRes = Double.IsNaN(m_prevPx) ? Constants.NaN : m_prevPx;

            if (opt == null)
                return failRes;

            ISecurity sec = opt.UnderlyingAsset;
            int len = sec.Bars.Count;
            if (len <= 0)
                return failRes;

            DateTime lastBarDate = sec.Bars[barNum].Date;
            DateTime today = lastBarDate.Date;
            IOptionSeries optSer = (from ser in opt.GetSeries()
                                    where (today <= ser.ExpirationDate.Date)
                                    orderby ser.ExpirationDate descending
                                    select ser).FirstOrDefault(); // В данном случае падать необязательно

            if (optSer == null)
                return failRes;

            double res = Execute(optSer, barNum);
            return res;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public double Execute(IOptionSeries optSer, int barNum)
        {
            double failRes = Constants.NaN;
            if (m_repeatLastPx)
                failRes = Double.IsNaN(m_prevPx) ? Constants.NaN : m_prevPx;

            if (optSer == null)
                return failRes;

            switch (m_pxMode)
            {
                case BasePxMode.TheorPxBased:
                    {
                        Dictionary<DateTime, double> basePrices;

                        #region Get cache
                        {
                            string cashKey = VariableId + "_basePrices";
                            basePrices = Context.LoadObject(cashKey) as Dictionary<DateTime, double>;
                            if (basePrices == null)
                            {
                                basePrices = new Dictionary<DateTime, double>();
                                Context.StoreObject(cashKey, basePrices);
                            }
                        }
                        #endregion Get cache

                        ISecurity sec = optSer.UnderlyingAsset;
                        int len = sec.Bars.Count;
                        if (len <= 0)
                            return failRes;

                        double px;
                        DateTime lastBarDate = sec.Bars[barNum].Date;
                        if ((basePrices.TryGetValue(lastBarDate, out px)) && (!Double.IsNaN(px)) && (px > 0))
                        {
                            m_prevPx = px;
                            // Раз мы нашли валидную цену в архиве, то можно обновить failRes
                            failRes = px;
                        }

                        // Цену в архиве нашли, теперь надо проверить свежие сведения.
                        {
                            int barsCount = ContextBarsCount;
                            if (barNum < barsCount - 1)
                            {
                                // Если история содержит осмысленное значение, то оно уже содержится в failRes
                                return failRes;
                            }
                            else
                            {
                                #region Process last bar(s)
                                IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
                                IOptionStrikePair pair = pairs[pairs.Length / 2];

                                if ((pair.PutFinInfo.TheoreticalPrice == null) ||
                                    (pair.CallFinInfo.TheoreticalPrice == null))
                                    return failRes;

                                double putPx = pair.PutFinInfo.TheoreticalPrice.Value;
                                double callPx = pair.CallFinInfo.TheoreticalPrice.Value;
                                px = callPx - putPx + pair.Strike;
                                m_prevPx = px;

                                basePrices[lastBarDate] = px;

                                double displayValue = FixedValue.ConvertToDisplayUnits(m_valueMode, px);
                                m_displayPrice.Value = displayValue;

                                return px;
                                #endregion Process last bar(s)
                            }
                        }
                    }

                default:
                    return Execute(optSer.UnderlyingAsset, barNum);
            }
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.SECURITY, чтобы подключаться к источнику-БА
        /// </summary>
        public double Execute(ISecurity sec, int barNum)
        {
            double failRes = Constants.NaN;
            if (m_repeatLastPx)
                failRes = Double.IsNaN(m_prevPx) ? Constants.NaN : m_prevPx;

            Dictionary<DateTime, double> basePrices;

            #region Get cache
            {
                string cashKey = VariableId + "_basePrices";
                basePrices = Context.LoadObject(cashKey) as Dictionary<DateTime, double>;
                if (basePrices == null)
                {
                    basePrices = new Dictionary<DateTime, double>();
                    Context.StoreObject(cashKey, basePrices);
                }
            }
            #endregion Get cache

            if (sec == null)
                return failRes;

            int len = sec.Bars.Count;
            if (len <= 0)
                return failRes;

            double px;
            DateTime lastBarDate = sec.Bars[barNum].Date;
            if ((basePrices.TryGetValue(lastBarDate, out px)) && (!Double.IsNaN(px)) && (px > 0))
            {
                m_prevPx = px;
                // Раз мы нашли валидную цену в архиве, то можно обновить failRes
                failRes = px;
            }
            
            // Цену в архиве нашли, теперь надо проверить свежие сведения.
            {
                int barsCount = ContextBarsCount;
                if (barNum < barsCount - 1)
                {
                    // Если история содержит осмысленное значение, то оно уже содержится в failRes
                    return failRes;
                }
                else
                {
                    #region Process last bar(s)

                    #region switch(m_pxMode)
                    switch (m_pxMode)
                    {
                        case BasePxMode.FixedPx:
                            px = m_fixedPx;
                            m_prevPx = px;
                            break;

                        case BasePxMode.LastTrade:
                            if (sec.FinInfo.LastPrice.HasValue)
                            {
                                px = sec.FinInfo.LastPrice.Value;
                                m_prevPx = px;
                            }
                            else
                            {
                                px = failRes;
                            }
                            break;

                        case BasePxMode.BidAskMidPoint:
                            {
                                FinInfo info = sec.FinInfo;
                                if (info.Ask.HasValue && info.Bid.HasValue && info.AskSize.HasValue
                                    && info.BidSize.HasValue && (info.AskSize.Value > 0) && (info.BidSize.Value > 0))
                                {
                                    px = (info.Ask.Value + info.Bid.Value) / 2;
                                    m_prevPx = px;
                                }
                                else if (info.Ask.HasValue && info.AskSize.HasValue && (info.AskSize.Value > 0))
                                {
                                    px = info.Ask.Value;
                                    m_prevPx = px;
                                }
                                else if (info.Bid.HasValue && info.BidSize.HasValue && (info.BidSize.Value > 0))
                                {
                                    px = info.Bid.Value;
                                    m_prevPx = px;
                                }
                                else
                                {
                                    // Приемлемо ли такое решение?
                                    if (info.LastPrice.HasValue)
                                    {
                                        px = info.LastPrice.Value;
                                        m_prevPx = px;
                                    }
                                    else
                                    {
                                        px = failRes;
                                    }
                                }
                            }
                            break;

                        default:
                            throw new NotImplementedException("pxMode:" + m_pxMode);
                    }
                    #endregion switch(m_pxMode)

                    basePrices[lastBarDate] = px;

                    double displayValue = FixedValue.ConvertToDisplayUnits(m_valueMode, px);
                    m_displayPrice.Value = displayValue;

                    return px;
                    #endregion Process last bar(s)
                }
            }
        }
    }
}
