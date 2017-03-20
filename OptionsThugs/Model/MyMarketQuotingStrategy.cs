using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Strategies.Quoting;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class MyMarketQuotingStrategy : MyQuotingStrategy
    {
        //private readonly decimal _targetPrice;
        //public MarketMyQuotingStrategy(Sides quotingSide, decimal quotingVolume, decimal quotePriceShift, decimal targetPrice)
        //    : base(quotingSide, quotingVolume, quotePriceShift)
        //{
        //    _targetPrice = targetPrice;
        //}

        //protected sealed override void QuotingProcess()
        //{
        //    Quote quote = GetSuitableBestQuote(MarketDepth);

        //    if (quote == null) return;

        //    var marketPrice = quote.Price;

        //    if (IsQuotingNeeded(MarketDepth, marketPrice, 0, RestVolume))
        //    {
        //        RegisterOrder(this.CreateOrder(QuotingSide, marketPrice, RestVolume));
        //        CancelOrder(OrderInWork);
        //    }
        //}

        //protected override bool IsQuotingNeeded(MarketDepth md, decimal marketPrice, decimal currentQuotingPrice, decimal currentQuotingVolume)
        //{
        //    if (marketPrice <= 0 || _targetPrice <= 0) return false;

        //    if (QuotingSide == Sides.Buy)
        //    {
        //        return marketPrice >= _targetPrice;
        //    }
        //    else
        //    {
        //        return marketPrice <= _targetPrice;
        //    }
        //}

        //private Quote GetSuitableBestQuote(MarketDepth depth)
        //{
        //    if (depth == null) return null;
        //    return QuotingSide == Sides.Buy ? depth.BestAsk : depth.BestBid;
        //}
        public MyMarketQuotingStrategy(Sides quotingSide, decimal quotingVolume) : base(quotingSide, quotingVolume)
        {
        }

        protected override void QuotingProcess()
        {
            throw new NotImplementedException();
        }
    }
}
