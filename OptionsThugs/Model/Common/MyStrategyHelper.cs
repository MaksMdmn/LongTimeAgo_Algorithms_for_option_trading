using System;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model.Common
{
    public static class MyStrategyHelper
    {
        public static decimal GetMarketPrice(this Security security, Sides orderSide, IConnector connector)
        {
            MarketDepth md = connector.GetMarketDepth(security);

            //TODO null проверочки

            return orderSide == Sides.Buy
                ? md.BestAsk.Price + 10 * security.PriceStep.Value
                : md.BestBid.Price - 10 * security.PriceStep.Value;
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

        public static decimal PrepareSizeToTrade(this decimal size, bool useAbsOrNot = true)
        {
            return useAbsOrNot ? Math.Floor(Math.Abs(size)) : Math.Floor(size);
        }
    }
}