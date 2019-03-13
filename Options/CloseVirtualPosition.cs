using System;
using System.ComponentModel;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Close virtual position
    /// \~russian Закрыть виртуальную позицию
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Close Virtual Pos (bar)", Language = Constants.En)]
    [HelperName("Закрыть вирт. позицию (бары)", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [Input(0, TemplateTypes.POSITION)]
    [OutputsCount(0)]
    [Description("Закрыть виртуальную позицию (побарный обработчик)")]
    [HelperDescription("Close virtual position (bar handler)", Constants.En)]
    public class CloseVirtualPosition : IContextUses, IValuesHandlerWithNumber 
    {
        private const string DefaultPx = "125000";
        private const string DefaultTtl = "15";

        private IContext m_context;

        private double m_timeToLive = Double.Parse(DefaultTtl);
        private double m_fixedPx = Double.Parse(DefaultPx);

        public IContext Context
        {
            get { return m_context; }
            set { m_context = value; }
        }

        #region Parameters
        /// <summary>
        /// \~english Exit price for virtual position
        /// \~russian Фиксированная цена закрытия позиции
        /// </summary>
        [HelperName("Fixed Price", Constants.En)]
        [HelperName("Фиксированная цена", Constants.Ru)]
        [Description("Фиксированная цена закрытия позиции")]
        [HelperDescription("Exit price for virtual position", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultPx)]
        public double FixedPx
        {
            get { return m_fixedPx; }
            set { m_fixedPx = value; }
        }

        /// <summary>
        /// \~english Position lifetime (in minutes)
        /// \~russian Время жизни позиции (в минутах)
        /// </summary>
        [HelperName("Time to Live", Constants.En)]
        [HelperName("Время жизни", Constants.Ru)]
        [Description("Время жизни позиции (в минутах)")]
        [HelperDescription("Position lifetime (in minutes)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultTtl)]
        public double TimeToLive
        {
            get { return m_timeToLive; }
            set { m_timeToLive = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.POSITION, чтобы подключаться к источнику-позиции
        /// </summary>
        public void Execute(IPosition pos, int barNum)
        {
            if (pos == null)
                return;

            int len = pos.Security.Bars.Count;
            if (len <= 0)
                return;

            if (barNum < m_context.BarsCount - 1)
                return;

            if (pos.Shares == 0)
                return;

            DateTime openTime = pos.EntryBar.Date;
            DateTime now = pos.Security.Bars[len - 1].Date;
            if ((now - openTime).TotalMinutes >= m_timeToLive)
            {
                for (int j = pos.EntryBarNum; j < len; j++)
                {
                    if ((pos.Security.Bars[j].Date - openTime).TotalMinutes >= m_timeToLive)
                    {
                        string msg = String.Format("Closing virtual position. j:{0}; Ticker:{1}; Qty:{2}; Px:{3}",
                            j, pos.Security.Symbol, 0, m_fixedPx);
                        m_context.Log(msg, MessageType.Info, true);

                        pos.VirtualChange(j, m_fixedPx, 0, "Close");
                        break;
                    }
                }
            }
        }
    }
}
