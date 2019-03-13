using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.DataSource;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Options bid prices. Strike is ignored if there is no demand in it.
    /// \~russian Цены покупки опционов. Если цены покупки нет, страйк игнорируется.
    /// </summary>
    [HelperName("Bid smile", Language = Constants.En)]
    [HelperName("Улыбка по бидам", Language = Constants.Ru)]
    [Description("Цены покупки опционов. Если цены покупки нет, страйк игнорируется.")]
    [HelperDescription("Options bid prices. Strike is ignored if there is no demand in it.", Constants.En)]
    public class BidSmile : OptionSeriesBaseN
    {
        protected override IEnumerable<Double2> Calculate(IOptionStrike[] strikes, int barNum)
        {
            return strikes.Where(st =>
            {
                var security = st.Security;
                return !security.IsDisposed && security.Bars.Count > barNum && st.Security.Bars[barNum] is IBar &&
                       ((IBar)st.Security.Bars[barNum]).Bid > 0;
            })
                .Select(st => new Double2 { V1 = st.Strike, V2 = ((IBar)st.Security.Bars[barNum]).Bid });
        }
    }

    /// <summary>
    /// \~english Options ask prices. Strike is ignored if there is no offer in it.
    /// \~russian Цены продажи опционов. Если цены продажи нет, страйк игнорируется.
    /// </summary>
    [HelperName("Ask smile", Language = Constants.En)]
    [HelperName("Улыбка по оферам", Language = Constants.Ru)]
    [Description("Цена продажи опционов. Если цены нет, страйк игнорируется.")]
    [HelperDescription("Options ask prices. Strike is ignored if there is no offer in it.", Constants.En)]
    public class AskSmile : OptionSeriesBaseN
    {
        protected override IEnumerable<Double2> Calculate(IOptionStrike[] strikes, int barNum)
        {
            return strikes.Where(st =>
            {
                var security = st.Security;
                return !security.IsDisposed && security.Bars.Count > barNum && st.Security.Bars[barNum] is IBar &&
                       ((IBar)st.Security.Bars[barNum]).Ask > 0;
            })
                .Select(st => new Double2 { V1 = st.Strike, V2 = ((IBar)st.Security.Bars[barNum]).Ask });
        }
    }
}
