using System;
using System.ComponentModel;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Fixed value
    /// \~russian Фиксированная величина
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsIndicators)]
    [HelperName("Fixed Value", Language = Constants.En)]
    [HelperName("Фиксированная величина", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY | TemplateTypes.OPTION_SERIES | TemplateTypes.OPTION, Name = Constants.AnyOption)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Фиксированная величина")]
    [HelperDescription("Fixed value", Constants.En)]
    public class FixedValue : BaseContextHandler, IValuesHandlerWithNumber
    {
        /// <summary>Внутреннее отображение числовой величины</summary>
        private double m_val = 120000;
        /// <summary>Нижняя допустимая граница на ВНУТРЕННЕЕ представление величины</summary>
        private double m_minVal = 1e-6;
        /// <summary>Единицы отображения (сотни, тысячи, как есть)</summary>
        private FixedValueMode m_valueMode = FixedValueMode.AsIs;

        #region Parameters
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
        /// \~english Minimum for internal representation
        /// \~russian Нижняя допустимая граница на ВНУТРЕННЕЕ представление величины
        /// </summary>
        [HelperName("Minimum", Constants.En)]
        [HelperName("Минимум", Constants.Ru)]
        [Description("Нижняя допустимая граница на ВНУТРЕННЕЕ представление величины")]
        [HelperDescription("Minimum for internal representation", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "1e-6", Min = "-10000000", Max = "10000000", Step = "1")]
        public double MinValue
        {
            get { return m_minVal; }
            set { m_minVal = value; }
        }

        /// <summary>
        /// \~english Constant value (always above the limit 'Minimum')
        /// \~russian Фиксированная величина (не меньше ограничения 'Минимум')
        /// </summary>
        [HelperName("Value", Constants.En)]
        [HelperName("Значение", Constants.Ru)]
        [Description("Фиксированная величина (не меньше ограничения 'Минимум')")]
        [HelperDescription("Constant value (always above the limit 'Minimum')", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "120000", Min = "-10000000", Max = "10000000", Step = "1")]
        public double Value
        {
            get
            {
                double res = ConvertToDisplayUnits(m_valueMode, m_val);
                return res;
            }
            set
            {
                double t = ConvertFromDisplayUnits(m_valueMode, value);
                m_val = Math.Max(t, m_minVal);
            }
        }

        /// <summary>
        /// Преобразовать абстрактное число к требуемым единицам измерения (например, домножить на миллион и показывать в миллионных долях)
        /// </summary>
        /// <param name="valueMode">режим преобразования числа</param>
        /// <param name="rawVal">сырое числовое значение</param>
        /// <returns>число, преобразованное к нужным единицам (например, к процентам или долям года)</returns>
        public static double ConvertToDisplayUnits(FixedValueMode valueMode, double rawVal)
        {
            switch (valueMode)
            {
                case FixedValueMode.AsIs:
                    return rawVal;

                case FixedValueMode.Percent:
                    return rawVal * Constants.PctMult;

                case FixedValueMode.Promille:
                    return rawVal * Constants.PromilleMult;

                case FixedValueMode.Thousand:
                    return rawVal / Constants.PromilleMult;

                case FixedValueMode.YearsAsDays:
                    {
                        double res = rawVal * OptionUtils.MinutesInYear / OptionUtils.MinutesInDay;
                        return res;
                    }

                case FixedValueMode.DaysAsYears:
                    {
                        double res = rawVal * OptionUtils.MinutesInDay / OptionUtils.MinutesInYear;
                        return res;
                    }

                case FixedValueMode.PartsPerMillion:
                    return rawVal * Constants.MillionMult;

                default:
                    throw new NotImplementedException("Not implemented. valueMode: " + valueMode);
            }
        }

        /// <summary>
        /// Преобразовать размерное число к внутренним сырым единицам (например, разделить проценты на 100 и получить безразмерную долю)
        /// </summary>
        /// <param name="valueMode">режим преобразования числа</param>
        /// <param name="rawVal">числовое значение с размерностью</param>
        /// <returns>число, преобразованное к сырым единицам (например, из 'дней года' перевести в 'доли года' или из процентов перевести в доли)</returns>
        public static double ConvertFromDisplayUnits(FixedValueMode valueMode, double rawVal)
        {
            switch (valueMode)
            {
                case FixedValueMode.AsIs:
                    return rawVal;

                case FixedValueMode.Percent:
                    return rawVal / Constants.PctMult;

                case FixedValueMode.Promille:
                    return rawVal / Constants.PromilleMult;

                case FixedValueMode.Thousand:
                    return rawVal * Constants.PromilleMult;

                case FixedValueMode.YearsAsDays:
                    {
                        double res = rawVal / OptionUtils.MinutesInYear * OptionUtils.MinutesInDay;
                        return res;
                    }

                case FixedValueMode.DaysAsYears:
                    {
                        double res = rawVal / OptionUtils.MinutesInDay * OptionUtils.MinutesInYear;
                        return res;
                    }

                case FixedValueMode.PartsPerMillion:
                    return rawVal / Constants.MillionMult;

                default:
                    throw new NotImplementedException("Not implemented. valueMode: " + valueMode);
            }
        }
        #endregion Parameters

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION, чтобы подключаться к источнику-серии
        /// </summary>
        public double Execute(IOption opt, int barNumber)
        {
            return Execute(opt.UnderlyingAsset, barNumber);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.OPTION_SERIES, чтобы подключаться к источнику-серии
        /// </summary>
        public double Execute(IOptionSeries optSer, int barNumber)
        {
            return Execute(optSer.UnderlyingAsset, barNumber);
        }

        /// <summary>
        /// Метод под флаг TemplateTypes.SECURITY, чтобы подключаться к источнику-серии
        /// </summary>
        public double Execute(ISecurity sec, int barNumber)
        {
            // А на возврат из кубика числа пойдут без модификации.
            return m_val;
        }
    }
}
