using System;
using OptionsThugs.Model.Trading.Common;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model.Trading
{
    public class PositionCloserStrategy : PrimaryStrategy
    {
        private readonly decimal _priceToClose;
        private readonly Security _securityWithSignalToClose;
        private readonly PriceDirection _securityDesirableDirection;
        private readonly Sides _strategyOrderSide;

        public PositionCloserStrategy(decimal priceToClose, decimal positionToClose)
            : this(priceToClose, null, PriceDirection.None, positionToClose) { }

        public PositionCloserStrategy(decimal priceToClose,
            Security securityWithSignalToClose, PriceDirection securityDesirableDirection, decimal posSizeToClose)
        {
            _priceToClose = priceToClose;
            _securityWithSignalToClose = securityWithSignalToClose;
            _securityDesirableDirection = securityDesirableDirection;
            _strategyOrderSide = posSizeToClose > 0 ? Sides.Sell : Sides.Buy;
            Volume = Math.Abs(posSizeToClose);
        }

        protected override void OnStarted()
        {
            if (Volume <= 0 || _priceToClose <= 0) throw new ArgumentException(
                $"Volume: {Volume} or price to close: {_priceToClose} cannot be below zero"); ;

            this.WhenPositionChanged()
                .Do(() =>
                {
                    if (Math.Abs(Position) >= Volume)
                    {
                        Stop();
                    }
                })
                .Apply(this);

            if (_securityWithSignalToClose == null)
            {
                Security.WhenMarketDepthChanged(Connector)
                    .Do(md =>
                    {
                        var mqs = new MarketQuoterStrategy(_strategyOrderSide, Volume, _priceToClose);

                        mqs.WhenStopped()
                            .Do(this.Stop)
                            .Once()
                            .Apply(this);

                        ChildStrategies.Add(mqs);
                    })
                    .Once()
                    .Apply(this);
            }
            else
            {
                Strategy mqs = null;

                var mqsStartRule = _securityWithSignalToClose.WhenMarketDepthChanged(Connector)
                    .Do(md =>
                    {
                        if (_securityDesirableDirection == PriceDirection.Up && md.BestBid.Price >= _priceToClose
                        || _securityDesirableDirection == PriceDirection.Down && md.BestAsk.Price <= _priceToClose)
                        {
                            // пока делаем по любой цене, как только сработает условие
                            mqs = new MarketQuoterStrategy(_strategyOrderSide, Volume, Security.GetMarketPrice(_strategyOrderSide));

                            mqs.WhenStopped()
                                .Do(this.Stop)
                                .Once()
                                .Apply(this);

                            ChildStrategies.Add(mqs);
                        }
                    })
                    .Until(() => mqs != null)
                    .Apply(this);

                this.WhenStopping()
                    .Do(() => { })
                    .Apply(this)
                    .Exclusive(mqsStartRule);
            }

            base.OnStarted();
        }
    }
}