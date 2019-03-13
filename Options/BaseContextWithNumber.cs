using System;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~englisg Base class for handlers with barNumber (implements of BaseContextTemplate, IValuesHandlerWithNumber)
    /// \~russian Базовый класс для блоков с номером бара (реализует BaseContextTemplate, IValuesHandlerWithNumber)
    /// </summary>
    public abstract class BaseContextWithNumber<T> : BaseContextTemplate<T>, IValuesHandlerWithNumber
        where T : struct
    {
    }
}
