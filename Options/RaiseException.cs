using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.Optimization;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Raise exception
    /// \~russian Выбросить исключение по команде юзера
    /// </summary>
#if !DEBUG
    [HandlerInvisible]
#endif
    [HandlerCategory(HandlerCategories.OptionsBugs)]
    [HelperName("Raise exception", Language = Constants.En)]
    [HelperName("Исключение", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION, Name = Constants.AnyOption)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Выбросить исключение по команде юзера")]
    [HelperDescription("Raise exception", Constants.En)]
    public class RaiseException : BaseContextHandler, IValuesHandlerWithNumber, IDisposable
    {
        private bool m_raise = false;

        #region Parameters
        /// <summary>
        /// Свойство надо привязать к кнопке
        /// </summary>
        [HandlerParameter(Name = "Raise exception", NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool Raise
        {
            get { return m_raise; }
            set { m_raise = value; }
        }
        #endregion Parameters

        public double Execute(IOption opt, int barNumber)
        {
            return Execute(opt.UnderlyingAsset, barNumber);
        }

        public double Execute(IOptionSeries optSer, int barNumber)
        {
            return Execute(optSer.UnderlyingAsset, barNumber);
        }

        public double Execute(ISecurity sec, int barNumber)
        {
            if (m_raise)
            {
                var msg = String.Format("[{0}] Raise exception!   VariableId: {1}",
                    GetType().Name, VariableId);
                Context.Log(msg, MessageType.Warning, true);

                throw new Exception(msg);
            }

            return 1;
        }

        public void Dispose()
        {
            if (Context != null) // если скрипт не исполнялся - контекст будет null, подписки на события тоже не будет
                Context.Log(String.Format("[DEBUG:{0}] {1} disposed", GetType().Name, VariableId));
        }
    }
}
