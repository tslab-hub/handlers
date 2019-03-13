using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using TSLab.DataSource;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Exchange smile rescaled to our internal time-to-expiry
    /// \~russian Биржевая улыбка, пересчитанная в наше внутреннее время
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Exchange Smile (Rescaled)", Language = Constants.En)]
    [HelperName("Биржевая улыбка (приведенная)", Language = Constants.Ru)]
    [InputsCount(4)]
    [Input(0, TemplateTypes.DOUBLE, Name = Constants.FutPx)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.Time)]
    [Input(2, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(3, TemplateTypes.DOUBLE, Name = Constants.RiskFreeRate)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Биржевая улыбка, пересчитанная в наше внутреннее время")]
    [HelperDescription("Exchange smile rescaled to our internal time-to-expiry", Constants.En)]
    public class ExchangeTheorSigma5 : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private bool m_rescaleTime = true;
        private StrikeType m_optionType = StrikeType.Call;

        private TimeSpan m_expiryTime = TimeSpan.Parse(Constants.DefaultFortsExpiryTimeStr);
        private string m_expiryTimeStr = Constants.DefaultFortsExpiryTimeStr;

        #region Parameters
        /// <summary>
        /// \~english Rescale time-to-expiry to our internal?
        /// \~russian Заменять время на 'правильное'?
        /// </summary>
        [HelperName("Rescale Time", Constants.En)]
        [HelperName("Заменить время", Constants.Ru)]
        [Description("Заменять время на 'правильное'?")]
        [HelperDescription("Rescale time-to-expiry to our internal?", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "true")]
        public bool RescaleTime
        {
            get { return m_rescaleTime; }
            set { m_rescaleTime = value; }
        }

        /// <summary>
        /// \~english Option type (parameter Any is not recommended)
        /// \~russian Тип опционов (использование типа Any может привести к неожиданному поведению)
        /// </summary>
        [HelperName("Option Type", Constants.En)]
        [HelperName("Вид опционов", Constants.Ru)]
        [Description("Тип опционов (использование типа Any может привести к неожиданному поведению)")]
        [HelperDescription("Option type (parameter Any is not recommended)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = "Call")]
        public StrikeType OptionType
        {
            get { return m_optionType; }
            set { m_optionType = value; }
        }

        ///// <summary>
        ///// \~english Price multiplier
        ///// \~russian Мультипликатор цен
        ///// </summary>
        //[HelperParameterName("Multiplier", Constants.En)]
        //[HelperParameterName("Мультипликатор", Constants.Ru)]
        //[Description("Мультипликатор цен")]
        //[HelperDescription("Price multiplier", Language = Constants.En)]
        //[HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
        //    Default = "1", Min = "-1000000", Max = "1000000", Step = "1")]
        //public double MultiplierPx
        //{
        //    get { return m_multPx; }
        //    set { m_multPx = value; }
        //}

        ///// <summary>
        ///// \~english Price shift (price steps)
        ///// \~russian Сдвиг цен (в шагах цены)
        ///// </summary>
        //[HelperParameterName("Shift", Constants.En)]
        //[HelperParameterName("Сдвиг цен", Constants.Ru)]
        //[Description("Сдвиг цен (в шагах цены)")]
        //[HelperDescription("Price shift (price steps)", Language = Constants.En)]
        //[HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
        //    Default = "0", Min = "-1000000", Max = "1000000", Step = "1")]
        //public double ShiftPx
        //{
        //    get { return m_shiftPx; }
        //    set { m_shiftPx = value; }
        //}

        /// <summary>
        /// \~english Exact expiration time of day (HH:mm)
        /// \~russian Точное время экспирации (ЧЧ:мм)
        /// </summary>
        [HelperName("Expiry Time", Constants.En)]
        [HelperName("Время истечения", Constants.Ru)]
        [Description("Точное время экспирации (HH:mm)")]
        [HelperDescription("Exact expiration time of day (HH:mm)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = Constants.DefaultFortsExpiryTimeStr, Name = "Expiry Time")]
        public string ExpiryTime
        {
            get { return m_expiryTimeStr; }
            set
            {
                TimeSpan tmp;
                if (TimeSpan.TryParse(value, out tmp))
                {
                    m_expiryTimeStr = value;
                    m_expiryTime = tmp;
                }
            }
        }
        #endregion Parameters

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES
        /// </summary>
        public InteractiveSeries Execute(double price, double trueTimeToExpiry, IOptionSeries optSer, int barNum)
        {
            if (optSer == null)
                return Constants.EmptySeries;

            InteractiveSeries res = Execute(price, trueTimeToExpiry, optSer, 0, barNum);
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES
        /// </summary>
        public InteractiveSeries Execute(double price, double trueTimeToExpiry, IOptionSeries optSer, double ratePct, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return Constants.EmptySeries;

            // PROD-5952 - Не надо дергать стакан без нужды
            //optSer.UnderlyingAsset.UpdateQueueData();
            FinInfo bSecFinInfo = optSer.UnderlyingAsset.FinInfo;
            if (bSecFinInfo.LastPrice == null)
                return Constants.EmptySeries;

            double futPx = price;
            // ФОРТС использует плоское календарное время
            DateTime optExpiry = optSer.ExpirationDate.Date.Add(m_expiryTime);
            double dT = (optExpiry - bSecFinInfo.LastUpdate).TotalYears();
            if (Double.IsNaN(dT) || (dT < Double.Epsilon))
            {
                // [{0}] Time to expiry must be positive value. dT:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.TimeMustBePositive", GetType().Name, dT);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (Double.IsNaN(futPx) || (futPx < Double.Epsilon))
            {
                // [{0}] Base asset price must be positive value. F:{1}
                string msg = RM.GetStringFormat("OptHandlerMsg.FutPxMustBePositive", GetType().Name, futPx);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (Double.IsNaN(trueTimeToExpiry) || (trueTimeToExpiry < Double.Epsilon))
            {
                string msg = String.Format("[{0}] trueTimeToExpiry must be positive value. dT:{1}", GetType().Name, trueTimeToExpiry);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }

            if (Double.IsNaN(ratePct))
                //throw new ScriptException("Argument 'ratePct' contains NaN for some strange reason. rate:" + rate);
                return Constants.EmptySeries;

            double effectiveTime = m_rescaleTime ? trueTimeToExpiry : dT;

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            IOptionStrikePair[] pairs = (from pair in optSer.GetStrikePairs()
                                         //orderby pair.Strike ascending -- уже отсортировано!
                                         select pair).ToArray();

            if (pairs.Length < 2)
            {
                string msg = String.Format("[{0}] optSer must contain few strike pairs. pairs.Length:{1}", GetType().Name, pairs.Length);
                m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptySeries;
            }

            for (int j = 0; j < pairs.Length; j++)
            {
                //bool showPoint = true;
                IOptionStrikePair sInfo = pairs[j];
                double k = sInfo.Strike;
                //// Сверхдалекие страйки игнорируем
                //if ((k < m_minStrike) || (m_maxStrike < k))
                //{
                //    showPoint = false;
                //}

                if ((sInfo.PutFinInfo == null) || (sInfo.CallFinInfo == null) ||
                    (!sInfo.PutFinInfo.TheoreticalPrice.HasValue) || (!sInfo.PutFinInfo.Volatility.HasValue) ||
                    (sInfo.PutFinInfo.TheoreticalPrice.Value <= 0) || (sInfo.PutFinInfo.Volatility.Value <= 0) ||
                    (!sInfo.CallFinInfo.TheoreticalPrice.HasValue) || (!sInfo.CallFinInfo.Volatility.HasValue) ||
                    (sInfo.CallFinInfo.TheoreticalPrice.Value <= 0) || (sInfo.CallFinInfo.Volatility.Value <= 0))
                    continue;

                // Биржа шлет несогласованную улыбку
                //double virtualExchangeF = sInfo.CallFinInfo.TheoreticalPrice.Value - sInfo.PutFinInfo.TheoreticalPrice.Value + sInfo.Strike;
                if ((m_optionType == StrikeType.Any) || (m_optionType == StrikeType.Put))
                {
                    double optSigma = sInfo.PutFinInfo.Volatility.Value;
                    if (m_rescaleTime)
                    {
                        // optSigma = FinMath.GetOptionSigma(futPx, k, effectiveTime, optPx, 0, false);
                        optSigma = FinMath.RescaleIvToAnotherTime(dT, optSigma, trueTimeToExpiry);
                    }

                    double optPx = sInfo.PutFinInfo.TheoreticalPrice ?? Double.NaN;

                    //// ReSharper disable once UseObjectOrCollectionInitializer
                    //InteractivePointActive ip = new InteractivePointActive(k, optSigma);
                    //ip.IsActive = m_showNodes;
                    //ip.Geometry = Geometries.Ellipse;
                    //ip.Color = System.Windows.Media.Colors.Cyan;
                    //ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}; Px:{2}\r\ndT:{3:0.000}; Date:{4}",
                    //    k, Constants.PctMult * optSigma, optPx,
                    //    FixedValue.ConvertToDisplayUnits(FixedValueMode.YearsAsDays, effectiveTime),
                    //    bSecFinInfo.LastUpdate.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture));

                    if (optSigma > 0)
                    {
                        // Это условие позволяет не вставлять точки с совпадающими абсциссами
                        if ((xs.Count <= 0) ||
                            (!DoubleUtil.AreClose(k, xs[xs.Count - 1])))
                        {
                            xs.Add(k);
                            ys.Add(optSigma);
                        }
                    }
                }

                if ((m_optionType == StrikeType.Any) || (m_optionType == StrikeType.Call))
                {
                    double optSigma = sInfo.CallFinInfo.Volatility.Value;
                    if (m_rescaleTime)
                    {
                        // optSigma = FinMath.GetOptionSigma(futPx, k, effectiveTime, optPx, 0, false);
                        optSigma = FinMath.RescaleIvToAnotherTime(dT, optSigma, trueTimeToExpiry);
                    }

                    double optPx = sInfo.CallFinInfo.TheoreticalPrice ?? Double.NaN;

                    //// ReSharper disable once UseObjectOrCollectionInitializer
                    //InteractivePointActive ip = new InteractivePointActive(k, optSigma);
                    //ip.IsActive = m_showNodes;
                    //ip.Geometry = Geometries.Ellipse;
                    //ip.Color = System.Windows.Media.Colors.Cyan;
                    //ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}; Px:{2}\r\ndT:{3:0.000}; Date:{4}",
                    //    k, Constants.PctMult * optSigma, optPx,
                    //    FixedValue.ConvertToDisplayUnits(FixedValueMode.YearsAsDays, effectiveTime),
                    //    bSecFinInfo.LastUpdate.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture));

                    if (optSigma > 0)
                    {
                        // Это условие позволяет не вставлять точки с совпадающими абсциссами
                        if ((xs.Count <= 0) ||
                            (!DoubleUtil.AreClose(k, xs[xs.Count - 1])))
                        {
                            xs.Add(k);
                            ys.Add(optSigma);
                        }
                    }
                }
            }

            #region 3. Строим сплайн по биржевой улыбке с узлами в страйках
            NotAKnotCubicSpline spline = null, splineD1 = null;
            try
            {
                if (xs.Count >= BaseCubicSpline.MinNumberOfNodes)
                {
                    spline = new NotAKnotCubicSpline(xs, ys);
                    splineD1 = spline.DeriveD1();
                }
                else
                    return Constants.EmptySeries;
            }
            catch (Exception ex)
            {
                string msg = String.Format("bSecFinInfo.LastUpdate:{0}; Bars.Last.Date:{1}\r\n\r\nException:{2}",
                    bSecFinInfo.LastUpdate, optSer.UnderlyingAsset.Bars[optSer.UnderlyingAsset.Bars.Count - 1].Date, ex);
                m_context.Log(msg, MessageType.Error, true);
                return Constants.EmptySeries;
            }
            #endregion 3. Строим сплайн по биржевой улыбке с узлами в страйках

            #region 5. С помощью сплайна уже можно строить гладкую улыбку
            double futStep = optSer.UnderlyingAsset.Tick;
            double dK = pairs[1].Strike - pairs[0].Strike;
            SortedDictionary<double, IOptionStrikePair> strikePrices;
            if (!SmileImitation5.TryPrepareImportantPoints(pairs, futPx, futStep, -1, out strikePrices))
            {
                string msg = String.Format("[WARNING:{0}] It looks like there is no suitable points for the smile. pairs.Length:{1}", GetType().Name, pairs.Length);
                m_context.Log(msg, MessageType.Warning, true);
                return Constants.EmptySeries;
            }

            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            //for (int j = 0; j < pairs.Length; j++)
            foreach (var kvp in strikePrices)
            {
                bool showPoint = (kvp.Value != null);
                double k = kvp.Key;
                double sigma;
                if (!spline.TryGetValue(k, out sigma))
                    continue;
                double vol = sigma;

                if (Double.IsNaN(sigma) || Double.IsInfinity(sigma) || (sigma < Double.Epsilon))
                    continue;

                InteractivePointLight ip;
                if (m_showNodes && showPoint)
                {
                    // Как правило, эта ветка вообще не используется,
                    // потому что я не смотрю узлы биржевой улыбки.
                    double optPx = Double.NaN;
                    // ReSharper disable once UseObjectOrCollectionInitializer
                    InteractivePointActive tip = new InteractivePointActive(k, vol);
                    tip.IsActive = m_showNodes && showPoint;
                    tip.Geometry = Geometries.Ellipse;
                    tip.Color = AlphaColors.Cyan;
                    tip.Tooltip = String.Format("K:{0}; IV:{1:P2}; Px:{2}\r\ndT:{3:0.000}; Date:{4}",
                        k, vol, optPx,
                        FixedValue.ConvertToDisplayUnits(FixedValueMode.YearsAsDays, effectiveTime),
                        bSecFinInfo.LastUpdate.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture));
                    ip = tip;
                }
                else
                    ip = new InteractivePointLight(k, vol);

                InteractiveObject obj = new InteractiveObject(ip);
                controlPoints.Add(obj);
            }
            #endregion 5. С помощью сплайна уже можно строить гладкую улыбку

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            var baseSec = optSer.UnderlyingAsset;
            DateTime scriptTime = baseSec.Bars[baseSec.Bars.Count - 1].Date;

            // ReSharper disable once UseObjectOrCollectionInitializer
            SmileInfo info = new SmileInfo();
            info.F = futPx;
            info.dT = effectiveTime;
            info.Expiry = optSer.ExpirationDate;
            info.ScriptTime = scriptTime;
            info.RiskFreeRate = ratePct;
            info.BaseTicker = baseSec.Symbol;

            info.ContinuousFunction = spline;
            info.ContinuousFunctionD1 = splineD1;

            res.Tag = info;

            return res;
        }
    }
}
