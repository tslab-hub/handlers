using System;
using System.ComponentModel;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Clear collection of virtual positions
    /// \~russian Удалить виртуальные позиции
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Clear Virtual Positions", Language = Constants.En)]
    [HelperName("Удалить виртуальные позиции", Language = Constants.Ru)]
    [InputsCount(0)]
    [OutputsCount(0)]
    [Description("Блок служит для удаления виртуальных позиций. Для этого нужно привязать его свойство 'Удалить позиции' к 'Контрольной панели' и оформить его в виде кнопки.")]
    [HelperDescription("This block allows you to delete virtual positions. Connect Delete positions property to Control Pane and create a button.", Constants.En)]
    public class DropVirtualPositions : IContextUses, IValuesHandlerWithNumber
    {
        private IContext m_context;

        private bool m_dropVirtualPositions = false;

        public IContext Context
        {
            get { return m_context; }
            set { m_context = value; }
        }

        #region Parameters
        /// <summary>
        /// \~english Drop virtual positions
        /// \~russian Очистить коллекцию виртуальных позиций
        /// </summary>
        [HelperName("Drop Positions", Constants.En)]
        [HelperName("Удалить позиции", Constants.Ru)]
        [Description("Очистить коллекцию виртуальных позиций")]
        [HelperDescription("Drop virtual positions", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "False")]
        public bool DropPositions
        {
            get { return m_dropVirtualPositions; }
            set { m_dropVirtualPositions = value; }
        }
        #endregion Parameters

        public void Execute(int barNum)
        {
            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return;

            if (m_dropVirtualPositions)
            {
                try
                {
                    PositionsManager posMan = PositionsManager.GetManager(m_context);
                    m_context.Log("All virtual positions will be dropped right now.", MessageType.Warning, true);
                    posMan.DropVirtualPositions(m_context);

                    // Безтолку делать повторный пересчет
                    //context.Recalc(true);
                }
                finally
                {
                    m_dropVirtualPositions = false;
                }
            }
        }
    }
}
