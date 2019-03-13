using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.DataSource;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Handler to get option series by its index. Series are sorted ascending from near (index 1) to further. It's bar handler.
    /// \~russian Получение опционной серии по её индексу. Серии отсортированы по порядку от ближней (индекс 1) к дальним. Побарный обработчик.
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Option Series by Number", Language = Constants.En)]
    [HelperName("Серия по номеру", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION)]
    [OutputType(TemplateTypes.OPTION_SERIES)]
    [Description("Получение опционной серии по её индексу. Серии отсортированы по порядку от ближней (индекс 1) к дальним. Побарный обработчик.")]
    [HelperDescription("Handler to get option series by its index. Series are sorted ascending from near (index 1) to further. It's bar handler.", Constants.En)]
    public class OptionSeriesByNumber2 : IContextUses, IValuesHandlerWithNumber
    {
        private const string DefaultExpiration = "17-11-2014 18:45";

        private ExpiryMode m_expiryMode = ExpiryMode.FixedExpiry;

        private string m_expDateStr = DefaultExpiration;
        private DateTime m_expirationDate = DateTime.ParseExact(DefaultExpiration, TimeToExpiry.DateTimeFormat, CultureInfo.InvariantCulture);

        private IContext m_context;

        public IContext Context
        {
            get { return m_context; }
            set { m_context = value; }
        }

        #region Parameters
        /// <summary>
        /// \~english Algorythm to determine expiration date
        /// \~russian Алгоритм определения даты экспирации
        /// </summary>
        [HelperName("Expiration Algo", Constants.En)]
        [HelperName("Алгоритм экспирации", Constants.Ru)]
        [Description("Алгоритм определения даты экспирации")]
        [HelperDescription("Algorythm to determine expiration date", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "FixedExpiry")]
        public ExpiryMode ExpirationMode
        {
            get { return m_expiryMode; }
            set { m_expiryMode = value; }
        }

        /// <summary>
        /// \~english Expiration datetime (including time of a day) for algorythm FixedExpiry
        /// \~russian Дата экспирации (включая время суток) для режима FixedExpiry
        /// </summary>
        [HelperName("Expiry", Constants.En)]
        [HelperName("Экспирация", Constants.Ru)]
        [Description("Дата экспирации (включая время суток) для режима FixedExpiry")]
        [HelperDescription("Expiration datetime (including time of a day) for algorythm FixedExpiry", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultExpiration)]
        public string Expiry
        {
            get { return m_expDateStr; }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                    return;

                if (m_expDateStr.Equals(value, StringComparison.InvariantCultureIgnoreCase))
                    return;

                DateTime t;
                if (DateTime.TryParseExact(value, TimeToExpiry.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out t))
                {
                    m_expDateStr = value;
                    m_expirationDate = t;
                }
            }
        }

        /// <summary>
        /// \~english Series index (only alive series) for algorythm ExpiryByNumber
        /// \~russian Индекс серии (учитываются только живые). Используется в режиме ExpiryByNumber.
        /// </summary>
        [HelperName("Series index", Constants.En)]
        [HelperName("Номер серии", Constants.Ru)]
        [Description("Индекс серии (учитываются только живые). Используется в режиме ExpiryByNumber.")]
        [HelperDescription("Series index (only alive series) for algorythm ExpiryByNumber", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "1", Min = "1")]
        public int Number { get; set; }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION, чтобы подключаться к источнику-опциону
        /// </summary>
        public IOptionSeries Execute(IOption opt, int barNum)
        {
            ISecurity sec = opt.UnderlyingAsset;
            IDataBar bar = sec.Bars[barNum];
            DateTime now = bar.Date;
            IOptionSeries optSer = null;
            switch (m_expiryMode)
            {
                case ExpiryMode.FixedExpiry:
                    {
                        optSer = (from ser in opt.GetSeries()
                                      let serExpDate = ser.ExpirationDate.Date
                                  where (now.Date <= serExpDate) &&
                                        (m_expirationDate.Date == serExpDate)
                                  select ser).FirstOrDefault();
                        break;
                    }

                case ExpiryMode.FirstExpiry:
                    {
                        optSer = (from ser in opt.GetSeries()
                                  where (now.Date <= ser.ExpirationDate.Date)
                                  orderby ser.ExpirationDate ascending
                                  select ser).FirstOrDefault();
                        break;
                    }

                case ExpiryMode.LastExpiry:
                    {
                        optSer = (from ser in opt.GetSeries()
                                  where (now.Date <= ser.ExpirationDate.Date)
                                  orderby ser.ExpirationDate descending
                                  select ser).FirstOrDefault();
                        break;
                    }

                case ExpiryMode.ExpiryByNumber:
                    {
                        IOptionSeries[] optSers = (from ser in opt.GetSeries()
                                                   where (now.Date <= ser.ExpirationDate.Date)
                                                   orderby ser.ExpirationDate ascending
                                                   select ser).ToArray();
                        int ind = Math.Min(Number - 1, optSers.Length - 1);
                        ind = Math.Max(ind, 0);
                        // Если все серии уже умерли, вернуть null, чтобы потом не было непоняток
                        // и чтобы поведение было согласовано с другими ветками
                        if (optSers.Length == 0)
                            optSer = null;
                        else
                            optSer = optSers[ind];
                        break;
                    }

                default:
                    throw new NotImplementedException("ExpirationMode:" + m_expiryMode);
            } // End of switch(m_expiryMode)

            // На последней свече логгирую
            if (barNum >= sec.Bars.Count - 1)
            {
                if (optSer == null)
                {
                    string msg = String.Format("Unable to get option series from source '{0}'. ExpirationMode:{1}; Number:{2}; Expiry:{3}",
                        sec.Symbol, ExpirationMode, Number, Expiry);
                    // Пишу только в лог агента. Ситуация достаточно стандартная.
                    // Например, когда фьючерс уже умер.
                    m_context.Log(msg, MessageType.Warning, false);
                }
                //else
                //{
                //    string msg = String.Format("Option series '{0}' from source '{1}'. ExpirationMode:{2}; Number:{3}; Expiry:{4}",
                //        optSer.ExpirationDate.ToString(IvOnF.DateFormat, CultureInfo.InvariantCulture),
                //        sec.Symbol, ExpirationMode, Number, Expiry);
                //    // Пишу только в лог агента. Ситуация стандартная.
                //    m_context.Log(msg, MessageType.Info, false);
                //}
            }

            return optSer;
        }
    }
}
