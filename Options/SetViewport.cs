using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Windows;

using TSLab.DataSource;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Configure chart visible region: ranges of axes, grid step, etc.
    /// \~russian Блок для настройки видимой области графика -- диапазоны по осям, шаг сетки и т.п.
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Set Viewport", Language = Constants.En)]
    [HelperName("Настройка графика", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = "Fut Px")]
    [Input(1, TemplateTypes.DOUBLE, Name = "dT")]
    [Input(2, TemplateTypes.DOUBLE | TemplateTypes.INTERACTIVESPLINE | TemplateTypes.OPTION_SERIES, Name = "Option Series or Sigma or Smile")]
    [Input(3, TemplateTypes.CANVASPANE, Name = "Canvas pane")]
    [OutputsCount(0)]
    [Description("Блок для настройки видимой области графика -- диапазоны по осям, шаг сетки и т.п.")]
    [HelperDescription("Configure chart visible region: ranges of axes, grid step, etc.", Constants.En)]
    public sealed class SetViewport : BaseCanvasDrawing, ICanvasPaneHandler, IValuesHandlerWithNumber
    {
        private const string KeySuffix = "_ViewportSize";

        /// <summary>
        /// При работе с Эксанте и прочим Западом Биржевой улыбки не будет.
        /// Поэтому надо просто использовать запасной вариант, чтобы не заморачиваться с построением улыбки по котировкам.
        /// </summary>
        private const double DefaultSigma = 0.3;
        /// <summary>
        /// При работе с Эксанте и прочим Западом Биржевой улыбки не будет.
        /// Поэтому надо просто использовать запасной вариант, чтобы не заморачиваться с построением улыбки по котировкам.
        /// </summary>
        private const double DefaultSigmaEs = 0.1;
        /// <summary>
        /// При работе с Дерибит Биржевой улыбки не будет.
        /// Поэтому надо просто использовать запасной вариант, чтобы не заморачиваться с построением улыбки по котировкам.
        /// </summary>
        private const double DefaultSigmaDeribit = 1.0;

        private double m_verticalMultiplier = 1.8;

        private double m_xAxisDivisor = 1000;
        private double m_xAxisGridStep = 5000;
        private double m_yAxisDivisor = 1;
        private double m_yAxisGridStep = 0.2;

        public SetViewport()
        {
            // TODO: прокинуть через АПИ настройки грида оси Y?
            ManageYGridStep = false;
        }

        #region Parameters
        /// <summary>
        /// \~english Manage horizontal axis
        /// \~russian При true будет управлять горизонтальной осью
        /// </summary>
        [HelperName("Manage X", Constants.En)]
        [HelperName("Ось X", Constants.Ru)]
        [Description("При true будет управлять горизонтальной осью")]
        [HelperDescription("Manage horizontal axis", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "true")]
        public bool ManageX { get; set; }

        /// <summary>
        /// \~english Manage vertical axis
        /// \~russian При true будет управлять вертикальной осью
        /// </summary>
        [HelperName("Manage Y", Constants.En)]
        [HelperName("Ось Y", Constants.Ru)]
        [Description("При true будет управлять вертикальной осью")]
        [HelperDescription("Manage vertical axis", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "false")]
        public bool ManageY { get; set; }

        /// <summary>
        /// \~english Manage horizontal axis grid step
        /// \~russian При true будет управлять расстоянием между вертикальными линиями грида
        /// </summary>
        [HelperName("Manage X grid step", Constants.En)]
        [HelperName("Сетка оси X", Constants.Ru)]
        [Description("При true будет управлять расстоянием между вертикальными линиями грида")]
        [HelperDescription("Manage horizontal axis grid step", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "false")]
        public bool ManageXGridStep { get; set; }

        /// <summary>
        /// \~english Manage vertical axis grid step
        /// \~russian При true будет управлять расстоянием между горизонтальными линиями грида
        /// </summary>
        [HelperName("Manage Y grid step", Constants.En)]
        [HelperName("Сетка оси Y", Constants.Ru)]
        [Description("При true будет управлять расстоянием между горизонтальными линиями грида")]
        [HelperDescription("Manage vertical axis grid step", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "false")]
        private bool ManageYGridStep { get; set; }

        /// <summary>
        /// \~english Multiplier to estimate viewport height
        /// \~russian Множитель для подстраивания вертикального диапазона
        /// </summary>
        [HelperName("Height Multiplier", Constants.En)]
        [HelperName("Множитель высоты", Constants.Ru)]
        [Description("Множитель для подстраивания вертикального диапазона")]
        [HelperDescription("Multiplier to estimate viewport height", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "1.8", Min = "0.001", Max = "10000000", Step = "1")]
        public double VerticalMultiplier
        {
            get { return m_verticalMultiplier; }
            set
            {
                if (value > 0)
                    m_verticalMultiplier = value;
            }
        }

        /// <summary>
        /// \~english X axis grid step
        /// \~russian Шаг линий сетки вдоль оси X
        /// </summary>
        [HelperName("X axis step", Constants.En)]
        [HelperName("Шаг сетки оси X", Constants.Ru)]
        [Description("Шаг линий сетки вдоль оси X")]
        [HelperDescription("X axis grid step", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "5000", Min = "0.000000001", Max = "10000000", Step = "1")]
        public double XAxisStep
        {
            get { return m_xAxisGridStep; }
            set
            {
                if (value > 0)
                    m_xAxisGridStep = value;
            }
        }

        /// <summary>
        /// \~english X axis divisor
        /// \~russian Делитель маркеров сетки оси X
        /// </summary>
        [HelperName("X axis divisor", Constants.En)]
        [HelperName("Делитель сетки оси X", Constants.Ru)]
        [Description("Делитель маркеров сетки оси X")]
        [HelperDescription("X axis divisor", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "1000", Min = "0.000000001", Max = "10000000", Step = "1")]
        public double XAxisDivisor
        {
            get { return m_xAxisDivisor; }
            set
            {
                if (value > 0)
                    m_xAxisDivisor = value;
            }
        }

        /// <summary>
        /// \~english Y axis grid step
        /// \~russian Шаг линий сетки вдоль оси Y
        /// </summary>
        [HelperName("Y Axis Step", Constants.En)]
        [HelperName("Шаг сетки оси Y", Constants.Ru)]
        [Description("Шаг линий сетки вдоль оси Y")]
        [HelperDescription("Y axis grid step", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0.2", Min = "0.000000001", Max = "10000000", Step = "0.1", NumberDecimalDigits = 2)]
        private double YAxisStep
        {
            get { return m_yAxisGridStep; }
            set
            {
                if (value > 0)
                    m_yAxisGridStep = value;
            }
        }

        /// <summary>
        /// \~english Y axis divisor
        /// \~russian Делитель маркеров сетки оси Y
        /// </summary>
        [HelperName("Y axis divisor", Constants.En)]
        [HelperName("Делитель сетки оси Y", Constants.Ru)]
        [Description("Делитель маркеров сетки оси Y")]
        [HelperDescription("Y axis divisor", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "1", Min = "0.000000001", Max = "10000000", Step = "1")]
        private double YAxisDivisor
        {
            get { return m_yAxisDivisor; }
            set
            {
                if (value > 0)
                    m_yAxisDivisor = value;
            }
        }

        /// <summary>
        /// \~english Apply visual settings
        /// \~russian Принудительное применение визуальных настроек
        /// </summary>
        [HelperName("Apply settings", Constants.En)]
        [HelperName("Применить настройки", Constants.Ru)]
        [Description("Принудительное применение визуальных настроек")]
        [HelperDescription("Apply visual settings", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool ApplyVisualSettings { get; set; }
        #endregion Parameters

        #region Управление основной панелью
        public void Execute(double price, double time, double sigma, ICanvasPane pane, int barNum)
        {
            int barsCount = ContextBarsCount;
            if (barNum < barsCount - 1)
                return;

            // PROD-3577
            pane.FeetToBorder2ByDefault = true;

            double futPx = price;
            double dT = time;
            //double sigma = sigmas[sigmas.Count - 1];

            if (!DoubleUtil.IsPositive(futPx))
                return;
            if (!DoubleUtil.IsPositive(dT))
                return;
            if (!DoubleUtil.IsPositive(sigma))
                return;

            if (pane != null)
            {
                Rect rect = PrepareVieportSettings(null, futPx, dT, sigma);
                ApplySettings(pane, rect);

                //if (ShouldWarmSecurities)
                //WarmSecurities(Context, rect);
            }
        }

        public void Execute(double price, double time, InteractiveSeries smile, ICanvasPane pane, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (smile == null) || (smile.Tag == null))
                return;

            // PROD-3577
            pane.FeetToBorder2ByDefault = true;

            SmileInfo smileInfo = smile.GetTag<SmileInfo>();
            if ((smileInfo == null) || (smileInfo.ContinuousFunction == null))
                return;

            double futPx = price;
            double dT = time;

            if (!DoubleUtil.IsPositive(futPx))
                return;
            if (!DoubleUtil.IsPositive(dT))
                return;

            double sigma;
            if ((!smileInfo.ContinuousFunction.TryGetValue(futPx, out sigma)) ||
                (!DoubleUtil.IsPositive(sigma)))
            {
                //При работе с Эксанте и прочим Западом Биржевой улыбки не будет
                //Поэтому надо иметь 'План Б': подставить фиксированную волатильность 30%!
                sigma = DefaultSigma;
            }

            if (pane != null)
            {
                string expiryStr = smileInfo.Expiry.ToString(TimeToExpiry.DateTimeFormat, CultureInfo.InvariantCulture);
                Rect rect = PrepareVieportSettings(expiryStr, futPx, dT, sigma);
                ApplySettings(pane, rect);

                int cpLen = smile.ControlPoints.Count;
                if (ManageXGridStep && (cpLen > 1))
                {
                    double dK = smile.ControlPoints[1].Anchor.ValueX - smile.ControlPoints[0].Anchor.ValueX;
                    if (cpLen > 2)
                    {
                        int t = cpLen / 2; // Делим нацело. Для cpLen==3 получаем 1
                        dK = smile.ControlPoints[t + 1].Anchor.ValueX - smile.ControlPoints[t].Anchor.ValueX;
                    }

                    pane.XAxisStep = GetXAxisStep(dK);
                    pane.XAxisDiviser = GetXAxisDivisor(dK);
                }
            }
        }

        public void Execute(double price, double time, IOptionSeries optSer, ICanvasPane pane, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return;

            // PROD-3577
            pane.FeetToBorder2ByDefault = true;

            double futPx = price;
            double dT = time;

            if (!DoubleUtil.IsPositive(futPx))
                return;
            if (!DoubleUtil.IsPositive(dT))
                return;

            double sigma;
            IFunction smileFunc = IvOnF.PrepareExchangeSmileSpline(optSer, Double.MinValue, Double.MaxValue);
            if ((smileFunc == null) || (!smileFunc.TryGetValue(futPx, out sigma)) ||
                (!DoubleUtil.IsPositive(sigma)))
            {
                //При работе с Эксанте и прочим Западом Биржевой улыбки не будет
                //Поэтому надо иметь 'План Б': подставить фиксированную волатильность 30%!
                sigma = DefaultSigma;

                // PROD-5968 - У биткойна совсем другой типичный уровень волатильности
                var parent = optSer.UnderlyingAsset;
                var secDesc = parent.SecurityDescription;
                var tp = secDesc.TradePlace;
                var dsClassName = tp?.DataSource?.GetType().Name;
                if (dsClassName == "DeribitDS")
                {
                    sigma = DefaultSigmaDeribit;
                }
                else if (dsClassName == "ExanteDataSource")
                {
                    if (tp.Id == "CME")
                    {
                        if (secDesc.ActiveType.IsFuture() && parent.Symbol.StartsWith("ES"))
                        {
                            sigma = DefaultSigmaEs;
                        }
                    }
                }
            }

            if (pane != null)
            {
                string expiryStr = optSer.ExpirationDate.ToString(TimeToExpiry.DateTimeFormat, CultureInfo.InvariantCulture);
                Rect rect = PrepareVieportSettings(expiryStr, futPx, dT, sigma);
                ApplySettings(pane, rect);

                if (ManageXGridStep)
                {
                    var pairs = optSer.GetStrikePairs().ToArray();
                    int pLen = pairs.Length;
                    if (pLen > 1)
                    {
                        double dK = pairs[1].Strike - pairs[0].Strike;
                        if (pLen > 2)
                        {
                            int t = pLen / 2; // Делим нацело. Для pLen==3 получаем 1
                            dK = pairs[t + 1].Strike - pairs[t].Strike;
                        }

                        pane.XAxisStep = GetXAxisStep(dK);
                        pane.XAxisDiviser = GetXAxisDivisor(dK);
                    }
                }
            }
        }
        #endregion Управление основной панелью

        /// <summary>
        /// Формирование ключа для поиска вьюпорта в локальном кеше
        /// </summary>
        /// <param name="handler">кубик управления -- он должен предоставить своё свойство VariableId</param>
        /// <param name="seriesExpiry">дата экспирации опционной серии (в крайнем случае можно передать null)</param>
        /// <returns>ключ для поиска в ГК</returns>
        public static string GetViewportCacheKey(SetViewport handler, string seriesExpiry)
        {
            string key = handler.VariableId + KeySuffix + "_" + (seriesExpiry ?? "NULL");
            return key;
        }

        /// <summary>
        /// Пытается угадать видимую область и если это получается результат складывает в локальный кеш
        /// </summary>
        private Rect PrepareVieportSettings(string seriesExpiry, double futPx, double dT, double sigma)
        {
            Rect rect;
            //string key = VariableId + KeySuffix + "_" + (seriesExpiry ?? "NULL");
            string key = GetViewportCacheKey(this, seriesExpiry);
            var container = Context.LoadObject(key) as NotClearableContainer<Rect>;
            // Проверка на ApplyVisualSettings нужна, чтобы безусловно поменять видимую область при нажатии на кнопку в UI
            if ((container != null) /*&& (container.Content != null)*/ && (!ApplyVisualSettings) &&
                DoubleUtil.IsPositive(container.Content.Width) && DoubleUtil.IsPositive(container.Content.Height))   // PROD-3901
                rect = container.Content;
            else
            {
                // PROD-5747 - Если контенер пуст, давайте сделаем в логе запись об этом?
                // Тогда будет понятно в какой момент он чистится.
                if ((container == null) /*|| (container.Content == null)*/ ||
                    (!DoubleUtil.IsPositive(futPx)) || (!DoubleUtil.IsPositive(sigma)))
                {
                    string expStr = seriesExpiry ?? "NULL";
                    string tradeName = (m_context.Runtime?.TradeName ?? "NULL").Replace(Constants.HtmlDot, ".");
                    string msg = String.Format(CultureInfo.InvariantCulture,
                        "[{0}.PrepareVieportSettings] Empty container. Key:'{1}'; futPx:{2}; dT:{3}; sigma:{4}; expiry:{5}; TradeName:'{6}'",
                        GetType().Name, key, futPx, dT, sigma, expStr, tradeName);
                    m_context.Log(msg, MessageType.Info, false);
                }
                else if ((container != null) /*&& (container.Content != null)*/ &&
                    ((!DoubleUtil.IsPositive(container.Content.Width)) ||
                     (!DoubleUtil.IsPositive(container.Content.Height))))
                {
                    string expStr = seriesExpiry ?? "NULL";
                    string tradeName = (m_context.Runtime?.TradeName ?? "NULL").Replace(Constants.HtmlDot, ".");
                    string msg = String.Format(CultureInfo.InvariantCulture,
                        "[{0}.PrepareVieportSettings] BAD RECT. Key:'{1}'; futPx:{2}; dT:{3}; sigma:{4}; expiry:{5}; TradeName:'{6}'; Width:{7}; Height:{8}",
                        GetType().Name, key, futPx, dT, sigma, expStr, tradeName, container.Content.Width, container.Content.Height);
                    m_context.Log(msg, MessageType.Info, false);
                }

                //// При самом первом запуске эмулирую нажатие кнопки Apply, чтобы заставить
                //// CanvasPane реагировать на мои настройки в Borders2.
                //ApplyVisualSettings = true;

                double width = (SigmaMult * sigma * Math.Sqrt(dT)) * futPx;
                // Общая ширина не менее 10% от futPx?
                //width = Math.Max(width, futPx * 0.05);

                double left = Math.Max(0, futPx - width);
                // Чтобы график был симметричен, ширину тоже подрезаю
                width = Math.Abs(futPx - left);
                double height = sigma * (m_verticalMultiplier - 1.0 / m_verticalMultiplier);
                rect = new Rect(futPx - width, sigma * m_verticalMultiplier, 2 * width, height);

                // PROD-5747 - Сохранять область в кеш можно только если прямоугольник имеет нормальные размеры
                if (DoubleUtil.IsPositive(rect.Width) && DoubleUtil.IsPositive(rect.Height))
                {
                    // При самом первом запуске эмулирую нажатие кнопки Apply, чтобы заставить
                    // CanvasPane реагировать на мои настройки в Borders2.
                    ApplyVisualSettings = true;

                    container = new NotClearableContainer<Rect>(rect);
                    Context.StoreObject(key, container);
                }
            }
            return rect;
        }

        /// <summary>
        /// Применяет настройки видимой области, переданные в аргументе rect
        /// </summary>
        /// <param name="rect">невалидные настройки игнорируются</param>
        private void ApplySettings(ICanvasPane pane, Rect rect)
        {
            // PROD-3577 - Если иы используем кубик SetViewport, то эта настройка всегда должна стоять
            pane.FeetToBorder2ByDefault = true;

            // PROD-5747 - Применяем только валидные настройки
            if ((pane == null) ||
                (!DoubleUtil.IsPositive(rect.Width)) || (!DoubleUtil.IsPositive(rect.Height)))
            {
                string tradeName = (m_context.Runtime?.TradeName ?? "NULL").Replace(Constants.HtmlDot, ".");
                string msg = String.Format(CultureInfo.InvariantCulture,
                    "[{0}.ApplySettings] BAD RECT. TradeName:'{1}'; Width:{2}; Height:{3}",
                    GetType().Name, tradeName, rect.Width, rect.Height);
                m_context.Log(msg, MessageType.Info, false);
                return;
            }

            if (ApplyVisualSettings)
            {
                // Флаг выставляется однократно по нажатию кнопки и потом сбрасывается, если нужно
                pane.FeetToBorder2 = true;
            }

            if (ManageX)
            {
                //pane.BorderX1 = rect.X;
                //pane.BorderX2 = rect.X + rect.Width;
                pane.Border2X1 = rect.X;
                pane.Border2X2 = rect.X + rect.Width;
            }

            if (ManageY)
            {
                pane.Border2Y1 = rect.Y - rect.Height;
                pane.Border2Y2 = rect.Y;
            }

            if (ManageXGridStep)
            {
                pane.XAxisStep = m_xAxisGridStep;
                pane.XAxisDiviser = m_xAxisDivisor;
            }

            if (ManageYGridStep)
            {
                // TODO: прокинуть через АПИ настройки грида оси Y?
                //pane.YA = m_yAxisGridStep;
                //pane.YAX = m_yAxisDivisor;
            }
        }

        /// <summary>
        /// Выбрать шаг сетки на основании расстояния между страйками
        /// </summary>
        /// <param name="dK">шаг между страйками</param>
        /// <returns>шаг сетки</returns>
        // ReSharper disable once MemberCanBePrivate.Global        
        public static double GetXAxisStep(double dK)
        {
            const double Epsilon = 1e-6;

            double res;
            if (DoubleUtil.AreClose(dK, 0.005)) // У ED шаг страйков 0.005
                res = 0.01;
            else if (dK <= 0.01 + Epsilon)
                res = dK;
            else if (DoubleUtil.AreClose(dK, 0.5)) // У SV шаг страйков 0.5
                res = 1;
            else if (dK <= 1 + Epsilon) // У BR шаг страйков 1
                res = dK;
            else if (dK <= 10 + Epsilon)  // У GOLD шаг страйков 10
                res = dK;
            else if (dK <= 90 + Epsilon) // У ДоуДжонса шаг страйков 50
                res = 50;
            else if (dK <= 100 + Epsilon) // Если у кого-то шаг 100, то так и надо рисовать грид
                res = 100;
            else if (DoubleUtil.AreClose(dK, 250)) // У Si, GZ, SR шаг страйков 250
                res = 1000;
            else if (DoubleUtil.AreClose(dK, 500)) // У LK, RN шаг страйков 500
                res = 1000;
            else if (dK <= 1000 + Epsilon)
                res = dK;
            else if (DoubleUtil.AreClose(dK, 2500)) // У RI шаг страйков бывает 2500
                res = 5000;
            else if (DoubleUtil.AreClose(dK, 5000)) // У RI шаг страйков бывает 5000
                res = 5000;
            else if (dK <= 10000 + Epsilon)
                res = dK;
            else
                res = 10000;

            return res;
        }

        /// <summary>
        /// Выбрать делитель сетки на основании расстояния между страйками
        /// </summary>
        /// <param name="dK">шаг между страйками</param>
        /// <returns>делитель</returns>
        // ReSharper disable once MemberCanBePrivate.Global        
        public static double GetXAxisDivisor(double dK)
        {
            const double Epsilon = 1e-6;

            double res;
            if (dK <= 100 + Epsilon)
                res = 1;
            else if (DoubleUtil.AreClose(dK, 250)) // У Si, GZ, SR шаг страйков 250
                res = 1000;
            else if (DoubleUtil.AreClose(dK, 500)) // У LK, RN шаг страйков 500
                res = 1000;
            else if (dK <= 1000 + Epsilon)
                res = 1000;
            else if (DoubleUtil.AreClose(dK, 2500)) // У RI шаг страйков бывает 2500
                res = 1000;
            else if (DoubleUtil.AreClose(dK, 5000)) // У RI шаг страйков бывает 5000
                res = 1000;
            else if (dK <= 1000000 + Epsilon)
                res = 1000;
            else
                res = 1000000;

            return res;
        }
    }
}