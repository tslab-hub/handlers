using System.Collections.Generic;
using System.Linq;

namespace TSLab.Script.Handlers
{
    internal static class ProfitExtensions
    {
        public static double GetProfit(this ISecurity security, int barNum)
        {
            var result = security.Positions.GetProfit(barNum);
            return result;
        }

        private static double GetProfit(this IEnumerable<IPosition> positions, int barNum)
        {
            var result = positions.Sum(item => item.GetProfit(barNum));
            return result;
        }

        public static double GetProfit(this IPosition position, int barNum)
        {
            var result = position.EntryBarNum > barNum ? 0 : position.IsActiveForBar(barNum) ? position.CurrentProfit(barNum) : position.Profit();
            return result;
        }

        public static double GetAccumulatedProfit(this ISecurity security, int barNum)
        {
            var result = security.Positions.GetAccumulatedProfit(barNum);
            return result;
        }

        private static double GetAccumulatedProfit(this IEnumerable<IPosition> positions, int barNum)
        {
            var result = positions.Sum(item => GetAccumulatedProfit(item, barNum));
            return result;
        }

        public static double GetAccumulatedProfit(this IPosition position, int barNum)
        {
            var result = position.EntryBarNum > barNum ? 0 : position.IsActiveForBar(barNum) ? position.GetAccumulatedProfit(barNum) : position.Profit();
            return result;
        }
    }
}
