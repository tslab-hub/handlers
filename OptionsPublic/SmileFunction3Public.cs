using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml.Linq;

using TSLab.Script.Options;

namespace TSLab.Script.Handlers.OptionsPublic
{
    // Вид: y(x) = ivATM + Depth*[ exp(-Shift^2) - exp(-(x-Shift)^2) ]

    /// <summary>
    /// \~english Arbitrary function with 3 parameters similar to observed smile
    /// \~russian Произвольная трёхпараметрическая функция качественно похожая на улыбку
    /// </summary>
    [Serializable]
    public class SmileFunction3Public : IFunction
    {
        /// <summary>Текущая цена БА</summary>
        public double F = 120000;
        /// <summary>Текущее время до экспирации</summary>
        public double dT = 1.0 / 12.0;

        /// <summary>Волатильность на деньгах (не в %, а 'как есть')</summary>
        public double IvAtm = 0.3;
        /// <summary>Сдвиг минимума (не в %, а 'как есть')</summary>
        public double Shift = 0.3;
        /// <summary>Глубина ямы (не в %, а 'как есть')</summary>
        public double Depth = 0.5;

        public SmileFunction3Public()
        {
        }

        public SmileFunction3Public(double ivAtm, double shift, double depth)
        {
            IvAtm = ivAtm;
            Shift = shift;
            Depth = depth;
        }

        public SmileFunction3Public(double ivAtm, double shift, double depth, double f, double dT)
            : this(ivAtm, shift, depth)
        {
            this.F = f;
            this.dT = dT;
        }

        public double Value(double strike)
        {
            double x = Math.Log(strike / F) / Math.Sqrt(dT);

            double eShift = Math.Exp(-Shift * Shift);
            double xShift = Math.Exp(-(x - Shift) * (x - Shift));

            double res = IvAtm + Depth * (eShift - xShift);

            return res;
        }

        /// <summary>
        /// Вычисление значения интерполированной функции в произвольной точке
        /// </summary>
        /// <param name="strike">аргумент функции (страйк)</param>
        /// <param name="dIvDk">значение IV в этой точке</param>
        /// <returns>false -- если возникли какие-то проблемы при вычислениях</returns>
        public bool TryGetValue(double strike, out double dIvDk)
        {
            if (strike > 0)
            {
                dIvDk = Value(strike);
                if (!Double.IsNaN(dIvDk))
                    return true;
                else
                    return false;
            }
            else
            {
                dIvDk = Double.NaN;
                return false;
            }
        }

        /// <summary>
        /// Сдвинуть весь график функции вдоль горизонтальной оси
        /// </summary>
        public IFunction HorizontalShift(double shift)
        {
            SmileFunction3 res = new SmileFunction3(IvAtm, Shift, Depth, F - shift, dT);
            return res;
        }

        /// <summary>
        /// Сдвинуть весь график функции вдоль вертикальной оси
        /// </summary>
        public IFunction VerticalShift(double vertShift)
        {
            SmileFunction3 res = new SmileFunction3(IvAtm + vertShift, Shift, Depth, F, dT);
            return res;
        }

        /// <summary>
        /// Определить параметр улыбки Depth, если известен её наклон на деньгах
        /// </summary>
        /// <param name="slopeAtm">наклон улыбки на деньгах</param>
        /// <returns>подходящая глубина, которая обеспечивает заказанный наклон</returns>
        public double GetDepthUsingSlopeATM(double slopeAtm)
        {
            double eShift = Math.Exp(Shift * Shift);

            double res = -0.5 * F * Math.Sqrt(dT) * slopeAtm * eShift / Shift;
            return res;
        }

        /// <summary>
        /// Первая производная улыбки по параметру 'Страйк'
        /// </summary>
        public IFunction DeriveD1()
        {
            DSmileFunction3Public_DK res = new DSmileFunction3Public_DK(IvAtm, Shift, Depth, F, dT);
            return res;
        }

        public XElement ToXElement()
        {
            XElement xel = new XElement(XName.Get(GetType().FullName, ""));

            xel.SetAttributeValue(XName.Get("IvAtm", ""), IvAtm.ToString(CultureInfo.InvariantCulture));
            xel.SetAttributeValue(XName.Get("Shift", ""), Shift.ToString(CultureInfo.InvariantCulture));
            xel.SetAttributeValue(XName.Get("Depth", ""), Depth.ToString(CultureInfo.InvariantCulture));
            xel.SetAttributeValue(XName.Get("F", ""), F.ToString(CultureInfo.InvariantCulture));
            xel.SetAttributeValue(XName.Get("dT", ""), dT.ToString(CultureInfo.InvariantCulture));

            return xel;
        }
    }

    // Вид: y(x) = 2*Depth*(x-Shift)*exp(-(x-Shift)^2)

    /// <summary>
    /// \~english First derivative DSmileFunction3_DK
    /// \~russian Первая производная DSmileFunction3_DK
    /// </summary>
    [Serializable]
    [SuppressMessage("StyleCopPlus.StyleCopPlusRules", "SP0100:AdvancedNamingRules", Justification = "Reviewed. Suppression is OK here.")]
    internal class DSmileFunction3Public_DK : IFunction
    {
        /// <summary>Текущая цена БА</summary>
        public double F = 120000;
        /// <summary>Текущее время до экспирации</summary>
        public double dT = 1.0 / 12.0;

        /// <summary>Волатильность на деньгах (в %)</summary>
        public double IvAtm = 0.3;
        /// <summary>Сдвиг минимума (в %)</summary>
        public double Shift = 0.3;
        /// <summary>Глубина ямы (в %)</summary>
        public double Depth = 0.5;

        public DSmileFunction3Public_DK()
        {
        }

        public DSmileFunction3Public_DK(double ivAtm, double shift, double depth)
        {
            IvAtm = ivAtm;
            Shift = shift;
            Depth = depth;
        }

        public DSmileFunction3Public_DK(double ivAtm, double shift, double depth, double F, double dT)
            : this(ivAtm, shift, depth)
        {
            this.F = F;
            this.dT = dT;
        }

        public double Value(double strike)
        {
            double x = Math.Log(strike / F) / Math.Sqrt(dT);

            double xShift = Math.Exp(-(x - Shift) * (x - Shift));

            double res = 2 * Depth * (x - Shift) * xShift;
            res = res / strike / Math.Sqrt(dT);

            return res;
        }

        /// <summary>
        /// Вычисление значения производной интерполированной функции в произвольной точке
        /// </summary>
        /// <param name="strike">аргумент функции (страйк)</param>
        /// <param name="dIvDk">значение dIV_dK в этой точке</param>
        /// <returns>false -- если возникли какие-то проблемы при вычислениях</returns>
        public bool TryGetValue(double strike, out double dIvDk)
        {
            if (strike > 0)
            {
                dIvDk = Value(strike);
                if (!Double.IsNaN(dIvDk))
                    return true;
                else
                    return false;
            }
            else
            {
                dIvDk = Double.NaN;
                return false;
            }
        }

        /// <summary>
        /// Сдвинуть весь график функции вдоль горизонтальной оси
        /// </summary>
        public IFunction HorizontalShift(double shift)
        {
            DSmileFunction3Public_DK res = new DSmileFunction3Public_DK(IvAtm, Shift, Depth, F - shift, dT);
            return res;
        }

        /// <summary>
        /// Сдвинуть весь график функции вдоль вертикальной оси
        /// </summary>
        public IFunction VerticalShift(double vertShift)
        {
            if (TSLab.Utils.DoubleUtil.IsZero(vertShift))
            {
                DSmileFunction3Public_DK res = new DSmileFunction3Public_DK(IvAtm, Shift, Depth, F, dT);
                return res;
            }

            //dSmileFunction3_dK res = new dSmileFunction3_dK(IvAtm + vertShift, Shift, Depth, F, dT);
            throw new NotImplementedException("Непонятно как сдвигать по вертикали первую производную. Получается, надо запоминать уровень относительно которого она построена???");
            //return res;
        }

        public XElement ToXElement()
        {
            XElement xel = new XElement(XName.Get(GetType().FullName, ""));

            xel.SetAttributeValue(XName.Get("IvAtm", ""), IvAtm.ToString(CultureInfo.InvariantCulture));
            xel.SetAttributeValue(XName.Get("Shift", ""), Shift.ToString(CultureInfo.InvariantCulture));
            xel.SetAttributeValue(XName.Get("Depth", ""), Depth.ToString(CultureInfo.InvariantCulture));
            xel.SetAttributeValue(XName.Get("F", ""), F.ToString(CultureInfo.InvariantCulture));
            xel.SetAttributeValue(XName.Get("dT", ""), dT.ToString(CultureInfo.InvariantCulture));

            return xel;
        }
    }
}
