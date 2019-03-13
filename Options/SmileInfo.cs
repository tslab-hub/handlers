using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using TSLab.Script.Options;
using TSLab.Utils;

namespace TSLab.Script.Handlers.Options
{
    /// <summary>
    /// \~english Metainformation common to all smile nodes: time, price, riskless rate.
    /// Also it containts continuous function which represents the smile.
    /// \~russian Некоторая метаинформация общая для всех узлов улыбки: время, цена БА, ставка доходности.
    /// Важно, что здесь же содержится непрерывная функция, описывающая текущую улыбку.
    /// </summary>
    [Serializable]
    public class SmileInfo
    {
        public double F;
        public double dT;
        public DateTime Expiry;
        public DateTime ScriptTime;
        /// <summary>Безрисковая ставка В ПРОЦЕНТАХ</summary>
        public double RiskFreeRate;
        public string BaseTicker;

        /// <summary>
        /// Волатильность на-деньгах (не в процентах, а 'как есть')
        /// </summary>
        public double IvAtm = Double.NaN;
        /// <summary>
        /// Наклон на-деньгах (не в процентах, а 'как есть' в безразмерных единицах)
        /// </summary>
        public double SkewAtm = Double.NaN;
        /// <summary>
        /// Форма улыбки
        /// </summary>
        public double Shape = Double.NaN;
        /// <summary>
        /// Дотнет-имя кубика, который создал эту улыбку.
        /// При необходимости можно будет создавать новую улыбку на основании SmileInfo и
        /// чтобы она при этом была правильного типа.
        /// </summary>
        public string SmileType;

        /// <summary>
        /// Проверка, что все 5 основных параметров улыбки валидны (F, dT, IvAtm, SkewAtm, Shape).
        /// Тогда на их основе можно строить SmileFunctionExtended и улыбку SmileImitation5.
        /// </summary>
        public bool IsValidSmileParams
        {
            get
            {
                if ((!DoubleUtil.IsPositive(F)) || (!DoubleUtil.IsPositive(dT)))
                    return false;

                // Волатильность на деньгах всегда должна быть строго положительна!
                if (!DoubleUtil.IsPositive(IvAtm))
                    return false;

                if (Double.IsNaN(SkewAtm) || Double.IsInfinity(SkewAtm))
                    return false;

                if (Double.IsNaN(Shape) || Double.IsInfinity(Shape))
                    return false;

                if (String.IsNullOrWhiteSpace(SmileType))
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Объект, реализующий непрерывное представление данного набора точек
        /// </summary>
        public IFunction ContinuousFunction;

        /// <summary>
        /// Объект, реализующий непрерывное представление первой производной данного набора точек
        /// </summary>
        public IFunction ContinuousFunctionD1;

        public XElement ToXElement()
        {
            XElement xel = new XElement(XName.Get(GetType().FullName, ""));

            xel.SetAttributeValue(XName.Get("F", ""), F);
            xel.SetAttributeValue(XName.Get("dT", ""), dT);
            xel.SetAttributeValue(XName.Get("Expiry", ""), Expiry.ToString(TimeToExpiry.DateTimeFormat, CultureInfo.InvariantCulture));
            xel.SetAttributeValue(XName.Get("ScriptTime", ""), ScriptTime.ToString(BaseContextHandler.DateTimeFormatWithMs, CultureInfo.InvariantCulture));
            xel.SetAttributeValue(XName.Get("RiskFreeRate", ""), RiskFreeRate);
            xel.SetAttributeValue(XName.Get("BaseTicker", ""), BaseTicker);

            if (IsValidSmileParams)
            {
                xel.SetAttributeValue(XName.Get("IvAtm", ""), IvAtm);
                xel.SetAttributeValue(XName.Get("SkewAtm", ""), SkewAtm);
                xel.SetAttributeValue(XName.Get("Shape", ""), Shape);
                xel.SetAttributeValue(XName.Get("SmileType", ""), SmileType);
            }

            XElement xContFunc = null;
            if (ContinuousFunction != null)
            {
                xContFunc = ContinuousFunction.ToXElement();
                xContFunc.Name = XName.Get("ContinuousFunction", "");
                xel.Add(xContFunc);
            }

            XElement xContFuncD1 = null;
            if (ContinuousFunctionD1 != null)
            {
                xContFuncD1 = ContinuousFunctionD1.ToXElement();
                xContFuncD1.Name = XName.Get("ContinuousFunctionD1", "");
                xel.Add(xContFuncD1);
            }

            return xel;
        }

        public static SmileInfo FromXElement(XElement xel)
        {
            if ((xel == null) || (xel.Name.LocalName != typeof(SmileInfo).FullName))
                return null;

            double futPx, dT, rate;
            {
                XAttribute xAttr = xel.Attribute("F");
                if ((xAttr == null) || String.IsNullOrWhiteSpace(xAttr.Value) ||
                    (!Double.TryParse(xAttr.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out futPx)))
                {
                    return null;
                }
            }

            {
                XAttribute xAttr = xel.Attribute("dT");
                if ((xAttr == null) || String.IsNullOrWhiteSpace(xAttr.Value) ||
                    (!Double.TryParse(xAttr.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out dT)))
                {
                    return null;
                }
            }

            {
                XAttribute xAttr = xel.Attribute("RiskFreeRate");
                if ((xAttr == null) || String.IsNullOrWhiteSpace(xAttr.Value) ||
                    (!Double.TryParse(xAttr.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out rate)))
                {
                    return null;
                }
            }

            DateTime expiry = new DateTime(), scriptTime = new DateTime();
            {
                XAttribute xAttr = xel.Attribute("Expiry");
                if ((xAttr != null) && (!String.IsNullOrWhiteSpace(xAttr.Value)))
                {
                    DateTime.TryParseExact(xAttr.Value, TimeToExpiry.DateTimeFormat,
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out expiry);
                }
            }

            {
                XAttribute xAttr = xel.Attribute("ScriptTime");
                if ((xAttr != null) && (!String.IsNullOrWhiteSpace(xAttr.Value)))
                {
                    DateTime.TryParseExact(xAttr.Value, BaseContextHandler.DateTimeFormatWithMs,
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out scriptTime);
                }
            }

            string baseTicker = null;
            {
                XAttribute xAttr = xel.Attribute("BaseTicker");
                if ((xAttr != null) && (!String.IsNullOrWhiteSpace(xAttr.Value)))
                {
                    baseTicker = xAttr.Value;
                }
            }

            IFunction func = null;
            {
                XElement xCf = (from node in xel.Descendants()
                                where (node.Name.LocalName == "ContinuousFunction")
                                select node).FirstOrDefault();
                if (xCf != null)
                {
                    FunctionDeserializer.TryDeserialize(xCf, out func);
                }
            }

            IFunction funcD1 = null;
            {
                XElement xCfD1 = (from node in xel.Descendants()
                                  where (node.Name.LocalName == "ContinuousFunctionD1")
                                  select node).FirstOrDefault();
                if (xCfD1 != null)
                {
                    FunctionDeserializer.TryDeserialize(xCfD1, out funcD1);
                }
            }

            // ReSharper disable once UseObjectOrCollectionInitializer
            SmileInfo res = new SmileInfo();
            res.F = futPx;
            res.dT = dT;
            res.Expiry = expiry;
            res.ScriptTime = scriptTime;
            res.RiskFreeRate = rate;
            res.BaseTicker = baseTicker;

            res.ContinuousFunction = func;
            res.ContinuousFunctionD1 = funcD1;

            #region PROD-2402 - Парсим дополнительные параметры улыбки
            double ivAtm, skewAtm, shape;
            {
                XAttribute xAttr = xel.Attribute("IvAtm");
                if ((xAttr == null) || String.IsNullOrWhiteSpace(xAttr.Value) ||
                    (!Double.TryParse(xAttr.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out ivAtm)))
                {
                    ivAtm = Double.NaN;
                }
            }

            {
                XAttribute xAttr = xel.Attribute("SkewAtm");
                if ((xAttr == null) || String.IsNullOrWhiteSpace(xAttr.Value) ||
                    (!Double.TryParse(xAttr.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out skewAtm)))
                {
                    skewAtm = Double.NaN;
                }
            }

            {
                XAttribute xAttr = xel.Attribute("Shape");
                if ((xAttr == null) || String.IsNullOrWhiteSpace(xAttr.Value) ||
                    (!Double.TryParse(xAttr.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out shape)))
                {
                    shape = Double.NaN;
                }
            }

            string smileType;
            {
                XAttribute xAttr = xel.Attribute("SmileType");
                if ((xAttr == null) || String.IsNullOrWhiteSpace(xAttr.Value))
                    smileType = null;
                else
                    smileType = xAttr.Value;
            }

            res.IvAtm = ivAtm;
            res.SkewAtm = skewAtm;
            res.Shape = shape;
            res.SmileType = smileType;

            // Если улыбка невалидна, значит все равно толку с 'правильных параметров' нет?
            if (!res.IsValidSmileParams)
            {
                res.IvAtm = Double.NaN;
                res.SkewAtm = Double.NaN;
                res.Shape = Double.NaN;
                res.SmileType = null;
            }
            #endregion PROD-2402 - Парсим дополнительные параметры улыбки

            return res;
        }
    }
}
