using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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

        private bool _isQuoting;

        protected QuoterStrategy(Sides quotingSide, decimal quotingVolume)
        {
            QuotingSide = quotingSide;
            Volume = quotingVolume;
            OrderSynchronizer = new OrderSynchronizer(this);
            PositionSynchronizer = new PositionSynchronizer();

            OrderSynchronizer.Timeout = Timeout;
            PositionSynchronizer.Timeout = Timeout;

            _isQuoting = false;

            WaitAllTrades = true;

            this.WhenPositionChanged()
                .Do(p => PositionSynchronizer.NewPositionChange(p))
                .Until(IsStrategyStopping)
                .Apply(this);

            this.WhenNewMyTrade()
                .Do(mt =>
                {
                    PositionSynchronizer.NewTradeChange(mt.Trade.Volume);
                })
                .Until(IsStrategyStopping)
                .Apply(this);

        }

        protected override void OnStarted()
        {
            DoStrategyPreparation(new Security[] { }, new Security[] { Security }, new Portfolio[] { Portfolio });

            if (Volume <= 0) throw new ArgumentException("Volume cannot be below zero: " + Volume);

            MarketDepth = GetMarketDepth(Security);

            if (IsTradingTime())
                QuotingProcess();
            else
                OrderSynchronizer.CancelCurrentOrder();

            Security.WhenMarketDepthChanged(Connector)
                .Do(() =>
                {
                    if (IsPrimaryStoppingStarted())
                    {
                        OrderSynchronizer.CancelCurrentOrder();
                        return;
                    }

                    if (!_isQuoting)
                    {
                        _isQuoting = true;

                        if (IsTradingTime())
                            QuotingProcess();
                        else
                            OrderSynchronizer.CancelCurrentOrder();

                        _isQuoting = false;
                    }
                })
                .Until(IsStrategyStopping)
                .Apply(this);

            this.WhenPositionChanged()
                .Do(() =>
                {
                    if (Math.Abs(Position) >= Volume)
                        PrimaryStopping();
                })
                .Until(IsStrategyStopping)
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

        public override void PrimaryStopping()
        {
            FromHerePrimaryStoppingStarted();

            Task.Run(() =>
            {
                if (OrderSynchronizer.IsOrderRegistering) { }

                while (OrderSynchronizer.IsOrderRegistering)
                {
                    /*NOP*/
                }

                OrderSynchronizer.CancelCurrentOrder();

                while (OrderSynchronizer.IsAnyOrdersInWork)
                {
                    /*NOP*/
                }

                base.PrimaryStopping();
            });
        }


        protected abstract void QuotingProcess();

        public override string ToString()
        {
            return $"{nameof(QuotingSide)}: {QuotingSide} " + base.ToString();
        }
    }
}
