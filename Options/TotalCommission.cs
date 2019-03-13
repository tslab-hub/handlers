using System;
using System.ComponentModel;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Total commission including closed positions
    /// \~russian Полная комиссия за время жизни скрипта (включая закрытые позиции)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Total Commission", Language = Constants.En)]
    [HelperName("Полная комиссия", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION, Name = Constants.AnyOption)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Полная комиссия за время жизни скрипта (включая закрытые позиции)")]
    [HelperDescription("Total commission including closed positions", Constants.En)]
    public class TotalCommission : IValuesHandlerWithNumber, IDoubleReturns
    {
        #region Parameters
        #endregion Parameters

        public double Execute(ISecurity sec, int barNumber)
        {
            double res = 0;
            var positions = sec.Positions.GetClosedOrActiveForBar(barNumber);
            foreach (IPosition pos in positions)
            {
                res += pos.EntryCommission;

                if (!pos.IsActiveForBar(barNumber))
                {
                    res += pos.ExitCommission;
                }
            }
            return res;
        }

        public double Execute(IOptionSeries optSer, int barNumber)
        {
            double res = Execute(optSer.UnderlyingAsset, barNumber);

            foreach (var strike in optSer.GetStrikes())
            {
                double comm = Execute(strike.Security, barNumber);
                res += comm;
            }

            return res;
        }

        public double Execute(IOption opt, int barNumber)
        {
            double res = Execute(opt.UnderlyingAsset, barNumber);

            foreach (var ser in opt.GetSeries())
            {
                double comm = Execute(ser, barNumber);
                res += comm;
            }

            return res;
        }
    }
}
