using System;
using OptionsThugs.Model.Common;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model.Trading
{
    public abstract class QuoterStrategy : PrimaryStrategy
    {
        public Sides QuotingSide { get; }
        protected MarketDepth MarketDepth { get; private set; }
        protected OrderSynchronizer OrderSynchronizer { get; }
        protected PositionSynchronizer PositionSynchronizer { get; }

        protected QuoterStrategy(Sides quotingSide, decimal quotingVolume)
        {
            QuotingSide = quotingSide;
            Volume = quotingVolume;
            OrderSynchronizer = new OrderSynchronizer(this);
            PositionSynchronizer = new PositionSynchronizer();

            OrderSynchronizer.Timeout = Timeout;
            PositionSynchronizer.Timeout = Timeout;

            this.WhenPositionChanged()
                .Do(p => PositionSynchronizer.NewPositionChange(p))
                .Apply(this);

            this.WhenNewMyTrade()
                .Do(mt => PositionSynchronizer.NewTradeChange(mt.Trade.Volume))
                .Apply(this);

        }

        protected override void OnStarted()
        {
            CheckIfStrategyReadyToWork();

            if (Volume <= 0) throw new ArgumentException("Volume cannot be below zero: " + Volume);

            MarketDepth = GetMarketDepth(Security);

            Security.WhenMarketDepthChanged(Connector)
                .Do(QuotingProcess)
                .Apply(this);

            this.WhenPositionChanged()
                .Do(() =>
                {
                    if (Math.Abs(Position) >= Volume)
                    {
                        Stop();
                    }
                })
                .Apply(this);

            base.OnStarted();
        }

        protected void IncrMaxErrorCountIfNotScared() => MaxErrorCount += 1;

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
