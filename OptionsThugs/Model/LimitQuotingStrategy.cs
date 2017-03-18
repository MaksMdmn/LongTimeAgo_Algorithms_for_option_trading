using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.ObjectBuilder2;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class LimitQuotingStrategy : QuotingStrategy
    {
        public decimal QuotePriceShift { get; }
        public decimal StopQuotingPrice { get; }

        public LimitQuotingStrategy(Sides quotingSide, decimal quotingVolume, decimal quotePriceShift)
            : base(quotingSide, quotingVolume)
        {
            QuotePriceShift = quotePriceShift;
            StopQuotingPrice = 0;
        }

        public LimitQuotingStrategy(Sides quotingSide, decimal quotingVolume, decimal quotePriceShift, decimal stopQuotingPrice)
            : base(quotingSide, quotingVolume)
        {
            QuotePriceShift = quotePriceShift;
            StopQuotingPrice = stopQuotingPrice;
        }

        protected sealed override void QuotingProcess()
        {
            try
            {
                if (!OrderSynchronizer.IsAnyOrdersInWork)
                {
                    Quote bestQuote = GetSuitableBestQuote(MarketDepth);

                    if (MarketDepth == null) return;
                    if (bestQuote == null) return;

                    decimal price = bestQuote.Price + QuotePriceShift;
                    decimal volume = Math.Abs(Volume - Position);

                    if (volume > 0 && IsMarketPriceAcceptableForQuoting(price))
                    {
                        var order = this.CreateOrder(QuotingSide, price, volume);

                        order.WhenRegistered(Connector)
                            .Do(() => ProcessOrder(order))
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

        private void ProcessOrder(Order order)
        {
            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    if (OrderSynchronizer.IsAnyOrdersInWork
                    && IsQuotingNeeded(md, order.Price))
                    {
                        OrderSynchronizer.CancelCurrentOrder();
                    }
                })
                //.Until(() => !IsActiveOrderRepresent)
                .Apply(this);
        }

        private bool IsQuotingNeeded(MarketDepth md, decimal currentQuotingPrice)
        {
            Quote bestQuote = GetSuitableBestQuote(md);
            Quote preBestQuote = GetSuitableQuotes(md)[1]; // 2ая лучшая котировка

            if (bestQuote == null || preBestQuote == null)
                return true; // снять заявку

            if (!IsMarketPriceAcceptableForQuoting(bestQuote.Price))
                return true; // снять заявку

            if (bestQuote.Price != currentQuotingPrice)
                return true; // цена выше бида или ниже аска

            if (Math.Abs(currentQuotingPrice - preBestQuote.Price) > Security.PriceStep.Value)
                return true; //есть гэп котировок в стакане и мы стоим выше чем на 1 шаг от лучшей котировки

            return false;
        }

        private bool IsMarketPriceAcceptableForQuoting(decimal price)
        {
            if (StopQuotingPrice == 0)
                return true;

            if (QuotingSide == Sides.Buy)
            {
                if (price <= StopQuotingPrice)
                    return true;
            }
            else
            {
                if (price >= StopQuotingPrice)
                    return true;
            }

            return false;
        }


        private Quote GetSuitableBestQuote(MarketDepth depth)
        {
            if (depth == null) return null;
            return QuotingSide == Sides.Buy ? depth.BestBid : depth.BestAsk;
        }

        private Quote[] GetSuitableQuotes(MarketDepth depth)
        {
            if (depth == null) return null;
            return QuotingSide == Sides.Buy ? depth.Bids : depth.Asks;
        }

    }
}
