using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Ecng.Collections;
using Microsoft.Practices.ObjectBuilder2;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model.Common
{
    public static class MyTradeHelper
    {
        public static decimal GetMarketPrice(this Security security, Sides orderSide)
        {
            var result = orderSide == Sides.Buy
                ? security.MaxPrice.CheckIfValueNullThenZero()
                : security.MinPrice.CheckIfValueNullThenZero();

            if (result <= 0)
                throw new ArgumentException($"Check market prices, something going wrong with: {security}");

            return result;
        }

        public static decimal CheckIfValueNullThenZero(this decimal? val)
        {
            return val ?? 0;
        }

        public static bool CheckIfWasCrossedByPrice(this PriceHedgeLevel level, decimal currentPrice)
        {
            switch (level.Direction)
            {
                case PriceDirection.Down:
                    if (level.Price >= currentPrice)
                        return true;
                    break;
                case PriceDirection.Up:
                    if (level.Price <= currentPrice)
                        return true;
                    break;
            }

            return false;
        }

        public static decimal GetSecurityPosition(this IConnector connector, Portfolio portfolio, Security security)
        {
            return connector.GetPosition(portfolio, security).CurrentValue.CheckIfValueNullThenZero();
        }

        public static SynchronizedDictionary<Security, decimal> GetSecuritiesPositions(this IConnector connector, Portfolio portfolio,
            List<Security> securities)
        {
            SynchronizedDictionary<Security, decimal> result = new SynchronizedDictionary<Security, decimal>();

            securities.ForEach(s => { result.Add(s, GetSecurityPosition(connector, portfolio, s)); });

            return result;
        }

        public static List<Security> GetSecuritiesWithPositions(this IConnector connector)
        {
            var result = new List<Security>();
            connector.Positions.ForEach(p => { result.Add(p.Security); });

            return result;
        }

        public static List<Security> GetSecuritiesWithPositions(this IConnector connector, SecurityTypes securitiesType)
        {
            return GetSecuritiesWithPositions(connector).Where(s => s.Type == securitiesType).ToList();
        }


        public static decimal PrepareSizeToTrade(this decimal size, bool useAbsOrNot = true)
        {
            return useAbsOrNot ? Math.Floor(Math.Abs(size)) : Math.Floor(size);
        }

        public static decimal ShrinkSizeToTrade(this decimal orientiedSize, Sides dealSide,
            decimal currentPosition, decimal limitedPosition)
        {
            switch (dealSide)
            {
                case Sides.Buy:
                    if (currentPosition >= limitedPosition)
                        return 0;

                    if (currentPosition + orientiedSize > limitedPosition)
                        return (limitedPosition - currentPosition).PrepareSizeToTrade();

                    return orientiedSize;
                case Sides.Sell:
                    if (currentPosition <= limitedPosition)
                        return 0;

                    if (currentPosition - orientiedSize < limitedPosition)
                        return (limitedPosition - currentPosition).PrepareSizeToTrade();

                    return orientiedSize;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dealSide), dealSide, @"impossible value of deal type");
            }
        }
    }
}