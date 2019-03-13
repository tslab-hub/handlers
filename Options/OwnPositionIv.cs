using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    [HelperName("Own position IV (num)", Language = Constants.En)]
    [HelperName("Волатильность позиции (число)", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(5)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(4, TemplateTypes.DOUBLE, Name = Constants.RiskFreeRate)]
    [OutputType(TemplateTypes.DOUBLE)]
    [Description("Волатильность своей позиции (такая волатильность, чтобы текущая прибыль стала нуль)")]
    [HelperDescription("Own position IV (effective volatility that makes current profit to become zero)", Constants.En)]
    public class OwnPositionIv : BaseContextHandler, IValuesHandlerWithNumber
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

        public double Execute(double price, double time, InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            var res = Execute(price, time, smile, optSer, 0, barNum);
            return res;
        }

        public double Execute(double price, double time, InteractiveSeries smile, IOptionSeries optSer, double riskFreeRatePct, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return Constants.NaN;

            // В оптимизации ничего рисовать не надо
            if (Context.IsOptimization)
                return Constants.NaN;

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
                return Constants.NaN;
            }

            if (!DoubleUtil.IsPositive(futPx))
            {
                // [{0}] Base asset price must be positive value. F:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.FutPxMustBePositive", GetType().Name, futPx);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.NaN;
            }

            if (smile == null)
            {
                string msg = String.Format("[{0}] Argument 'smile' must be filled with InteractiveSeries.", GetType().Name);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.NaN;
            }

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if (oldInfo == null)
            {
                string msg = String.Format("[{0}] Property Tag of object smile must be filled with SmileInfo. Tag:{1}", GetType().Name, smile.Tag);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.NaN;
            }

            if (!oldInfo.IsValidSmileParams)
            {
                string msg = String.Format("[{0}] SmileInfo must have valid smile params. IsValidSmileParams:{1}", GetType().Name, oldInfo.IsValidSmileParams);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, false);
                return Constants.NaN;
            }

            double ivAtm;
            if (!oldInfo.ContinuousFunction.TryGetValue(futPx, out ivAtm))
                return Constants.NaN;

            if (!DoubleUtil.IsPositive(ivAtm))
            {
                // [{0}] ivAtm must be positive value. ivAtm:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.IvAtmMustBePositive", GetType().Name, ivAtm);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.NaN;
            }

            // TODO: Нужно ли писать отдельный код для лаборатории? Чтобы показывать позиции из симуляции?
            // if (!Context.Runtime.IsAgentMode)

            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();
            if (pairs.Length < 2)
            {
                string msg = String.Format("[{0}] optSer must contain few strike pairs. pairs.Length:{1}", GetType().Name, pairs.Length);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Warning, true);
                return Constants.NaN;
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
                return Constants.NaN;

            // 3. Вычисляем эффективную волатильность и заодно разбиваем позицию на длинные и короткие
            //    Но это имеет смысл только если сразу рисовать позу!!!
            double effectiveLongIvAtm, effectiveShortIvAtm;
            bool ok = posMan.TryEstimateEffectiveIv(oldInfo, optSer, lastBarIndex,
                out effectiveLongIvAtm, out effectiveShortIvAtm);
            if (!ok)
                return Constants.NaN;

            double res;
            if (ShowLongPositions)
                res = effectiveLongIvAtm;
            else
                res = effectiveShortIvAtm;

            double disp = FixedValue.ConvertToDisplayUnits(m_valueMode, res);
            m_displayIv.Value = disp;

            SetHandlerInitialized(now, true);

            return res;
        }
    }
}
