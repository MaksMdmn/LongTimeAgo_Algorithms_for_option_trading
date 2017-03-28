using System;
using StockSharp.Messages;

namespace OptionsThugs.Model.Common
{
    public static class GreeksCalculator
    {
        public static decimal DaysInYear = 365;
        public static decimal MaxVolaValue = 3;
        public static int NumberOfDecimalPlaces = 4;

        public static decimal CalculateDelta(OptionTypes type, decimal spotPrice, decimal strike, decimal daysLeft, decimal daysInYear, decimal vola)
        {
            decimal result = 0;

            switch (type)
            {
                case OptionTypes.Call:
                    result = (decimal)CalculateDistributionOfStNrmDstr(Calculate_d1(spotPrice, strike, daysLeft, daysInYear, vola));
                    break;
                case OptionTypes.Put:
                    result = (decimal)CalculateDistributionOfStNrmDstr(Calculate_d1(spotPrice, strike, daysLeft, daysInYear, vola)) - 1;
                    break;
                default:
                    break;
            }

            return GetRoundValue(result);
        }

        public static decimal CalculateDelta(OptionTypes type, decimal distr_d1)
        {
            decimal result = 0;

            switch (type)
            {
                case OptionTypes.Call:
                    result = distr_d1;
                    break;
                case OptionTypes.Put:
                    result = distr_d1 - 1;
                    break;
                default:
                    break;
            }
            return GetRoundValue(result);
        }

        public static decimal CalculateGamma(decimal spotPrice, decimal strike, decimal daysLeft, decimal daysInYear, decimal vola)
        {
            decimal optionTime = daysLeft / daysInYear;
            decimal d1 = Calculate_d1(spotPrice, strike, daysLeft, daysInYear, vola);

            return GetRoundValue(GreeksDistribution(d1) / (spotPrice * vola * (decimal)Math.Sqrt((double)optionTime)));
        }

        public static decimal CalculateGamma(decimal spotPrice, decimal distr_d1, decimal optionTime, decimal vola)
        {
            return GetRoundValue(distr_d1 / (spotPrice * vola * (decimal)Math.Sqrt((double)optionTime)));
        }

        public static decimal CalculateVega(decimal spotPrice, decimal strike, decimal daysLeft, decimal daysInYear, decimal vola)
        {
            decimal optionTime = daysLeft / daysInYear;
            decimal d1 = Calculate_d1(spotPrice, strike, daysLeft, daysInYear, vola);

            return GetRoundValue((spotPrice * (decimal)Math.Sqrt((double)optionTime) * GreeksDistribution(d1)) / 100);
        }

        public static decimal CalculateVega(decimal spotPrice, decimal distr_d1, decimal optionTime)
        {
            return GetRoundValue((spotPrice * (decimal)Math.Sqrt((double)optionTime) * distr_d1) / 100);
        }

        public static decimal CalculateTheta(decimal spotPrice, decimal strike, decimal daysLeft, decimal daysInYear, decimal vola)
        {
            decimal optionTime = daysLeft / daysInYear;
            decimal d1 = Calculate_d1(spotPrice, strike, daysLeft, daysInYear, vola);

            return GetRoundValue(-(spotPrice * vola * GreeksDistribution(d1)) / (2 * (decimal)Math.Sqrt((double)optionTime)) / daysInYear);
        }

        public static decimal CalculateTheta(decimal spotPrice, decimal ditsr_d1, decimal daysInYear, decimal optionTime, decimal vola, bool overloadProblem_nvm)
        {
            return GetRoundValue(-(spotPrice * vola * ditsr_d1) / (2 * (decimal)Math.Sqrt((double)optionTime)) / daysInYear);
        }

        public static decimal CalculateOptionPrice_BS(OptionTypes type, decimal spotPrice, decimal strike, decimal daysLeft, decimal daysInYear, decimal vola)
        {
            double d1 = 0.0;
            double d2 = 0.0;
            double interestRate = 0.0;
            double optionTime = (double)(daysLeft / daysInYear);
            double dBlackScholes = 0.0;

            d1 = (double)Calculate_d1(spotPrice, strike, daysLeft, daysInYear, vola);
            d2 = (double)Calculate_d2(spotPrice, strike, daysLeft, daysInYear, vola);

            switch (type)
            {
                case OptionTypes.Call:
                    dBlackScholes = (double)spotPrice * CalculateDistributionOfStNrmDstr((decimal)d1) - (double)strike * Math.Exp(-interestRate * optionTime) * CalculateDistributionOfStNrmDstr((decimal)d2);
                    break;
                case OptionTypes.Put:
                    dBlackScholes = (double)strike * Math.Exp(-interestRate * optionTime) * CalculateDistributionOfStNrmDstr((decimal)-d2) - (double)spotPrice * CalculateDistributionOfStNrmDstr((decimal)-d1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
            return (decimal)dBlackScholes;
        }
        public static decimal Calculate_d1(decimal spotPrice, decimal strike, decimal daysLeft, decimal daysInYear, decimal vola)
        {
            double interestRate = 0.0;
            double optionTime = (double)(daysLeft / daysInYear);

            double tempRes = (Math.Log((double)(spotPrice / strike)) + (interestRate + (double)(vola * vola) / 2.0) * optionTime) / ((double)vola * Math.Sqrt(optionTime));

            return (decimal)tempRes;
        }
        public static decimal Calculate_d2(decimal spotPrice, decimal strike, decimal daysLeft, decimal daysInYear, decimal vola)
        {
            double optionTime = (double)(daysLeft / daysInYear);

            return (decimal)((double)Calculate_d1(spotPrice, strike, daysLeft, daysInYear, vola) - (double)vola * Math.Sqrt(optionTime));
        }
        public static double CalculateDistributionOfStNrmDstr(decimal value)
        {
            double result;

            double L = 0.0;
            double K = 0.0;
            double dCND = 0.0;
            const double a1 = 0.31938153;
            const double a2 = -0.356563782;
            const double a3 = 1.781477937;
            const double a4 = -1.821255978;
            const double a5 = 1.330274429;
            L = Math.Abs((double) value);
            K = 1.0 / (1.0 + 0.2316419 * L);
            dCND = 1.0 - 1.0 / Math.Sqrt(2 * Convert.ToDouble(Math.PI.ToString())) *
                Math.Exp(-L * L / 2.0) * (a1 * K + a2 * K * K + a3 * Math.Pow(K, 3.0) +
                a4 * Math.Pow(K, 4.0) + a5 * Math.Pow(K, 5.0));

            if (value < 0)
            {
                result = 1.0 - dCND;
            }
            else
            {
                result = dCND;
            }
            return result;
        }

        public static decimal GreeksDistribution(decimal value)
        {
            decimal result = (decimal)(Math.Exp((double)(value * value) * 0.5 * -1) / Math.Sqrt(2 * Math.PI));
            return result;
        }

        public static decimal CalculateImpliedVolatility(OptionTypes type, decimal spotPrice, decimal strike, decimal daysLeft, 
            decimal daysInYear, decimal optionPrice, decimal volaGuess)
        {
            double dVol = 0.00001;
            double epsilon = 0.00001;
            double maxIterNumber = 100;
            double vol1 = (double) volaGuess;
            int i = 1;

            double vol2 = 0.0;
            double tempVal1 = 0.0;
            double tempVal2 = 0.0;
            double dx = 0.0;


            while (true)
            {
                tempVal1 = (double)CalculateOptionPrice_BS(type, spotPrice, strike, daysLeft, daysInYear, (decimal)vol1);
                vol2 = vol1 - dVol;
                tempVal2 = (double)CalculateOptionPrice_BS(type, spotPrice, strike, daysLeft, daysInYear, (decimal)vol2);
                dx = (tempVal2 - tempVal1) / dVol;

                if (Math.Abs(dx) < epsilon || i == maxIterNumber)
                {
                    break;
                }

                vol1 = vol1 - ((double)optionPrice - tempVal1) / dx;

                i++;
            }

            return GetRoundValue((decimal)vol1);
        }

        public static decimal GetFilteredVolatilityValue(decimal value)
        {
            return value > MaxVolaValue || value < 0 ? 0 : value;
        }

        //public static decimal CalculateOptionPnLOnExpiration(Security option, decimal expirPrice)
        //{
        //    decimal result = 0.0;

        //    if (option.Position.Quantity == 0)
        //    {
        //        throw new ArgumentException("option's position is zero, PnL calculations are impossible to do.");
        //    }
        //    else
        //    {
        //        if (option.OptionTypes == OptionTypes.Call)
        //        {
        //            decimal deltaInPrices = expirPrice - option.Strike;
        //            decimal optPremium = -1 * option.Position.EnterPrice * option.Position.Quantity;
        //            int optPosizion = option.Position.Quantity;

        //            if (option.Position.Quantity > 0)
        //            {
        //                if (expirPrice > option.Strike)
        //                {
        //                    result = deltaInPrices * optPosizion + optPremium;
        //                }
        //                else
        //                {
        //                    result = optPremium;
        //                }
        //            }
        //            else
        //            {
        //                if (expirPrice < option.Strike)
        //                {
        //                    result = optPremium;
        //                }
        //                else
        //                {
        //                    result = deltaInPrices * optPosizion + optPremium;
        //                }
        //            }
        //        }

        //        if (option.OptionTypes == OptionTypes.Put)
        //        {
        //            decimal deltaInPrices = option.Strike - expirPrice;
        //            decimal optPremium = -1 * option.Position.EnterPrice * option.Position.Quantity;
        //            int optPosizion = option.Position.Quantity;

        //            if (option.Position.Quantity > 0)
        //            {
        //                if (expirPrice < option.Strike)
        //                {
        //                    result = deltaInPrices * optPosizion + optPremium;
        //                }
        //                else
        //                {
        //                    result = optPremium;
        //                }
        //            }
        //            else
        //            {
        //                if (expirPrice > option.Strike)
        //                {
        //                    result = optPremium;
        //                }
        //                else
        //                {
        //                    result = deltaInPrices * optPosizion + optPremium;
        //                }
        //            }
        //        }
        //    }

        //    return result;
        //}


        private static decimal GetRoundValue(decimal value)
        {
            return Math.Round(value, NumberOfDecimalPlaces);
        }


    }
}