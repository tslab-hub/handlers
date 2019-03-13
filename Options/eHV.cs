using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using TSLab.DataSource;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// Оценка исторической волатильности. При реализации оператора усреднения SMA заменена на EMA.
    /// </summary>
    //[HandlerCategory(HandlerCategories.OptionsIndicators)]
    //[HandlerName("eHV", Language = "en-US")]
    //[InputsCount(1)]
    //[Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION, Name = Constants.AnyOption)]
    //[OutputType(TemplateTypes.DOUBLE)]
    //[Description("Оценка исторической волатильности. При реализации оператора усреднения SMA заменена на EMA.")]
    [SuppressMessage("StyleCopPlus.StyleCopPlusRules", "SP0100:AdvancedNamingRules", Justification = "Reviewed. Suppression is OK here.")]
    public class eHV : IContextUses, IStreamHandler, INeedVariableId
    {
        private const double PctMult = 100.0;

        private const string DefaultPeriod = "810";
        private const string DefaultMult = "452";

        private IContext m_context;
        private string m_variableId;

        private bool m_useAllData = false;
        private int m_period = Int32.Parse(DefaultPeriod);
        private double m_annualizingMultiplier = Double.Parse(DefaultMult);

        public IContext Context
        {
            get { return m_context; }
            set { m_context = value; }
        }

        public string VariableId
        {
            get { return m_variableId; }
            set { m_variableId = value; }
        }

        #region Parameters
        /// <summary>
        /// При true будет учитывать все данные, включая ночные гепы
        /// </summary>
        [Description("При true будет учитывать все данные, включая ночные гепы")]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "false")]
        public bool UseAllData
        {
            get { return m_useAllData; }
            set { m_useAllData = value; }
        }

        [Description("Период расчета исторической волатильности")]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = DefaultPeriod, Min = "2", EditorMin = "2")]
        public int Period
        {
            get { return m_period; }
            set { m_period = Math.Max(2, value); }
        }

        [Description("Множитель для перевода волатильности в годовое исчисление")]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = DefaultMult, Min = "0")]
        public double AnnualizingMultiplier
        {
            get { return m_annualizingMultiplier; }
            set
            {
                if (value > 0)
                    m_annualizingMultiplier = value;
            }
        }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION, чтобы подключаться к источнику-БА
        /// </summary>
        public IList<double> Execute(IOption opt)
        {
            return Execute(opt.UnderlyingAsset);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-БА
        /// </summary>
        public IList<double> Execute(IOptionSeries optSer)
        {
            return Execute(optSer.UnderlyingAsset);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.SECURITY, чтобы подключаться к источнику-БА
        /// </summary>
        public IList<double> Execute(ISecurity sec)
        {
            List<double> historySigmas = m_context.LoadObject(VariableId + "historySigmas") as List<double>;
            if (historySigmas == null)
            {
                historySigmas = new List<double>();
                m_context.StoreObject(VariableId + "historySigmas", historySigmas);
            }

            int len = sec.Bars.Count;
            int barLengthInSeconds = (int)sec.IntervalInstance.ToSeconds();
            LinkedList<KeyValuePair<DateTime, double>> logs = m_context.LoadObject(VariableId + "logs") as LinkedList<KeyValuePair<DateTime, double>>;
            if (logs == null)
            {
                logs = new LinkedList<KeyValuePair<DateTime, double>>();
                m_context.StoreObject(VariableId + "logs", logs);
            }

            // Типа, кеширование?
            for (int j = historySigmas.Count; j < len; j++)
            {
                IDataBar bar = sec.Bars[j];
                DateTime t = bar.Date;
                double v = bar.Close;
                double ln = Math.Log(v);

                logs.AddLast(new KeyValuePair<DateTime, double>(t, ln));

                double hv;
                if (HV.TryEstimateHv(
                    logs, m_period, barLengthInSeconds, m_annualizingMultiplier,
                    m_useAllData, out hv))
                {
                    double vol = hv;
                    historySigmas.Add(vol);
                }
                else
                    historySigmas.Add(Double.NaN);
            }

            return new ReadOnlyCollection<double>(historySigmas);
        }

        /// <summary>
        /// Выполнение оценки исторической волатильности
        /// </summary>
        /// <param name="logs"></param>
        /// <param name="period"></param>
        /// <param name="barLengthInSeconds"></param>
        /// <param name="annualizingMultiplier"></param>
        /// <param name="useAllData"></param>
        /// <param name="hv"></param>
        /// <returns></returns>
        public static bool TryEstimateEHV(LinkedList<KeyValuePair<DateTime, double>> data, int period,
            int barLengthInSeconds, double annualizingMultiplier, bool useAllData, out double res)
        {
            res = 0;
            if (data.Count <= period)
                return false;

            var last = data.Last;
            if (last == null)
                return false;

            uint counter = 0;
            double sum = 0, sum2 = 0;
            if (barLengthInSeconds > 0)
            {
                while (counter < period)
                {
                    var prev = last.Previous;
                    if (prev == null)
                        break;

                    double r;
                    TimeSpan ts = last.Value.Key - prev.Value.Key;
                    if (useAllData || ((int)ts.TotalSeconds == barLengthInSeconds))
                    {
                        r = last.Value.Value - prev.Value.Value;
                        sum += r;
                        sum2 += r * r;

                        counter++;
                    }

                    last = prev;
                }

                // Чистимся?
                if (last.Previous != null)
                {
                    last = last.Previous;
                    var first = data.First;
                    while ((first != null) && (first != last))
                    {
                        first = first.Next;
                        data.RemoveFirst();
                        //pxs.RemoveFirst();
                    }
                }
            }
            else
            {
                throw new NotImplementedException("BarLengthInSeconds must be above zero.");
            }

            if (counter < period)
                return false;

            double dispersion = sum2 / counter - sum * sum / counter / counter;
            double sigma = (dispersion > 0) ? Math.Sqrt(dispersion) : 0;

            res = sigma * annualizingMultiplier;
            return true;
        }
    }
}
