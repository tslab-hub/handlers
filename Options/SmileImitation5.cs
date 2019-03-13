using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TSLab.Script.CanvasPane;
using TSLab.Script.Optimization;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Smile imitation using application-wide user function in Global Cache
    /// \~russian Имитация улыбки, используя пользовательскую функцию в глобальном кеше
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Smile Imitation v5", Language = Constants.En)]
    [HelperName("Имитация улыбки v5", Language = Constants.Ru)]
    [InputsCount(5)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [Input(3, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(4, TemplateTypes.DOUBLE, Name = Constants.RiskFreeRate)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Имитация улыбки, используя пользовательскую функцию в глобальном кеше")]
    [HelperDescription("Smile imitation using application-wide user function in Global Cache", Constants.En)]
    public class SmileImitation5 : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const double DefaultPow = 0.56;
        private const int DefaultOptQty = 10;

        private bool m_generateTails = true;
        private bool m_setIvByHands = false;
        private bool m_setSlopeByHands = false;
        private bool m_setShapeByHands = false;
        //private bool m_isVisiblePoints = true;

        private bool m_useSmileTails = true;

        private bool m_useLocalTemplate = false;

        private string m_frozenSmileId = "FrozenSmile";
        private string m_globalSmileId = "GlobalSmile";

        private OptimProperty m_ivAtmPct = new OptimProperty(30, false, double.MinValue, double.MaxValue, 1.0, 3);
        private OptimProperty m_slopePct = new OptimProperty(-10, false, double.MinValue, double.MaxValue, 1.0, 3);
        private OptimProperty m_shapePct = new OptimProperty(0, false, double.MinValue, double.MaxValue, 1.0, 3);

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
        /// \~english Set shape manually
        /// \~russian Форма задаётся руками
        /// </summary>
        [HelperName("Set Shape Manually", Constants.En)]
        [HelperName("Форма принудительно", Constants.Ru)]
        [Description("Форма задаётся руками")]
        [HelperDescription("Set shape manually", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool SetShapeByHands
        {
            get { return m_setShapeByHands; }
            set { m_setShapeByHands = value; }
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

        ///// <summary>
        ///// \~english Show nodes
        ///// \~russian Показывать точки на кривой
        ///// </summary>
        //[HelperName("Show nodes", Constants.En)]
        //[HelperName("Показывать узлы", Constants.Ru)]
        //[Description("Показывать точки на кривой")]
        //[HelperDescription("Show nodes", Language = Constants.En)]
        //[HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "True")]
        //public bool IsVisiblePoints
        //{
        //    get { return m_isVisiblePoints; }
        //    set { m_isVisiblePoints = value; }
        //}

        /// <summary>
        /// \~english Use template from global or from local cache
        /// \~russian Использовать шаблон из глобального или из локального кеша
        /// </summary>
        [HelperName("Use Local Template", Constants.En)]
        [HelperName("Локальный шаблон", Constants.Ru)]
        [Description("Использовать шаблон из глобального или из локального кеша")]
        [HelperDescription("Use template from global or from local cache", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool UseLocalTemplate
        {
            get { return m_useLocalTemplate; }
            set { m_useLocalTemplate = value; }
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
        /// \~english Skew (percents)
        /// \~russian Наклон (в процентах)
        /// </summary>
        [HelperName("Skew, %", Constants.En)]
        [HelperName("Наклон, %", Constants.Ru)]
        [Description("Наклон (в процентах)")]
        [HelperDescription("Skew (percents)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "-10.0",
            Min = "-10000.0", Max = "10000.0", Step = "0.01")]
        public OptimProperty SlopePct
        {
            get { return m_slopePct; }
            set { m_slopePct = value; }
        }

        /// <summary>
        /// \~english Shape (percents)
        /// \~russian Форма (в процентах)
        /// </summary>
        [HelperName("Shape, %", Constants.En)]
        [HelperName("Форма, %", Constants.Ru)]
        [Description("Форма (в процентах)")]
        [HelperDescription("Shape (percents)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "0.0",
            Min = "-10000.0", Max = "10000.0", Step = "0.01")]
        public OptimProperty ShapePct
        {
            get { return m_shapePct; }
            set { m_shapePct = value; }
        }

        /// <summary>
        /// \~english Smile ID to be used with Local Cache
        /// \~russian Ключ улыбки в локальном кеше
        /// </summary>
        [HelperName("Frozen Smile ID", Constants.En)]
        [HelperName("Локальный ключ", Constants.Ru)]
        [Description("Ключ улыбки в локальном кеше")]
        [HelperDescription("Smile ID to be used with Local Cache", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "FrozenSmile")]
        public string FrozenSmileID
        {
            get { return m_frozenSmileId; }
            set { m_frozenSmileId = value; }
        }

        /// <summary>
        /// \~english Smile ID to be used with Global Cache
        /// \~russian Ключ улыбки в глобальном кеше
        /// </summary>
        [HelperName("Global Smile ID", Constants.En)]
        [HelperName("Глобальный ключ", Constants.Ru)]
        [Description("Ключ улыбки в глобальном кеше")]
        [HelperDescription("Smile ID to be used with Global Cache", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "GlobalSmile")]
        public string GlobalSmileID
        {
            get { return m_globalSmileId; }
            set { m_globalSmileId = value; }
        }

//#if DEBUG
//        /// <summary>
//        /// \~english Extrapolate template with smooth continuation
//        /// \~russian Продлевать шаблон на бесконечность
//        /// </summary>
//        [HelperName("Extrapolate Template", Constants.En)]
//        [HelperName("Продлевать шаблон", Constants.Ru)]
//        [Description("Продлевать шаблон на бесконечность")]
//        [HelperDescription("Extrapolate template with smooth continuation", Language = Constants.En)]
//        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "true")]
//        public bool ExtrapolateSmile
//        {
//            get { return m_useSmileTails; }
//            set { m_useSmileTails = value; }
//        }
//#endif
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
            if (!DoubleUtil.IsPositive(dT))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (!DoubleUtil.IsPositive(futPx))
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

            double ivAtm, slopeAtm, shape;
            if ((!oldInfo.ContinuousFunction.TryGetValue(futPx, out ivAtm)) ||
                (!oldInfo.ContinuousFunctionD1.TryGetValue(futPx, out slopeAtm)))
                return Constants.EmptySeries;

            if (m_setIvByHands)
            {
                ivAtm = m_ivAtmPct.Value / Constants.PctMult;
            }

            if (m_setSlopeByHands)
            {
                slopeAtm = m_slopePct.Value / Constants.PctMult;
                //slopeAtm = slopeAtm / F / Math.Pow(dT, Pow + shape);
            }

            //if (setShapeByHands)
            {
                shape = m_shapePct.Value / Constants.PctMult;
            }

            if (!DoubleUtil.IsPositive(ivAtm))
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

            SmileInfo templateInfo;
            #region Fill templateInfo
            if (m_useLocalTemplate)
            {
                InteractiveSeries templateSmile = m_context.LoadObject(m_frozenSmileId) as InteractiveSeries;
                if (templateSmile == null)
                {
                    // [{0}] There is no LOCAL smile with ID:{1}
                    string msg = RM.GetStringFormat("SmileImitation5.NoLocalSmile", GetType().Name, m_frozenSmileId);
                    if (wasInitialized)
                        m_context.Log(msg, MessageType.Error, true);
                    return Constants.EmptySeries;
                }

                SmileInfo locInfo = new SmileInfo();
                locInfo.F = futPx;
                locInfo.dT = dT;
                locInfo.RiskFreeRate = oldInfo.RiskFreeRate;

                List<double> locXs = new List<double>();
                List<double> locYs = new List<double>();
                foreach (InteractiveObject oldObj in templateSmile.ControlPoints)
                {
                    if (!oldObj.AnchorIsActive)
                        continue;

                    double k = oldObj.Anchor.ValueX;
                    double sigma = oldObj.Anchor.ValueY;

                    double x = Math.Log(k / futPx) / Math.Pow(dT, DefaultPow + shape) / ivAtm;
                    double sigmaNormalized = sigma / ivAtm;

                    locXs.Add(x);
                    locYs.Add(sigmaNormalized);
                }

                try
                {
                    NotAKnotCubicSpline spline = new NotAKnotCubicSpline(locXs, locYs);

                    locInfo.ContinuousFunction = spline;
                    locInfo.ContinuousFunctionD1 = spline.DeriveD1();

                    templateInfo = locInfo;
                }
                catch (Exception ex)
                {
                    m_context.Log(ex.ToString(), MessageType.Error, true);
                    return Constants.EmptySeries;
                }
            }
            else
            {
                //templateSmile = context.LoadGlobalObject(globalSmileID, true) as InteractiveSeries;
                templateInfo = m_context.LoadGlobalObject(m_globalSmileId, true) as SmileInfo;
                if (templateInfo == null)
                {
                    // [{0}] There is no global templateInfo with ID:{1}. I'll try to use default one.
                    string msg = RM.GetStringFormat("SmileImitation5.TemplateWasSaved", GetType().Name, m_globalSmileId);
                    m_context.Log(msg, MessageType.Error, true);

                    System.Xml.Linq.XDocument xDoc = System.Xml.Linq.XDocument.Parse(SmileFunction5.XmlSmileRiz4Nov1);
                    System.Xml.Linq.XElement xInfo = xDoc.Root;
                    SmileInfo templateSmile = SmileInfo.FromXElement(xInfo);

                    // Обновляю уровень IV ATM?
                    if (Double.IsNaN(ivAtm))
                    {
                        ivAtm = oldInfo.ContinuousFunction.Value(futPx);

                        m_context.Log(String.Format("[DEBUG:{0}] ivAtm was NaN. I'll use value ivAtm:{1}", GetType().Name, ivAtm), MessageType.Warning, true);

                        if (Double.IsNaN(ivAtm))
                        {
                            throw new Exception(String.Format("[DEBUG:{0}] ivAtm is NaN.", GetType().Name));
                        }
                    }

                    templateSmile.F = futPx;
                    templateSmile.dT = dT;
                    templateSmile.RiskFreeRate = oldInfo.RiskFreeRate;

                    m_context.StoreGlobalObject(m_globalSmileId, templateSmile, true);

                    // [{0}] Default templateInfo was saved to Global Cache with ID:{1}.
                    msg = RM.GetStringFormat("SmileImitation5.TemplateWasSaved", GetType().Name, m_globalSmileId);
                    m_context.Log(msg, MessageType.Warning, true);

                    templateInfo = templateSmile;
                }
            }
            #endregion Fill templateInfo

            if (!m_setIvByHands)
            {
                m_ivAtmPct.Value = ivAtm * Constants.PctMult;
            }

            if (!m_setShapeByHands)
            {
                // так я ещё не умею
            }

            if (!m_setSlopeByHands)
            {
                // Пересчитываю наклон в безразмерку
                double dSigmaDx = GetDSigmaDx(futPx, dT, slopeAtm, shape);
                m_slopePct.Value = dSigmaDx * Constants.PctMult;

                // и теперь апдейчу локальную переменную slopeAtm:
                slopeAtm = m_slopePct.Value / Constants.PctMult;
            }

            // Это функция в нормированных координатах
            // поэтому достаточно обычной симметризации
            // PROD-3111: заменяю вызов на SmileFunctionExtended
            //SimmetrizeFunc simmFunc = new SimmetrizeFunc(templateInfo.ContinuousFunction);
            //SimmetrizeFunc simmFuncD1 = new SimmetrizeFunc(templateInfo.ContinuousFunctionD1);
            //SmileFunction5 smileFunc = new SmileFunction5(simmFunc, simmFuncD1, ivAtm, slopeAtm, shape, futPx, dT);
            SmileFunctionExtended smileFunc = new SmileFunctionExtended(
                (NotAKnotCubicSpline)templateInfo.ContinuousFunction, ivAtm, slopeAtm, shape, futPx, dT);
            smileFunc.UseTails = m_useSmileTails;

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
            double maxK = pairs[pairs.Length - 1].Strike;
            double futStep = optSer.UnderlyingAsset.Tick;
            double dK = pairs[1].Strike - pairs[0].Strike;
            double width = (SigmaMult * ivAtm * Math.Sqrt(dT)) * futPx;
            width = Math.Max(width, 10 * dK);
            // Нельзя вылезать за границу страйков???
            width = Math.Min(width, Math.Abs(futPx - minK));
            width = Math.Min(width, Math.Abs(maxK - futPx));
            // Generate left invisible tail
            if (m_generateTails)
                GaussSmile.AppendLeftTail(smileFunc, xs, ys, minK, dK, false);

            SortedDictionary<double, IOptionStrikePair> strikePrices;
            if (!TryPrepareImportantPoints(pairs, futPx, futStep, width, out strikePrices))
            {
                string msg = String.Format("[{0}] It looks like there is no suitable points for the smile. pairs.Length:{1}", GetType().Name, pairs.Length);
                if (wasInitialized)
                    m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptySeries;
            }

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            //for (int j = 0; j < pairs.Length; j++)
            foreach (var kvp in strikePrices)
            {
                bool showPoint = (kvp.Value != null);
                //IOptionStrikePair pair = pairs[j];
                //// Сверхдалекие страйки игнорируем
                //if ((pair.Strike < futPx - width) || (futPx + width < pair.Strike))
                //{
                //    showPoint = false;
                //}

                //double k = pair.Strike;
                double k = kvp.Key;
                double sigma;
                if (!smileFunc.TryGetValue(k, out sigma))
                    continue;
                double vol = sigma;

                if (!DoubleUtil.IsPositive(sigma))
                    continue;

                //InteractivePointActive ip = new InteractivePointActive(k, vol);
                //ip.Color = (optionPxMode == OptionPxMode.Ask) ? Colors.DarkOrange : Colors.DarkCyan;
                //ip.DragableMode = DragableMode.None;
                //ip.Geometry = Geometries.Rect; // (optionPxMode == OptionPxMode.Ask) ? Geometries.Rect : Geometries.Rect;

                // Иначе неправильно выставляются координаты???
                //ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}", k, PctMult * sigma);

                InteractivePointLight ip;
                if (showPoint)
                {
                    var tip = new InteractivePointActive(k, vol);
                    if (k <= futPx) // Puts
                        SmileImitation3.FillNodeInfo(tip, futPx, dT, kvp.Value, StrikeType.Put, OptionPxMode.Mid, sigma, false, m_showNodes, ratePct);
                    else // Calls
                        SmileImitation3.FillNodeInfo(tip, futPx, dT, kvp.Value, StrikeType.Call, OptionPxMode.Mid, sigma, false, m_showNodes, ratePct);
                    ip = tip;
                }
                else
                    ip = new InteractivePointLight(k, vol);

                InteractiveObject obj = new InteractiveObject(ip);

                //if (showPoint)
                // Теперь мы понимаем, что точки либо рисуются
                // потому что это страйки (и тогда они автоматом InteractivePointActive)
                // либо они присутствуют, но не рисуются их узлы. Потому что они InteractivePointLight.
                {
                    controlPoints.Add(obj);
                }

                xs.Add(k);
                ys.Add(vol);
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries();
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            // Generate right invisible tail
            if (m_generateTails)
                GaussSmile.AppendRightTail(smileFunc, xs, ys, maxK, dK, false);

            var baseSec = optSer.UnderlyingAsset;
            DateTime scriptTime = baseSec.Bars[baseSec.Bars.Count - 1].Date;

            // ReSharper disable once UseObjectOrCollectionInitializer
            SmileInfo info = new SmileInfo();
            info.F = futPx;
            info.dT = dT;
            info.RiskFreeRate = ratePct;

            info.IvAtm = ivAtm;
            info.SkewAtm = slopeAtm;
            info.Shape = shape;
            info.SmileType = GetType().FullName;

            info.ScriptTime = scriptTime;
            info.Expiry = optSer.ExpirationDate;
            info.BaseTicker = baseSec.Symbol;

            info.ContinuousFunction = smileFunc;
            info.ContinuousFunctionD1 = smileFunc.DeriveD1();

            res.Tag = info;

            SetHandlerInitialized(now);

            return res;
        }

        /// <summary>
        /// Функция вычисления наклона улыбки в безразмерных координатах
        /// </summary>
        /// <param name="futPx">цена фьючерса (должна быть строго положительная)</param>
        /// <param name="dT">время до экспирации в долях года (должно быть строго положительно)</param>
        /// <param name="slopeAtm">наклон улыбки в обычных размерных координатах (НЕ в процентах, а в 'сыром' виде)</param>
        /// <param name="shape">форма улыбки (НЕ в процентах, а в 'сыром' виде)</param>
        /// <returns>наклон улыбки в безразмерных координатах (если аргументы невалидны -- вернет в лучшем случае null)</returns>
        internal static double GetDSigmaDx(double futPx, double dT, double slopeAtm, double shape = 0)
        {
            double res = slopeAtm * futPx * Math.Pow(dT, DefaultPow + shape);
            return res;
        }

        /// <summary>
        /// Функция формирует отсортированный список точек (вдоль оси страйков), в которых нужно вычислить улыбку или профиль позиции.
        /// В окрестности текущей цены futPx плотность точек увеличивается для получения визуально более гладкой кривой.
        /// futPx гарантированно является одной из точек расчета.
        /// 
        /// Дополнительно к этому добавляются все реальные страйки с присоединенным к ним объектом IOptionStrikePair для последующего использования.
        /// </summary>
        /// <param name="pairs">массив страйков (отсортирован по возрастанию)</param>
        /// <param name="futPx">текущая цена базового актива</param>
        /// <param name="futStep">шаг цены базового актива</param>
        /// <param name="width">ширина рабочей области (при отрицательном значении ограничение снимается)</param>
        /// <param name="strikePrices">результирующий список точек для расчетов</param>
        /// <returns>true, если массив подготовлен; false, если массив точек подготовить не удалось (например, при невалидных аргументах)</returns>
        internal static bool TryPrepareImportantPoints(
            IOptionStrikePair[] pairs, double futPx, double futStep, double width,
            out SortedDictionary<double, IOptionStrikePair> strikePrices)
        {
            strikePrices = null;
            if ((pairs == null) || (pairs.Length < 2))
                return false;

            // Оценка расстояния между двумя соседними страйками
            double dK = pairs[1].Strike - pairs[0].Strike;

            // 1. Плотная нарезка точек в окрестности futPx, включая саму futPx
            // Таблица фьючерсных цен + флаг того, что узел надо показывать
            strikePrices = new SortedDictionary<double, IOptionStrikePair>();
            // Дополняю несколькими точками в окрестности futPx с флагом "скрыть" (на сколько частей надо нарезать dK?)
            const int HalfNum = 20;
            // Это вместо DoubleUtil.Round(...)
            double t = Math.Round(dK / HalfNum / futStep) * futStep;
            double dF = Math.Max(10 * futStep, t); // Возможно, надо ещё плотнее нарезать точки???
            // 2017-04-20 - Алексей заказал увеличить диапазон подробного расчета улыбок в 2 раза
            for (int index = -2 * HalfNum; index <= 2 * HalfNum; index++)
            {
                double k = futPx + index * dF;
                // PROD-3346: Функция работает неправильно!
                //k = DoubleUtil.Round(k, futStep);
                k = Math.Round(k / futStep) * futStep;
                strikePrices[k] = null;
            }

            // 2. Добавляю страйки причем с флагом "показывать"
            for (int index = 0; index < pairs.Length; index++)
            {
                double k = pairs[index].Strike;
                // Сверхдалекие страйки игнорируем
                if ((futStep < width)
                    && ((k < futPx - width) || (futPx + width < k)))
                {
                    // Вообще ничего не делаем!
                    // Иначе слишком много получается ;-)
                    // strikePrices[k] = false;
                }
                else
                    strikePrices[k] = pairs[index];
            }

            return true;
        }
    }
}
