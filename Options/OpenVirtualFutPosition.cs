using System;
using System.ComponentModel;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Open virtual position in base asset (stream handler)
    /// \~russian Создание виртуальной позиции по БА (потоковый обработчик)
    /// </summary>
    [HandlerCategory(HandlerCategories.Position)] // Перекидываю в общую категорию работы с позициями
    [HelperName("Open Virtual Pos", Language = Constants.En)]
    [HelperName("Открыть вирт. позицию", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION | TemplateTypes.OPTION_SERIES, Name = Constants.AnyOption)]
    [OutputType(TemplateTypes.POSITION)]
    [Description("Создание виртуальной позиции по БА (потоковый обработчик)")]
    [HelperDescription("Open virtual position in base asset (stream handler)", Constants.En)]
    public class OpenVirtualFutPosition : IContextUses, IStreamHandler // IValuesHandlerWithNumber 
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
        public IPosition Execute(IOption opt)
        {
            return Execute(opt.UnderlyingAsset);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public IPosition Execute(IOptionSeries optSer)
        {
             return Execute(optSer.UnderlyingAsset);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.SECURITY, чтобы подключаться к источнику-БА
        /// </summary>
        public IPosition Execute(ISecurity sec)
        {
            IPosition res = null;
            int len = sec.Bars.Count;
            if (len <= 0)
                return res;
            
            DateTime openTime = sec.Bars[len - 1].Date;
            int j = len - 1;
            // Итерируемся до предыдущего дня
            while ((j > 0) && (sec.Bars[j].Date.Date == openTime.Date))
                j--;
            // Возвращаемся в сегодняшнее утро
            j++;

            string msg = String.Format("Creating virtual FUT position. j:{0}; Ticker:{1}; Qty:{2}; Px:{3}",
                j, sec.Symbol, m_fixedQty, m_fixedPx);
            m_context.Log(msg, MessageType.Info, true);

            //context.Log(msg, MessageType.Debug, true);
            //context.Log(msg, MessageType.Info, true);
            //context.Log(msg, MessageType.Warning, true);
            //context.Log(msg, MessageType.Error, true);
            //context.Log(msg, MessageType.Alert, true);

            res = sec.Positions.MakeVirtualPosition(j, m_fixedQty, m_fixedPx, "Open FUT");

            return res;
        }
    }
}
