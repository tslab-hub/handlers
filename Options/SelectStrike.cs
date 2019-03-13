using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Use drop-down control to select single strike from option series
    /// \~russian Выбрать из серии один страйк с помощью выпадающего списка
    /// </summary>
    [HandlerCategory(HandlerCategories.Options)]
    [HelperName("Select strike", Language = Constants.En)]
    [HelperName("Выбор страйка", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Выбрать из серии один страйк с помощью выпадающего списка. Для этого надо связать свойство 'Страйк' с Контрольной панелью и оформить его в виде выпадающего списка.")]
    [HelperDescription("Use drop-down control to select single strike from option series. To use it link 'Strike' property to Control pane.", Constants.En)]
    // ReSharper disable once UnusedMember.Global
    public class SelectStrike : BaseContextHandler, IStreamHandler, ICustomListValues
    {
        private const string DefaultStrike = "120000";

        private double m_strikeStep = 0;
        private string m_strike = DefaultStrike;

        /// <summary>
        /// Множество опционных страйков в локальном кеше кубика
        /// </summary>
        private HashSet<string> StrikeList
        {
            get
            {
                HashSet<string> strikeList = Context.LoadObject(VariableId + "_strikeList") as HashSet<string>;
                if (strikeList == null)
                {
                    strikeList = new HashSet<string> { DefaultStrike };
                    Context.StoreObject(VariableId + "_strikeList", strikeList);
                }

                if (strikeList.Count == 0)
                    strikeList.Add(DefaultStrike);

                return strikeList;
            }
        }

        /// <summary>
        /// Локальный кеш страйков
        /// </summary>
        private List<double> LocalStrikeHistory
        {
            get
            {
                List<double> hvSigmas = Context.LoadObject(VariableId + "_historyStrikes") as List<double>;
                if (hvSigmas == null)
                {
                    hvSigmas = new List<double>();
                    Context.StoreObject(VariableId + "_historyStrikes", hvSigmas);
                }

                return hvSigmas;
            }
        }

        #region Parameters
        /// <summary>
        /// \~english Option strike
        /// \~russian Страйк опциона
        /// </summary>
        [HelperName("Strike", Constants.En)]
        [HelperName("Страйк", Constants.Ru)]
        [Description("Страйк опциона")]
        [HelperDescription("Option strike", Constants.En)]
        [HandlerParameter(Default = DefaultStrike)]
        // ReSharper disable once UnusedMember.Global
        public string Strike
        {
            get { return m_strike; }
            set { m_strike = value; }
        }

        /// <summary>
        /// \~english Strike step to extract most important options
        /// \~russian Шаг страйков для выделения главных подсерий
        /// </summary>
        [HelperName("Strike step", Constants.En)]
        [HelperName("Шаг страйков", Constants.Ru)]
        [HandlerParameter(Name = "Strike Step", NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "0", Max = "10000000", Step = "1")]
        [Description("Шаг страйков для выделения главных подсерий")]
        [HelperDescription("Strike step to extract most important options", Constants.En)]
        public double StrikeStep
        {
            get
            {
                return m_strikeStep;
            }
            set
            {
                m_strikeStep = value;
            }
        }
        #endregion Parameters

        // ReSharper disable once UnusedMember.Global
        public IList<double> Execute(IOptionSeries optSer)
        {
            if (optSer == null)
                return Constants.EmptyListDouble;

            // 1. Список страйков
            HashSet<string> serList = StrikeList;
            serList.Clear();

            IOptionStrikePair[] pairs;
            if (Double.IsNaN(m_strikeStep) || (m_strikeStep <= Double.Epsilon))
            {
                pairs = optSer.GetStrikePairs().ToArray();
            }
            else
            {
                // Выделяем страйки, которые нацело делятся на StrikeStep
                pairs = (from p in optSer.GetStrikePairs()
                         let test = m_strikeStep * Math.Round(p.Strike / m_strikeStep)
                         where DoubleUtil.AreClose(p.Strike, test)
                         select p).ToArray();

                // [2015-12-24] Если шаг страйков по ошибке задан совершенно неправильно,
                // то в коллекцию ставим все имеющиеся страйки.
                // Пользователь потом разберется
                if (pairs.Length <= 0)
                    pairs = optSer.GetStrikePairs().ToArray();
            }
            //if (pairs.Length < 2)
            //    return Constants.EmptyListDouble;

            foreach (IOptionStrikePair pair in pairs)
            {
                double k = pair.Strike;
                serList.Add(k.ToString(CultureInfo.InvariantCulture));
            }

            // 2. Локальный кеш страйков
            List<double> historyStrikes = LocalStrikeHistory;

            if (/* m_reset || */ m_context.Runtime.IsFixedBarsCount)
                historyStrikes.Clear();

            // Типа, кеширование?
            int len = Context.BarsCount;
            for (int j = historyStrikes.Count; j < len; j++)
            {
                double k;
                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (Double.TryParse(m_strike, NumberStyles.Any, CultureInfo.InvariantCulture, out k))
                    historyStrikes.Add(k);
                else
                    historyStrikes.Add(Constants.NaN);
            }

            return new ReadOnlyCollection<double>(historyStrikes);
        }

        #region Implementation of ICustomListValues
        public IEnumerable<string> GetValuesForParameter(string paramName)
        {
            if (paramName.Equals("Strike", StringComparison.InvariantCultureIgnoreCase) ||
                paramName.Equals("Страйк", StringComparison.InvariantCultureIgnoreCase))
            {
                HashSet<string> res = StrikeList;
                //res.Sort();
                //var res = from s in series
                //          where s.StartsWith(m_baseSecPrefix, StringComparison.InvariantCultureIgnoreCase)
                //          select s;
                return res;
            }
                
            throw new ArgumentException("Parameter '" + paramName + "' is not supported.", "paramName");
        }
        #endregion
    }
}
