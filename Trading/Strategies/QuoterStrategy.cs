using System;
using System.Diagnostics;
using System.Linq;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using Trading.Common;

namespace Trading.Strategies
{
    public abstract class QuoterStrategy : PrimaryStrategy
    {
        public Sides QuotingSide { get; }
        protected MarketDepth MarketDepth { get; private set; }
        protected OrderSynchronizer OrderSynchronizer { get; }
        protected PositionSynchronizer PositionSynchronizer { get; }
        protected TimingController TimingController { get; private set; }

        protected QuoterStrategy(Sides quotingSide, decimal quotingVolume)
        {
            QuotingSide = quotingSide;
            Volume = quotingVolume;
            OrderSynchronizer = new OrderSynchronizer(this);
            PositionSynchronizer = new PositionSynchronizer();

            OrderSynchronizer.Timeout = Timeout;
            PositionSynchronizer.Timeout = Timeout;

            WaitAllTrades = true;

            this.WhenPositionChanged()
                .Do(p => PositionSynchronizer.NewPositionChange(p))
                .Apply(this);

            this.WhenNewMyTrade()
                .Do(mt =>
                {
                    PositionSynchronizer.NewTradeChange(mt.Trade.Volume);
                })
                .Apply(this);

        }

        protected override void OnStarted()
        {
            DoStrategyPreparation(new Security[] { }, new Security[] { Security }, new Portfolio[] { Portfolio });

            if (Volume <= 0) throw new ArgumentException("Volume cannot be below zero: " + Volume);

            MarketDepth = GetMarketDepth(Security);

            TimingController = new TimingController(QuotingProcess, 700, 1000);

            QuotingProcess();

            Security.WhenMarketDepthChanged(Connector)
                .Do(() =>
                {
                    QuotingProcess();
                    TimingController?.TimingMethodHappened();
                })
                .Apply(this);

            this.WhenPositionChanged()
                .Do(() =>
                {
                    if (Math.Abs(Position) >= Volume)
                    {
                        TimingController?.EndTimingControl();
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

        public override string ToString()
        {
            return $"{nameof(QuotingSide)}: {QuotingSide}";
        }
    }
}
