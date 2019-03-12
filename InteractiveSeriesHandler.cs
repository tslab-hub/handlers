#if DEBUG
using System;
using System.Collections.Generic;
using System.ComponentModel;

using TSLab.Script.CanvasPane;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.ClusterAnalysis)]
    [HelperName("InteractiveSeriesHandler", Language = Constants.En)]
    [HelperName("InteractiveSeriesHandler", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("БЕЗ ОПИСАНИЯ (кубик виден только в девелоперской версии)")]
    [HelperDescription("БЕЗ ОПИСАНИЯ (кубик виден только в девелоперской версии)", Constants.En)]
#if !DEBUG
    [HandlerInvisible]
#endif
    public sealed class InteractiveSeriesHandler : IOneSourceHandler, IStreamHandler, ISecurityInputs
    {
        public InteractiveSeries Execute(ISecurity security)
        {
            var interactiveObjects = new List<InteractiveObject>();
            for (byte i = 0; i < 31; i++)
            {
                var j = (byte)(i << 3);
                var interactivePointActive = new InteractivePointActive(i, i)
                {
                    BackColor = new Color(j, 0, 0),
                    ForeColor = new Color(0, j, 0),

                    IsActive = true,
                    Label = i.ToString(),
                    Color = new AlphaColor(255, j, 0, 0),
                    DateTime = DateTime.UtcNow.AddDays(1),

                    ValueXBackColor = new AlphaColor(255, j, 0, 0),
                    ValueXForeColor = new AlphaColor(255, 0, j, 0),

                    ValueYBackColor = new AlphaColor(255, 0, j, 0),
                    ValueYForeColor = new AlphaColor(255, 0, 0, j),

                    DateTimeBackColor = new AlphaColor(255, 0, 0, j),
                    DateTimeForeColor = new AlphaColor(255, j, 0, 0),
                };
                interactiveObjects.Add(new InteractiveObject { Anchor = interactivePointActive });
            }
            return new InteractiveSeries(interactiveObjects);
        }
    }
}
#endif
