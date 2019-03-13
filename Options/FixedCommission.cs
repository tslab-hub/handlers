using System;
using System.ComponentModel;

using TSLab.Script.Options;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Fixed commission (it can apply 'scalper discount' available in MOEX)
    /// \~russian Фиксированная комиссия (с возможностью учесть скальперскую скидку на МБ)
    /// </summary>
    [HandlerCategory(HandlerCategories.OptionsPositions)]
    [HelperName("Fixed Commission", Language = Constants.En)]
    [HelperName("Фиксированная комиссия", Language = Constants.Ru)]
    [HandlerAlwaysKeep]
    [InputsCount(1)]
    [InputAttribute(0, TemplateTypes.OPTION, Name = Constants.OptionSource)]
    [OutputsCount(0)]
    [Description("Фиксированная комиссия (с возможностью учесть скальперскую скидку на МБ)")]
    [HelperDescription("Fixed commission (it can apply 'scalper discount' available in MOEX)", Constants.En)]
    public class FixedCommission : IContextUses, IHandler
    {
        private const double RtsFirstHour = 19;

        private bool m_scalpingRule = true;
        private double m_futComm, m_optComm;

        public IContext Context { get; set; }

        #region Parameters
        /// <summary>
        /// \~english Apply 'scalper discount' available in MOEX
        /// \~russian Учет в расчетах скальперской скидки, доступной на ФОРТС
        /// </summary>
        [HelperName("Scalping Discount", Constants.En)]
        [HelperName("Скальперская скидка", Constants.Ru)]
        [Description("Учет в расчетах скальперской скидки, доступной на ФОРТС")]
        [HelperDescription("Apply 'scalper discount' available in MOEX", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true, Default = "true")]
        public bool ScalpingRule
        {
            get { return m_scalpingRule; }
            set { m_scalpingRule = value; }
        }

        /// <summary>
        /// \~english Futures commission
        /// \~russian Комиссия по фьючерсам
        /// </summary>
        [HelperName("Futures commission", Constants.En)]
        [HelperName("Комиссия по фьючерсам", Constants.Ru)]
        [Description("Комиссия по фьючерсам")]
        [HelperDescription("Futures commission", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "0.8", Min = "0", Max = "10000000", Step = "1")]
        public double FutCommission
        {
            get { return m_futComm; }
            set
            {
                if (value >= 0)
                    m_futComm = value;
            }
        }

        /// <summary>
        /// \~english Option commission
        /// \~russian Комиссия по опционам
        /// </summary>
        [HelperName("Option commission", Constants.En)]
        [HelperName("Комиссия по опционам", Constants.Ru)]
        [Description("Комиссия по опционам")]
        [HelperDescription("Option commission", Language = Constants.En)]
        [HandlerParameter(true, NotOptimized = false, IsVisibleInBlock = true,
            Default = "1.6", Min = "0", Max = "10000000", Step = "1")]
        public double OptCommission
        {
            get { return m_optComm; }
            set
            {
                if (value >= 0)
                    m_optComm = value;
            }
        }
        #endregion Parameters

        /// <summary>
        /// Обработчик под тип данных IOption
        /// </summary>
        public void Execute(IOption source)
        {
            source.UnderlyingAsset.Commission = CalculateCommission;

            bool showError = true;
            foreach (var strike in source.GetStrikes())
            {
                try
                {
                    strike.Commission = CalculateCommission;
                }
                catch (NullReferenceException nre)
                {
                    if (showError)
                    {
                        showError = false;

                        string msg = String.Format("[DEBUG:{0}] {1}. Property 'Security' is empty for strike {2}. FullName:{3}. (The message is displayed only once.)",
                            GetType().Name, nre.GetType().FullName, strike.Strike, strike.FinInfo.Security.FullName);
                        Context.Log(msg, MessageType.Error, true);
                    }
                }
            }
        }

        public double CalculateCommission(IPosition pos, double price, double shares, bool isEntry, bool isPart)
        {
            double comm;
            if (isEntry || (!m_scalpingRule))
            {
                if (pos.Security.SecurityDescription.IsOption)
                    comm = m_optComm;
                else
                    comm = m_futComm;
            }
            else
            {
                // Правило для скальперской комиссии учитывается только при закрытии позы
                DateTime beg = pos.EntryBar.Date.AddHours(-RtsFirstHour);
                DateTime end = pos.ExitBar.Date.AddHours(-RtsFirstHour);

                if (beg.Date == end.Date)
                    comm = 0;
                else
                {
                    if (pos.Security.SecurityDescription.IsOption)
                        comm = m_optComm;
                    else
                        comm = m_futComm;
                }
            }

            double res = shares * comm;
            return res;
        }
    }
}
