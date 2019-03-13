using System;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    // ВАЖНО: по целочисленным значениям этого перечисления будет работать блок SmileSelector
    // поэтому их надо менять синхронно

    /// <summary>
    /// \~english Smile type
    /// \~russian Тип улыбки
    /// </summary>
    public enum SmileType
    {
        /// <summary> \~english Market smile \~russian Рыночная улыбка</summary>
        //[LocalizeDisplayName("SmileType.Market")]
        [LocalizeDescription("SmileType.Market")]
        Market = 0,

        /// <summary> \~english Model smile (it is used for delta-hedge) \~russian Модельная улыбка (используется для дельта-хеджа)</summary>
        //[LocalizeDisplayName("SmileType.Model")]
        [LocalizeDescription("SmileType.Model")]
        Model = 1,

        /// <summary> \~english Exchange smile (if exists) \~russian Биржевая улыбка (если транслируется провайдером)</summary>
        //[LocalizeDisplayName("SmileType.Exchange")]
        [LocalizeDescription("SmileType.Exchange")]
        Exchange = 2,

        ///// <summary> \~english Fit to option quotes \~russian Подгон под опционные котировки</summary>
        //FitToQuotes,
    }
}
