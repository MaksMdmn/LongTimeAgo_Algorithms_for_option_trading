using System;
using System.Threading;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Strategies.Quoting;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class CatcherStrategy : BestByPriceQuotingStrategy
    {
        public CatcherStrategy(Sides quotingDirection, decimal quotingVolume) : base(quotingDirection, quotingVolume)
        {
        }

        public CatcherStrategy(Order order, Unit bestPriceOffset) : base(order, bestPriceOffset)
        {
        }
       
    }
}
