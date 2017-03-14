using System;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class LimitQuotingStrategy : QuotingStrategy
    {
        private decimal _correctedLimitPrice;

        public LimitQuotingStrategy(Sides quotingSide, decimal quotingVolume, decimal quotePriceShift)
            : base(quotingSide, quotingVolume, quotePriceShift)
        {
        }

        protected sealed override void QuotingProcess()
        {
            //Order tempOrder;
            //if (OrderInWork == null)
            //{
            //    Quote quote = GetSuitableBestQuote(MarketDepth);
            //    _correctedLimitPrice = 0;
            //    var newPrice = quote.Price;
            //    var oldPrice = OrderInWork == null ? newPrice : OrderInWork.Price;

            //    tempOrder = this.CreateOrder(QuotingSide, newPrice + QuotePriceShift, RestVolume);
            //    OrderInWork = tempOrder;

            //    RegisterOrder(tempOrder);

            //    var rollRule = tempOrder.WhenRegistered(Connector)
            //        .Do(() =>
            //        {
            //            Security.WhenMarketDepthChanged(Connector)
            //                .Do(() =>
            //                    {
            //                        if (IsQuotingNeeded(MarketDepth, newPrice, oldPrice, RestVolume))
            //                        {
            //                            CancelOrder(OrderInWork);

            //                        }
            //                    })
            //                .Apply(this);
            //        })
            //        .Apply(this);

            //    tempOrder.WhenCancelFailed(Connector)
            //        .Or(tempOrder.WhenRegisterFailed(Connector))
            //        .Or(tempOrder.WhenMatched(Connector))
            //        .Or(tempOrder.WhenCanceled(Connector))
            //        .Do(() =>
            //        {
            //            Rules.RemoveRulesByToken(rollRule.Token, rollRule);
            //            OrderInWork = null;
            //        })
            //        .Once()
            //        .Apply(this);
            //}
        }

        protected override bool IsQuotingNeeded(MarketDepth md, decimal marketPrice, decimal currentQuotingPrice, decimal currentQuotingVolume)
        {
            return marketPrice != currentQuotingPrice
                || IsQuoteGapRepresent(out _correctedLimitPrice, marketPrice, GetSuitableQuotes(md)[1].Price, currentQuotingPrice);
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

        private bool IsQuoteGapRepresent(decimal marketBestPrice, decimal marketSecondPrice, decimal currentQuotingPrice)
        {
            if (marketBestPrice != currentQuotingPrice)
                return false;

            if (Math.Abs(currentQuotingPrice - marketSecondPrice) > Security.PriceStep.Value)
                return true;

            return false;
        }

        private bool IsQuoteGapRepresent(out decimal correctLimitBestPrice, decimal marketBestPrice,
            decimal marketSecondPrice, decimal currentQuotingPrice)
        {
            var answer = IsQuoteGapRepresent(marketBestPrice, marketSecondPrice, currentQuotingPrice);

            correctLimitBestPrice = answer ? marketSecondPrice : 0;

            return answer;
        }
    }
}
