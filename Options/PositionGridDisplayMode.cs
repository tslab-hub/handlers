using System;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Type of data to be displayed in a grid column
    /// \~russian Вид данных, которые надо показать в столбце грида
    /// </summary>
    public enum PositionGridDisplayMode
    {
        /// <summary> \~english IV \~russian Волатильность</summary>
        Iv,
        /// <summary> \~english Price \~russian Цена</summary>
        Px,
        /// <summary> \~english Direction \~russian Направление</summary>
        Dir,
        /// <summary> \~english Qty \~russian Количество</summary>
        Qty,
        /// <summary> \~english Symbol \~russian Символ</summary>
        Symbol,
        /// <summary> \~english Is virtual \~russian Виртуальная или реальная</summary>
        IsVirtual,
    }
}
