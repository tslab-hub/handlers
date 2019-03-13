using System;
using System.ComponentModel;

using TSLab.Script.CanvasPane;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Select one of several available smiles using handler parameter
    /// \~russian Выбрать одну из улыбок на основании параметра блока
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Select smile", Language = Constants.En)]
    [HelperName("Выбрать улыбку", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(2, 3)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = "Market")]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = "Model")]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = "Exchange")]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Выбрать одну из улыбок на основании параметра блока")]
    [HelperDescription("Select one of several available smiles using handler parameter", Constants.En)]
    public class SmileSelector : IValuesHandlerWithNumber
    {
        private const string MsgId = "SmileSelector";

        private int m_smileIndex = 0;

        #region Parameters
        /// <summary>
        /// \~english Input index (start from 1)
        /// \~russian Индекс входа (начинается с 1)
        /// </summary>
        [HelperName("Input index", Constants.En)]
        [HelperName("Индекс входа", Constants.Ru)]
        [Description("Индекс входа (начинается с 1)")]
        [HelperDescription("Input index (start from 1)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "1", Min = "1", Max = "3", Step = "1")]
        public int SmileIndex
        {
            get { return m_smileIndex; }
            set { m_smileIndex = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Обработчик для двух улыбок
        /// </summary>
        public InteractiveSeries Execute(InteractiveSeries marketSmile, InteractiveSeries modelSmile, int barNum)
        {
            InteractiveSeries res = SelectSmile(m_smileIndex,
                marketSmile, modelSmile);
            return res;
        }

        /// <summary>
        /// Обработчик для трех улыбок
        /// </summary>
        public InteractiveSeries Execute(InteractiveSeries marketSmile, InteractiveSeries modelSmile,
            InteractiveSeries exchangeSmile, int barNum)
        {
            InteractiveSeries res = SelectSmile(m_smileIndex,
                marketSmile, modelSmile, exchangeSmile);
            return res;
        }

        internal static InteractiveSeries SelectSmile(int index, params InteractiveSeries[] smiles)
        {
            if ((smiles == null) || (smiles.Length <= 0) ||
                (index < 0) || (smiles.Length <= index))
                return null;

            InteractiveSeries res = smiles[index];
            return res;
        }
    }
}
