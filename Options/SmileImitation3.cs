using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using TSLab.Script.CanvasPane;
using TSLab.Script.Optimization;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Smile imitation using arbitrary function with 3 parameters
    /// \~russian Имитация улыбки произвольной трёхпараметрической функцией
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Smile Imitation v3", Language = Constants.En)]
    [HelperName("Имитация улыбки v3", Language = Constants.Ru)]
    [InputsCount(5)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(4, TemplateTypes.DOUBLE, Name = Constants.RiskFreeRate)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Имитация улыбки произвольной трёхпараметрической функцией")]
    [HelperDescription("Smile imitation using arbitrary function with 3 parameters", Constants.En)]
    public class SmileImitation3 : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private bool m_setIvByHands = false;
        private bool m_generateTails = true;
        private bool m_isVisiblePoints = true;
        private bool m_setSlopeByHands = false;

        private double m_shift = 0.3;
        private OptimProperty m_ivAtmPct = new OptimProperty(30, false, double.MinValue, double.MaxValue, 1.0, 3);
        private OptimProperty m_depthPct = new OptimProperty(50, false, double.MinValue, double.MaxValue, 1.0, 3);
        private OptimProperty m_internalSlopePct = new OptimProperty(-10, false, double.MinValue, double.MaxValue, 1.0, 3);

        #region Parameters
        /// <summary>
        /// \~english Set IV manually
        /// \~russian Уровень IV ATM задаётся руками (в %)
        /// </summary>
        [HelperName("Set IV", Constants.En)]
        [HelperName("Задать IV", Constants.Ru)]
        [Description("Уровень IV ATM задаётся руками (в %)")]
        [HelperDescription("Set IV manually", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool SetIvByHands
        {
            get { return m_setIvByHands; }
            set { m_setIvByHands = value; }
        }

        /// <summary>
        /// \~english Set skew manually
        /// \~russian Уровень наклона задаётся принудительно
        /// </summary>
        [HelperName("Set Skew Manually", Constants.En)]
        [HelperName("Наклон принудительно", Constants.Ru)]
        [Description("Уровень наклона задаётся принудительно")]
        [HelperDescription("Set skew manually", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool SetSlopeByHands
        {
            get { return m_setSlopeByHands; }
            set { m_setSlopeByHands = value; }
        }

        /// <summary>
        /// \~english Generate invisible tails
        /// \~russian Генерировать невидимые края
        /// </summary>
        [HelperName("Generate Tails", Constants.En)]
        [HelperName("Формировать края", Constants.Ru)]
        [Description("Генерировать невидимые края")]
        [HelperDescription("Generate invisible tails", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "true")]
        public bool GenerateTails
        {
            get { return m_generateTails; }
            set { m_generateTails = value; }
        }

        /// <summary>
        /// \~english IV ATM (percents)
        /// \~russian Волатильность на-деньгах (в процентах)
        /// </summary>
        [HelperName("IV ATM, %", Constants.En)]
        [HelperName("IV ATM, %", Constants.Ru)]
        [Description("Волатильность на-деньгах (в процентах)")]
        [HelperDescription("IV ATM (percents)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "30.0",
            Min = "0.000001", Max = "10000.0", Step = "0.01")]
        public OptimProperty IvAtmPct
        {
            get { return m_ivAtmPct; }
            set { m_ivAtmPct = value; }
        }

        /// <summary>
        /// \~english Shift (percents)
        /// \~russian Сдвиг (в процентах)
        /// </summary>
        [HelperName("Shift, %", Constants.En)]
        [HelperName("Сдвиг, %", Constants.Ru)]
        [Description("Сдвиг (в процентах)")]
        [HelperDescription("Shift (percents)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "30.0",
            Min = "-10000.0", Max = "10000.0", Step = "0.01")]
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
        /// \~russian Глубина (в процентах)
        /// </summary>
        [HelperName("Depth, %", Constants.En)]
        [HelperName("Глубина, %", Constants.Ru)]
        [Description("Глубина (в процентах)")]
        [HelperDescription("Depth (percents)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "50.0",
            Min = "0.0", Max = "10000.0", Step = "0.01")]
        public OptimProperty DepthPct
        {
            get { return m_depthPct; }
            set { m_depthPct = value; }
        }

        /// <summary>
        /// \~english Skew (percents)
        /// \~russian Наклон (в процентах)
        /// </summary>
        [HelperName("Skew, %", Constants.En)]
        [HelperName("Наклон, %", Constants.Ru)]
        [Description("Наклон (в процентах)")]
        [HelperDescription("Skew (percents)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "10.0",
            Min = "-10000.0", Max = "10000.0", Step = "0.01")]
        public OptimProperty SlopeAtmPct
        {
            get { return m_internalSlopePct; }
            set { m_internalSlopePct = value; }
        }
        #endregion Parameters

        public InteractiveSeries Execute(double price, double time, InteractiveSeries smile, IOptionSeries optSer, int barNum)
        {
            if (optSer == null)
                return Constants.EmptySeries;

            InteractiveSeries res = Execute(price, time, smile, optSer, 0, barNum);
            return res;
        }

        public InteractiveSeries Execute(double price, double time, InteractiveSeries smile, IOptionSeries optSer, double ratePct, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (smile == null) || (optSer == null))
                return Constants.EmptySeries;

            SmileInfo oldInfo = smile.GetTag<SmileInfo>();
            if ((oldInfo == null) ||
                (oldInfo.ContinuousFunction == null) || (oldInfo.ContinuousFunctionD1 == null))
                return Constants.EmptySeries;

            int lastBarIndex = optSer.UnderlyingAsset.Bars.Count - 1;
            DateTime now = optSer.UnderlyingAsset.Bars[Math.Min(barNum, lastBarIndex)].Date;
            bool wasInitialized = HandlerInitializedToday(now);

            double futPx = price;
            double dT = time;
            if (Double.IsNaN(dT) || (dT < Double.Epsilon))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (Double.IsNaN(futPx) || (futPx < Double.Epsilon))
            {
                // [{0}] Base asset price must be positive value. F:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.FutPxMustBePositive", GetType().Name, futPx);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (Double.IsNaN(ratePct))
                //throw new ScriptException("Argument 'ratePct' contains NaN for some strange reason. rate:" + rate);
                return Constants.EmptySeries;

            double ivAtm, slopeAtm;
            if ((!oldInfo.ContinuousFunction.TryGetValue(futPx, out ivAtm)) ||
                (!oldInfo.ContinuousFunctionD1.TryGetValue(futPx, out slopeAtm)))
                return Constants.EmptySeries;

            if (m_setIvByHands)
            {
                ivAtm = m_ivAtmPct.Value / Constants.PctMult;
            }

            if (m_setSlopeByHands)
            {
                slopeAtm = m_internalSlopePct.Value / Constants.PctMult;
                slopeAtm = slopeAtm / futPx / Math.Sqrt(dT);
            }

            if (Double.IsNaN(ivAtm) || (ivAtm < Double.Epsilon))
            {
                // [{0}] ivAtm must be positive value. ivAtm:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.IvAtmMustBePositive", GetType().Name, ivAtm);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (Double.IsNaN(slopeAtm))
            {
                // [{0}] Smile skew at the money must be some number. skewAtm:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.SkewAtmMustBeNumber", GetType().Name, slopeAtm);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            //{
            //    string msg = String.Format("[DEBUG:{0}] ivAtm:{1}; shift:{2}; depth:{3};   F:{4}; dT:{5}; ",
            //        GetType().Name, ivAtm, shift, depth, F, dT);
            //    context.Log(msg, MessageType.Info, true);
            //}

            SmileFunction3 tempFunc = new SmileFunction3(ivAtm, m_shift, 0.5, futPx, dT);
            double depth = tempFunc.GetDepthUsingSlopeATM(slopeAtm);

            SmileFunction3 smileFunc = new SmileFunction3(ivAtm, m_shift, depth, futPx, dT);

            if (!m_setIvByHands)
            {
                m_ivAtmPct.Value = ivAtm * Constants.PctMult;
            }
            m_depthPct.Value = depth * Constants.PctMult;

            if (!m_setSlopeByHands)
            {
                double dSigmaDx = slopeAtm * futPx * Math.Sqrt(dT);
                m_internalSlopePct.Value = dSigmaDx * Constants.PctMult;
            }

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            IOptionStrikePair[] pairs = optSer.GetStrikePairs().ToArray();

            if (pairs.Length < 2)
            {
                string msg = String.Format("[{0}] optSer must contain few strike pairs. pairs.Length:{1}", GetType().Name, pairs.Length);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptySeries;
            }

            double minK = pairs[0].Strike;
            double dK = pairs[1].Strike - pairs[0].Strike;
            double width = (SigmaMult * ivAtm * Math.Sqrt(dT)) * futPx;
            width = Math.Max(width, 2 * dK);
            // Generate left invisible tail
            if (m_generateTails)
                GaussSmile.AppendLeftTail(smileFunc, xs, ys, minK, dK, false);

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            for (int j = 0; j < pairs.Length; j++)
            {
                bool showPoint = true;
                IOptionStrikePair pair = pairs[j];
                // Сверхдалекие страйки игнорируем
                if ((pair.Strike < futPx - width) || (futPx + width < pair.Strike))
                {
                    showPoint = false;
                }

                double k = pair.Strike;
                double sigma = smileFunc.Value(k);
                double vol = sigma;

                if (Double.IsNaN(sigma) || Double.IsInfinity(sigma) || (sigma < Double.Epsilon))
                    continue;

                InteractivePointActive ip = new InteractivePointActive(k, vol);
                //ip.Color = (optionPxMode == OptionPxMode.Ask) ? Colors.DarkOrange : Colors.DarkCyan;
                //ip.DragableMode = DragableMode.None;
                //ip.Geometry = Geometries.Rect; // (optionPxMode == OptionPxMode.Ask) ? Geometries.Rect : Geometries.Rect;

                // Иначе неправильно выставляются координаты???
                //ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}", k, PctMult * sigma);

                if (showPoint)
                {
                    if (k <= futPx) // Puts
                        FillNodeInfo(ip, futPx, dT, pair, StrikeType.Put, OptionPxMode.Mid, sigma, false, m_isVisiblePoints, ratePct);
                    else // Calls
                        FillNodeInfo(ip, futPx, dT, pair, StrikeType.Call, OptionPxMode.Mid, sigma, false, m_isVisiblePoints, ratePct);
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
            InteractiveSeries res = new InteractiveSeries();
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            double maxK = pairs[pairs.Length - 1].Strike;
            // Generate right invisible tail
            if (m_generateTails)
                GaussSmile.AppendRightTail(smileFunc, xs, ys, maxK, dK, false);

            var baseSec = optSer.UnderlyingAsset;
            DateTime scriptTime = baseSec.Bars[baseSec.Bars.Count - 1].Date;

            // ReSharper disable once UseObjectOrCollectionInitializer
            SmileInfo info = new SmileInfo();
            info.F = futPx;
            info.dT = dT;
            info.Expiry = optSer.ExpirationDate;
            info.ScriptTime = scriptTime;
            info.RiskFreeRate = ratePct;
            info.BaseTicker = baseSec.Symbol;

            info.ContinuousFunction = smileFunc;
            info.ContinuousFunctionD1 = smileFunc.DeriveD1();

            res.Tag = info;

            SetHandlerInitialized(now);

            return res;
        }

        internal static void FillNodeInfo(InteractivePointActive ip,
            double f, double dT, IOptionStrikePair sInfo,
            StrikeType optionType, OptionPxMode optPxMode,
            double optSigma, bool returnPct, bool isVisiblePoints, double riskfreeRatePct)
        {
            if (optionType == StrikeType.Any)
                throw new ArgumentException("Option type 'Any' is not supported.", "optionType");

            bool isCall = (optionType == StrikeType.Call);
            double optPx = FinMath.GetOptionPrice(f, sInfo.Strike, dT, optSigma, 0, isCall);

            // ReSharper disable once UseObjectOrCollectionInitializer
            SmileNodeInfo nodeInfo = new SmileNodeInfo();
            nodeInfo.F = f;
            nodeInfo.dT = dT;
            nodeInfo.RiskFreeRate = riskfreeRatePct;
            nodeInfo.Sigma = returnPct ? optSigma * Constants.PctMult : optSigma;
            nodeInfo.OptPx = optPx;
            nodeInfo.Strike = sInfo.Strike;
            // Сюда мы приходим когда хотим торговать, поэтому обращение к Security уместно
            nodeInfo.Security = (optionType == StrikeType.Put) ? sInfo.Put.Security : sInfo.Call.Security;
            nodeInfo.PxMode = optPxMode;
            nodeInfo.OptionType = optionType;
            nodeInfo.Pair = sInfo;
            //nodeInfo.ScriptTime = optTime;
            nodeInfo.CalendarTime = DateTime.Now;

            nodeInfo.Symbol = nodeInfo.Security.Symbol;
            nodeInfo.DSName = nodeInfo.Security.SecurityDescription.DSName;
            nodeInfo.Expired = nodeInfo.Security.SecurityDescription.Expired;
            nodeInfo.FullName = nodeInfo.Security.SecurityDescription.FullName;

            ip.Tag = nodeInfo;
            //ip.Color = Colors.Yellow;
            ip.IsActive = isVisiblePoints;
            ip.Geometry = Geometries.Ellipse;
            ip.DragableMode = DragableMode.None;
            //ip.Value = new System.Windows.Point(sInfo.Strike, nodeInfo.Sigma);
            // Поскольку цены скорее всего расчетные, показываю на 1 знак болььше, чем объявлена точность в инструменте
            int decim = Math.Max(0, nodeInfo.Security.Decimals + 1);
            string optPxStr = optPx.ToString("N" + decim, CultureInfo.InvariantCulture);
            ip.Tooltip = String.Format(CultureInfo.InvariantCulture,
                " K: {0}; IV: {1:#0.00}%\r\n   {2} px {3}",
                sInfo.Strike, optSigma * Constants.PctMult, optionType, optPxStr);
        }
    }
}
