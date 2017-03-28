using System;
using OptionsThugs.Model.Common;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model.Primary
{
    public abstract class QuoterStrategy : Strategy
    {
        private readonly int _timeout = 2000;

        protected Sides QuotingSide { get; private set; }
        protected MarketDepth MarketDepth { get; private set; }
        protected OrderSynchronizer OrderSynchronizer { get; private set; }
        protected PositionSynchronizer PositionSynchronizer { get; private set; }

        protected QuoterStrategy(Sides quotingSide, decimal quotingVolume)
        {
            QuotingSide = quotingSide;
            Volume = quotingVolume;
            OrderSynchronizer = new OrderSynchronizer(this);
            PositionSynchronizer = new PositionSynchronizer();

            OrderSynchronizer.Timeout = _timeout;
            PositionSynchronizer.Timeout = _timeout;

            this.WhenPositionChanged()
                .Do(p => PositionSynchronizer.NewPositionChange(p))
                .Apply(this);

            this.WhenNewMyTrade()
                .Do(mt => PositionSynchronizer.NewTradeChange(mt.Trade.Volume))
                .Apply(this);


            CancelOrdersWhenStopping = true;
            CommentOrders = true;
            DisposeOnStop = false;
            MaxErrorCount = 10;
            OrdersKeepTime = TimeSpan.Zero;
        }

        protected override void OnStarted()
        {
            //TODO обработчики ретёрнов
            if (Connector == null || Security == null || Portfolio == null) return;
            if (Volume <= 0) return;
            
            Connector.RegisterMarketDepth(Security);

            MarketDepth = GetMarketDepth(Security);

            //start here
            Security.WhenMarketDepthChanged(Connector)
                .Do(QuotingProcess)
                .Apply(this);

            this.WhenPositionChanged()
                .Do(() =>
                {
                    if (Math.Abs(Position) >= Volume)
                    {
                        //TODO логи
                        Stop();
                    }
                })
                .Apply(this);

            //TODO this.WhenError();  this.Connector.OrderRegisterFailed;

            base.OnStarted();
        }

        protected void IncrMaxErrorCountIfNotScared() => MaxErrorCount += 1;

        protected Quote GetSuitableBestLimitQuote()
        {
            if (MarketDepth == null) return null;
            return QuotingSide == Sides.Buy ? MarketDepth.BestBid : MarketDepth.BestAsk;
        }

        protected Quote[] GetSuitableLimitQuotes()
        {
            if (MarketDepth == null) return null;
            return QuotingSide == Sides.Buy ? MarketDepth.Bids : MarketDepth.Asks;
        }

        protected Quote GetSuitableBestMarketQuote()
        {
            if (MarketDepth == null) return null;
            return QuotingSide == Sides.Buy ? MarketDepth.BestAsk : MarketDepth.BestBid;
        }

        protected Quote[] GetSuitableMarketQuotes()
        {
            if (MarketDepth == null) return null;
            return QuotingSide == Sides.Buy ? MarketDepth.Asks : MarketDepth.Bids;
        }

        protected bool IsPriceAcceptableForQuoting(decimal currentPrice, decimal worstPossibleQuotingPrice)
        {
            if (QuotingSide == Sides.Buy)
            {
                if (currentPrice <= worstPossibleQuotingPrice)
                    return true;
            }
            else
            {
                if (currentPrice >= worstPossibleQuotingPrice)
                    return true;
            }

            return false;
        }

        protected abstract void QuotingProcess();

    }
}
