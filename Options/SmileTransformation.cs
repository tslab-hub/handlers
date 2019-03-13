using System;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Smile transformation
    /// \~russian Вид преобразования улыбки
    /// </summary>
    public enum SmileTransformation
    {
        /// <summary>
        /// \~english No changes
        /// \~russian Без изменений
        /// </summary>
        //[LocalizeDisplayName("SmileTransformation.None")]
        [LocalizeDescription("SmileTransformation.None")]
        None,

        /// <summary>
        /// \~english Simmetrise in linear coordinates
        /// \~russian Симметризовать в линейных координатах
        /// </summary>
        //[LocalizeDisplayName("SmileTransformation.Simmetrise")]
        [LocalizeDescription("SmileTransformation.Simmetrise")]
        Simmetrise,

        /// <summary>
        /// \~english Simmetrise in logarythmic coordinates
        /// \~russian Симметризовать в логарифмических координатах
        /// </summary>
        //[LocalizeDisplayName("SmileTransformation.LogSimmetrise")]
        [LocalizeDescription("SmileTransformation.LogSimmetrise")]
        LogSimmetrise,
    }
}
