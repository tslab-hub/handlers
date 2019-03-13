using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;

using TSLab.Script.Optimization;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Central strike of an Option Series
    /// \~russian Центральный страйк серии
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsIndicators)]
    [HelperName("Central Strike", Language = Constants.En)]
    [HelperName("Центральный страйк", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Центральный страйк серии")]
    [HelperDescription("Central strike of an Option Series", Constants.En)]
    public class CentralStrike : BaseContextHandler, IStreamHandler
    {
        private const string DefaultPx = "120000";
        private const string DateFormat = "yyyy-MM-dd";

        private int m_shiftStrike = 0;
        private double m_switchRatio = 0.62;
        private double m_strikeStep = 0;
        private FixedValueMode m_valueMode = FixedValueMode.AsIs;
        private OptimProperty m_displayPrice = new OptimProperty(
            Double.Parse(DefaultPx, CultureInfo.InvariantCulture), false, Double.MinValue, Double.MaxValue, 1.0, 4);

        /// <summary>
        /// Коллекция с единственным нулевым значением.
        /// Используется при вычислении шага между страйками в аварийной ситуации.
        /// </summary>
        private static readonly ReadOnlyCollection<double> s_singleZero = new ReadOnlyCollection<double>(new[] { 0.0 });

        /// <summary>
        /// Локальный кеш центральных страйков
        /// </summary>
        private Dictionary<DateTime, double> LocalHistory
        {
            get
            {
                Dictionary<DateTime, double> cks = Context.LoadObject(VariableId + "CentralK") as Dictionary<DateTime, double>;
                if (cks == null)
                {
                    cks = new Dictionary<DateTime, double>();
                    Context.StoreObject(VariableId + "CentralK", cks);
                }

                return cks;
            }
        }

        /// <summary>Мультипликаторы для определения рабочих страйков. Выбраны те шаги, которые чаще всего используются на практике.</summary>
        private static readonly ReadOnlyCollection<int> s_stepMultipliers = new ReadOnlyCollection<int>(new[] { 0, 1, 2, 4, 10 });

        #region Parameters
        /// <summary>
        /// \~english Shift central strike (number of strikes)
        /// \~russian Искуственный сдвиг центрального страйка (в штуках страйков)
        /// </summary>
        [HelperName("Shift strike", Constants.En)]
        [HelperName("Сдвиг страйка", Constants.Ru)]
        [HandlerParameter(Name = "Shift Strike", NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "-10000000", Max = "10000000", Step = "1")]
        [Description("Искуственный сдвиг центрального страйка (в штуках страйков)")]
        [HelperDescription("Shift central strike (number of strikes)", Constants.En)]
        public int ShiftStrike
        {
            get
            {
                return m_shiftStrike;
            }
            set
            {
                m_shiftStrike = value;
            }
        }

        /// <summary>
        /// \~english Market should pass this percent of distance to next strike to switch central strike
        /// \~russian Процент движения к следующему страйку для переключения
        /// </summary>
        [HelperName("Switch Ratio Pct", Constants.En)]
        [HelperName("Процент для переключения", Constants.Ru)]
        [HandlerParameter(Name = "Switch Ratio Pct", NotOptimized = false, IsVisibleInBlock = true,
            Default = "62", Min = "50", Max = "100", Step = "1")]
        [Description("Процент движения к следующему страйку для переключения")]
        [HelperDescription("Market should pass this percent of distance to next strike to switch central strike", Constants.En)]
        public double SwitchRatioPct
        {
            get
            {
                return m_switchRatio * Constants.PctMult;
            }
            set
            {
                double t = Math.Max(50, value);
                t = Math.Min(100, t);
                m_switchRatio = t / Constants.PctMult;
            }
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

        /// <summary>
        /// \~english Display units (hundreds, thousands, as is)
        /// \~russian Единицы отображения (сотни, тысячи, как есть)
        /// </summary>
        [HelperName("Display Units", Constants.En)]
        [HelperName("Единицы отображения", Constants.Ru)]
        [Description("Единицы отображения (сотни, тысячи, как есть)")]
        [HelperDescription("Display units (hundreds, thousands, as is)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "AsIs")]
        public FixedValueMode DisplayUnits
        {
            get { return m_valueMode; }
            set { m_valueMode = value; }
        }

        /// <summary>
        /// \~english Base asset price (only to display at UI)
        /// \~russian Цена БА (только для отображения в интерфейсе)
        /// </summary>
        [ReadOnly(true)]
        [HelperName("Display Price", Constants.En)]
        [HelperName("Цена для интерфейса", Constants.Ru)]
        [Description("Цена БА (только для отображения в интерфейсе)")]
        [HelperDescription("Base asset price (only to display at UI)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultPx, IsCalculable = true)]
        public OptimProperty DisplayPrice
        {
            get { return m_displayPrice; }
            set { m_displayPrice = value; }
        }
        #endregion Parameters

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES
        /// </summary>
        public IList<double> Execute(IOptionSeries optSer)
        {
            if (optSer == null)
                return Constants.EmptyListDouble;

            ISecurity sec = optSer.UnderlyingAsset;
            int len = sec.Bars.Count;
            if (len <= 0)
                return Constants.EmptyListDouble;

            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            if ((!Double.IsNaN(m_strikeStep)) && (m_strikeStep > Double.Epsilon))
            {
                // Выделяем страйки, которые нацело делятся на StrikeStep
                var tmp = (from p in pairs
                           let test = m_strikeStep * Math.Round(p.Strike / m_strikeStep)
                           where DoubleUtil.AreClose(p.Strike, test)
                           select p).ToArray();
                if (tmp.Length > 1)
                {
                    // Нормальная ситуация
                    pairs = tmp;
                }
                // [02-06-2016] PROD-2812 - Защита от ошибок при указании шага страйков
                // В противном случае буду показывать все страйки (уже лежат в pairs).
                // Это хотя бы позволит Пользователю продолжить работу.
            }
            if (pairs.Length < 2)
                return Constants.EmptyListDouble;

            double[] res = Context?.GetArray<double>(len) ?? new double[len];
            var history = LocalHistory;
            double prevK = Double.NaN;
            for (int m = 0; m < len; m++)
            {
                DateTime now = sec.Bars[m].Date;

                double ck;
                if (history.TryGetValue(now, out ck))
                {
                    prevK = ck;
                    res[m] = prevK;
                }
                else
                {
                    double futPx = sec.Bars[m].Close;

                    // 0. Валидация диапазона
                    IOptionStrikePair left = pairs[0];
                    IOptionStrikePair right = pairs[pairs.Length - 1];
                    if ((futPx <= left.Strike + Double.Epsilon) || (right.Strike - Double.Epsilon <= futPx))
                    {
                        res[m] = Double.IsNaN(prevK) ? Constants.NaN : prevK;
                        continue;
                    }

                    // 1. Пробуем проверить середину серии в качестве кандидата на левую границу
                    int li = pairs.Length / 2;
                    if (pairs[li].Strike < futPx)
                        left = pairs[li];
                    else
                        li = 0;

                    // 2. Ищем правый страйк
                    double ratio = Double.NaN;
                    int leftIndex = Int32.MinValue, rightIndex = Int32.MaxValue;
                    for (int j = li; j < pairs.Length - 1; j++)
                    {
                        if ((pairs[j].Strike - Double.Epsilon <= futPx) && (futPx < pairs[j + 1].Strike))
                        {
                            left = pairs[j];
                            right = pairs[j + 1];

                            leftIndex = Math.Max(0, j + m_shiftStrike);
                            leftIndex = Math.Min(leftIndex, pairs.Length - 2);
                            rightIndex = Math.Max(1, j + 1 + m_shiftStrike);
                            rightIndex = Math.Min(rightIndex, pairs.Length - 1);

                            ratio = (futPx - left.Strike) / (right.Strike - left.Strike);
                            break;
                        }
                    }

                    if (ratio <= (1.0 - m_switchRatio))
                    {
                        prevK = pairs[leftIndex].Strike; // left.Strike;
                        res[m] = prevK;
                        history[now] = prevK;
                    }
                    else if (m_switchRatio <= ratio)
                    {
                        prevK = pairs[rightIndex].Strike; // right.Strike;
                        res[m] = prevK;
                        history[now] = prevK;
                    }
                    else
                    {
                        if (Double.IsNaN(prevK) || (prevK <= 0))
                        {
                            try
                            {
                                prevK = pairs[rightIndex].Strike; // right.Strike;
                            }
                            catch (IndexOutOfRangeException ioex)
                            {
                                string msg = String.Format(CultureInfo.InvariantCulture,
                                    "{0} when trying to get StrikePair. rightIndex:{1}; pairs.Length:{2}; leftIndex:{3}; li:{4}; ratio:{5}; prevK:{6}; futPx:{7}; ticker:{8}",
                                    ioex.GetType().FullName, rightIndex, pairs.Length, leftIndex, li, ratio, prevK, futPx, optSer.UnderlyingAsset.Symbol);
                                m_context.Log(msg, MessageType.Error, true);

                                // Здесь ПОКА оставляю выброс исключения, чтобы ситуация с самозакрытием окон агента воспроизводилась.
                                throw;
                            }
                            res[m] = prevK;
                            history[now] = prevK;
                        }
                        else
                        {
                            res[m] = prevK;
                            // Надо ли здесь обновить history или это бессмысленно и расточительно???
                        }
                    }
                }

                //// "For some strange reason I didn't fill value at index m:" + m);
                //if (DoubleUtil.IsZero(res[m]))
                //    m_context.Log("[DEBUG:CentralStrike] For some strange reason I didn't fill value at index m:" + m, MessageType.Error, true);
            }

            double displayValue = FixedValue.ConvertToDisplayUnits(m_valueMode, res[len - 1]);
            m_displayPrice.Value = displayValue;

            return res;
        }

        /// <summary>
        /// Выбрать допустимые шаги рабочих страйков на основании расстояния между ними
        /// </summary>
        /// <param name="dK">шаг между страйками</param>
        /// <param name="context"/>
        /// <returns>отсортированный список возможных рабочих расстояний</returns>
        // ReSharper disable once MemberCanBePrivate.Global        
        public static ReadOnlyCollection<double> GetStrikeSteps(double dK)
        {
            Contract.Assert(DoubleUtil.IsPositive(dK), "Что делать с отрицательным шагом стрйков? dK: " + dK);
            Contract.Assert(s_stepMultipliers.Count > 0, "Мало мультипликаторов? s_stepMultipliers.Count: " + s_stepMultipliers.Count);

            // Если беда, просто возвращаем 0
            if ((!DoubleUtil.IsPositive(dK)) || (s_stepMultipliers.Count <= 0))
            {
                return s_singleZero;
            }

            double[] steps = new double[s_stepMultipliers.Count];
            for (int j = 0; j < s_stepMultipliers.Count; j++)
            {
                steps[j] = dK * s_stepMultipliers[j];
                // Выполняю округление до 9-го знака, чтобы красивее выглядели шаги страйков на EDZ7
                steps[j] = Math.Round(steps[j], 9);
            }

            ReadOnlyCollection<double> res = new ReadOnlyCollection<double>(steps);
            return res;
        }
    }
}
