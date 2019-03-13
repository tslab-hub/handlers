using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TSLab.DataSource;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Exchange smile (bar handler)
    /// \~russian Биржевая улыбка (побарный обработчик)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTick)]
    [HelperName("Exchange Smile", Language = Constants.En)]
    [HelperName("Биржевая улыбка", Language = Constants.Ru)]
    [InputsCount(2)]
    [Input(0, TemplateTypes.OPTION_SERIES, Name = Constants.OptionSeries)]
    [Input(1, TemplateTypes.DOUBLE, Name = Constants.RiskFreeRate)]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Биржевая улыбка (побарный обработчик)")]
    [HelperDescription("Exchange smile (bar handler)", Constants.En)]
    public class ExchangeTheorSigma3 : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private double m_multPx = 1, m_shiftPx = 0;
        private StrikeType m_optionType = StrikeType.Call;

        private TimeSpan m_expiryTime = TimeSpan.Parse(Constants.DefaultFortsExpiryTimeStr);
        private string m_expiryTimeStr = Constants.DefaultFortsExpiryTimeStr;

        #region Parameters
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

        /// <summary>
        /// \~english Price multiplier
        /// \~russian Мультипликатор цен
        /// </summary>
        [HelperName("Multiplier", Constants.En)]
        [HelperName("Мультипликатор", Constants.Ru)]
        [Description("Мультипликатор цен")]
        [HelperDescription("Price multiplier", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "1", Min = "-1000000", Max = "1000000", Step = "1")]
        public double MultiplierPx
        {
            get { return m_multPx; }
            set { m_multPx = value; }
        }

        /// <summary>
        /// \~english Price shift (price steps)
        /// \~russian Сдвиг цен (в шагах цены)
        /// </summary>
        [HelperName("Shift", Constants.En)]
        [HelperName("Сдвиг цен", Constants.Ru)]
        [Description("Сдвиг цен (в шагах цены)")]
        [HelperDescription("Price shift (price steps)", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true,
            Default = "0", Min = "-1000000", Max = "1000000", Step = "1")]
        public double ShiftPx
        {
            get { return m_shiftPx; }
            set { m_shiftPx = value; }
        }

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
        public InteractiveSeries Execute(IOptionSeries optSer, int barNum)
        {
            if (optSer == null)
                return Constants.EmptySeries;

            InteractiveSeries res = Execute(optSer, 0, barNum);
            return res;
        }

        /// <summary>
        /// Обработчик под тип входных данных OPTION_SERIES
        /// </summary>
        public InteractiveSeries Execute(IOptionSeries optSer, double ratePct, int barNum)
        {
            int barsCount = ContextBarsCount;
            if ((barNum < barsCount - 1) || (optSer == null))
                return Constants.EmptySeries;

            FinInfo bSecFinInfo = optSer.UnderlyingAsset.FinInfo;
            if ((!bSecFinInfo.LastPrice.HasValue))
                return Constants.EmptySeries;

            //IDataBar bar = optSer.UnderlyingAsset.Bars[len - 1];
            double futPx = bSecFinInfo.LastPrice.Value;
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

            if (Double.IsNaN(ratePct))
                //throw new ScriptException("Argument 'ratePct' contains NaN for some strange reason. rate:" + rate);
                return Constants.EmptySeries;

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            IOptionStrikePair[] pairs = (from pair in optSer.GetStrikePairs()
                                         //orderby pair.Strike ascending -- уже отсортировано!
                                         select pair).ToArray();
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            for (int j = 0; j < pairs.Length; j++)
            {
                bool showPoint = true;
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

                double prec;
                // Биржа шлет несогласованную улыбку
                //double virtualExchangeF = sInfo.CallFinInfo.TheoreticalPrice.Value - sInfo.PutFinInfo.TheoreticalPrice.Value + sInfo.Strike;
                if ((m_optionType == StrikeType.Any) || (m_optionType == StrikeType.Put))
                {
                    double optSigma = sInfo.PutFinInfo.Volatility.Value;
                    if ((!DoubleUtil.IsOne(m_multPx)) || (!DoubleUtil.IsZero(m_shiftPx)))
                    {
                        double optPx = sInfo.PutFinInfo.TheoreticalPrice.Value * m_multPx + m_shiftPx * sInfo.Tick;
                        if ((optPx <= 0) || (Double.IsNaN(optPx)))
                            optSigma = 0;
                        else
                            optSigma = FinMath.GetOptionSigma(futPx, k, dT, optPx, ratePct, false, out prec);
                    }

                    double vol = optSigma;

                    InteractivePointActive ip = new InteractivePointActive(k, vol);
                    ip.Geometry = Geometries.Ellipse;
                    ip.Color = AlphaColors.DarkCyan;
                    ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}", k, Constants.PctMult * optSigma);

                    if (showPoint && (vol > 0))
                    {
                        controlPoints.Add(new InteractiveObject(ip));
                    }

                    if ((xs.Count <= 0) ||
                        (!DoubleUtil.AreClose(k, xs[xs.Count - 1])))
                    {
                        xs.Add(k);
                        ys.Add(vol);
                    }
                }

                if ((m_optionType == StrikeType.Any) || (m_optionType == StrikeType.Call))
                {
                    double optSigma = sInfo.CallFinInfo.Volatility.Value;
                    if ((!DoubleUtil.IsOne(m_multPx)) || (!DoubleUtil.IsZero(m_shiftPx)))
                    {
                        double optPx = sInfo.CallFinInfo.TheoreticalPrice.Value * m_multPx + m_shiftPx * sInfo.Tick;
                        if ((optPx <= 0) || (Double.IsNaN(optPx)))
                            optSigma = 0;
                        else
                            optSigma = FinMath.GetOptionSigma(futPx, k, dT, optPx, ratePct, true, out prec);
                    }

                    double vol = optSigma;

                    // ReSharper disable once UseObjectOrCollectionInitializer
                    InteractivePointActive ip = new InteractivePointActive(k, vol);
                    ip.Geometry = Geometries.Ellipse;
                    ip.Color = AlphaColors.DarkCyan;
                    ip.Tooltip = String.Format("K:{0}; IV:{1:0.00}", k, Constants.PctMult * optSigma);

                    if (showPoint && (vol > 0))
                    {
                        controlPoints.Add(new InteractiveObject(ip));
                    }

                    if ((xs.Count <= 0) ||
                        (!DoubleUtil.AreClose(k, xs[xs.Count - 1])))
                    {
                        xs.Add(k);
                        ys.Add(vol);
                    }
                }
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

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

            try
            {
                if (xs.Count >= BaseCubicSpline.MinNumberOfNodes)
                {
                    NotAKnotCubicSpline spline = new NotAKnotCubicSpline(xs, ys);

                    info.ContinuousFunction = spline;
                    info.ContinuousFunctionD1 = spline.DeriveD1();

                    res.Tag = info;
                }
            }
            catch (Exception ex)
            {
                m_context.Log(ex.ToString(), MessageType.Error, true);
                return Constants.EmptySeries;
            }

            return res;
        }
    }
}
