using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Choose one of several option sources
    /// \~russian Выбор одного опционного источника из нескольких альтернатив
    /// </summary>
    [HandlerCategory(HandlerCategories.Options)]
    [HelperName("Option Selector", Language = Constants.En)]
    [HelperName("Выбор Опциона", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1, 8)]
    [Input(0, TemplateTypes.OPTION, Name = "Option 1")]
    [Input(1, TemplateTypes.OPTION, Name = "Option 2")]
    [Input(2, TemplateTypes.OPTION, Name = "Option 3")]
    [Input(3, TemplateTypes.OPTION, Name = "Option 4")]
    [Input(4, TemplateTypes.OPTION, Name = "Option 5")]
    [Input(5, TemplateTypes.OPTION, Name = "Option 6")]
    [Input(6, TemplateTypes.OPTION, Name = "Option 7")]
    [Input(7, TemplateTypes.OPTION, Name = "Option 8")]
    [OutputType(TemplateTypes.OPTION)]
    [Description("Выбор одного опционного источника из нескольких альтернатив")]
    [HelperDescription("Choose one of several option sources", Constants.En)]
    public class OptionSelector : BaseContextHandler, IStreamHandler, ICustomListValues
    {
        /// <summary>
        /// Спецификация кодировки месяца истечения фьючерса согласно http://moex.com/s205
        /// </summary>
        private const string MonthLetterCodes = "FGHJKMNQUVXZ";
        private const string DefaultOptionSeries = "RIM7";
        private const string DefaultBaseSecurityPrefix = "RI";

        // GLSP-435 - Проверяю другие варианты названий
        private const string VisibleBaseSecPrefixNameEn = "Base asset";
        private const string VisibleBaseSecPrefixNameRu = "Базовый актив";
        private const string VisibleOptionSeriesNameEn = "Option Series";
        private const string VisibleOptionSeriesNameRu = "Опционная серия";

        private string m_optionSeries = DefaultOptionSeries;
        private string m_baseSecPrefix = DefaultBaseSecurityPrefix;

        private static readonly char[] s_monthLetters = MonthLetterCodes.ToCharArray();

        /// <summary>
        /// Множество префиксов БА в локальном кеше кубика
        /// </summary>
        private HashSet<string> PrefixList
        {
            get
            {
                HashSet<string> prefixList = Context.LoadObject(VariableId + "_prefixList") as HashSet<string>;
                if (prefixList == null)
                {
                    prefixList = new HashSet<string>();
                    prefixList.Add(DefaultBaseSecurityPrefix);
                    Context.StoreObject(VariableId + "_prefixList", prefixList);
                }

                if (prefixList.Count == 0)
                    prefixList.Add(DefaultBaseSecurityPrefix);

                return prefixList;
            }
        }

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
                    seriesList.Add(DefaultOptionSeries);
                    Context.StoreObject(VariableId + "_seriesList", seriesList);
                }

                if (seriesList.Count == 0)
                    seriesList.Add(DefaultOptionSeries);
                
                return seriesList;
            }
        }

        #region Parameters
        /// <summary>
        /// \~english Select Base Asset (RI, Si, Eu, ES, ...)
        /// \~russian Выбор Базового Актива (RI, Si, Eu, ES, ...)
        /// </summary>
        [HelperName(VisibleBaseSecPrefixNameEn, Constants.En)]
        [HelperName(VisibleBaseSecPrefixNameRu, Constants.Ru)]
        [Description("Выбор Базового Актива (RI, Si, Eu, ES, ...)")]
        [HelperDescription("Select Base Asset (RI, Si, Eu, ES, ...)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultBaseSecurityPrefix)]
        public string BaseSecPrefix
        {
            get { return m_baseSecPrefix; }
            set
            {
                //if (!m_baseSecPrefix.Equals(value, StringComparison.InvariantCultureIgnoreCase))
                //{
                    m_baseSecPrefix = value;

                    //// Защищаемся от возврата пустого значения?..
                    //HashSet<string> serList = SeriesList;
                    //string serName = (from s in serList
                    //                  where s.StartsWith(m_baseSecPrefix, StringComparison.InvariantCultureIgnoreCase)
                    //                  select s).FirstOrDefault();
                    //if (!String.IsNullOrWhiteSpace(serName))
                    //{
                    //    OptionSeries = serName;
                    //}
                //}
            }
        }

        /// <summary>
        /// \~english Select option series (RIH5, SiG5, ESM6, ...)
        /// \~russian Выбор опционной серии (RIH5, SiG5, ESM6, ...)
        /// </summary>
        [HelperName(VisibleOptionSeriesNameEn, Constants.En)]
        [HelperName(VisibleOptionSeriesNameRu, Constants.Ru)]
        [Description("Выбор опционной серии (RIH5, SiG5, ESM6, ...)")]
        [HelperDescription("Select option series (RIH5, SiG5, ESM6, ...)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultOptionSeries)]
        public string OptionSeries
        {
            get { return m_optionSeries; }
            set { m_optionSeries = value; }
        }
        #endregion Parameters

        #region Многочисленные обработчики Execute под разное количество входов
        /// <summary>
        /// Обработчик под тип входных данных OPTION (1 аргумент)
        /// </summary>
        public IOption Execute(IOption opt)
        {
            IOption res = Select(new[] { opt });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION (2 аргумента)
        /// </summary>
        public IOption Execute(IOption opt1, IOption opt2)
        {
            IOption res = Select(new[] { opt1, opt2 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION (3 аргумента)
        /// </summary>
        public IOption Execute(IOption opt1, IOption opt2, IOption opt3)
        {
            IOption res = Select(new[] { opt1, opt2, opt3 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION (4 аргумента)
        /// </summary>
        public IOption Execute(IOption opt1, IOption opt2, IOption opt3, IOption opt4)
        {
            IOption res = Select(new[] { opt1, opt2, opt3, opt4 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION (5 аргументов)
        /// </summary>
        public IOption Execute(IOption opt1, IOption opt2, IOption opt3, IOption opt4, IOption opt5)
        {
            IOption res = Select(new[] { opt1, opt2, opt3, opt4, opt5 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION (6 аргументов)
        /// </summary>
        public IOption Execute(IOption opt1, IOption opt2, IOption opt3, IOption opt4, IOption opt5, IOption opt6)
        {
            IOption res = Select(new[] { opt1, opt2, opt3, opt4, opt5, opt6 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION (7 аргументов)
        /// </summary>
        public IOption Execute(IOption opt1, IOption opt2, IOption opt3, IOption opt4, IOption opt5, IOption opt6, IOption opt7)
        {
            IOption res = Select(new[] { opt1, opt2, opt3, opt4, opt5, opt6, opt7 });
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION (8 аргументов)
        /// </summary>
        public IOption Execute(IOption opt1, IOption opt2, IOption opt3, IOption opt4, IOption opt5, IOption opt6, IOption opt7, IOption opt8)
        {
            IOption res = Select(new[] { opt1, opt2, opt3, opt4, opt5, opt6, opt7, opt8 });
            return res;
        }
        #endregion Многочисленные обработчики Execute под разное количество входов

        /// <summary>
        /// Общий метод формирования списка префиксов
        /// </summary>
        private IOption Select(params IOption[] options)
        {
            if ((options == null) || (options.Length <= 0))
            {
                string msg = String.Format("Empty argument '{0}' is not supported. I return NULL immediately.", "options");
                return null;
            }

            HashSet<string> prefList = PrefixList;
            prefList.Clear();

            HashSet<string> serList = SeriesList;
            serList.Clear();

            IOption res = null;
            //DateTime now = bar.Date;
            bool seriesFound = false;
            for (int j = 0; j < options.Length; j++)
            {
                IOption opt = options[j];
                if (opt == null)
                    continue;
                string prefix = opt.UnderlyingAsset.Symbol.Substring(0, 2);
                prefList.Add(prefix);

                // Заполняю ВСЕ серии
                // TODO: использовать только активные???
                foreach (var ser in opt.GetSeries())
                {
                    char month = s_monthLetters[ser.ExpirationDate.Month - 1];
                    string serName = prefix + month + (ser.ExpirationDate.Year % 10);
                    serList.Add(serName);

                    // Здесь идея в том, что при работе на двух комбиках внешнее состояние дочернего бокса меняется правильно,
                    // а внутреннее остаётся некорректным. Поэтому помимо точного совпадения серий надо перепроверять ещё и префикс.
                    if ((serName.Equals(m_optionSeries, StringComparison.InvariantCultureIgnoreCase)) &&
                        (prefix.StartsWith(m_baseSecPrefix, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        res = opt; // НАШЛИ ОПЦИОН! УРА!
                        seriesFound = true;
                    }
                }

                // Нашли БА? Отлично! Надо его запомнить на всякий случай. Но только первый раз.
                if (prefix.StartsWith(m_baseSecPrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    if ((res == null) && (!seriesFound))
                        res = opt;
                }
            }

            // Если серию найти не удалось, восстанавливаю значения переменных и возвращаю первый аргумент
            if ((res == null) && (options.Length > 0) && (options[0] != null))
            {
                res = options[0];
                string msg = String.Format(RM.GetString("OptHandlerMsg.OptionSelector.OptionSeriesNotFound"),
                    m_baseSecPrefix, m_optionSeries, res.Symbol);
                m_context.Log(msg, MessageType.Error);
            }

            return res;
        }

        public IEnumerable<string> GetValuesForParameter(string paramName)
        {
            if (paramName.Equals(nameof(BaseSecPrefix), StringComparison.InvariantCultureIgnoreCase) ||
                // GLSP-435 - Проверяю другие варианты названий
                paramName.Equals(VisibleBaseSecPrefixNameEn, StringComparison.InvariantCultureIgnoreCase) ||
                paramName.Equals(VisibleBaseSecPrefixNameRu, StringComparison.InvariantCultureIgnoreCase))
            {
                HashSet<string> res = PrefixList;
                //res.Sort();
                return res.ToArray();
            }
            else if (paramName.Equals(nameof(OptionSeries), StringComparison.InvariantCultureIgnoreCase) ||
                     // GLSP-435 - Проверяю другие варианты названий
                     paramName.Equals(VisibleOptionSeriesNameEn, StringComparison.InvariantCultureIgnoreCase) ||
                     paramName.Equals(VisibleOptionSeriesNameRu, StringComparison.InvariantCultureIgnoreCase))
            {
                HashSet<string> series = SeriesList;
                //res.Sort();
                var res = from s in series
                          where s.StartsWith(m_baseSecPrefix, StringComparison.InvariantCultureIgnoreCase)
                          select s;
                return res;
            }
            else
                throw new ArgumentException("Parameter '" + paramName + "' is not supported.", "paramName");
        }
    }
}
