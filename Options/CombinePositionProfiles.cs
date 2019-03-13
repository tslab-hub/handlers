using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TSLab.Script.CanvasPane;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Add 2 position profiles
    /// \~russian Сложить два профиля позиций
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Combine Series Profiles", Language = Constants.En)]
    [HelperName("Сложить профили позиций", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = "Profile1")]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = "Profile2")]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Сложить два профиля позиций")]
    [HelperDescription("Add 2 position profiles", Constants.En)]
    public class CombinePositionProfiles : BaseContextHandler, IValuesHandlerWithNumber
    {
        #region Parameters
        #endregion Parameters

        public InteractiveSeries Execute(InteractiveSeries ser1, InteractiveSeries ser2, int barNum)
        {
            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            if ((ser1 == null) && (ser2 == null))
                return Constants.EmptySeries;
            else if ((ser1 == null) && (ser2 != null))
                return ser2;
            else if ((ser1 != null) && (ser2 == null))
                return ser1;

            var query = (from s1 in ser1.ControlPoints
                         from s2 in ser2.ControlPoints
                         where DoubleUtil.AreClose(s1.Anchor.Value.X, s2.Anchor.Value.X)
                         select new { cp1 = s1, cp2 = s2 });

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (var pair in query)
            {
                double x = pair.cp1.Anchor.Value.X;
                double y = pair.cp1.Anchor.Value.Y + pair.cp2.Anchor.Value.Y;
                InteractivePointActive ip = new InteractivePointActive(x, y);
                //ip.Geometry = Geometries.Rect;
                ip.Tooltip = String.Format("F:{0}; PnL:{1}", x, y);

                controlPoints.Add(new InteractiveObject(ip));
            }

            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            return res;
        }
    }
}
