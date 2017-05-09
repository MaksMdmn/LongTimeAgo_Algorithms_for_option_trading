using System;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;
using Trading.Common;

namespace Trading.Strategies
{
    public class MarketQuoterStrategy : QuoterStrategy
    {
        public decimal TargetPrice { get; }

        public MarketQuoterStrategy(Sides quotingSide, decimal quotingVolume, decimal targetPrice)
            : base(quotingSide, quotingVolume)
        {
            TargetPrice = targetPrice;
        }

        protected sealed override void QuotingProcess()
        {
            try
            {
                if (!OrderSynchronizer.IsAnyOrdersInWork
                    && PositionSynchronizer.IsPosAndTradesEven)
                {
                    Quote bestQuote = MarketDepth.GetSuitableBestMarketQuote(QuotingSide);

                    if (bestQuote == null) return;

                    decimal volume = Math.Abs(Volume) - Math.Abs(Position);

                    if (volume > 0 && IsMarketPriceAcceptableForQuoting(bestQuote.Price))
                    {
                        var order = this.CreateOrder(QuotingSide, Security.ShrinkPrice(TargetPrice), volume);

                        order.WhenRegistered(Connector)
                            .Do(o =>
                            {
                                try
                                {
                                    OrderSynchronizer.CancelCurrentOrder();
                                }
                                catch (InvalidOperationException ex)
                                {
                                    IncrMaxErrorCountIfNotScared();
                                    this.AddWarningLog("MaxErrorCount was incremented");
                                    this.AddErrorLog(ex);
                                }
                            })
                            .Once()
                            .Apply(this);

                        OrderSynchronizer.PlaceOrder(order);
                    }
                }
            }
            catch (Exception ex)
            {
                this.AddErrorLog(ex);
            }
        }

        private bool IsMarketPriceAcceptableForQuoting(decimal currentPrice)
        {
            if (TargetPrice <= 0)
                return false;

            return IsPriceAcceptableForQuoting(currentPrice, TargetPrice);
        }
    }
}
