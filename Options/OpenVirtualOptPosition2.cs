using System;
using System.ComponentModel;
using System.Linq;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Open virtual position in options (bar handler)
    /// \~russian Создание виртуальной позиции в опционах (побарный обработчик)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Open Virtual Opt Pos (bar)", Language = Constants.En)]
    [HelperName("Открыть вирт. позицию в опционах (бары)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.POSITION)]
    [Description("Создание виртуальной позиции в опционах (побарный обработчик)")]
    [HelperDescription("Open virtual position in options (bar handler)", Constants.En)]
    public class OpenVirtualOptPosition2 : IContextUses, IValuesHandlerWithNumber
    {
        private const string DefaultPx = "10000";
        private const string DefaultQty = "-1";
        private const string DefaultStrike = "120000";

        private IContext m_context;

        private StrikeType m_optionType = StrikeType.Call;
        private int m_fixedQty = Int32.Parse(DefaultQty);
        private double m_fixedPx = Double.Parse(DefaultPx);
        private double m_fixedStrike = Double.Parse(DefaultStrike);

        public IContext Context
        {
            get { return m_context; }
            set { m_context = value; }
        }

        #region Parameters
        /// <summary>
        /// \~english Strike of this virtual position
        /// \~russian Страйк виртуальной позиции
        /// </summary>
        [HelperName("Strike", Constants.En)]
        [HelperName("Страйк", Constants.Ru)]
        [Description("Страйк виртуальной позиции")]
        [HelperDescription("Strike of this virtual position", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultStrike)]
        public double FixedStrike
        {
            get { return m_fixedStrike; }
            set { m_fixedStrike = value; }
        }

        /// <summary>
        /// \~english Option type (parameter Any is not recommended)
        /// \~russian Тип опционов (использование типа Any может привести к неожиданному поведению)
        /// </summary>
        [HelperName("Option Type", Constants.En)]
        [HelperName("Вид опционов", Constants.Ru)]
        [Description("Тип опционов (использование типа Any может привести к неожиданному поведению)")]
        [HelperDescription("Option type (parameter Any is not recommended)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Call")]
        public StrikeType OptionType
        {
            get { return m_optionType; }
            set { m_optionType = value; }
        }

        /// <summary>
        /// \~english Entry price of this virtual position
        /// \~russian Цена открытия этой виртуальной позиции
        /// </summary>
        [HelperName("Price", Constants.En)]
        [HelperName("Цена", Constants.Ru)]
        [Description("Цена открытия этой виртуальной позиции")]
        [HelperDescription("Entry price of this virtual position", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultPx)]
        public double FixedPx
        {
            get { return m_fixedPx; }
            set { m_fixedPx = value; }
        }

        /// <summary>
        /// \~english Entry size of this virtual position
        /// \~russian Объём открытия этой виртуальной позиции
        /// </summary>
        [HelperName("Size", Constants.En)]
        [HelperName("Кол-во", Constants.Ru)]
        [Description("Объём открытия этой виртуальной позиции")]
        [HelperDescription("Entry size of this virtual position", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultQty)]
        public int FixedQty
        {
            get { return m_fixedQty; }
            set { m_fixedQty = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public IPosition Execute(IOptionSeries optSer, int barNum)
        {
            IPosition res = null;
            int len = optSer.UnderlyingAsset.Bars.Count;
            if (len <= 0)
                return res;

            if (barNum < m_context.BarsCount - 1)
                return res;

            IOptionStrikePair pair;
            if (!optSer.TryGetStrikePair(m_fixedStrike, out pair))
                return res;

            if (m_optionType == StrikeType.Put)
            {
                ISecurity sec = (from s in m_context.Runtime.Securities
                                 where (s.SecurityDescription.Equals(pair.Put.Security.SecurityDescription))
                                 select s).Single();
                int j = GetTodayOpeningBar(sec);
                string msg = String.Format("Creating virtual PUT position. j:{0}; Ticker:{1}; Qty:{2}; Px:{3}",
                    j, sec.Symbol, m_fixedQty, m_fixedPx);
                m_context.Log(msg, MessageType.Info, true);
                res = sec.Positions.MakeVirtualPosition(j, m_fixedQty, m_fixedPx, "Open PUT");
            }
            else if (m_optionType == StrikeType.Call)
            {
                ISecurity sec = (from s in m_context.Runtime.Securities
                                 where (s.SecurityDescription.Equals(pair.Call.Security.SecurityDescription))
                                 select s).Single();
                int j = GetTodayOpeningBar(sec);
                string msg = String.Format("Creating virtual CALL position. j:{0}; Ticker:{1}; Qty:{2}; Px:{3}",
                    j, sec.Symbol, m_fixedQty, m_fixedPx);
                m_context.Log(msg, MessageType.Info, true);
                res = sec.Positions.MakeVirtualPosition(j, m_fixedQty, m_fixedPx, "Open CALL");
            }
            else
            {
                string msg = String.Format("optionType:{0} is not yet supported.", m_optionType);
                m_context.Log(msg, MessageType.Warning, true);
            }

            return res;
        }

        private int GetTodayOpeningBar(ISecurity sec)
        {
            int len = sec.Bars.Count;
            DateTime now = sec.Bars[len - 1].Date;
            int j = len - 1;
            // Итерируемся до предыдущего дня
            while ((j > 0) && (sec.Bars[j].Date.Date == now.Date))
                j--;
            // Возвращаемся в сегодняшнее утро
            j++;

            return Math.Min(j, len - 1);
        }
    }
}
