using System;
using System.Linq;
using System.Threading;
using Microsoft.Practices.ObjectBuilder2;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class LimitQuotingStrategy : QuotingStrategy
    {
        public decimal QuotePriceShift { get; private set; }

        private volatile Order _orderInWork;
        private decimal _oldPosValue;

        public LimitQuotingStrategy(Sides quotingSide, decimal quotingVolume, decimal quotePriceShift)
            : base(quotingSide, quotingVolume)
        {
            QuotePriceShift = quotePriceShift;
            _orderInWork = null;
        }

        protected sealed override void QuotingProcess()
        {
            if (_orderInWork == null)
            {
                Quote bestQuote = GetSuitableBestQuote(MarketDepth);

                if (MarketDepth == null) return;
                if (bestQuote == null) return;

                decimal price = bestQuote.Price + QuotePriceShift;
                decimal volume = Math.Abs(Volume - Position);

                if (volume > 0)
                {
                    _orderInWork = this.CreateOrder(QuotingSide, price, volume);

                    _orderInWork.WhenChanged(Connector)
                        .Do(o =>
                        {
                            if (o.State == OrderStates.Done || o.State == OrderStates.Failed)
                            {
                                _orderInWork = null;
                            }
                        })
                        .Until(() => _orderInWork == null)
                        .Apply(this);

                    //_orderInWork.WhenNewTrade(Connector)
                    //    .Do(WaitForEqualsPositions)
                    //    .Apply(this);

                    _orderInWork.WhenRegistered(Connector)
                        .Do(ProcessOrder)
                        .Once()
                        .Apply(this);

                    RegisterOrder(_orderInWork);
                }
            }
        }

        private void WaitForEqualsPositions()
        {
            int safeCounter = 0;
            int maxSafeCounter = 100;
            int delay = 50;

            Rules.ForEach(rule => rule.Suspend(true));

            while (Position == _oldPosValue)
            {
                Thread.Sleep(delay);
                safeCounter++;

                if (safeCounter > maxSafeCounter)
                    throw new TimeoutException("have no respond from terminal, timeout: " + maxSafeCounter * delay);
            }
            _oldPosValue = Position;

            Rules.ForEach(rule => rule.Suspend(false));
        }

        private void ProcessOrder()
        {
            Security.WhenMarketDepthChanged(Connector)
                .Do((mr, md) =>
                {
                    if (_orderInWork != null && IsQuotingNeeded(md, _orderInWork.Price))
                    {
                        CancelOrder(_orderInWork);
                        _orderInWork = null;
                    }
                })
                .Until(() => _orderInWork == null)
                .Apply(this);
        }

        private bool IsQuotingNeeded(MarketDepth md, decimal currentQuotingPrice)
        {
            Quote bestQuote = GetSuitableBestQuote(md);
            Quote preBestQuote = GetSuitableQuotes(md)[1]; // 2ая лучшая котировка

            if (bestQuote == null || preBestQuote == null)
                return true; // снять заявку

            if (bestQuote.Price != currentQuotingPrice)
                return true; // цена выше бида или ниже аска

            if (Math.Abs(currentQuotingPrice - preBestQuote.Price) > Security.PriceStep.Value)
                return true; //есть гэп котировок в стакане и мы стоим выше чем на 1 шаг от лучшей котировки

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
