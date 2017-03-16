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

        private Order _orderInWork;
        private decimal _positionValueOnStart;

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
                _positionValueOnStart = Position;

                if (MarketDepth == null) return;
                if (bestQuote == null) return;

                decimal price = bestQuote.Price + QuotePriceShift;
                decimal volume = Math.Abs(Volume - Position);

                if (volume > 0)
                {
                    _orderInWork = this.CreateOrder(QuotingSide, price, volume);

                    _orderInWork.WhenMatched(Connector)
                        .Or(_orderInWork.WhenCanceled(Connector))
                        .Do(o =>
                        {
                            SyncPositionByOrderExecution(o);
                        })
                        .Until(() => _orderInWork == null)
                        .Apply(this);

                    _orderInWork.WhenCancelFailed(Connector)
                        .Do(of =>
                        {
                            SyncPositionByOrderExecution(of.Order);
                        })
                        .Until(() => _orderInWork == null)
                        .Apply(this);

                    _orderInWork.WhenRegistered(Connector)
                        .Do(ProcessOrder)
                        .Once()
                        .Apply(this);

                    RegisterOrder(_orderInWork);
                }
            }
        }


        private void SyncPositionByOrderExecution(Order order)
        {
            var executedVolume = order.GetTrades(Connector).Sum(t => t.Trade.Volume);

            while (Math.Abs(_positionValueOnStart) + Math.Abs(executedVolume) != Math.Abs(Position))
            {
                //NOP
            }

            _orderInWork = null;
        }

        private void ProcessOrder()
        {
            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
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
