using System.Collections.Generic;
using System.ComponentModel;

using TSLab.DataSource;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Compress", Language = Constants.En)]
    [HelperName("Сжать", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Сжатие текущего временного диапазона (минуты - в минуты, дни - в дни) баров в более длительный. Сжимать можно только в кратные диапазоны. Например, 15 минут можно сжать в 15, 30, 45, 60 минут и т.д.")]
    [HelperDescription("Compresses current bars time range (minutes into minutes, days into days) into a longer one. Only divisible ranges can be used. For example, 15 minutes can be compressed into 15, 30, 45, 60 minutes and so on.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD-BarsHeld.xml", "Модифицированный пример по индикатору MACD", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD-BarsHeld.xml", "Modified example of MACD", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class Compress : ICompressHandler
    {
        /// <summary>
        /// \~english Target interval in units of security timeframe. I.e. Interval=5 for timeframe H1 results in bars in timeframe H5. But Interval=4 for timeframe M2 results in timeframe M4.
        /// \~russian Целевой таймфрейм в единицах таймфрейма инструмента. Например, Интервал=5 для таймфрейма H1 даст бары в таймфрейме H5. Но Интервал=4 для таймфрейма M2 даст бары в таймфрейме M4.
        /// </summary>
        [HelperName("Interval", Constants.En)]
        [HelperName("Интервал", Constants.Ru)]
        [Description("Целевой таймфрейм в единицах таймфрейма инструмента. Например, Интервал=5 для таймфрейма H1 даст бары в таймфрейме H5. Но Интервал=4 для таймфрейма M2 даст бары в таймфрейме M4.")]
        [HelperDescription("Target interval in units of security timeframe. I.e. Interval=5 for timeframe H1 results in bars in timeframe H5. But Interval=4 for timeframe M2 results in timeframe M4.", Constants.En)]
        [HandlerParameter(true, "5", Min = "5", Max = "60", Step = "5")]
        public int Interval { get; set; }

        /// <summary>
        /// \~english Shift
        /// \~russian Сдвиг
        /// </summary>
        [HelperName("Shift", Constants.En)]
        [HelperName("Сдвиг", Constants.Ru)]
        [Description("Сдвиг")]
        [HelperDescription("Shift", Constants.En)]
        [HandlerParameter(true, "0", Min = "0", Max = "60", Step = "5")]
        public int Shift { get; set; }

        public ISecurity Execute(ISecurity source)
        {
            var interval = new Interval(Interval, source.IntervalBase);
            switch (interval.Base)
            {
                case DataIntervals.VOLUME:
                    return source.CompressToVolume(interval);
                case DataIntervals.PRICERANGE:
                    return source.CompressToPriceRange(interval);
                default:
                    return source.CompressTo(interval, Shift);
            }
        }
    }

    //[HandlerName("Compress (Advanced)")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Compress (Advanced)", Language = Constants.En)]
    [HelperName("Сжать (Расшир)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Сжатие текущего временного диапазона (минуты - в минуты, дни - в дни) баров в более длительный. Сжимать можно только в кратные диапазоны. Например, 15 минут можно сжать в 15, 30, 45, 60 минут и т.д.")]
    [HelperDescription("Compresses a current bars time range (minutes into minutes, days into days) of bars into a longer one. Only divisible ranges can be used. For example, 15 minutes can be compressed into 15, 30, 45, 60 minutes and so on.", Constants.En)]
    public sealed class CompressAdvanced : ICompressHandler
    {
        /// <summary>
        /// \~english Target timeframe base (DAYS, MINUTE, SECONDS, TICK, VOLUME, PRICERANGE).
        /// \~russian База итогового таймфрейма (Дни, Минуты, Секунды, Тики, Объём, Шаги цены).
        /// </summary>
        [HelperName("Interval base", Constants.En)]
        [HelperName("База интервала", Constants.Ru)]
        [Description("База итогового таймфрейма (Дни, Минуты, Секунды, Тики, Объём, Шаги цены).")]
        [HelperDescription("Target timeframe base (DAYS, MINUTE, SECONDS, TICK, VOLUME, PRICERANGE).", Constants.En)]
        [HandlerParameter(Default = "MINUTE", NotOptimized = true)]
        public DataIntervals IntervalBase { get; set; }

        /// <summary>
        /// \~english Target interval in units of parameter 'Interval base'.
        /// \~russian Целевой таймфрейм в единицах параметра 'База интервала'.
        /// </summary>
        [HelperName("Interval", Constants.En)]
        [HelperName("Интервал", Constants.Ru)]
        [Description("Целевой таймфрейм в единицах параметра 'База интервала'.")]
        [HelperDescription("Target interval in units of parameter 'Interval base'.", Constants.En)]
        [HandlerParameter(true, "5", Min = "5", Max = "60", Step = "5")]
        public int Interval { get; set; }

        /// <summary>
        /// \~english Shift
        /// \~russian Сдвиг
        /// </summary>
        [HelperName("Shift", Constants.En)]
        [HelperName("Сдвиг", Constants.Ru)]
        [Description("Сдвиг")]
        [HelperDescription("Shift", Constants.En)]
        [HandlerParameter(true, "0", Min = "0", Max = "60", Step = "5")]
        public int Shift { get; set; }

        [HandlerParameter(true, "1440", Min = "60", Max = "10080", Step = "60")]
        public int Adjustment { get; set; }

        [HandlerParameter(true, "600", Min = "0", Max = "1440", Step = "60")]
        public int AdjShift { get; set; }

        public ISecurity Execute(ISecurity source)
        {
            var interval = new Interval(Interval, IntervalBase);
            switch (interval.Base)
            {
                case DataIntervals.VOLUME:
                    return source.CompressToVolume(interval);
                case DataIntervals.PRICERANGE:
                    return source.CompressToPriceRange(interval);
                default:
                    return source.CompressTo(interval, Shift, Adjustment, AdjShift);
            }
        }
    }

    //[HandlerName("Compress to Seconds")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Compress into seconds", Language = Constants.En)]
    [HelperName("Сжать в секунды", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.SECURITY)]
    [Description("Сжатие текущего временного диапазона баров в более длительный. Сжимать можно только в кратные диапазоны. Например, 1 минуту можно сжать в 60, 120, 180, 240 секунд и т.д.")]
    [HelperDescription("Compresses current time range of bars into a longer one. Only divisible ranges can be used. For example, 1 minute can be compressed into 60, 120, 180, 240 seconds and so on.", Constants.En)]
    public sealed class CompressToSeconds : ICompressHandler
    {
        #region Comments and attributes
        /// <summary>
        /// \~english Target interval in seconds. I.e. Interval=16 results in timeframe S16. The source must be compatible with a target timeframe.
        /// \~russian Целевой таймфрейм в секундах. Например, Интервал=16 даст бары в таймфрейме S16. Но источник должен быть совместим с требуемым таймфреймом.
        /// </summary>
        [HelperName("Interval", Constants.En)]
        [HelperName("Интервал", Constants.Ru)]
        [Description("Целевой таймфрейм в секундах. Например, Интервал=16 даст бары в таймфрейме S16. Но источник должен быть совместим с требуемым таймфреймом.")]
        [HelperDescription("Target interval in seconds. I.e. Interval=16 results in timeframe S16. The source must be compatible with a target timeframe.", Constants.En)]
        #endregion Comments and attributes
        [HandlerParameter(true, "5", Min = "5", Max = "60", Step = "5")]
        public int Interval { get; set; }

        public ISecurity Execute(ISecurity source)
        {
            return source.CompressTo(new Interval(Interval, DataIntervals.SECONDS));
        }
    }

    //[HandlerName("Decompress")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Decompress", Language = Constants.En)]
    [HelperName("Разжать", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(2)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [Input(1, TemplateTypes.DOUBLE)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Разжать посчитанные числовые данные в сжатом диапазоне, для последующего их использования с данными в оригинальном диапазоне. Блок 'разжать' необходимо соединить с разжимаемым блоком, а также с блоком 'сжать', соответствующим разжимаемому блоку. " +
        "[br]В программе существует три метода декомпрессии данных. " +
        "[br]Внимание! Метод № 2 не применим для исторического тестирования, поскольку приводит к заглядыванию в будущее и приводит к существенному искажению результатов.")]
    [HelperDescription("Decompresses calculated numeric data to be used further with other data in authentic range. The Decompress block should be connected to a block being compressed and to the Compress block linked to a block being decompressed. " +
        "[br]TSLab has three decompression methods. " +
        "[br]Attention! The 2nd method cannot be applied to offline data testing, as it results in looking into the future and, as a result, corrupts results significantly.", Constants.En)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD-BarsHeld.xml", "Модифицированный пример по индикатору MACD", Constants.Ru)]
    [HelperLink(@"http://www.tslab.ru/files/script/MACD-BarsHeld.xml", "Modified example of MACD", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class Decompres : IDecompress, IBarDoubleAccumHandler
    {
        /// <summary>
        /// \~english Decompress algorythm
        /// \~russian Метод распаковки свечей
        /// </summary>
        [HelperName("Decompress algorythm", Constants.En)]
        [HelperName("Способ распаковки свечей", Constants.Ru)]
        [Description("Метод распаковки свечей")]
        [HelperDescription("Decompress algorythm", Constants.En)]
        [HandlerParameter(true, "Default", NotOptimized = true)]
        public DecompressMethodWithDef Method { get; set; }

        public IList<double> Execute(ISecurity security, IList<double> source)
        {
            return security.Decompress(source, Method);
        }
    }

    //[HandlerName("DecompressBool")]
    [HandlerCategory(HandlerCategories.TradeMath)]
    [HelperName("Decompress boolean", Language = Constants.En)]
    [HelperName("Разжать логическое", Language = Constants.Ru)]
    #region Атрибуты с описанием и ссылками
    [InputsCount(2)]
    [Input(0, TemplateTypes.SECURITY, Name = Constants.SecuritySource)]
    [Input(1, TemplateTypes.BOOL)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.BOOL)]
    [Description("Разжать посчитанные в сжатом диапазоне логические данные для последующего их использования с данными в оригинальном диапазоне. Блок 'Разжать' необходимо соединить с разжимаемым блоком, а также с блоком 'Сжать', соответствующим разжимаемому блоку.")]
    [HelperDescription("Decompresses Boolean data which have been calculated in compressed range to be used further with other data in authentic range. The Decompress block should be connected to a block being decompressed and with the Compress block, linked to a block being decompressed.", Constants.En)]
    #endregion Атрибуты с описанием и ссылками
    public sealed class DecompresBool : IDecompress, IBarBoolAccumHandler
    {
        /// <summary>
        /// \~english Decompress algorythm
        /// \~russian Метод распаковки свечей
        /// </summary>
        [HelperName("Decompress algorythm", Constants.En)]
        [HelperName("Способ распаковки свечей", Constants.Ru)]
        [Description("Метод распаковки свечей")]
        [HelperDescription("Decompress algorythm", Constants.En)]
        [HandlerParameter(true, "Default", NotOptimized = true)]
        public DecompressMethodWithDef Method { get; set; }

        public IList<bool> Execute(ISecurity security, IList<bool> source)
        {
            return security.Decompress(source, Method);
        }
    }
}
