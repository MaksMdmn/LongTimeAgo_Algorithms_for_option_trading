using System;
using System.Collections.Generic;
using System.Linq;
using Ecng.Collections;
using Microsoft.Practices.ObjectBuilder2;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model.Trading.Common
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

        public static Quote GetSuitableBestLimitQuote(this MarketDepth md, Sides quotingSide)
        {
            if (md == null) return null;
            return quotingSide == Sides.Buy ? md.BestBid : md.BestAsk;
        }

        public static Quote[] GetSuitableLimitQuotes(this MarketDepth md, Sides quotingSide)
        {
            if (md == null) return null;
            return quotingSide == Sides.Buy ? md.Bids : md.Asks;
        }

        public static Quote GetSuitableBestMarketQuote(this MarketDepth md, Sides quotingSide)
        {
            if (md == null) return null;
            return quotingSide == Sides.Buy ? md.BestAsk : md.BestBid;
        }

        public static Quote[] GetSuitableMarketQuotes(this MarketDepth md, Sides quotingSide)
        {
            if (md == null) return null;
            return quotingSide == Sides.Buy ? md.Asks : md.Bids;
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

        public static void SafeStop(this Strategy strategy)
        {
            if (strategy?.ProcessState == ProcessStates.Started)
                strategy.Stop();
        }

        public static decimal CalculateWorstLimitSpreadPrice(this MarketDepth md, Sides dealSide,
            decimal desirableSpread)
        {
            if (!md.CheckIfSpreadExist())
                throw new ArgumentException("spread does not exist, calculation impossible: " + md.BestPair);

            var diff = md.BestPair.SpreadPrice.Value - desirableSpread;
            var halfDiff = diff / 2;

            if (diff % 2 == 0)
                return md.Security.ShrinkPrice(dealSide == Sides.Buy ? md.BestBid.Price + halfDiff : md.BestAsk.Price - halfDiff);



            return md.Security.ShrinkPrice(dealSide == Sides.Buy
                ? md.BestBid.Price + Math.Floor(halfDiff)
                : md.BestAsk.Price - Math.Ceiling(halfDiff));
        }

        public static bool CheckIfSpreadExist(this MarketDepth md)
        {
            return md?.BestBid != null && md.BestAsk != null;
        }

        public static bool CheckIfQuotesEqual(this Quote marketQuote, Quote myQuote, bool includingVolumes = true)
        {
            if (marketQuote == null || myQuote == null)
                throw new NullReferenceException($"one of quotes is null, comparing impossible: market- {marketQuote} my- {myQuote}");

            return includingVolumes
                ? marketQuote.Price == myQuote.Price && marketQuote.Volume == myQuote.Volume
                : marketQuote.Price == myQuote.Price;
        }
    }
}