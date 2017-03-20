using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Strategies.Quoting;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class MyMarketQuotingStrategy : MyQuotingStrategy
    {
        public decimal TargetPrice { get; }

        public MyMarketQuotingStrategy(Sides quotingSide, decimal quotingVolume, decimal targetPrice) : base(quotingSide, quotingVolume)
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
                    Quote bestQuote = GetSuitableBestMarketQuote();

                    if (bestQuote == null) return;

                    decimal volume = Math.Abs(Volume) - Math.Abs(Position);

                    if (volume > 0 && IsMarketPriceAcceptableForQuoting(bestQuote.Price))
                    {
                        var order = this.CreateOrder(QuotingSide, TargetPrice, volume);

                        order.WhenRegistered(Connector)
                            .Do(o =>
                            {
                                OrderSynchronizer.CancelCurrentOrder();
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
