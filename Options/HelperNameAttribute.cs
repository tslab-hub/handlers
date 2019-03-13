using System;
using System.Diagnostics;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// Название кубика как его будет видеть Пользователь.
    /// Вспомогательный атрибут для упрощения локализации зоопарка кубиков (как опционных, так и обычных),
    /// а также для автоматизированного формирования документации.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public class HelperNameAttribute : Attribute
    {
        public HelperNameAttribute(string name)
            : this(name, Constants.Ru)
        {
        }

        public HelperNameAttribute(string name, string language)
        {
            Name = name ?? String.Empty;
            Language = String.IsNullOrWhiteSpace(language) ? Constants.Ru : language;
        }

        public string Name { get; set; }

        public string Language { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
