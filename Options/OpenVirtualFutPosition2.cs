using System;
using System.ComponentModel;
using System.Linq;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Open virtual position in base asset (bar handler)
    /// \~russian Создание виртуальной позиции по БА (побарный обработчик)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Open Virtual Fut Pos (bar)", Language = Constants.En)]
    [HelperName("Открыть вирт. позу в БА (бары)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION | TemplateTypes.OPTION_SERIES, Name = Constants.AnyOption)]
    //[OutputsCount(1)]
    [OutputType(TemplateTypes.POSITION)]
    [Description("Создание виртуальной позиции по БА (побарный обработчик)")]
    [HelperDescription("Open virtual position in base asset (bar handler)", Constants.En)]
    public class OpenVirtualFutPosition2 : IContextUses, IValuesHandlerWithNumber
    {
        private const string DefaultPx = "120000";
        private const string DefaultQty = "1";

        private IContext m_context;

        private double m_fixedPx = Double.Parse(DefaultPx);
        private int m_fixedQty = Int32.Parse(DefaultQty);

        public IContext Context
        {
            get { return m_context; }
            set { m_context = value; }
        }

        #region Parameters
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
        /// Метод под флаг TemplateTypes.OPTION, чтобы подключаться к источнику-опциону
        /// </summary>
        public IPosition Execute(IOption opt, int barNum)
        {
            return Execute(opt.UnderlyingAsset, barNum);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public IPosition Execute(IOptionSeries optSer, int barNum)
        {
            return Execute(optSer.UnderlyingAsset, barNum);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.SECURITY, чтобы подключаться к источнику-БА
        /// </summary>
        public IPosition Execute(ISecurity security, int barNum)
        {
            IPosition res = null;
            int len = security.Bars.Count;
            if (len <= 0)
                return res;

            if (barNum < m_context.BarsCount - 1)
                return res;
            
            DateTime openTime = security.Bars[len - 1].Date;
            int j = len - 1;
            // Итерируемся до предыдущего дня
            while ((j > 0) && (security.Bars[j].Date.Date == openTime.Date))
                j--;
            // Возвращаемся в сегодняшнее утро
            j++;

            ISecurity sec = (from s in m_context.Runtime.Securities
                                where (s.Symbol == security.Symbol)
                                select s).Single();

            string msg = String.Format("Creating virtual FUT position. j:{0}; Ticker:{1}; Qty:{2}; Px:{3}",
                j, sec.Symbol, m_fixedQty, m_fixedPx);
            m_context.Log(msg, MessageType.Info, true);
            res = sec.Positions.MakeVirtualPosition(j, m_fixedQty, m_fixedPx, "Open FUT");

            return res;
        }
    }
}
