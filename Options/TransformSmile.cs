using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Transform smile as requested in parameters
    /// \~russian Преобразовать улыбку в соответствии с заданным алгоритмом
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Transform Smile", Language = Constants.En)]
    [HelperName("Преобразовать улыбку", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Преобразовать улыбку в соответствии с заданным алгоритмом")]
    [HelperDescription("Transform smile as requested in parameters", Constants.En)]
    public class TransformSmile : BaseContextHandler, IValuesHandlerWithNumber
    {
        private const int DefaultOptQty = 10;

        private double m_shiftIv = 0;
        private double m_simmWeight = 0.5;
        private OptionPxMode m_optionPxMode = OptionPxMode.Mid;
        private SmileTransformation m_transformation = SmileTransformation.LogSimmetrise;

        #region Parameters
        /// <summary>
        /// \~english Algorythm to transform smile (LogSimmetrise, Simmetrise, None)
        /// \~russian Вид преобразования улыбки (LogSimmetrise, Simmetrise, None)
        /// </summary>
        [HelperName("Transformation", Constants.En)]
        [HelperName("Преобразование", Constants.Ru)]
        [Description("Вид преобразования улыбки (LogSimmetrise, Simmetrise, None)")]
        [HelperDescription("Algorythm to transform smile (LogSimmetrise, Simmetrise, None)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "LogSimmetrise")]
        public SmileTransformation Transformation
        {
            get { return m_transformation; }
            set { m_transformation = value; }
        }

        /// <summary>
        /// \~english Additional vertical smile shift (in percents)
        /// \~russian Аддитивный сдвиг на заданное количество ПРОЦЕНТОВ ВОЛАТИЛЬНОСТИ вверх
        /// </summary>
        [HelperName("Shift IV, %", Constants.En)]
        [HelperName("Сдвиг волатильности, %", Constants.Ru)]
        [Description("Аддитивный сдвиг на заданное количество ПРОЦЕНТОВ ВОЛАТИЛЬНОСТИ вверх")]
        [HelperDescription("Additional vertical smile shift (in percents)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0.0",
            Min = "-10000.0", Max = "10000.0", Step = "0.01")]
        public double ShiftIvPct
        {
            get { return m_shiftIv * Constants.PctMult; }
            set
            {
                if (!Double.IsNaN(value))
                    m_shiftIv = value / Constants.PctMult;
            }
        }

        /// <summary>
        /// \~english Weight (0 -- initial function; 0.5 -- simmetrised; 1 -- mirrored)
        /// \~russian Вес симметризации (0 -- исходная функция; 0.5 -- симметричная; 1 -- отраженная)
        /// </summary>
        [HelperName("Weight", Constants.En)]
        [HelperName("Вес симметризации", Constants.Ru)]
        [Description("Вес симметризации (0 -- исходная функция; 0.5 -- симметричная; 1 -- отраженная)")]
        [HelperDescription("Weight (0 -- initial function; 0.5 -- simmetrised; 1 -- mirrored)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0.5",
            Min = "-10000.0", Max = "10000.0", Step = "0.01")]
        public double SimmWeight
        {
            get { return m_simmWeight; }
            set { m_simmWeight = value; }
        }

        /// <summary>
        /// \~english Algorythm to get option price
        /// \~russian Алгоритм расчета цены опциона
        /// </summary>
        [HelperName("Price Mode", Constants.En)]
        [HelperName("Вид цены", Constants.Ru)]
        [Description("Алгоритм расчета цены опциона")]
        [HelperDescription("Algorythm to get option price", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "Mid")]
        public OptionPxMode OptPxMode
        {
            get { return m_optionPxMode; }
            set { m_optionPxMode = value; }
        }
        #endregion Parameters

        public InteractiveSeries Execute(InteractiveSeries smile, int barNum)
        {
            if (m_transformation == SmileTransformation.None)
            {
                if (DoubleUtil.IsZero(m_shiftIv))
                    return smile;
            }

            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return smile;

            // Пустая улыбка останется пустой. Что с ней делать непонятно.
            if (smile.ControlPoints.Count <= 1)
                return smile;

            SmileInfo sInfo = smile.GetTag<SmileInfo>();
            if (sInfo == null)
                throw new ScriptException("ArgumentException: smile.Tag must be filled with SmileInfo object.");

            var cps = smile.ControlPoints;
            int len = cps.Count;

            IFunction symmetrizedFunc = null;
            IFunction symmetrizedFuncD1 = null;
            InteractiveSeries res = new InteractiveSeries();
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            if (m_transformation == SmileTransformation.Simmetrise)
            {
                #region Simmetrise
                double f = sInfo.F;
                double minX = cps[0].Anchor.ValueX;
                double maxX = cps[len - 1].Anchor.ValueX;

                double width = Math.Min(maxX - f, f - minX);
                if (width < 0)
                    throw new ScriptException("ArgumentException: current price is outside of the smile.");

                // TODO: сократить вычисления вдвое, учитывая явное требование симметричности результирующей улыбки
                double step = 2.0 * width / (len - 1);
                List<InteractiveObject> controlPoints = new List<InteractiveObject>();
                for (int j = 0; j < len; j++)
                {
                    double kLeft = (f - width) + j * step;
                    double ivLeft = sInfo.ContinuousFunction.Value(kLeft);

                    double kRight = (f + width) - j * step;
                    double ivRight = sInfo.ContinuousFunction.Value(kRight);

                    double iv = 0.5 * (ivLeft + ivRight) + m_shiftIv;

                    InteractiveObject oldObj = cps[j];

                    if (oldObj.Anchor is InteractivePointActive)
                    {
                        InteractivePointActive ip = (InteractivePointActive)oldObj.Anchor.Clone();

                        ip.Color = (m_optionPxMode == OptionPxMode.Ask) ? AlphaColors.DarkOrange : AlphaColors.DarkCyan;
                        ip.DragableMode = DragableMode.None;
                        ip.Geometry = Geometries.Rect;
                            // (optionPxMode == OptionPxMode.Ask) ? Geometries.Rect : Geometries.Rect;
                        ip.IsActive = (m_optionPxMode != OptionPxMode.Mid);

                        ip.Value = new Point(kLeft, iv);
                        ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}", kLeft, iv * Constants.PctMult);

                        InteractiveObject newObj = new InteractiveObject(ip);
                        controlPoints.Add(newObj);
                    }
                    else if (oldObj.Anchor is InteractivePointLight)
                    {
                        InteractivePointLight ip = (InteractivePointLight)oldObj.Anchor.Clone();
                        ip.Value = new Point(kLeft, iv);
                        InteractiveObject newObj = new InteractiveObject(ip);
                        controlPoints.Add(newObj);
                    }
                    else
                    {
                        string msg = String.Format("[{0}] Point of type '{1}' is not supported.",
                            GetType().Name, oldObj.Anchor.GetType().Name);
                        throw new ScriptException(msg);
                    }

                    xs.Add(kLeft);
                    ys.Add(iv);
                }

                res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);
                #endregion Simmetrise
            }
            else if (m_transformation == SmileTransformation.LogSimmetrise)
            {
                #region Log Simmetrise
                double f = sInfo.F;

                LogSimmetrizeFunc lsf = new LogSimmetrizeFunc(sInfo.ContinuousFunction, f, m_simmWeight);
                symmetrizedFunc = lsf;
                symmetrizedFuncD1 = new LogSimmetrizeFunc(sInfo.ContinuousFunctionD1, f, m_simmWeight);
                List<InteractiveObject> controlPoints = new List<InteractiveObject>();
                foreach (var oldObj in smile.ControlPoints)
                {
                    double k = oldObj.Anchor.ValueX;
                    double iv;
                    if (lsf.TryGetValue(k, out iv))
                    {
                        iv += m_shiftIv;

                        if (oldObj.Anchor is InteractivePointActive)
                        {
                            InteractivePointActive ip = (InteractivePointActive)oldObj.Anchor.Clone();

                            ip.Color = (m_optionPxMode == OptionPxMode.Ask) ? AlphaColors.DarkOrange : AlphaColors.DarkCyan;
                            ip.DragableMode = DragableMode.None;
                            ip.Geometry = Geometries.Rect;
                                // (optionPxMode == OptionPxMode.Ask) ? Geometries.Rect : Geometries.Rect;
                            ip.IsActive = (m_optionPxMode != OptionPxMode.Mid);

                            ip.Value = new Point(k, iv);
                            ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}", k, iv * Constants.PctMult);

                            InteractiveObject newObj = new InteractiveObject(ip);
                            controlPoints.Add(newObj);
                        }
                        else if (oldObj.Anchor is InteractivePointLight)
                        {
                            InteractivePointLight ip = (InteractivePointLight)oldObj.Anchor.Clone();
                            ip.Value = new Point(k, iv);
                            InteractiveObject newObj = new InteractiveObject(ip);
                            controlPoints.Add(newObj);
                        }
                        else
                        {
                            string msg = String.Format("[{0}] Point of type '{1}' is not supported.",
                                GetType().Name, oldObj.Anchor.GetType().Name);
                            throw new ScriptException(msg);
                        }

                        xs.Add(k);
                        ys.Add(iv);
                    }
                }

                res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);
                #endregion Log Simmetrise
            }
            else if (m_transformation == SmileTransformation.None)
            {
                #region None (only vertical shift)
                double f = sInfo.F;
                double dT = sInfo.dT;
                double rate = sInfo.RiskFreeRate;
                List<InteractiveObject> controlPoints = new List<InteractiveObject>();
                for (int j = 0; j < len; j++)
                {
                    InteractiveObject oldObj = cps[j];
                    SmileNodeInfo nodeInfo = oldObj.Anchor.Tag as SmileNodeInfo;
                    // Обязательно нужна полная информация об узле улыбки, чтобы потом можно было торговать
                    if (nodeInfo == null)
                        continue;

                    double k = oldObj.Anchor.Value.X;
                    double iv = oldObj.Anchor.Value.Y + m_shiftIv;

                    if (oldObj.Anchor is InteractivePointActive)
                    {
                        InteractivePointActive ip = (InteractivePointActive)oldObj.Anchor.Clone();

                        ip.Color = (m_optionPxMode == OptionPxMode.Ask) ? AlphaColors.DarkOrange : AlphaColors.DarkCyan;
                        ip.DragableMode = DragableMode.None;
                        ip.Geometry = Geometries.Rect;
                            // (optionPxMode == OptionPxMode.Ask) ? Geometries.Rect : Geometries.Rect;
                        ip.IsActive = (m_optionPxMode != OptionPxMode.Mid);

                        //ip.Value = new Point(oldObj.Anchor.Value.X, iv);
                        //ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}", k, iv * PctMult);

                        InteractiveObject newObj = new InteractiveObject(ip);

                        if (k <= f) // Puts
                            FillNodeInfo(ip, f, dT, nodeInfo.Pair, StrikeType.Put, m_optionPxMode, iv, false, rate);
                        else // Calls
                            FillNodeInfo(ip, f, dT, nodeInfo.Pair, StrikeType.Call, m_optionPxMode, iv, false, rate);

                        controlPoints.Add(newObj);
                    }
                    else if (oldObj.Anchor is InteractivePointLight)
                    {
                        InteractivePointLight ip = (InteractivePointLight)oldObj.Anchor.Clone();
                        ip.Value = new Point(k, iv);
                        InteractiveObject newObj = new InteractiveObject(ip);

                        if (k <= f) // Puts
                            FillNodeInfo(ip, f, dT, nodeInfo.Pair, StrikeType.Put, m_optionPxMode, iv, false, rate);
                        else // Calls
                            FillNodeInfo(ip, f, dT, nodeInfo.Pair, StrikeType.Call, m_optionPxMode, iv, false, rate);

                        controlPoints.Add(newObj);
                    }
                    else
                    {
                        string msg = String.Format("[{0}] Point of type '{1}' is not supported.",
                            GetType().Name, oldObj.Anchor.GetType().Name);
                        throw new ScriptException(msg);
                    }

                    xs.Add(k);
                    ys.Add(iv);
                }

                res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);
                #endregion None (only vertical shift)
            }
            else
                throw new NotImplementedException("Transformation: " + m_transformation);

            // ReSharper disable once UseObjectOrCollectionInitializer
            SmileInfo info = new SmileInfo();
            info.F = sInfo.F;
            info.dT = sInfo.dT;
            info.Expiry = sInfo.Expiry;
            info.ScriptTime = sInfo.ScriptTime;
            info.RiskFreeRate = sInfo.RiskFreeRate;
            info.BaseTicker = sInfo.BaseTicker;

            try
            {
                if (symmetrizedFunc == null)
                {
                    NotAKnotCubicSpline spline = new NotAKnotCubicSpline(xs, ys);

                    info.ContinuousFunction = spline;
                    info.ContinuousFunctionD1 = spline.DeriveD1();
                }
                else
                {
                    // По факту эта ветка обслуживает только алгоритм LogSimm
                    info.ContinuousFunction = symmetrizedFunc;
                    info.ContinuousFunctionD1 = symmetrizedFuncD1;
                }
                res.Tag = info;
            }
            catch (Exception ex)
            {
                m_context.Log(ex.ToString(), MessageType.Error, true);
                return Constants.EmptySeries;
            }

            return res;
        }

        protected static void FillNodeInfo(InteractivePointLight ip,
            double f, double dT, IOptionStrikePair sInfo,
            StrikeType optionType, OptionPxMode optPxMode,
            double optSigma, bool returnPct, double pctRate)
        {
            if (optionType == StrikeType.Any)
                throw new ArgumentException("Option type 'Any' is not supported.", "optionType");

            bool isCall = (optionType == StrikeType.Call);
            double optPx = FinMath.GetOptionPrice(f, sInfo.Strike, dT, optSigma, pctRate, isCall);

            // ReSharper disable once UseObjectOrCollectionInitializer
            SmileNodeInfo nodeInfo = new SmileNodeInfo();
            nodeInfo.F = f;
            nodeInfo.dT = dT;
            nodeInfo.RiskFreeRate = pctRate;
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
            nodeInfo.DSName = nodeInfo.Security.SecurityDescription.DSName;
            nodeInfo.FullName = nodeInfo.Security.SecurityDescription.FullName;
            nodeInfo.Expired = nodeInfo.Security.SecurityDescription.Expired;

            ip.Tag = nodeInfo;
            ip.Value = new Point(sInfo.Strike, nodeInfo.Sigma);

            if (ip is InteractivePointActive)
            {
                InteractivePointActive ipa = (InteractivePointActive)ip;
                ipa.Tooltip = String.Format("K:{0}; IV:{1:#0.00}%\r\n{2} {3} @ {4}",
                    sInfo.Strike, optSigma * Constants.PctMult, optionType, optPx, DefaultOptQty);
            }
        }
    }
}
