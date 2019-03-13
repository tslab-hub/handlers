using System;
using System.Diagnostics;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// Ссылка на скрипт или статью с использованием данного кубика.
    /// Это вспомогательный атрибут для упрощения локализации зоопарка кубиков (как опционных, так и обычных),
    /// а также для автоматизированного формирования документации.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public class HelperLinkAttribute : Attribute
    {
        public HelperLinkAttribute(string link)
            : this(link, link, Constants.Ru)
        {
        }

        public HelperLinkAttribute(string link, string name, string language)
        {
            Link = link ?? String.Empty;
            Name = name ?? String.Empty;
            Language = String.IsNullOrWhiteSpace(language) ? Constants.Ru : language;
        }

        public string Link { get; set; }

        public string Name { get; set; }

        public string Language { get; set; }

        public override string ToString()
        {
            return Name + ": " + Link;
        }
    }
}
