using System;
using System.ComponentModel;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~englisg Base class to draw curve on CanvasPane. Main feature -- automatic adaptation to current price, volatility and time to expiry.
    /// \~russian Базовый класс для рисования чего угодно на CanvasPane. Главная особенность -- автоматическая привязка к текущей цене БА, волатильности и времени до экспирации.
    /// </summary>
    public abstract class BaseCanvasDrawing : BaseContextHandler
    {
        protected bool m_showNodes = false;
        /// <summary>Множитель ширины</summary>
        protected double m_sigmaMult = 7;

        #region Parameters
        /// <summary>
        /// \~english Width Multiplier
        /// \~russian Множитель ширины
        /// </summary>
        [HelperName("Width multiplier", Constants.En)]
        [HelperName("Множитель ширины", Constants.Ru)]
        [Description("Множитель ширины")]
        [HelperDescription("Width multiplier", Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Name = "Sigma Mult",
            Default = "7", Min = "0", Max = "1000000", Step = "1", NumberDecimalDigits = 3)]
        public double SigmaMult
        {
            get { return m_sigmaMult; }
            set
            {
                if (value > 0)
                    m_sigmaMult = value;
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
    }
}
