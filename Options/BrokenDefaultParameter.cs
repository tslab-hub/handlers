using System;
using System.ComponentModel;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// BrokenDefaultParameter
    /// </summary>
    //[HandlerCategory(Constants.Bugs)]
    //[HandlerName("BrokenDefaultParameter")]
    //[InputsCount(1)]
    //[Input(0, TemplateTypes.OPTION)]
    //[OutputsCount(1)]
    //[OutputType(TemplateTypes.DOUBLE)]
    //[Description("BrokenDefaultParameter")]
    public class BrokenDefaultParameter : BaseContextHandler, IValuesHandlerWithNumber
    {
        protected double m_prevRnd = 3.1415;
        protected System.Random m_rnd = new System.Random((int)DateTime.Now.Ticks);

        #region Parameters
        //[Description("Rnd")]
        //[HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "Просто текст")]
        //public double Rnd
        //{
        //    get { return m_prevRnd; }
        //    set { }
        //}
        #endregion Parameters

        public double Execute(IOption opt, int barNumber)
        {
            m_prevRnd = 100.0 * m_rnd.NextDouble();

            if (barNumber >= m_context.BarsCount - 1)
            {
                m_context.Log(String.Format("RND[{0}]: {1}", barNumber, m_prevRnd), MessageType.Warning, true);
            }

            return m_prevRnd;
        }
    }
}
