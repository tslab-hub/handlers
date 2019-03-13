using System;
using System.Diagnostics;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// Описание кубика как его будет видеть Пользователь.
    /// Это вспомогательный атрибут для упрощения локализации зоопарка кубиков (как опционных, так и обычных).
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public class HelperDescriptionAttribute : Attribute
    {
        public HelperDescriptionAttribute()
            : this(String.Empty, Constants.Ru)
        {
        }

        public HelperDescriptionAttribute(string description)
            : this(description, Constants.Ru)
        {
        }

        public HelperDescriptionAttribute(string description, string language)
        {
            Description = description ?? String.Empty;
            Language = String.IsNullOrWhiteSpace(language) ? Constants.Ru : language;
        }

        public string Description { get; set; }

        public string Language { get; set; }

        public override string ToString()
        {
            return Description;
        }
    }
}
