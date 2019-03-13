using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Choose one of several option series
    /// \~russian Выбор одной опционной серии из нескольких альтернатив
    /// </summary>
    [HandlerCategory(HandlerCategories.Options)]
    [HelperName("Series Selector", Language = Constants.En)]
    [HelperName("Выбор Серии", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1, 9)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = "Option 1")]
    [Input(1, TemplateTypes.OPTION_SERIES, Name = "Option 2")]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = "Option 3")]
    [Input(3, TemplateTypes.OPTION_SERIES, Name = "Option 4")]
    [Input(4, TemplateTypes.OPTION_SERIES, Name = "Option 5")]
    [Input(5, TemplateTypes.OPTION_SERIES, Name = "Option 6")]
    [Input(6, TemplateTypes.OPTION_SERIES, Name = "Option 7")]
    [Input(7, TemplateTypes.OPTION_SERIES, Name = "Option 8")]
    [Input(7, TemplateTypes.OPTION_SERIES, Name = "Option 9")]
    [OutputType(TemplateTypes.OPTION_SERIES)]
    [Description("Выбор одной опционной серии из нескольких альтернатив")]
    [HelperDescription("Choose one of several option series", Constants.En)]
    public class SeriesSelector : BaseContextHandler, IStreamHandler, ICustomListValues
    {
        /// <summary>
        /// Спецификация кодировки месяца истечения фьючерса согласно http://moex.com/s205
        /// </summary>
        private const string MonthLetterCodes = "FGHJKMNQUVXZ";

        // GLSP-435 - Проверяю другие варианты названий
        private const string VisibleOptionSeriesNameEn = "Option Series";
        private const string VisibleOptionSeriesNameRu = "Опционная серия";

        private bool m_aliveOnly = true;
        private string m_optionSeries = "NULL";

        private static readonly char[] s_monthLetters = MonthLetterCodes.ToCharArray();

        /// <summary>
        /// Множество опционных серий в локальном кеше кубика
        /// </summary>
        private HashSet<string> SeriesList
        {
            get
            {
                HashSet<string> seriesList = Context.LoadObject(VariableId + "_seriesList") as HashSet<string>;
                if (seriesList == null)
                {
                    seriesList = new HashSet<string>();
                    //seriesList.Add(DefaultOptionSeries);
                    Context.StoreObject(VariableId + "_seriesList", seriesList);
                }

                //if (seriesList.Count == 0)
                //    seriesList.Add(DefaultOptionSeries);

                return seriesList;
            }
        }

        #region Parameters
        /// <summary>
        /// \~english Handler will use only alive option series
        /// \~russian При true будет находить и использовать только живые серии
        /// </summary>
        [HelperName("Alive Only", Constants.En)]
        [HelperName("Только живые", Constants.Ru)]
        [Description("При true будет находить и использовать только живые серии")]
        [HelperDescription("Handler will use only alive option series", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "true", Name = "Alive Only")]
        public bool AliveOnly
        {
            get { return m_aliveOnly; }
            set { m_aliveOnly = value; }
        }

        // ВАЖНО! Если видимое название свойства изменится, нужно будет изменить также код функции GetValuesForParameter
        /// <summary>
        /// \~english Select option series (RIH5, SiG5, ESM6, ...)
        /// \~russian Выбор опционной серии (RIH5, SiG5, ESM6, ...)
        /// </summary>
        [HelperName(VisibleOptionSeriesNameEn, Constants.En)]
        [HelperName(VisibleOptionSeriesNameRu, Constants.Ru)]
        [Description("Выбор опционной серии (RIH5, SiG5, ESM6, ...)")]
        [HelperDescription("Select option series (RIH5, SiG5, ESM6, ...)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "NULL")]
        public string OptionSeries
        {
            get { return m_optionSeries; }
            set
            {
                HashSet<string> res = SeriesList;
                if (res.Contains(value))
                    m_optionSeries = value;
                else
                {
                    if (res.Count > 0)
                    {
                        m_optionSeries = res.First();
                    }
                }
            }
        }
        #endregion Parameters

        #region Многочисленные обработчики Execute под разное количество входов
        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES (1 аргумент)
        /// </summary>
        public IOptionSeries Execute(IOptionSeries opt)
        {
            IOptionSeries res = Select(new[] { opt });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES (2 аргумента)
        /// </summary>
        public IOptionSeries Execute(IOptionSeries opt1, IOptionSeries opt2)
        {
            IOptionSeries res = Select(new[] { opt1, opt2 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES (3 аргумента)
        /// </summary>
        public IOptionSeries Execute(IOptionSeries opt1, IOptionSeries opt2, IOptionSeries opt3)
        {
            IOptionSeries res = Select(new[] { opt1, opt2, opt3 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES (4 аргумента)
        /// </summary>
        public IOptionSeries Execute(IOptionSeries opt1, IOptionSeries opt2, IOptionSeries opt3, IOptionSeries opt4)
        {
            IOptionSeries res = Select(new[] { opt1, opt2, opt3, opt4 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES (5 аргументов)
        /// </summary>
        public IOptionSeries Execute(IOptionSeries opt1, IOptionSeries opt2, IOptionSeries opt3, IOptionSeries opt4, IOptionSeries opt5)
        {
            IOptionSeries res = Select(new[] { opt1, opt2, opt3, opt4, opt5 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES (6 аргументов)
        /// </summary>
        public IOptionSeries Execute(IOptionSeries opt1, IOptionSeries opt2, IOptionSeries opt3,
            IOptionSeries opt4, IOptionSeries opt5, IOptionSeries opt6)
        {
            IOptionSeries res = Select(new[] { opt1, opt2, opt3, opt4, opt5, opt6 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES (7 аргументов)
        /// </summary>
        public IOptionSeries Execute(IOptionSeries opt1, IOptionSeries opt2, IOptionSeries opt3,
            IOptionSeries opt4, IOptionSeries opt5, IOptionSeries opt6, IOptionSeries opt7)
        {
            IOptionSeries res = Select(new[] { opt1, opt2, opt3, opt4, opt5, opt6, opt7 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES (8 аргументов)
        /// </summary>
        public IOptionSeries Execute(IOptionSeries opt1, IOptionSeries opt2, IOptionSeries opt3,
            IOptionSeries opt4, IOptionSeries opt5, IOptionSeries opt6, IOptionSeries opt7, IOptionSeries opt8)
        {
            IOptionSeries res = Select(new[] { opt1, opt2, opt3, opt4, opt5, opt6, opt7, opt8 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES (9 аргументов)
        /// </summary>
        public IOptionSeries Execute(IOptionSeries opt1, IOptionSeries opt2, IOptionSeries opt3,
            IOptionSeries opt4, IOptionSeries opt5, IOptionSeries opt6, IOptionSeries opt7, IOptionSeries opt8,
            IOptionSeries opt9)
        {
            IOptionSeries res = Select(new[] { opt1, opt2, opt3, opt4, opt5, opt6, opt7, opt8, opt9 });
            return res;
        }
        #endregion Многочисленные обработчики Execute под разное количество входов

        /// <summary>
        /// Общий метод формирования списка префиксов
        /// </summary>
        private IOptionSeries Select(params IOptionSeries[] optionSeries)
        {
            if ((optionSeries == null) || (optionSeries.Length <= 0))
            {
                string msg = String.Format("Empty argument '{0}' is not supported. I return NULL immediately.", "optionSeries");
                return null;
            }

            HashSet<string> serList = SeriesList;
            serList.Clear();

            IOptionSeries res = null;
            for (int j = 0; j < optionSeries.Length; j++)
            {
                IOptionSeries ser = optionSeries[j];
                if (ser == null)
                    continue;
                DateTime today = ser.UnderlyingAsset.FinInfo.LastUpdate.Date;
                if (m_aliveOnly && (ser.ExpirationDate.Date < today))
                    continue;

                if (m_aliveOnly && (ser.ExpirationDate.Date < today))
                    continue;

                //// TODO: как быть с инструментами типа RTS-3.15???
                //string prefix = ser.UnderlyingAsset.Symbol.Substring(0, 2);
                string prefix = ser.UnderlyingAsset.Symbol;

                ////char month = s_monthLetters[ser.ExpirationDate.Month - 1];
                ////string serName = prefix + month + (ser.ExpirationDate.Year % 10);
                //// Эмпирическое правило, что опционы недельные...
                //// TODO: тикет на флаг IsWeekly?..
                //if (Math.Abs(ser.ExpirationDate.Day - 15) > 6)
                //    serName = serName + "w" + (ser.ExpirationDate.Day / 7 + 1);

                string serName = prefix + " " + ser.ExpirationDate.ToString("MMMM dd", CultureInfo.InvariantCulture);
                serList.Add(serName);

                if (serName.Equals(m_optionSeries, StringComparison.InvariantCultureIgnoreCase))
                {
                    res = ser; // НАШЛИ СЕРИЮ! УРА!
                }
            }

            // Если серию найти не удалось, возвращаю первый аргумент
            if ((res == null) && (optionSeries.Length > 0) && (optionSeries[0] != null))
            {
                res = optionSeries[0];
                string msg = String.Format("Option series not found. Base asset: '{0}'; series: '{1}'. I'll return option '{2}'.",
                    res.UnderlyingAsset.Symbol, m_optionSeries, res.UnderlyingAsset.Symbol);
                m_context.Log(msg, MessageType.Info, false);
            }

            return res;
        }

        public IEnumerable<string> GetValuesForParameter(string paramName)
        {
            if (paramName.Equals(nameof(OptionSeries), StringComparison.InvariantCultureIgnoreCase) ||
                // GLSP-435 - Проверяю другие варианты названий
                paramName.Equals(VisibleOptionSeriesNameEn, StringComparison.InvariantCultureIgnoreCase) ||
                paramName.Equals(VisibleOptionSeriesNameRu, StringComparison.InvariantCultureIgnoreCase))
            {
                HashSet<string> res = SeriesList;
                //res.Sort();
                //var res = from s in series
                //          where s.StartsWith(m_baseSecPrefix, StringComparison.InvariantCultureIgnoreCase)
                //          select s;
                return res;
            }
            else
                throw new ArgumentException("Parameter '" + paramName + "' is not supported.", nameof(paramName));
        }
    }
}
