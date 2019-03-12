using System;

namespace TSLab.Script.Handlers
{
    public class StatMath
    {
        /// <summary>
        /// Вычисляет значение интеграла вероятности
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static double Erf(double value)
        {
            var fX = Math.Abs(value);
            var fS = Math.Sign(value);
            var xSq = fX * fX;

            if (fX < 0.5)
            {
                double fP = 0.007547728033418631287834;
                fP = 0.288805137207594084924010 + xSq * fP;
                fP = 14.3383842191748205576712 + xSq * fP;
                fP = 38.0140318123903008244444 + xSq * fP;
                fP = 3017.82788536507577809226 + xSq * fP;
                fP = 7404.07142710151470082064 + xSq * fP;
                fP = 80437.3630960840172832162 + xSq * fP;
                
                double fQ = 0.0;
                fQ = 1.00000000000000000000000 + xSq * fQ;
                fQ = 38.0190713951939403753468 + xSq * fQ;
                fQ = 658.070155459240506326937 + xSq * fQ;
                fQ = 6379.60017324428279487120 + xSq * fQ;
                fQ = 34216.5257924628539769006 + xSq * fQ;
                fQ = 80437.3630960840172826266 + xSq * fQ;

                return fS * 1.1283791670955125738961589031 * fX * fP / fQ;
            }
            if (fX >= 10.0)
            {
                return fS;
            }
            return fS * (1.0 - ErfC(fX));
        }

        /// <summary>
        /// Вычисляет значение обратного интеграла вероятности
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static double InvErf(double value)
        {
            return InvNormalDistribution(0.5 * (value + 1.0)) / Math.Sqrt(2.0);
        }

        /// <summary>
        /// Вычисляет значение дополнительного интеграла вероятности
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static double ErfC(double value)
        {
            if (value < 0.0)
            {
                return (2.0 - ErfC(-value));
            }
            else if (value < 0.5)
            {
                return (1.0 - Erf(value));
            }
            else if (value >= 10)
            {
                return 0.0;
            }

            double fP = 0.0;
            fP = 0.5641877825507397413087057563 + value * fP;
            fP = 9.675807882987265400604202961 + value * fP;
            fP = 77.08161730368428609781633646 + value * fP;
            fP = 368.5196154710010637133875746 + value * fP;
            fP = 1143.262070703886173606073338 + value * fP;
            fP = 2320.439590251635247384768711 + value * fP;
            fP = 2898.0293292167655611275846 + value * fP;
            fP = 1826.3348842295112592168999 + value * fP;
            
            double fQ = 1.0;
            fQ = 17.14980943627607849376131193 + value * fQ;
            fQ = 137.1255960500622202878443578 + value * fQ;
            fQ = 661.7361207107653469211984771 + value * fQ;
            fQ = 2094.384367789539593790281779 + value * fQ;
            fQ = 4429.612803883682726711528526 + value * fQ;
            fQ = 6089.5424232724435504633068 + value * fQ;
            fQ = 4958.82756472114071495438422 + value * fQ;
            fQ = 1826.3348842295112595576438 + value * fQ;

            return Math.Exp(-(value * value)) * fP / fQ;
        }

        /// <summary>
        /// Вычисляет значение интеграла нормального распределения
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static double NormalDistribution(double value)
        {
            return (0.5 * (Erf(value / 1.41421356237309504880) + 1.0));
        }

        /// <summary>
        /// Вычисляет значение обратного интеграла нормального распределения
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static double InvNormalDistribution(double value)
        {
            var fExpm2 = 0.13533528323661269189;
            var fs2pi = 2.50662827463100050242;

            if (value <= 0)
            {
                return -double.MaxValue;
            }
            else if (value >= 1.0)
            {
                return double.MaxValue;
            }

            var fCode = 1.0;
            var tmpVal = value;

            if (tmpVal > (1.0 - fExpm2))
            {
                tmpVal = 1.0 - tmpVal;
                fCode = 0.0;
            }

            if (tmpVal > fExpm2)
            {
                tmpVal = tmpVal - 0.5;

                var tmpVal2 = tmpVal * tmpVal;

                var fP0 = -59.9633501014107895267;
                fP0 = 98.0010754185999661536 + tmpVal2 * fP0;
                fP0 = -56.6762857469070293439 + tmpVal2 * fP0;
                fP0 = 13.9312609387279679503 + tmpVal2 * fP0;
                fP0 = -1.23916583867381258016 + tmpVal2 * fP0;

                var fQ0 = 1.0;
                fQ0 = 1.95448858338141759834 + tmpVal2 * fQ0;
                fQ0 = 4.67627912898881538453 + tmpVal2 * fQ0;
                fQ0 = 86.3602421390890590575 + tmpVal2 * fQ0;
                fQ0 = -225.462687854119370527 + tmpVal2 * fQ0;
                fQ0 = 200.260212380060660359 + tmpVal2 * fQ0;
                fQ0 = -82.0372256168333339912 + tmpVal2 * fQ0;
                fQ0 = 15.9056225126211695515 + tmpVal2 * fQ0;
                fQ0 = -1.18331621121330003142 + tmpVal2 * fQ0;
                return (fs2pi * (value + value * tmpVal2 * fP0 / fQ0));
            }

            var fX = Math.Sqrt(-2.0 * Math.Log(value));
            var fX0 = fX - Math.Log(fX) / fX;
            var fZ = 1.0 / fX;

            var fX1 = 0.0;
            if (fX < 8.0)
            {
                double fP1 = 4.05544892305962419923;
                fP1 = 31.5251094599893866154 + fZ * fP1;
                fP1 = 57.1628192246421288162 + fZ * fP1;
                fP1 = 44.0805073893200834700 + fZ * fP1;
                fP1 = 14.6849561928858024014 + fZ * fP1;
                fP1 = 2.18663306850790267539 + fZ * fP1;
                fP1 = -1.40256079171354495875 * 0.1 + fZ * fP1;
                fP1 = -3.50424626827848203418 * 0.01 + fZ * fP1;
                fP1 = -8.57456785154685413611 * 0.0001 + fZ * fP1;

                double fQ1 = 1.0;
                fQ1 = 15.7799883256466749731 + fZ * fQ1;
                fQ1 = 45.3907635128879210584 + fZ * fQ1;
                fQ1 = 41.3172038254672030440 + fZ * fQ1;
                fQ1 = 15.0425385692907503408 + fZ * fQ1;
                fQ1 = 2.50464946208309415979 + fZ * fQ1;
                fQ1 = -1.42182922854787788574 * 0.1 + fZ * fQ1;
                fQ1 = -3.80806407691578277194 * 0.01 + fZ * fQ1;
                fQ1 = -9.33259480895457427372 * 0.0001 + fZ * fQ1;
                fX1 = fZ * fP1 / fQ1;
            }
            else
            {
                double fP2 = 3.23774891776946035970;
                fP2 = 6.91522889068984211695 + fZ * fP2;
                fP2 = 3.93881025292474443415 + fZ * fP2;
                fP2 = 1.33303460815807542389 + fZ * fP2;
                fP2 = 2.01485389549179081538 * 0.1 + fZ * fP2;
                fP2 = 1.23716634817820021358 * 0.01 + fZ * fP2;
                fP2 = 3.01581553508235416007 * 0.0001 + fZ * fP2;
                fP2 = 2.65806974686737550832 * 0.000001 + fZ * fP2;
                fP2 = 6.23974539184983293730 * 0.000000001 + fZ * fP2;
                
                double fQ2 = 1.0;
                fQ2 = 6.02427039364742014255 + fZ * fQ2;
                fQ2 = 3.67983563856160859403 + fZ * fQ2;
                fQ2 = 1.37702099489081330271 + fZ * fQ2;
                fQ2 = 2.16236993594496635890 * 0.1 + fZ * fQ2;
                fQ2 = 1.34204006088543189037 * 0.01 + fZ * fQ2;
                fQ2 = 3.28014464682127739104 * 0.0001 + fZ * fQ2;
                fQ2 = 2.89247864745380683936 * 0.000001 + fZ * fQ2;
                fQ2 = 6.79019408009981274425 * 0.000000001 + fZ * fQ2;
                fX1 = fZ * fP2 / fQ2;
            }

            fX = fX0 - fX1;

            return (fCode != 0.0) ? -fX : fX;
        }
    }
}
