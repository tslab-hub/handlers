using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Edit template smile
    /// \~russian Редактировать шаблон улыбки
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Edit Template Smile", Language = Constants.En)]
    [HelperName("Редактировать шаблон улыбки", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(1, TemplateTypes.INTERACTIVESPLINE, Name = Constants.Smile)]
    [OutputsCount(1)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Редактировать шаблон улыбки")]
    [HelperDescription("Edit template smile", Constants.En)]
    public class EditTemplateSmile : BaseCanvasDrawing, IValuesHandlerWithNumber, IDisposable
    {
        private static readonly Common.Logging.ILog s_log = Common.Logging.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const double DefaultPow = 0.56;
        private const string GlobalSmile = "GlobalSmile";

        private double m_shape = 0;
        private double m_nodeStep = 1.5;
        private int m_numberOfNodes = 11;
        private bool m_resetSmile = false;
        private bool m_loadSplineCoeffs = false;
        private bool m_prepareSplineCoeffs = false;
        private bool m_pasteGlobal = false;
        private string m_frozenSmileId = "FrozenSmile";
        private int m_globalSmileId = 0;

        private InteractiveSeries FrozenSmile
        {
            get
            {
                InteractiveSeries res = m_context.LoadObject(m_frozenSmileId) as InteractiveSeries;
                if (res == null)
                {
                    res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
                    m_context.StoreObject(m_frozenSmileId, res);
                }
                return res;
            }
        }

        private string GlobalSmileKey
        {
            get { return GlobalSmile + m_globalSmileId; }
        }

        #region Parameters
        /// <summary>
        /// \~english Number of nodes to edit
        /// \~russian Количество редактируемых точек
        /// </summary>
        [HelperName("Number of Nodes", Constants.En)]
        [HelperName("Кол-во узлов", Constants.Ru)]
        [Description("Количество редактируемых точек")]
        [HelperDescription("Number of nodes to edit", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "11", Min = "3", Max = "100000", Step = "1", NumberDecimalDigits = 0)]
        public int NumberOfNodes
        {
            get { return m_numberOfNodes; }
            set { m_numberOfNodes = value; }
        }

        /// <summary>
        /// \~english Node step
        /// \~russian Шаг узлов интерполяции
        /// </summary>
        [HelperName("Node Step", Constants.En)]
        [HelperName("Шаг узлов", Constants.Ru)]
        [Description("Шаг узлов интерполяции")]
        [HelperDescription("Node step", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "1.5", Min = "0", Max = "100000", Step = "1", NumberDecimalDigits = 3)]
        public double NodeStep
        {
            get { return m_nodeStep; }
            set { m_nodeStep = value; }
        }

        /// <summary>
        /// \~english Shape
        /// \~russian Параметр формы
        /// </summary>
        [HelperName("Shape", Constants.En)]
        [HelperName("Форма", Constants.Ru)]
        [Description("Параметр формы")]
        [HelperDescription("Shape", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0.0", Min = "-100000", Max = "100000", Step = "1", NumberDecimalDigits = 3)]
        public double ShapePct
        {
            get { return m_shape * Constants.PctMult; }
            set { m_shape = value / Constants.PctMult; }
        }

        /// <summary>
        /// \~english Button to reset smile to initial state
        /// \~russian Разложить улыбку по симметризованному шаблону
        /// </summary>
        [HelperName("Reset", Constants.En)]
        [HelperName("Сброс", Constants.Ru)]
        [Description("Разложить улыбку по симметризованному шаблону")]
        [HelperDescription("Button to reset smile to initial state", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool ResetSmile
        {
            get { return m_resetSmile; }
            set { m_resetSmile = value; }
        }

        /// <summary>
        /// \~english Load spline coefficients from Global Cache
        /// \~russian Загрузить коэффициенты сплайна из глобального кеша
        /// </summary>
        [HelperName("Load spline", Constants.En)]
        [HelperName("Загрузить сплайн", Constants.Ru)]
        [Description("Загрузить коэффициенты сплайна из глобального кеша")]
        [HelperDescription("Load spline coefficients from Global Cache", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool LoadSplineCoeffs
        {
            get { return m_loadSplineCoeffs; }
            set { m_loadSplineCoeffs = value; }
        }

        /// <summary>
        /// \~english Get spline from clipboard
        /// \~russian Получить настройки сплайна из клипборда
        /// </summary>
        [HelperName("Paste", Constants.En)]
        [HelperName("Из буфера", Constants.Ru)]
        [Description("Получить настройки сплайна из клипборда")]
        [HelperDescription("Get spline from clipboard", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool PasteGlobal
        {
            get { return m_pasteGlobal; }
            set { m_pasteGlobal = value; }
        }

        /// <summary>
        /// \~english Prepare spline coefficients
        /// \~russian Подготовить коэффициенты сплайна
        /// </summary>
        [HelperName("Prepare Spline", Constants.En)]
        [HelperName("Из буфера", Constants.Ru)]
        [Description("Подготовить коэффициенты сплайна")]
        [HelperDescription("Prepare spline coefficients", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool PrepareSplineCoeffs
        {
            get { return m_prepareSplineCoeffs; }
            set { m_prepareSplineCoeffs = value; }
        }

        /// <summary>
        /// \~english Smile ID to be used with Local Cache
        /// \~russian Ключ улыбки в локальном кеше
        /// </summary>
        [HelperName("Frozen Smile ID", Constants.En)]
        [HelperName("Локальный ключ", Constants.Ru)]
        [Description("Ключ улыбки в локальном кеше")]
        [HelperDescription("Smile ID to be used with Local Cache", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "FrozenSmile")]
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
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0", Min = "0", Max = "1000000", Step = "1")]
        public int GlobalSmileID
        {
            get { return m_globalSmileId; }
            set { m_globalSmileId = value; }
        }
        #endregion Parameters

        public InteractiveSeries Execute(double trueTimeToExpiry, InteractiveSeries smile, int barNum)
        {
            int barsCount = m_context.BarsCount;
            if (!m_context.IsLastBarUsed)
                barsCount--;
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            SmileInfo refSmileInfo = smile.GetTag<SmileInfo>();
            if ((refSmileInfo == null) || (refSmileInfo.ContinuousFunction == null))
                return Constants.EmptySeries;

            if (Double.IsNaN(trueTimeToExpiry) || (trueTimeToExpiry < Double.Epsilon))
            {
                string msg = String.Format("[{0}] trueTimeToExpiry must be positive value. dT:{1}", GetType().Name, trueTimeToExpiry);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            InteractiveSeries res = FrozenSmile;

            // Поскольку редактируемые узлы идут с заданным шагом, то
            // допустим только режим Yonly.
            DragableMode dragMode = DragableMode.Yonly;
            SmileInfo oldInfo = res.GetTag<SmileInfo>();
            if (m_resetSmile ||
                (oldInfo == null) || (oldInfo.ContinuousFunction == null))
            {
                oldInfo = refSmileInfo;

                //oldInfo.F = sInfo.F;
                //oldInfo.dT = sInfo.dT;
                //oldInfo.RiskFreeRate = sInfo.RiskFreeRate;
            }

            double futPx = refSmileInfo.F;
            double dT = trueTimeToExpiry;
            double ivAtm = refSmileInfo.ContinuousFunction.Value(futPx);

            SmileInfo info = new SmileInfo();
            {
                info.F = futPx;
                info.dT = trueTimeToExpiry;
                info.RiskFreeRate = oldInfo.RiskFreeRate;

                List<double> xs = new List<double>();
                List<double> ys = new List<double>();
                List<InteractiveObject> visibleNodes = (from oldObj in res.ControlPoints
                                                        where oldObj.AnchorIsActive
                                                        select oldObj).ToList();
                int visibleNodesCount = visibleNodes.Count;
                if (m_resetSmile || (visibleNodesCount != m_numberOfNodes))
                {
                    // Здесь обязательно в начале
                    //res.ControlPoints.Clear();
                    res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(new InteractiveObject[0]);

                    int half = m_numberOfNodes / 2; // Целочисленное деление!

                    #region 1. Готовлю сплайн
                    {
                        double mult = m_nodeStep * ivAtm * Math.Sqrt(dT);
                        double dK = futPx * (Math.Exp(mult) - 1);
                        // Сдвигаю точки, чтобы избежать отрицательных значений
                        while ((futPx - half * dK) <= Double.Epsilon)
                            half--;
                        for (int j = 0; j < m_numberOfNodes; j++)
                        {
                            double k = futPx + (j - half) * dK;
                            // Обычно здесь будет лежать сплайн от замороженной улыбки...
                            double sigma;
                            if ((!oldInfo.ContinuousFunction.TryGetValue(k, out sigma)) || Double.IsNaN(sigma))
                            {
                                string msg = String.Format("[DEBUG:{0}] Unable to get IV for strike:{1}. Please, try to decrease NodeStep.",
                                    GetType().Name, k);
                                m_context.Log(msg, MessageType.Warning, true);
                                return res;
                            }

                            xs.Add(k);
                            ys.Add(sigma);
                        }
                    }
                    #endregion 1. Готовлю сплайн
                }
                else
                {
                    // Здесь обязательно в начале
                    //res.ControlPoints.Clear();
                    res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(new InteractiveObject[0]);

                    int half = m_numberOfNodes / 2; // Целочисленное деление!

                    #region 2. Готовлю сплайн
                    {
                        double mult = m_nodeStep * ivAtm * Math.Sqrt(dT);
                        double dK = futPx * (Math.Exp(mult) - 1);
                        // Сдвигаю точки, чтобы избежать отрицательных значений
                        while ((futPx - half * dK) <= Double.Epsilon)
                            half--;

                        // внутренние узлы...
                        for (int j = 0; j < m_numberOfNodes; j++)
                        {
                            double k = futPx + (j - half) * dK;
                            //// Обычно здесь будет лежать сплайн от замороженной улыбки...
                            //double sigma = oldInfo.ContinuousFunction.Value(k);
                            double sigma = visibleNodes[j].Anchor.ValueY;

                            xs.Add(k);
                            ys.Add(sigma);
                        }
                    }
                    #endregion 2. Готовлю сплайн
                }

                try
                {
                    if (xs.Count >= BaseCubicSpline.MinNumberOfNodes)
                    {
                        NotAKnotCubicSpline spline = new NotAKnotCubicSpline(xs, ys);

                        info.ContinuousFunction = spline;
                        info.ContinuousFunctionD1 = spline.DeriveD1();

                        //info.F = F;
                        //info.dT = trueTimeToExpiry;
                        res.Tag = info;
                    }
                }
                catch (Exception ex)
                {
                    m_context.Log(ex.ToString(), MessageType.Error, true);
                    return Constants.EmptySeries;
                }

                // 2. Формирую кривую с более плотным шагом
                List<InteractiveObject> controlPoints = new List<InteractiveObject>();
                int editableNodesCount = FillEditableCurve(info, controlPoints, xs, dragMode);
                res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

                if (editableNodesCount != m_numberOfNodes)
                {
                    string msg = String.Format("[DEBUG:{0}] {1} nodes requested, but only {2} were prepared.",
                        GetType().Name,
                        m_numberOfNodes,
                        editableNodesCount);
                    m_context.Log(msg, MessageType.Warning, true);
                }
            }

            if (!m_resetSmile)
            {
                if (m_loadSplineCoeffs)
                {
                }

                if (m_prepareSplineCoeffs)
                {
                    #region Prepare global spline
                    // Надо пересчитать сплайн в безразмерные коэффициенты

                    // Обновляю уровень IV ATM?
                    ivAtm = info.ContinuousFunction.Value(futPx);

                    SmileInfo globInfo = new SmileInfo();
                    globInfo.F = futPx;
                    globInfo.dT = trueTimeToExpiry;
                    globInfo.RiskFreeRate = oldInfo.RiskFreeRate;

                    StringBuilder sb = new StringBuilder(GlobalSmileKey);
                    sb.AppendLine();
                    sb.AppendFormat(CultureInfo.InvariantCulture, "F:{0}", futPx);
                    sb.AppendLine();
                    sb.AppendFormat(CultureInfo.InvariantCulture, "dT:{0}", dT);
                    sb.AppendLine();
                    sb.AppendFormat(CultureInfo.InvariantCulture, "IvAtm:{0}", ivAtm);
                    sb.AppendLine();
                    sb.AppendFormat(CultureInfo.InvariantCulture, "RiskFreeRate:{0}", globInfo.RiskFreeRate);
                    sb.AppendLine();
                    sb.AppendFormat(CultureInfo.InvariantCulture, "ShapePct:{0}", ShapePct);
                    sb.AppendLine();
                    sb.AppendLine("~~~");
                    sb.AppendFormat(CultureInfo.InvariantCulture, "X;Y");
                    sb.AppendLine();

                    //LogSimmetrizeFunc logSimmFunc = new LogSimmetrizeFunc(info.ContinuousFunction, F, 0.5);

                    List<double> xs = new List<double>();
                    List<double> ys = new List<double>();
                    foreach (InteractiveObject oldObj in res.ControlPoints)
                    {
                        if (!oldObj.AnchorIsActive)
                            continue;

                        double k = oldObj.Anchor.ValueX;
                        double x = Math.Log(k / futPx) / Math.Pow(dT, DefaultPow + m_shape) / ivAtm;

                        double sigma = oldObj.Anchor.ValueY;
                        double sigmaNormalized = sigma / ivAtm;

                        xs.Add(x);
                        ys.Add(sigmaNormalized);

                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0};{1}", x, sigmaNormalized);
                        sb.AppendLine();
                    }

                    try
                    {
                        NotAKnotCubicSpline globSpline = new NotAKnotCubicSpline(xs, ys);

                        globInfo.ContinuousFunction = globSpline;
                        globInfo.ContinuousFunctionD1 = globSpline.DeriveD1();

                        //global.Tag = globInfo;

                        m_context.StoreGlobalObject(GlobalSmileKey, globInfo, true);

                        string msg = String.Format("[{0}] The globInfo was saved in Global cache as '{1}'.",
                            GetType().Name, GlobalSmileKey);
                        m_context.Log(msg, MessageType.Warning, true);

                        msg = String.Format("[{0}] The globInfo was saved in file tslab.log also. Smile:\r\n{1}",
                            GetType().Name, sb);
                        m_context.Log(msg, MessageType.Info, true);

                        // Запись в клипбоард
                        try
                        {
                            //Thread thread = ThreadProfiler.Create(() => System.Windows.Clipboard.SetText(sb.ToString()));
                            XElement xel = globInfo.ToXElement();
                            string xelStr =
@"<?xml version=""1.0""?>
" + xel.AsString();
                            // PROD-5987 - Отключаю работу с клипбордом. Только пишу в tslab.log
                            //Thread thread = ThreadProfiler.Create(() => System.Windows.Clipboard.SetText(xelStr));
                            //thread.SetApartmentState(ApartmentState.STA); //Set the thread to STA
                            //thread.Start();
                            //thread.Join(); // Надо ли делать Join?

                            s_log.WarnFormat("Global smile info:\r\n\r\n{0}\r\n\r\n", xelStr);
                        }
                        catch (Exception clipEx)
                        {
                            m_context.Log(clipEx.ToString(), MessageType.Error, true);
                        }

                        m_context.Recalc();
                    }
                    catch (Exception ex)
                    {
                        m_context.Log(ex.ToString(), MessageType.Error, true);
                        //return Constants.EmptySeries;
                    }
                    #endregion Prepare global spline
                }
                else if (m_pasteGlobal)
                {
                    // PROD-5987 - Работа с клипбордом отключена. Функция вставки сейчас работать не будет.
                    m_context.Log($"[{GetType().Name}] Clipboard is not available. Sorry.", MessageType.Warning, true);

                    #region Paste spline from clipboard
                    //string xelStr = "";
                    //// Чтение из клипбоард
                    //try
                    //{
                    //    // PROD-5987 - Работа с клипбордом отключена. Функция вставки сейчас работать не будет.
                    //    ////Thread thread = ThreadProfiler.Create(() => System.Windows.Clipboard.SetText(sb.ToString()));
                    //    //Thread thread = ThreadProfiler.Create(() => xelStr = System.Windows.Clipboard.GetText());
                    //    //thread.SetApartmentState(ApartmentState.STA); //Set the thread to STA
                    //    //thread.Start();
                    //    //thread.Join(); // Надо ли делать Join?

                    //    if (!String.IsNullOrWhiteSpace(xelStr))
                    //    {
                    //        XDocument xDoc = XDocument.Parse(xelStr);
                    //        XElement xInfo = xDoc.Root;
                    //        SmileInfo templateSmile = SmileInfo.FromXElement(xInfo);

                    //        // Обновляю уровень IV ATM?
                    //        // TODO: перепроверить как работает редактирование шаблона
                    //        ivAtm = info.ContinuousFunction.Value(futPx);
                    //        if (Double.IsNaN(ivAtm))
                    //        {
                    //            ivAtm = refSmileInfo.ContinuousFunction.Value(futPx);

                    //            m_context.Log(String.Format("[DEBUG:{0}] ivAtm was NaN. I'll use value ivAtm:{1}", GetType().Name, ivAtm), MessageType.Warning, true);

                    //            if (Double.IsNaN(ivAtm))
                    //            {
                    //                throw new Exception(String.Format("[DEBUG:{0}] ivAtm is NaN.", GetType().Name));
                    //            }
                    //        }

                    //        templateSmile.F = futPx;
                    //        templateSmile.dT = trueTimeToExpiry;
                    //        templateSmile.RiskFreeRate = oldInfo.RiskFreeRate;

                    //        // Здесь обязательно в начале
                    //        //res.ControlPoints.Clear();
                    //        res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(new InteractiveObject[0]);

                    //        SmileFunction5 func = new SmileFunction5(templateSmile.ContinuousFunction, templateSmile.ContinuousFunctionD1,
                    //            ivAtm, 0, 0, futPx, dT);
                    //        info.ContinuousFunction = func;
                    //        info.ContinuousFunctionD1 = func.DeriveD1();

                    //        // info уже лежит в Tag, поэтому при следующем пересчете сплайн уже должен пересчитаться
                    //        // по новой улыбке. Правильно?

                    //        string msg = String.Format("[DEBUG:{0}] SmileInfo was loaded from clipboard.", GetType().Name);
                    //        m_context.Log(msg, MessageType.Warning, true);

                    //        m_context.Recalc();
                    //    }
                    //}
                    //catch (Exception clipEx)
                    //{
                    //    m_context.Log(clipEx.ToString(), MessageType.Error, true);
                    //}
                    #endregion Paste spline from clipboard
                }
            }

            //res.ClickEvent -= res_ClickEvent;
            //res.ClickEvent += res_ClickEvent;
            //res.DragEvent -= res_DragEvent;
            //res.DragEvent += res_DragEvent;
            res.EndDragEvent -= res_EndDragEvent;
            res.EndDragEvent += res_EndDragEvent;

            return res;
        }

        private int FillEditableCurve(SmileInfo info, List<InteractiveObject> controlPoints, List<double> xs, DragableMode dragMode)
        {
            if (xs.Count < 2)
            {
                string msg = String.Format("[DEBUG:{0}] Not enough points in argument xs. xs.Count:{0}", xs.Count);
                m_context.Log(msg, MessageType.Warning, true);
                return 0;
            }

            int j = 0;
            double k = xs[0];
            double strikeStep = (xs[xs.Count - 1] - xs[0]) / (xs.Count - 1);
            while (k <= xs[xs.Count - 1])
            {
                if ((j < xs.Count) &&
                    (k <= xs[j]) && (xs[j] < k + strikeStep))
                {
                    #region Узел должен быть показан и доступен для редактирования
                    {
                        double sigma = info.ContinuousFunction.Value(xs[j]);

                        InteractivePointActive ip = new InteractivePointActive(xs[j], sigma);
                        ip.IsActive = true;
                        ip.Geometry = Geometries.Ellipse;
                        ip.DragableMode = dragMode;
                        ip.Color = AlphaColors.GreenYellow;
                        ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}", xs[j], Constants.PctMult * sigma);

                        InteractiveObject obj = new InteractiveObject(ip);

                        controlPoints.Add(obj);
                    }
                    #endregion Узел должен быть показан и доступен для редактирования

                    j++;
                }
                else
                {
                    #region Просто точка без маркера
                    {
                        double sigma = info.ContinuousFunction.Value(k);

                        //InteractivePointActive ip = new InteractivePointActive(k, sigma);
                        //ip.IsActive = false;
                        //ip.Geometry = Geometries.Ellipse;
                        //ip.DragableMode = DragableMode.None;
                        //ip.Color = System.Windows.Media.Colors.Green;
                        //ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}", k, Constants.PctMult * sigma);

                        InteractivePointLight ip = new InteractivePointLight(k, sigma);
                        InteractiveObject obj = new InteractiveObject(ip);

                        controlPoints.Add(obj);
                    }
                    #endregion Просто точка без маркера
                }

                k += strikeStep;
            }

            return j;
        }

        private void res_EndDragEvent(object sender, InteractiveActionEventArgs e)
        {
            InteractiveSeries oldLine = sender as InteractiveSeries;
            if (oldLine != null)
            {
                //SmileInfo info = oldLine.GetTag<SmileInfo>();
                //if (info != null)
                //{
                //    List<double> xs = new List<double>();
                //    List<double> ys = new List<double>();

                //    foreach (InteractiveObject oldObj in oldLine.ControlPoints)
                //    {
                //        if (!(oldObj.Anchor.IsVisible ?? oldObj.AnchorGraphPointData.IsVisible))
                //            continue;

                //        double k = oldObj.Anchor.ValueX;
                //        double sigma = oldObj.Anchor.ValueY;

                //        xs.Add(k);
                //        ys.Add(sigma);
                //    }

                //    try
                //    {
                //        NotAKnotCubicSpline spline = new NotAKnotCubicSpline(xs, ys);

                //        info.ContinuousFunction = spline;
                //        info.ContinuousFunctionD1 = spline.DeriveD1();

                //        oldLine.Tag = info;
                //    }
                //    catch (Exception ex)
                //    {
                //        m_context.Log(ex.ToString(), MessageType.Error, true);
                //        //return Constants.EmptySeries;
                //    }
                //}

                // Сразу отписываюсь!
                oldLine.EndDragEvent -= res_EndDragEvent;
            }

            m_context.Recalc();
        }

        public void Dispose()
        {
            if (m_context != null)
            {
                InteractiveSeries oldLine = m_context.LoadObject(m_frozenSmileId) as InteractiveSeries;
                if (oldLine != null)
                {
                    // Просто выполняю отписку от старых событий?
                    oldLine.EndDragEvent -= res_EndDragEvent;
                }
            }
        }
    }
}
