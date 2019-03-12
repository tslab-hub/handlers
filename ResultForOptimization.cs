using System;

namespace TSLab.Script.Handlers
{
    [HandlerCategory(HandlerCategories.TradeMath)]
    [InputsCount(1)]
    [Input(0, TemplateTypes.DOUBLE)]
    [OutputsCount(0)]
    public class ResultForOptimization : IOneSourceHandler, IValuesHandler, IDoubleInputs, IDoubleReturns, IContextUses
    {
        public IContext Context { set; get; }

        public double Execute(double value, int barNum)
        {
            Context.ScriptResult = value;
            return value;
        }
    }
}
