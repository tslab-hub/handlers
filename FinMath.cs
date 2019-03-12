using System;
using System.Diagnostics;
using System.Globalization;

using TSLab.Utils;

namespace TSLab.Script.Handlers
{
    public class FinMath
    {
        #region БАЗОВЫЕ АЛГОРИТМЫ

        /// <summary>
        /// Функция возвращает накопленное значение некоторой суммы через указанный период
        /// с заданной ставкой процента ежегодного начисления, количеством начисления
        /// процентов и срока кредитования.
        /// </summary>
        /// <param name="volume">Начальная сумма</param>
        /// <param name="rate">Ставка ежегодного начисления (0.1 - 10%, 0.15 - 15% ...)</param>
        /// <param name="charge">Количество начислений процентов в году (1-ежегодное, 2-полугодовое, 4-ежеквартальное, 12-ежемесячное ...)</param>
        /// <param name="period">Срок кредитования в годах</param>
        /// <returns>Сумму накопленного значения</returns>
        public double AccumulatedValue(double volume, double rate, int charge, double period)
        {
            return (charge == 0) ? 0 : (double)(volume * Math.Pow((1.0 + rate / (double)charge), (period * charge)));
        }

        /// <summary>
        /// Функция возвращает приведенное значение накопленной суммы на текущий момент,
        /// с учетом заданного процента ежегодного начисления и срока накопления.
        /// </summary>
        /// <param name="volume">Сумма накопленного значения</param>
        /// <param name="rate">Ставка ежегодного начисления (0.1 - 10%, 0.15 - 15% ...)</param>
        /// <param name="period">Срок кредитования в годах</param>
        /// <returns>Приведенное значение накопленной суммы на текущий момент</returns>
        public double ReducedValue(double volume, double rate, double period)
        {
            return (double)volume * Math.Pow((1 + rate), -period);
        }

        #endregion

        /// <summary>
        /// Расчет будущей цены опциона с учетом заданной волатильности
        /// </summary>
        /// <param name="basePrice">Цена базового актива</param>
        /// <param name="strike">Цена страйк</param>
        /// <param name="expTime">Время до экспирации в долях года (с учетом рабочих дней биржи)</param>
        /// <param name="sigma">Волатильность (всегда положительное число; без перевода в проценты)</param>
        /// <param name="pctRate">Процентная ставка без риска (В ПРОЦЕНТАХ)</param>
        /// <param name="isCall">Тип опциона, True = CALL</param>
        /// <returns>Цена опциона</returns>
        public static double GetOptionPrice(double basePrice, double strike, double expTime, double sigma, double pctRate, bool isCall = true)
        {
            if ((basePrice <= 0) || (strike <= 0) ||
                (expTime <= 0) || (sigma <= 0))
                return 0;

            double sst = sigma * sigma * expTime;
            double sqst = Math.Sqrt(sst);

            double lnP = Math.Log(basePrice / strike);

            // [2015-12-09] Википедия даёт другую формулу ПРИ УЧЕТЕ ПРОЦЕНТНОЙ СТАВКИ
            // https://en.wikipedia.org/wiki/Black%E2%80%93Scholes_model
            double rate = pctRate / 100.0;

            //double d1 = (lnP + 0.5 * sst) / sqst;
            //double d2 = (lnP - 0.5 * sst) / sqst;

            double d1 = (lnP + 0.5 * sst + rate * expTime) / sqst;
            double d2 = (lnP - 0.5 * sst + rate * expTime) / sqst;

            double normDistD1;
            try
            {
                normDistD1 = StatMath.NormalDistribution(d1);
            }
            catch (ArithmeticException arex)
            {
                ArgumentException argEx = new ArgumentException(
                    String.Format("d1:{0}; lnP:{1}; basePrice:{2}; strike:{3}; sigma:{4}; sst:{5}; sqst:{6}",
                        d1, lnP, basePrice, strike, sigma, sst, sqst),
                    "d1", arex);
                throw argEx;
            }

            double normDistD2;
            try
            {
                normDistD2 = StatMath.NormalDistribution(d2);
            }
            catch (ArithmeticException arex)
            {
                ArgumentException argEx = new ArgumentException(
                    String.Format("d2:{0}; lnP:{1}; basePrice:{2}; strike:{3}; sigma:{4}; sst:{5}; sqst:{6}",
                        d2, lnP, basePrice, strike, sigma, sst, sqst),
                    "d2", arex);
                throw argEx;
            }

            double expRt = Math.Exp(-rate * expTime);
            double callPx = basePrice * normDistD1 - expRt * strike * normDistD2;
            if (isCall)
            {
                //return Math.Exp(-rate * expTime) * (basePrice * normDistD1 - strike * normDistD2);
                return callPx;
            }
            else
            {
                //return Math.Exp(-rate * expTime) * (basePrice * (normDistD1 - 1) - strike * (normDistD2 - 1));
                
                // Цена ПУТ вычисляется через паритет:
                double putPx = callPx + expRt * strike - basePrice;
                return putPx;
            }
        }

        /// <summary>
        /// Расчет будущей цены стредла с учетом заданной волатильности
        /// </summary>
        /// <param name="basePrice">Цена базового актива</param>
        /// <param name="strike">Цена страйк</param>
        /// <param name="expTime">Время до экспирации в долях года (с учетом рабочих дней биржи)</param>
        /// <param name="sigma">Волатильность (всегда положительное число; без перевода в проценты)</param>
        /// <param name="pctRate">Процентная ставка без риска (В ПРОЦЕНТАХ)</param>
        /// <returns>Цена стредла</returns>
        public static double GetStradlePrice(double basePrice, double strike, double expTime, double sigma, double pctRate)
        {
            if ((basePrice <= 0) || (strike <= 0) ||
                (expTime <= 0) || (sigma <= 0))
                return 0;

            double rate = pctRate / 100.0;

            double sst = sigma * sigma * expTime;
            double sqst = Math.Sqrt(sst);

            double lnP = Math.Log(basePrice / strike);

            double putPx, callPx;
            {
                double d1 = (lnP + 0.5 * sst) / sqst;
                double d2 = (lnP - 0.5 * sst) / sqst;

                double normDistD1;
                try
                {
                    normDistD1 = StatMath.NormalDistribution(d1);
                }
                catch (ArithmeticException arex)
                {
                    ArgumentException argEx = new ArgumentException(
                        String.Format("d1:{0}; lnP:{1}; basePrice:{2}; strike:{3}; sigma:{4}; sst:{5}; sqst:{6}",
                            d1, lnP, basePrice, strike, sigma, sst, sqst),
                        "d1", arex);
                    throw argEx;
                }

                double normDistD2;
                try
                {
                    normDistD2 = StatMath.NormalDistribution(d2);
                }
                catch (ArithmeticException arex)
                {
                    ArgumentException argEx = new ArgumentException(
                        String.Format("d2:{0}; lnP:{1}; basePrice:{2}; strike:{3}; sigma:{4}; sst:{5}; sqst:{6}",
                            d2, lnP, basePrice, strike, sigma, sst, sqst),
                        "d2", arex);
                    throw argEx;
                }

                callPx = Math.Exp(-rate * expTime) * (basePrice * normDistD1 - strike * normDistD2);
                putPx = Math.Exp(-rate * expTime) *
                    (basePrice * (normDistD1 - 1) - strike * (normDistD2 - 1));
            }

            double res = putPx + callPx;
            return res;
        }

        /// <summary>
        /// Вычисляет текущую волатильность исходя из известной цены опциона и стандартных параметров
        /// </summary>
        /// <param name="basePrice">Цена базового актива</param>
        /// <param name="strike">Цена страйк</param>
        /// <param name="expTime">Время до экспирации в долях года</param>
        /// <param name="optPrice">Цена опциона в пунктах</param>
        /// <param name="pctRate">Процентная ставка (В ПРОЦЕНТАХ)</param>
        /// <param name="isCall">Тип опциона true = CALL</param>
        /// <returns>Вычисленное значение волатильности (без перевода в проценты)</returns>
        public static double GetOptionSigma(double basePrice, double strike, double expTime, double optPrice, double pctRate, bool isCall)
        {
            double precision;
            double iv = GetOptionSigma(basePrice, strike, expTime, optPrice, pctRate, isCall, out precision);
            return iv;
        }

        /// <summary>
        /// Вычисляет текущую волатильность исходя из известной цены опциона и стандартных параметров
        /// </summary>
        /// <param name="basePrice">Цена базового актива</param>
        /// <param name="strike">Цена страйк</param>
        /// <param name="expTime">Время до экспирации в долях года</param>
        /// <param name="optPrice">Цена опциона в пунктах</param>
        /// <param name="pctRate">Процентная ставка (В ПРОЦЕНТАХ)</param>
        /// <param name="isCall">Тип опциона true = CALL</param>
        /// <param name="precision">Точность вычисленного значения волатильности</param>
        /// <returns>Вычисленное значение волатильности (без перевода в проценты)</returns>
        public static double GetOptionSigma(double basePrice, double strike, double expTime, double optPrice, double pctRate, bool isCall, out double precision)
        {
            // максимум волатильности (оправданность предположения?)
            const double FMaxSigma = 12;
            const double SigmaStep = 0.00005;

            double fInitial = GetOptionPrice(basePrice, strike, expTime, SigmaStep, pctRate, isCall);
            if (optPrice <= fInitial)
            {
                precision = SigmaStep;
                return 0;
            }
            else
            {
                const double LargeSigmaStep = 0.05;
                int iCount = (int)(FMaxSigma / LargeSigmaStep);
                int tmpCount = Int32.MaxValue;

                for (int index = 1; index <= iCount; index++)
                {
                    double testPx = GetOptionPrice(basePrice, strike, expTime, (index * LargeSigmaStep), pctRate, isCall);
                    if ((index == iCount) || (optPrice < testPx))
                    {
                        tmpCount = index;
                        break;
                    }
                }

                // Аварийное завершение, когда предложенная цена зашкаливает за все мыслимые лимиты
                if (tmpCount >= iCount)
                {
                    precision = FMaxSigma * 2;
                    return FMaxSigma;
                }

                double iLeft = (tmpCount - 1) * LargeSigmaStep;
                double iRight = tmpCount * LargeSigmaStep;

                double price0;
                if (tmpCount == 1)
                {
                    // Защита от 0
                    price0 = fInitial;
                    iLeft = SigmaStep;
                }
                else
                {
                    price0 = GetOptionPrice(basePrice, strike, expTime, iLeft, pctRate, isCall);
                }
                double price1 = GetOptionPrice(basePrice, strike, expTime, iRight, pctRate, isCall);

                while (Math.Abs(iRight - iLeft) > SigmaStep)
                {
                    double iMed = 0.5 * (iLeft + iRight);
                    double tmpPrice = GetOptionPrice(basePrice, strike, expTime, iMed, pctRate, isCall);

                    if (tmpPrice > optPrice)
                    {
                        price1 = tmpPrice;
                        iRight = iMed;
                    }
                    else
                    {
                        price0 = tmpPrice;
                        iLeft = iMed;
                    }
                }

                precision = Math.Abs(iRight - iLeft);

                double fRet;
                if (!DoubleUtil.IsZero(price1 - price0))
                    fRet = (iLeft + (optPrice - price0) / (price1 - price0) * (iRight - iLeft));
                else
                    fRet = 0.5 * (iRight + iLeft);

                return fRet;
            }
        }

        /// <summary>
        /// Вычисляет текущую волатильность исходя из известной цены стредла и стандартных параметров
        /// </summary>
        /// <param name="basePrice">Цена базового актива</param>
        /// <param name="strike">Цена страйк</param>
        /// <param name="expTime">Время до экспирации в долях года</param>
        /// <param name="optPrice">Цена опциона в пунктах</param>
        /// <param name="pctRate">Процентная ставка (В ПРОЦЕНТАХ)</param>
        /// <param name="precision">Точность вычисленного значения волатильности</param>
        /// <returns>Вычисленное значение волатильности (без перевода в проценты)</returns>
        public static double GetStradleSigma(double basePrice, double strike, double expTime, double optPrice, double pctRate)
        {
            double precision;
            double iv = GetStradleSigma(basePrice, strike, expTime, optPrice, pctRate, out precision);
            return iv;
        }

        /// <summary>
        /// Вычисляет текущую волатильность исходя из известной цены стредла и стандартных параметров
        /// </summary>
        /// <param name="basePrice">Цена базового актива</param>
        /// <param name="strike">Цена страйк</param>
        /// <param name="expTime">Время до экспирации в долях года</param>
        /// <param name="optPrice">Цена опциона в пунктах</param>
        /// <param name="pctRate">Процентная ставка (В ПРОЦЕНТАХ)</param>
        /// <param name="precision">Точность вычисленного значения волатильности</param>
        /// <returns>Вычисленное значение волатильности (без перевода в проценты)</returns>
        public static double GetStradleSigma(double basePrice, double strike, double expTime, double optPrice, double pctRate, out double precision)
        {
            // максимум волатильности (оправданность предположения?)
            const double FMaxSigma = 12;
            const double SigmaStep = 0.0001;

            double fInitial = GetStradlePrice(basePrice, strike, expTime, SigmaStep, pctRate);
            if (optPrice <= fInitial)
            {
                precision = SigmaStep;
                return 0;
            }
            else
            {
                double fStep = 0.1;
                int iCount = (int)(FMaxSigma / fStep);
                int tmpCount = Int32.MaxValue;

                for (int index = 1; index <= iCount; index++)
                {
                    double testPx = GetStradlePrice(basePrice, strike, expTime, (index * fStep), pctRate);
                    if ((index == iCount) || (optPrice < testPx))
                    {
                        tmpCount = index;
                        break;
                    }
                }

                // Аварийное завершение, когда предложенная цена зашкаливает за все мыслимые лимиты
                if (tmpCount >= iCount)
                {
                    precision = FMaxSigma * 2;
                    return FMaxSigma;
                }

                double iLeft = (tmpCount - 1) * fStep;
                double iRight = tmpCount * fStep;

                double price0;
                if (tmpCount == 1)
                {
                    // Защита от 0
                    price0 = fInitial;
                    iLeft = SigmaStep;
                }
                else
                {
                    price0 = GetStradlePrice(basePrice, strike, expTime, iLeft, pctRate);
                }
                double price1 = GetStradlePrice(basePrice, strike, expTime, iRight, pctRate);

                while (Math.Abs(iRight - iLeft) > SigmaStep)
                {
                    double iMed = 0.5 * (iLeft + iRight);
                    double tmpPrice = GetStradlePrice(basePrice, strike, expTime, iMed, pctRate);

                    if (tmpPrice > optPrice)
                    {
                        price1 = tmpPrice;
                        iRight = iMed;
                    }
                    else
                    {
                        price0 = tmpPrice;
                        iLeft = iMed;
                    }
                }

                precision = Math.Abs(iRight - iLeft);

                double fRet;
                if (!DoubleUtil.IsZero(price1 - price0))
                    fRet = (iLeft + (optPrice - price0) / (price1 - price0) * (iRight - iLeft));
                else
                    fRet = 0.5 * (iRight + iLeft);

                return fRet;
            }
        }

        /// <summary>
        /// Теоретическая дельта опциона согласно модели Блека-Шолза
        /// </summary>
        /// <param name="basePrice">Цена базового актива</param>
        /// <param name="strike">Цена страйк</param>
        /// <param name="expTime">Время до экспирации в долях года (с учетом рабочих дней биржи)</param>
        /// <param name="sigma">Волатильность (всегда положительное число; без перевода в проценты)</param>
        /// <param name="pctRate">Процентная ставка без риска (В ПРОЦЕНТАХ)</param>
        /// <param name="isCall">Тип опциона, True = CALL</param>
        /// <returns>Дельта опциона</returns>
        public static double GetOptionDelta(double basePrice, double strike, double expTime, double sigma, double pctRate, bool isCall = true)
        {
            double rate = pctRate / 100.0;

            if (isCall)
            {
                if (expTime < Double.Epsilon)
                {
                    if (basePrice <= strike)
                        return 0;
                    else
                        return 1;
                }

                var variation = sigma * Math.Sqrt(expTime);
                var d1 = Math.Log(basePrice / strike) + rate * expTime + 0.5 * variation * variation;
                d1 /= variation;
                var nd1 = StatMath.NormalDistribution(d1);

                return nd1;
            }
            else
            {
                if (expTime < Double.Epsilon)
                {
                    if (basePrice >= strike)
                        return 0;
                    else
                        return -1;
                }

                var variation = sigma * Math.Sqrt(expTime);
                var d1 = Math.Log(basePrice / strike) + rate * expTime + 0.5 * variation * variation;
                d1 /= variation;
                var nd1 = StatMath.NormalDistribution(d1);

                return nd1 - 1.0;
            }
        }

        /// <summary>
        /// Теоретическая тета опциона согласно модели Блека-Шолза
        /// </summary>
        /// <param name="basePrice">Цена базового актива</param>
        /// <param name="strike">Цена страйк</param>
        /// <param name="expTime">Время до экспирации в долях года (с учетом рабочих дней биржи)</param>
        /// <param name="sigma">Волатильность (всегда положительное число; без перевода в проценты)</param>
        /// <param name="pctRate">Процентная ставка без риска (В ПРОЦЕНТАХ)</param>
        /// <param name="isCall">Тип опциона, True = CALL</param>
        /// <returns>Тета опциона</returns>
        public static double GetOptionTheta(double basePrice, double strike, double expTime, double sigma, double pctRate, bool isCall = true)
        {
            double rate = pctRate / 100.0;

            if (isCall)
            {
                if (expTime < Double.Epsilon)
                    return 0;

                var variation = sigma * Math.Sqrt(expTime);
                var d1 = Math.Log(basePrice / strike) + rate * expTime + 0.5 * variation * variation;
                d1 /= variation;
                var d2 = d1 - variation;

                var dnd1 = Math.Exp(-d1 * d1 / 2.0) / Math.Sqrt(2.0 * Math.PI);
                var nd2 = StatMath.NormalDistribution(d2);

                var result = -basePrice * dnd1 * sigma / 2.0 / Math.Sqrt(expTime) - rate * strike * nd2 * Math.Exp(-rate * expTime);

                // know-how: тета нормируется на число дней в году
                return result / TSLab.Script.Handlers.Options.OptionUtils.DaysInYear;
            }
            else
            {
                if (expTime < Double.Epsilon)
                    return 0;

                var variation = sigma * Math.Sqrt(expTime);
                var d1 = Math.Log(basePrice / strike) + rate * expTime + 0.5 * variation * variation;
                d1 /= variation;
                var d2 = d1 - variation;

                var dnd1 = Math.Exp(-d1 * d1 / 2.0) / Math.Sqrt(2.0 * Math.PI);
                var nd2 = StatMath.NormalDistribution(-d2);

                var result = -basePrice * dnd1 * sigma / 2.0 / Math.Sqrt(expTime) + rate * strike * nd2 * Math.Exp(-rate * expTime);

                // know-how: тета нормируется на число дней в году
                return result / TSLab.Script.Handlers.Options.OptionUtils.DaysInYear;
            }
        }

        /// <summary>
        /// Теоретическая вега опциона согласно модели Блека-Шолза
        /// </summary>
        /// <param name="basePrice">Цена базового актива</param>
        /// <param name="strike">Цена страйк</param>
        /// <param name="expTime">Время до экспирации в долях года (с учетом рабочих дней биржи)</param>
        /// <param name="sigma">Волатильность (всегда положительное число; без перевода в проценты)</param>
        /// <param name="pctRate">Процентная ставка без риска (В ПРОЦЕНТАХ)</param>
        /// <param name="isCall">Тип опциона, True = CALL</param>
        /// <returns>Вега опциона</returns>
        public static double GetOptionVega(double basePrice, double strike, double expTime, double sigma, double pctRate, bool isCall = true)
        {
            double rate = pctRate / 100.0;

            if (expTime < Double.Epsilon)
                return 0;

            var variation = sigma * Math.Sqrt(expTime);
            var d1 = Math.Log(basePrice / strike) + rate * expTime + 0.5 * variation * variation;
            d1 /= variation;

            var dPrice = basePrice * Math.Sqrt(expTime);

            var dnd1 = Math.Exp(-d1 * d1 / 2.0) / Math.Sqrt(2.0 * Math.PI);

            var result = dPrice * dnd1;

            // know-how: вегу нужно ещё разделить на 100, чтобы совместиться с РТС-ной
            // но мы этого делать не будем, чтобы не попадать на этот множитель в дальнейшем
            return result;
        }

        /// <summary>
        /// Теоретическая гамма опциона согласно модели Блека-Шолза
        /// </summary>
        /// <param name="basePrice">цена базового актива</param>
        /// <param name="strike">цена страйк</param>
        /// <param name="expTime">время до экспирации в долях года (с учетом рабочих дней биржи)</param>
        /// <param name="sigma">волатильность (всегда положительное число; без перевода в проценты)</param>
        /// <param name="pctRate">процентная ставка без риска (В ПРОЦЕНТАХ)</param>
        /// <param name="isCall">тип опциона, True = CALL</param>
        /// <returns>гамма опциона</returns>
        public static double GetOptionGamma(double basePrice, double strike, double expTime, double sigma, double pctRate, bool isCall = true)
        {
            double rate = pctRate / 100.0;

            if (expTime < Double.Epsilon)
                return 0;

            var variation = sigma * Math.Sqrt(expTime);
            var d1 = Math.Log(basePrice / strike) + rate * expTime + 0.5 * variation * variation;
            d1 /= variation;

            var dPrice = variation * basePrice;
            dPrice = 1.0 / dPrice;

            var dnd1 = Math.Exp(-d1 * d1 / 2.0) / Math.Sqrt(2.0 * Math.PI);

            var result = dPrice * dnd1;

            return result;
        }

        /// <summary>
        /// Перевести подразумеваемую волатильность к другому времени
        /// через условие равенства абсолютных цен опционов.
        /// 
        /// При условии, что безрисковая ставка равна 0!
        /// </summary>
        /// <param name="oldT">старое время</param>
        /// <param name="oldSigma">старая сигма (подразумеваемая волатильность)</param>
        /// <param name="newT">новое время</param>
        /// <returns>новое значение подразумеваемой волатильности (если всё удачно сложилось)</returns>
        public static double RescaleIvToAnotherTime(double oldT, double oldSigma, double newT)
        {
            double newSigma = oldSigma * Math.Sqrt(oldT / newT);
            return newSigma;
        }

        /// <summary>
        /// Перевести подразумеваемую волатильность к другому времени
        /// и к другой безрисковой ставке через условие равенства абсолютных цен опционов
        /// </summary>
        /// <param name="basePrice">цена базового актива</param>
        /// <param name="strike">цена страйк</param>
        /// <param name="isCall">Тип опциона, True = CALL</param>
        /// <param name="oldT">старое время</param>
        /// <param name="oldSigma">старая сигма (подразумеваемая волатильность)</param>
        /// <param name="oldPctRate">старая безрисковая ставка (В ПРОЦЕНТАХ)</param>
        /// <param name="newT">новое время</param>
        /// <param name="newPctRate">новая безрисковая ставка (В ПРОЦЕНТАХ)</param>
        /// <param name="newSigma">новое значение подразумеваемой волатильности (если всё удачно сложилось)</param>
        /// <returns>true -- если всё получилось</returns>
        public static bool TryRescaleIvToAnotherTime(double basePrice, double strike, bool isCall,
            double oldT, double oldSigma, double oldPctRate,
            double newT, double newPctRate, out double newSigma)
        {
            // Использование процентной ставки может сделать опцион "в деньгах"
            // дешевле внутренней стоимости. В итоге подобрать новую волатильность не получится.

            double precision;
            double optPx = FinMath.GetOptionPrice(basePrice, strike, oldT, oldSigma, oldPctRate, isCall);
            newSigma = FinMath.GetOptionSigma(basePrice, strike, newT, optPx, newPctRate, isCall, out precision);

            if (Double.IsNaN(newSigma) || (newSigma <= Double.Epsilon))
                return false;

#if DEBUG
            double testPx = FinMath.GetOptionPrice(basePrice, strike, newT, newSigma, newPctRate, isCall);
            string msg = String.Format(CultureInfo.InvariantCulture,
                "Условие равенства цен опционов не выполнено. optPx: {0}; testPx:{1}", optPx, testPx);
            Debug.Assert(Math.Abs(testPx - optPx) < 1e-3, msg);
#endif

            return true;
        }
    }
}
