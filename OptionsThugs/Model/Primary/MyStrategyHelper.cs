using System;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model.Primary
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
    }
}