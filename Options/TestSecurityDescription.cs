using System;
using System.ComponentModel;

using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// Тест заполнения SecurityDescription при выключенном источнике.
    /// </summary>
    [HandlerCategory(Constants.Bugs)]
    [HelperName("Test Security Description")]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION, Name = Constants.AnyOption)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Тест заполнения SecurityDescription при выключенном источнике.")]
    public class TestSecurityDescription : BaseContextHandler, IValuesHandlerWithNumber
    {
        #region Parameters
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION, чтобы подключаться к источнику-серии
        /// </summary>
        public double Execute(IOption opt, int barNumber)
        {
            return Execute(opt.UnderlyingAsset, barNumber);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public double Execute(IOptionSeries optSer, int barNumber)
        {
            double res = Execute(optSer.UnderlyingAsset, barNumber);

            if (barNumber < m_context.BarsCount - 1)
                return res;

            double accum = 0;
            foreach (IOptionStrike strike in optSer.GetStrikes())
            {
                ISecurity sec = strike.Security;
                accum += Execute(sec, barNumber);
            }

            return res + accum;
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.SECURITY, чтобы подключаться к источнику-серии
        /// </summary>
        public double Execute(ISecurity sec, int barNumber)
        {
            if (barNumber < m_context.BarsCount - 1)
                return Double.NaN;

            if (sec.SecurityDescription == null)
                m_context.Log("NOT INITIALIZED!", MessageType.Error, true);
            else
            {
                string msg = String.Format("Symbol: {0}; Expired: {1}; ExpirationDate:{2}", sec.Symbol, sec.SecurityDescription.Expired, sec.SecurityDescription.ExpirationDate);
                m_context.Log(msg, MessageType.Info, true);
            }

            return DateTime.Now.TimeOfDay.Seconds;
        }
    }
}
