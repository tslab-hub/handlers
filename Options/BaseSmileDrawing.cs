using System;
using System.ComponentModel;
using System.Globalization;
using TSLab.Script.CanvasPane;
using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Base class to draw curve on CanvasPane. Strike limits are set explicitly.
    /// \~russian Базовый класс для рисования улыбок на CanvasPane. Диапазон страйков задаётся в явном виде.
    /// </summary>
    public abstract class BaseSmileDrawing : BaseContextHandler
    {
        protected bool m_showNodes = false;
        protected double m_minStrike = 1, m_maxStrike = 1500000, m_strikeStep = 1000;

        #region Parameters
        /// <summary>
        /// \~english Min strike to be processed by handler
        /// \~russian Минимальный обрабатываемый страйк
        /// </summary>
        [HelperName("Min strike", Constants.En)]
        [HelperName("Минимальный страйк", Constants.Ru)]
        [Description("Минимальный обрабатываемый страйк")]
        [HelperDescription("Min strike to be processed by handler", Language = Constants.En)]
        [HandlerParameter(true, "1", Min = "0", Max = "10000000", NotOptimized = true, Step = "1")]
        public double MinStrike
        {
            get { return m_minStrike; }
            set
            {
                if (value > 0)
                    m_minStrike = Math.Min(value, m_maxStrike);
            }
        }

        /// <summary>
        /// \~english Max strike to be processed by handler
        /// \~russian Максимальный обрабатываемый страйк
        /// </summary>
        [HelperName("Max strike", Constants.En)]
        [HelperName("Максимальный страйк", Constants.Ru)]
        [Description("Максимальный обрабатываемый страйк")]
        [HelperDescription("Max strike to be processed by handler", Language = Constants.En)]
        [HandlerParameter(true, "1500000", Min = "0", Max = "10000000", NotOptimized = true, Step = "1")]
        public double MaxStrike
        {
            get { return m_maxStrike; }
            set
            {
                if (value > 0)
                    m_maxStrike = Math.Max(value, m_minStrike);
            }
        }

        /// <summary>
        /// \~english Strike step
        /// \~russian Шаг между страйками
        /// </summary>
        [HelperName("Strike step", Constants.En)]
        [HelperName("Шаг страйков", Constants.Ru)]
        [Description("Шаг между страйками")]
        [HelperDescription("Strike step", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = true, IsVisibleInBlock = true, Name = "Step",
            Default = "2500", Min = "0", Max = "1000000", Step = "1")]
        public double StrikeStep
        {
            get { return m_strikeStep; }
            set
            {
                if (value > 0)
                    m_strikeStep = value;
            }
        }

        /// <summary>
        /// \~english Nodes are shown when true
        /// \~russian При true будет показывать узлы на отображаемой линии
        /// </summary>
        [HelperName("Show nodes", Constants.En)]
        [HelperName("Показывать узлы", Constants.Ru)]
        [Description("При true будет показывать узлы на отображаемой линии")]
        [HelperDescription("Nodes are shown when true", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "false")]
        public bool ShowNodes
        {
            get { return m_showNodes; }
            set { m_showNodes = value; }
        }
        #endregion Parameters

        internal static void FillNodeInfo(InteractivePointActive ip,
            double f, double dT, IOptionStrikePair sInfo,
            StrikeType optionType, OptionPxMode optPxMode,
            double optPx, double optQty, double optSigma, DateTime optTime, bool returnPct, double riskfreeRatePct)
        {
            if (optionType == StrikeType.Any)
                throw new ArgumentException("Option type 'Any' is not supported.", "optionType");

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
            nodeInfo.ScriptTime = optTime;
            nodeInfo.CalendarTime = DateTime.Now;

            nodeInfo.Symbol = nodeInfo.Security.Symbol;
            nodeInfo.DSName = nodeInfo.Security.SecurityDescription.DSName;
            nodeInfo.Expired = nodeInfo.Security.SecurityDescription.Expired;
            nodeInfo.FullName = nodeInfo.Security.SecurityDescription.FullName;

            ip.Tag = nodeInfo;
            ip.DragableMode = DragableMode.None;
            ip.Value = new Point(sInfo.Strike, nodeInfo.Sigma);
            // [2015-08-26] Алексей дал инструкцию убрать дату из тултипа.
            bool tooltipWithTime = false;
#if DEBUG
            tooltipWithTime = true;
#endif
            // [2015-12-07] В режиме отладки возвращаю.
            // Потому что иначе вообще непонятно что происходит и с какими данными скрипт работает.
            if (optQty > 0)
            {
                if (tooltipWithTime)
                {
                    ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "K:{0}; IV:{1:#0.00}%\r\n{2} px {3} @ {4}\r\nDate: {5}",
                        sInfo.Strike, optSigma * Constants.PctMult, optionType, optPx, optQty,
                        optTime.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture));
                }
                else
                {
                    ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "K:{0}; IV:{1:#0.00}%\r\n{2} px {3} @ {4}",
                        sInfo.Strike, optSigma * Constants.PctMult, optionType, optPx, optQty);
                }
            }
            else
            {
                if (tooltipWithTime)
                {
                    ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "K:{0}; IV:{1:#0.00}%\r\n{2} px {3}\r\nDate: {4}",
                        sInfo.Strike, optSigma * Constants.PctMult, optionType, optPx,
                        optTime.ToString(DateTimeFormatWithMs, CultureInfo.InvariantCulture));
                }
                else
                {
                    ip.Tooltip = String.Format(CultureInfo.InvariantCulture, "K:{0}; IV:{1:#0.00}%\r\n{2} px {3}",
                        sInfo.Strike, optSigma * Constants.PctMult, optionType, optPx);
                }
            }
        }
    }
}
