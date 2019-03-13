using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Arbitrary function with 3 parameters similar to observed smile
    /// \~russian Произвольная трёхпараметрическая функция качественно похожая на улыбку
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Gauss 'Smile'", Language = Constants.En)]
    [HelperName("Гауссова 'Улыбка'", Language = Constants.Ru)]
    [InputsCount(3)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Произвольная трёхпараметрическая функция качественно похожая на улыбку")]
    [HelperDescription("Arbitrary function with 3 parameters similar to observed smile", Constants.En)]
    public class GaussSmile : BaseSmileDrawing, IValuesHandlerWithNumber
    {
        private const int DefaultOptQty = 10;

        private bool m_generateTails = true;
        private bool m_isVisiblePoints = true;
        private double m_ivAtm = 0.3, m_shift = 0.3, m_depth = 0.5;

        #region Parameters
        /// <summary>
        /// \~english Show nodes
        /// \~russian Показывать узлы на кривой
        /// </summary>
        [HelperName("Show nodes", Constants.En)]
        [HelperName("Показывать узлы", Constants.Ru)]
        [Description("Показывать узлы на кривой")]
        [HelperDescription("Show nodes", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "True")]
        public bool IsVisiblePoints
        {
            get { return m_isVisiblePoints; }
            set { m_isVisiblePoints = value; }
        }

        /// <summary>
        /// \~english Prepare invisible tails to extend working range
        /// \~russian Генерировать невидимые края, чтобы расширить рабочий диапазон
        /// </summary>
        [HelperName("Generate Tails", Constants.En)]
        [HelperName("Формировать края", Constants.Ru)]
        [Description("Генерировать невидимые края, чтобы расширить рабочий диапазон")]
        [HelperDescription("Prepare invisible tails to extend working range", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "true")]
        public bool GenerateTails
        {
            get { return m_generateTails; }
            set { m_generateTails = value; }
        }

        /// <summary>
        /// \~english IV ATM (percents)
        /// \~russian Волатильность на деньгах (проценты)
        /// </summary>
        [HelperName("IV ATM", Constants.En)]
        [HelperName("IV ATM", Constants.Ru)]
        [Description("Волатильность на деньгах (проценты)")]
        [HelperDescription("IV ATM (percents)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "30.0",
            Min = "0.000001", Max = "10000.0", Step = "0.01")]
        public double IvAtmPct
        {
            get { return m_ivAtm * Constants.PctMult; }
            set
            {
                if ((!Double.IsNaN(value)) && (value > 0))
                {
                    m_ivAtm = value / Constants.PctMult;
                }
            }
        }

        /// <summary>
        /// \~english Shift (percents)
        /// \~russian Сдвиг нижней точки (проценты)
        /// </summary>
        [HelperName("Shift", Constants.En)]
        [HelperName("Сдвиг", Constants.Ru)]
        [Description("Сдвиг нижней точки (проценты)")]
        [HelperDescription("Shift (percents)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "30.0",
            Min = "-100000.0", Max = "100000.0", Step = "0.01")]
        public double ShiftPct
        {
            get { return m_shift * Constants.PctMult; }
            set
            {
                if (!Double.IsNaN(value))
                {
                    m_shift = value / Constants.PctMult;
                }
            }
        }

        /// <summary>
        /// \~english Depth (percents)
        /// \~russian Глубина прогиба (проценты)
        /// </summary>
        [HelperName("Depth", Constants.En)]
        [HelperName("Глубина", Constants.Ru)]
        [Description("Глубина прогиба (проценты)")]
        [HelperDescription("Depth (percents)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "50.0",
            Min = "0.0", Max = "10000.0", Step = "0.01")]
        public double DepthPct
        {
            get { return m_depth * Constants.PctMult; }
            set
            {
                if (!Double.IsNaN(value))
                {
                    m_depth = value / Constants.PctMult;
                }
            }
        }
        #endregion Parameters

        public InteractiveSeries Execute(double price, double time, IOptionSeries optSer, int barNum)
        {
            int barsCount = ContextBarsCount;
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            double f = price;
            double dT = time;
            if (Double.IsNaN(dT) || (dT < Double.Epsilon))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (Double.IsNaN(f) || (f < Double.Epsilon))
            {
                // [{0}] Base asset price must be positive value. F:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.FutPxMustBePositive", GetType().Name, f);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            //{
            //    string msg = String.Format("[DEBUG:{0}] ivAtm:{1}; shift:{2}; depth:{3};   F:{4}; dT:{5}; ",
            //        GetType().Name, ivAtm, shift, depth, F, dT);
            //    context.Log(msg, MessageType.Info, true);
            //}

            SmileFunction3 smileFunc = new SmileFunction3(m_ivAtm, m_shift, m_depth, f, dT);

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();

            if (pairs.Length < 2)
            {
                string msg = String.Format("[WARNING:{0}] optSer must contain few strike pairs. pairs.Length:{1}", GetType().Name, pairs.Length);
                m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptySeries;
            }

            double minK = pairs[0].Strike;
            double dK = pairs[1].Strike - pairs[0].Strike;
            // Generate left invisible tail
            if (m_generateTails)
                AppendLeftTail(smileFunc, xs, ys, minK, dK, false);

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            for (int j = 0; j < pairs.Length; j++)
            {
                bool showPoint = true;
                IOptionStrikePair pair = pairs[j];
                // Сверхдалекие страйки игнорируем
                if ((pair.Strike < m_minStrike) || (m_maxStrike < pair.Strike))
                {
                    showPoint = false;
                }

                double k = pair.Strike;
                double sigma = smileFunc.Value(k);
                double vol = sigma;

                InteractivePointActive ip = new InteractivePointActive(k, vol);
                //ip.Color = (optionPxMode == OptionPxMode.Ask) ? Colors.DarkOrange : Colors.DarkCyan;
                //ip.DragableMode = DragableMode.None;
                //ip.Geometry = Geometries.Rect; // (optionPxMode == OptionPxMode.Ask) ? Geometries.Rect : Geometries.Rect;

                // Иначе неправильно выставляются координаты???
                //ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}", k, PctMult * sigma);

                if (showPoint)
                {
                    if (k <= f) // Puts
                        FillNodeInfo(ip, f, dT, pair, StrikeType.Put, OptionPxMode.Mid, sigma, false, m_isVisiblePoints);
                    else // Calls
                        FillNodeInfo(ip, f, dT, pair, StrikeType.Call, OptionPxMode.Mid, sigma, false, m_isVisiblePoints);
                }

                InteractiveObject obj = new InteractiveObject(ip);

                if (showPoint)
                {
                    controlPoints.Add(obj);
                }

                xs.Add(k);
                ys.Add(vol);
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            double maxK = pairs[pairs.Length - 1].Strike;
            // Generate right invisible tail
            if (m_generateTails)
                AppendRightTail(smileFunc, xs, ys, maxK, dK, false);

            // ReSharper disable once UseObjectOrCollectionInitializer
            SmileInfo info = new SmileInfo();
            info.F = f;
            info.dT = dT;
            info.RiskFreeRate = 0;

            info.ContinuousFunction = smileFunc;
            info.ContinuousFunctionD1 = smileFunc.DeriveD1();

            res.Tag = info;

            return res;
        }

        /// <summary>
        /// Достраивает дополнительные точки слева за пределами диапазона страйков, существующих на рынке
        /// </summary>
        /// <param name="smileFunc">улыбка</param>
        /// <param name="xs">таблица исксов</param>
        /// <param name="ys">таблица значений</param>
        /// <param name="maxNonInclusive">максимальная граница (исключая)</param>
        /// <param name="dK">шаг страйков</param>
        /// <param name="returnPct">возвращать ли значения в процентах?</param>
        internal static void AppendLeftTail(IFunction smileFunc,
            List<double> xs, List<double> ys, double maxNonInclusive, double dK, bool returnPct)
        {
            for (double k = dK * Math.Round(0.5 * maxNonInclusive / dK); k < maxNonInclusive; k += dK)
            {
                double sigma;
                if (!smileFunc.TryGetValue(k, out sigma))
                    continue;

                double vol = returnPct ? sigma * Constants.PctMult : sigma;

                if (Double.IsNaN(sigma) || Double.IsInfinity(sigma) || (sigma < Double.Epsilon))
                    continue;

                xs.Add(k);
                ys.Add(vol);
            }
        }

        /// <summary>
        /// Достраивает дополнительные точки справа за пределами диапазона страйков, существующих на рынке
        /// </summary>
        /// <param name="smileFunc">улыбка</param>
        /// <param name="xs">таблица исксов</param>
        /// <param name="ys">таблица значений</param>
        /// <param name="minNonInclusive">минимальная граница (исключая)</param>
        /// <param name="dK">шаг страйков</param>
        /// <param name="returnPct">возвращать ли значения в процентах?</param>
        internal static void AppendRightTail(IFunction smileFunc,
            List<double> xs, List<double> ys, double minNonInclusive, double dK, bool returnPct)
        {
            double max = dK * Math.Round(1.5 * minNonInclusive / dK);
            for (double k = minNonInclusive + dK; k < max; k += dK)
            {
                double sigma;
                if (!smileFunc.TryGetValue(k, out sigma))
                    continue;

                double vol = returnPct ? sigma * Constants.PctMult : sigma;

                if (Double.IsNaN(sigma) || Double.IsInfinity(sigma) || (sigma < Double.Epsilon))
                    continue;

                xs.Add(k);
                ys.Add(vol);
            }
        }

        protected static void FillNodeInfo(InteractivePointActive ip,
            double f, double dT, IOptionStrikePair sInfo,
            StrikeType optionType, OptionPxMode optPxMode,
            double optSigma, bool returnPct, bool isVisiblePoints)
        {
            if (optionType == StrikeType.Any)
                throw new ArgumentException("Option type 'Any' is not supported.", "optionType");

            bool isCall = (optionType == StrikeType.Call);
            double optPx = FinMath.GetOptionPrice(f, sInfo.Strike, dT, optSigma, 0, isCall);

            SmileNodeInfo nodeInfo = new SmileNodeInfo();
            nodeInfo.F = f;
            nodeInfo.dT = dT;
            nodeInfo.Sigma = returnPct ? optSigma * Constants.PctMult : optSigma;
            nodeInfo.OptPx = optPx;
            nodeInfo.Strike = sInfo.Strike;
            nodeInfo.Security = (optionType == StrikeType.Put) ? sInfo.Put.Security : sInfo.Call.Security;
            nodeInfo.PxMode = optPxMode;
            nodeInfo.OptionType = optionType;
            nodeInfo.Pair = sInfo;
            //nodeInfo.ScriptTime = optTime;
            nodeInfo.CalendarTime = DateTime.Now;

            nodeInfo.Symbol = nodeInfo.Security.Symbol;
            nodeInfo.Expired = nodeInfo.Security.SecurityDescription.Expired;

            ip.Tag = nodeInfo;
            ip.Color = AlphaColors.Yellow;
            ip.IsActive = isVisiblePoints;
            ip.Geometry = Geometries.Ellipse;
            ip.DragableMode = DragableMode.None;
            //ip.Value = new System.Windows.Point(sInfo.Strike, nodeInfo.Sigma);
            ip.Tooltip = String.Format("F:{0}; K:{1}; IV:{2:#0.00}%\r\n{3} {4} @ {5}",
                f, sInfo.Strike, optSigma * Constants.PctMult, optionType, optPx, DefaultOptQty);
        }
    }
}
