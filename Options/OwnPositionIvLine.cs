using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Optimization;
using TSLab.Script.Options;
using TSLab.Script.Realtime;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Own position IV (effective volatility that makes current profit to become zero)
    /// \~russian Волатильность своей позиции (такая волатильность, чтобы текущая прибыль стала нуль)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Own position IV (line)", Language = Constants.En)]
    [HelperName("Волатильность позиции (линия)", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(5)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(4, TemplateTypes.DOUBLE, Name = Constants.RiskFreeRate)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Волатильность своей позиции (такая волатильность, чтобы текущая прибыль стала нуль)")]
    [HelperDescription("Own position IV (effective volatility that makes current profit to become zero)", Constants.En)]
    public class OwnPositionIvLine : BaseContextHandler, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0";

        //private string m_tooltipFormat = DefaultTooltipFormat;
        private FixedValueMode m_valueMode = FixedValueMode.AsIs;
        private OptimProperty m_displayIv = new OptimProperty(0, false, double.MinValue, double.MaxValue, 1.0, 0);

        #region Parameters
        /// <summary>
        /// \~english Show long or short positions
        /// \~russian Показывать длинные позиции или короткие?
        /// </summary>
        [HelperName("Show long", Constants.En)]
        [HelperName("Показывать длинные", Constants.Ru)]
        [Description("Показывать длинные позиции или короткие?")]
        [HelperDescription("Show long or short positions", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool ShowLongPositions { get; set; }

        /// <summary>
        /// \~english Display units (hundreds, thousands, as is)
        /// \~russian Единицы отображения (сотни, тысячи, как есть)
        /// </summary>
        [HelperName("Display units", Constants.En)]
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
        /// \~english Effective position volatility (only to display at UI)
        /// \~russian Эффективная волатильность позиции (только для отображения в интерфейсе)
        /// </summary>
        [ReadOnly(true)]
        [HelperName("Display IV", Constants.En)]
        [HelperName("Волатильность", Constants.Ru)]
        [Description("Эффективная волатильность позиции (только для отображения в интерфейсе)")]
        [HelperDescription("Effective position volatility (only to display at UI)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0", IsCalculable = true)]
        public OptimProperty DisplayIv
        {
            get { return m_displayIv; }
            set { m_displayIv = value; }
        }

        ///// <summary>
        ///// \~english Tooltip format (i.e. '0.00', '0.0##' etc)
        ///// \~russian Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.
        ///// </summary>
        //[HelperName("Tooltip Format", Constants.En)]
        //[HelperName("Формат подсказки", Constants.Ru)]
        //[Description("Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.")]
        //[HelperDescription("Tooltip format (i.e. '0.00', '0.0##' etc)", Constants.En)]
        //[HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = DefaultTooltipFormat)]
        //public string TooltipFormat
        //{
        //    get { return m_tooltipFormat; }
        //    set
        //    {
        //        if (!String.IsNullOrWhiteSpace(value))
        //        {
        //            try
        //            {
        //                string yStr = Math.PI.ToString(value);
        //                m_tooltipFormat = value;
        //            }
        //            catch
        //            {
        //                m_context.Log("Tooltip format error. I'll keep old one: " + m_tooltipFormat, MessageType.Warning, true);
        //            }
        //        }
        //    }
        //}
        #endregion Parameters

        public InteractiveSeries Execute(double price, double time, InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            var res = Execute(price, time, smile, optSer, 0, barNum);
            return res;
        }

        public InteractiveSeries Execute(double price, double time, InteractiveSeries smile, IOptionSeries optSer, double riskFreeRatePct, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return Constants.EmptySeries;

            // В оптимизации ничего рисовать не надо
            if (Context.IsOptimization)
                return Constants.EmptySeries;

            int lastBarIndex = optSer.UnderlyingAsset.Bars.Count - 1;
            DateTime now = optSer.UnderlyingAsset.Bars[Math.Min(barNum, lastBarIndex)].Date;
            bool wasInitialized = HandlerInitializedToday(now);

            double futPx = price;
            double dT = time;

            if (!DoubleUtil.IsPositive(dT))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (!DoubleUtil.IsPositive(futPx))
            {
                // [{0}] Base asset price must be positive value. F:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.FutPxMustBePositive", GetType().Name, futPx);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (smile == null)
            {
                string msg = String.Format("[{0}] Argument 'smile' must be filled with InteractiveSeries.", GetType().Name);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.EmptySeries;
            }

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if (oldInfo == null)
            {
                string msg = String.Format("[{0}] Property Tag of object smile must be filled with SmileInfo. Tag:{1}", GetType().Name, smile.Tag);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.EmptySeries;
            }

            if (!oldInfo.IsValidSmileParams)
            {
                string msg = String.Format("[{0}] SmileInfo must have valid smile params. IsValidSmileParams:{1}", GetType().Name, oldInfo.IsValidSmileParams);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.EmptySeries;
            }

            double ivAtm;
            if (!oldInfo.ContinuousFunction.TryGetValue(futPx, out ivAtm))
                return Constants.EmptySeries;

            if (!DoubleUtil.IsPositive(ivAtm))
            {
                // [{0}] ivAtm must be positive value. ivAtm:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.IvAtmMustBePositive", GetType().Name, ivAtm);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            // TODO: Нужно ли писать отдельный код для лаборатории? Чтобы показывать позиции из симуляции?
            // if (!Context.Runtime.IsAgentMode)

            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            if (pairs.Length < 2)
            {
                string msg = String.Format("[{0}] optSer must contain few strike pairs. pairs.Length:{1}", GetType().Name, pairs.Length);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptySeries;
            }

            double futStep = optSer.UnderlyingAsset.Tick;
            PositionsManager posMan = PositionsManager.GetManager(m_context);
            // Вытаскиваем ВСЕ позиции фьючерса
            ReadOnlyCollection<IPosition> basePositions = posMan.GetClosedOrActiveForBar(optSer.UnderlyingAsset);
            // Вытаскиваем ВСЕ позиции опционов
            var optPositions = SingleSeriesProfile.GetAllOptionPositions(posMan, pairs);

            // 1. Если в позиции вообще нет опционов -- сразу выходим. Эффективную волатильность построить нельзя.
            int posAmount = (from t in optPositions select (t.Item1.Count + t.Item2.Count)).Sum();
            if (posAmount <= 0)
                return Constants.EmptySeries;

            // 2. TODO: посчитать позиции без учета синтетических фьючерсов. Если позиция только из синтетики -- выходим.

            // 3. Вычисляем эффективную волатильность и заодно разбиваем позицию на длинные и короткие
            //    Но это имеет смысл только если сразу рисовать позу!!!
            double effectiveLongIvAtm, effectiveShortIvAtm;
            Tuple<ReadOnlyCollection<IPosition>, ReadOnlyCollection<IPosition>>[] longPositions;
            Tuple<ReadOnlyCollection<IPosition>, ReadOnlyCollection<IPosition>>[] shortPositions;
            bool ok = posMan.TryEstimateEffectiveIv(oldInfo, optSer, lastBarIndex,
                out effectiveLongIvAtm, out effectiveShortIvAtm, out longPositions, out shortPositions);
            if (!ok)
            {
                // Мы не смогли завершить алгоритм, но получили какую-то оценку волатильностей. Нарисуем ее?
                if ((!DoubleUtil.IsPositive(effectiveLongIvAtm)) ||
                    (!DoubleUtil.IsPositive(effectiveShortIvAtm)))
                {
                    return Constants.EmptySeries;
                }
            }

            Contract.Assert(longPositions != null, "longPositions==null ???");
            Contract.Assert(shortPositions != null, "shortPositions==null ???");

            double actualIv = ShowLongPositions ? effectiveLongIvAtm : effectiveShortIvAtm;
            double displayValue = FixedValue.ConvertToDisplayUnits(m_valueMode, actualIv);
            m_displayIv.Value = displayValue;

            Contract.Assert(DoubleUtil.IsPositive(actualIv), $"Это вообще что-то странное. Почему плохой айви? actualIv:{actualIv}");

            // Это вообще что-то странное. Как так?
            if (!DoubleUtil.IsPositive(actualIv))
                return Constants.EmptySeries;

            // 5. Подготавливаю улыбку (достаточно функции, без обвязки)
            var lowSmileFunc = new SmileFunctionExtended(
                SmileFunction5.TemplateFuncRiz4Nov1,
                actualIv, oldInfo.SkewAtm, oldInfo.Shape, futPx, dT);

            // 7. Подготавливаем графическое отображение позиции. Причем нам даже сплайн не нужен.
            var actualPositions = ShowLongPositions ? longPositions : shortPositions;
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            for (int j = 0; j < pairs.Length; j++)
            {
                var pair = pairs[j];
                var tuple = actualPositions[j];

                // На данном страйке позиций нет? Идем дальше.
                if ((tuple.Item1.Count <= 0) && (tuple.Item2.Count <= 0))
                    continue;

                double sigma;
                if ((!lowSmileFunc.TryGetValue(pair.Strike, out sigma)) ||
                    (!DoubleUtil.IsPositive(sigma)))
                {
                    // TODO: Это очень странно. Вывести в лог? Проигнорировать страйк?
                    sigma = actualIv;
                }

                var putPositions = tuple.Item1;
                var callPositions = tuple.Item2;
                double putQty = PositionsManager.GetTotalQty(putPositions);
                double callQty = PositionsManager.GetTotalQty(callPositions);

                int decimals = optSer.UnderlyingAsset.Decimals + 1;
                double putPx = FinMath.GetOptionPrice(futPx, pair.Strike, dT, sigma, riskFreeRatePct, false);
                double callPx = FinMath.GetOptionPrice(futPx, pair.Strike, dT, sigma, riskFreeRatePct, true);
                string putPxStr = putPx.ToString("N" + decimals, CultureInfo.InvariantCulture);
                string callPxStr = callPx.ToString("N" + decimals, CultureInfo.InvariantCulture);

                // ReSharper disable once UseObjectOrCollectionInitializer
                InteractivePointActive ip = new InteractivePointActive();
                // TODO: вывести в тултип дополнительные подробности о составе позиции на этом страйке
                ip.Tooltip = String.Format(CultureInfo.InvariantCulture,
                    " K: {0}; IV: {1:#0.00}%\r\n PutQty: {2}; CallQty: {3}\r\n PutPx: {4}; CallPx: {5}\r\n Total: {6}",
                    pair.Strike, sigma * Constants.PctMult, putQty, callQty, putPxStr, callPxStr, putQty + callQty);
                ip.Value = new Point(pair.Strike, sigma);

                controlPoints.Add(new InteractiveObject(ip));
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            SetHandlerInitialized(now, true);

            return res;
        }
    }
}
