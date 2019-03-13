using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

using TSLab.Script.CanvasPane;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Merge two series of numbers to InteractiveSeries
    /// \~russian Объединить две серии чисел в интерактивную линию
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsIndicators)]
    [HelperName("Prepare line", Language = Constants.En)]
    [HelperName("Подготовить линию", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.DOUBLE, Name = "X")]
    [Input(1, TemplateTypes.DOUBLE, Name = "Y")]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Объединить две серии чисел в интерактивную линию")]
    [HelperDescription("Merge two series of numbers to InteractiveSeries", Constants.En)]
    public class PrepareLineForCanvasPane : BaseContextHandler, IStreamHandler, IValuesHandlerWithNumber, IDisposable
    {
        /// <summary>
        /// Локальный накопитель точек для побарного обработчика
        /// </summary>
        private List<InteractiveObject> m_controlPoints;

        public InteractiveSeries Execute(IList<double> xValues, IList<double> yValues)
        {
            if ((xValues == null) || (xValues.Count <= 0) || (yValues == null) || (yValues.Count <= 0))
            {
                string msg = String.Format("[{0}] Null or empty data series.", GetType().Name);
                Context.Log(msg, MessageType.Error, false);
                return Constants.EmptySeries;
            }

            if (xValues.Count != yValues.Count)
            {
                string msg = String.Format("[{0}] Data series have different length. X.Len:{1}; Y.Len:{2}", GetType().Name, xValues.Count, yValues.Count);
                Context.Log(msg, MessageType.Warning, false);
                //return Constants.EmptySeries;
            }

            int len = Math.Min(xValues.Count, yValues.Count);
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            for (int j = 0; j < len; j++)
            {
                InteractivePointLight ip = new InteractivePointLight();
                ip.Value = new Point(xValues[j], yValues[j]);
                //ip.Tooltip = String.Format("F:{0}; D:{1}", f, yStr);

                controlPoints.Add(new InteractiveObject(ip));
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            return res;
        }

        public InteractiveSeries Execute(double xVal, double yVal, int barNum)
        {
            // 1. Если локальный накопитель еще не проинициализирован -- делаем это
            if (m_controlPoints == null)
                m_controlPoints = new List<InteractiveObject>();

            // 3. Добавляем новую точку в локальный накопитель
            InteractivePointLight ip = new InteractivePointLight();
            ip.Value = new Point(xVal, yVal);
            //ip.Tooltip = String.Format("F:{0}; D:{1}", f, yStr);

            m_controlPoints.Add(new InteractiveObject(ip));

            // 5. Если мы еще не добрались до правого края графика -- возвращаем пустую серию
            int barsCount = ContextBarsCount;
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            // 7. На правом краю графика возвращаем подготовленную серию точек
            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(m_controlPoints);

            return res;
        }

        public void Dispose()
        {
            if (m_controlPoints != null)
            {
                // Ни в коем случае нельзя чистить коллекцию! Только обнуляем указатель.
                //m_controlPoints.Clear();
                m_controlPoints = null;
            }
        }
    }
}
