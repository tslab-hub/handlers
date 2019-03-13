using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;

using TSLab.Script.CanvasPane;
using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Estimate delta using position profile
    /// \~russian Численный расчет дельты позиции непосредственно на основе её профиля
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsTickByTickDeribit /* HandlerCategories.OptionsPositions */)]
    [HelperName("Numerical delta Deribit (IntSer)", Language = Constants.En)]
    [HelperName("Численная дельта Deribit (IntSer)", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.INTERACTIVESPLINE, Name = "Position Profile")]
    [OutputType(TemplateTypes.INTERACTIVESPLINE)]
    [Description("Численный расчет дельты позиции непосредственно на основе её профиля")]
    [HelperDescription("Estimates a delta using a position profile", Constants.En)]
    public class SingleSeriesNumericalDeltaDeribit3 : BaseCanvasDrawing, IValuesHandlerWithNumber
    {
        private const string DefaultTooltipFormat = "0.000";
        private const string DefaultBtcTooltipFormat = "e3";

        private string m_tooltipFormat = DefaultTooltipFormat;
        private double m_futNominal = Double.Parse(SingleSeriesProfileDeribit.DefaultFutNominal);

        #region Parameters
        /// <summary>
        /// \~english Nominal value of Deribit futures (by default is 10 USD)
        /// \~russian Номинальный размер фьючерса Дерибит (по умолчанию 10 USD)
        /// </summary>
        [HelperName("Futures nominal", Constants.En)]
        [HelperName("Номинал фьючерсов", Constants.Ru)]
        [Description("Номинальный размер фьючерса Дерибит (по умолчанию 10 USD)")]
        [HelperDescription("Nominal value of Deribit futures (by default is 10 USD)", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = SingleSeriesProfileDeribit.DefaultFutNominal)]
        public double FutNominal
        {
            get
            {
                return m_futNominal;
            }
            set
            {
                //// Новое значение принимается только если все хорошо?
                //if (DoubleUtil.IsPositive(value))
                m_futNominal = value;
            }
        }

        /// <summary>
        /// \~english Position profile as bitcoins
        /// \~russian Профиль позиции БЫЛ вычислен в биткойнах
        /// </summary>
        [HelperName("Profile in BTC?", Constants.En)]
        [HelperName("Профиль в биткойнах?", Constants.Ru)]
        [Description("Вычислить профиль позиции в биткойнах")]
        [HelperDescription("Calculate profile as bitcoins", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool ProfileAsBtc { get; set; }

        /// <summary>
        /// \~english Tooltip format (i.e. '0.00', '0.0##' etc)
        /// \~russian Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.
        /// </summary>
        [HelperName("Tooltip Format", Constants.En)]
        [HelperName("Формат подсказки", Constants.Ru)]
        [Description("Формат числа для тултипа. Например, '0.00', '0.0##' и т.п.")]
        [HelperDescription("Tooltip format (i.e. '0.00', '0.0##' etc)", Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Default = DefaultTooltipFormat)]
        public string TooltipFormat
        {
            get { return m_tooltipFormat; }
            set
            {
                if (!String.IsNullOrWhiteSpace(value))
                {
                    try
                    {
                        string yStr = Math.PI.ToString(value);
                        m_tooltipFormat = value;
                    }
                    catch
                    {
                        m_context.Log("Tooltip format error. I'll keep old one: " + m_tooltipFormat, MessageType.Warning, true);
                    }
                }
            }
        }
        #endregion Parameters

        public InteractiveSeries Execute(InteractiveSeries positionProfile, int barNum)
        {
            int barsCount = ContextBarsCount;
            if (barNum < barsCount - 1)
                return Constants.EmptySeries;

            if (positionProfile == null)
                return Constants.EmptySeries;

            SmileInfo sInfo = positionProfile.GetTag<SmileInfo>();
            if ((sInfo == null) ||
                (sInfo.ContinuousFunction == null) || (sInfo.ContinuousFunctionD1 == null))
                return Constants.EmptySeries;

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            var profilePoints = positionProfile.ControlPoints;
            List<InteractiveObject> controlPoints = new List<InteractiveObject>();
            foreach (InteractiveObject iob in profilePoints)
            {
                double rawDelta, f = iob.Anchor.ValueX;
                if (sInfo.ContinuousFunctionD1.TryGetValue(f, out rawDelta))
                {
                    // ReSharper disable once UseObjectOrCollectionInitializer
                    InteractivePointActive ip = new InteractivePointActive();
                    ip.IsActive = m_showNodes;
                    //ip.DragableMode = DragableMode.None;
                    //ip.Geometry = Geometries.Rect;
                    //ip.Color = System.Windows.Media.Colors.Orange;
                    double y;
                    if (ProfileAsBtc)
                    {
                        // Переводим дельту в ШТУКИ ФЬЮЧЕРСОВ в терминах БИТКОЙНОВ
                        y = rawDelta * (f * f / m_futNominal);
                        string rawStr = rawDelta.ToString(DefaultBtcTooltipFormat, CultureInfo.InvariantCulture);
                        string futDeltaStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);

                        ip.Tooltip = String.Format(" F: {0}\r\n D(B): {1}\r\n D(F): {2}", f, rawStr, futDeltaStr);
                    }
                    else
                    {
                        // Переводим дельту в ШТУКИ ФЬЮЧЕРСОВ в терминах ДОЛЛАРОВ
                        y = rawDelta * (f / m_futNominal);
                        string rawStr = rawDelta.ToString(DefaultBtcTooltipFormat, CultureInfo.InvariantCulture);
                        string futDeltaStr = y.ToString(m_tooltipFormat, CultureInfo.InvariantCulture);

                        ip.Tooltip = String.Format(" F: {0}\r\n D($): {1}\r\n D(F): {2}", f, rawStr, futDeltaStr);
                    }

                    ip.Value = new Point(f, y);

                    controlPoints.Add(new InteractiveObject(ip));

                    xs.Add(f);
                    ys.Add(y);
                }
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            InteractiveSeries res = new InteractiveSeries(); // Здесь так надо -- мы делаем новую улыбку
            res.ControlPoints = new ReadOnlyCollection<InteractiveObject>(controlPoints);

            try
            {
                if (xs.Count >= BaseCubicSpline.MinNumberOfNodes)
                {
                    // ReSharper disable once UseObjectOrCollectionInitializer
                    SmileInfo info = new SmileInfo();
                    info.F = sInfo.F;
                    info.dT = sInfo.dT;
                    info.RiskFreeRate = sInfo.RiskFreeRate;

                    NotAKnotCubicSpline spline = new NotAKnotCubicSpline(xs, ys);

                    info.ContinuousFunction = spline;
                    info.ContinuousFunctionD1 = spline.DeriveD1();

                    res.Tag = info;
                }
            }
            catch (DivideByZeroException dvbEx)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("Divide-by-zero exception. Probably it is from NotAKnotCubicSpline:");
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine(dvbEx.ToString());
                sb.AppendLine();

                sb.AppendLine("Content of arguments...");
                sb.AppendLine("xs;ys");
                for (int tmpIndex = 0; tmpIndex < xs.Count; tmpIndex++)
                {
                    sb.Append(xs[tmpIndex].ToString(CultureInfo.InvariantCulture) + ";");
                    sb.Append(ys[tmpIndex].ToString(CultureInfo.InvariantCulture));

                    sb.AppendLine();
                }
                sb.AppendLine();

                m_context.Log(sb.ToString(), MessageType.Error, false);
                m_context.Log(dvbEx.ToString(), MessageType.Error, true);
                return Constants.EmptySeries;
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
