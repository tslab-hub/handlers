using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TSLab.Script.Handlers.Options;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.ServiceElements)]
    [HelperName("Message", Language = Constants.En)]
    [HelperName("Сообщение", Language = Constants.Ru)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.BOOL)]
    [OutputsCount(0)]
    [Description("При появлении на входе блока значения 'Истина' выводит в лог программы пользовательское сообщение.")]
    [HelperDescription("When input becomes TRUE a handler sends user message to a TSLab log.", Constants.En)]
    public sealed class MessageHandler : IOneSourceHandler, IBooleanReturns, IStreamHandler, IValuesHandlerWithNumber, IBooleanInputs, IContextUses
    {
        private const string UserMessageTag = "$UserMessageTag";

        public IContext Context { get; set; }

        /// <summary>
        /// \~english Message
        /// \~russian Текст
        /// </summary>
        [HelperName("Message", Constants.En)]
        [HelperName("Текст", Constants.Ru)]
        [Description("Текст")]
        [HelperDescription("Message", Constants.En)]
        [HandlerParameter(true, "", NotOptimized = true)]
        public string Message { get; set; }

        /// <summary>
        /// \~english Additional user tag
        /// \~russian Дополнительная пользовательская метка
        /// </summary>
        [HelperName("Tag", Constants.En)]
        [HelperName("Метка", Constants.Ru)]
        [Description("Дополнительная пользовательская метка")]
        [HelperDescription("Additional user tag", Constants.En)]
        [HandlerParameter(true, "Tag", NotOptimized = true)]
        public string Tag { get; set; }

        /// <summary>
        /// \~english Message importance (Info, Warning, Error)
        /// \~russian Важность сообщения (Info, Warning, Error)
        /// </summary>
        [HelperName("Importance", Constants.En)]
        [HelperName("Важность", Constants.Ru)]
        [Description("Важность сообщения (Info, Warning, Error)")]
        [HelperDescription("Message importance (Info, Warning, Error)", Constants.En)]
        [HandlerParameter(true, "Info", NotOptimized = true)]
        public MessageType Type { get; set; }

        public void Execute(IList<bool> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.LastOrDefault())
                Log();
        }

        public void Execute(bool value, int number)
        {
            if (value && number == Context.BarsCount - (Context.IsLastBarUsed ? 1 : 2))
                Log();
        }

        private void Log()
        {
            var args = new Dictionary<string, object> { { UserMessageTag, Tag ?? string.Empty } };
            Context.Log(Message, Type, true, args);
        }
    }
}
