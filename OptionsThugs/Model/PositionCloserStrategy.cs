using System;
using OptionsThugs.Model.Common;
using OptionsThugs.Model.Primary;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class PositionCloserStrategy : Strategy
    {
        private readonly decimal _priceToClose;
        private readonly Security _securityToClose;
        private readonly PriceDirection _securityDesirableDirection;
        private readonly Sides _strategyOrderSide;

        public PositionCloserStrategy(decimal priceToClose, Security securityToClose, PriceDirection securityDesirableDirection, decimal posSizeToClose)
        {
            _priceToClose = priceToClose;
            _securityToClose = securityToClose;
            _securityDesirableDirection = securityDesirableDirection;
            _strategyOrderSide = posSizeToClose > 0 ? Sides.Sell : Sides.Buy;
            Volume = Math.Abs(posSizeToClose);
        }

        public PositionCloserStrategy(decimal priceToClose, decimal positionToClose) : this(priceToClose, null, PriceDirection.None, positionToClose) { }

        protected override void OnStarted()
        {
            if (Connector == null || Security == null || Portfolio == null) return;
            if (Volume <= 0 || _priceToClose <= 0) return;

            Connector.RegisterMarketDepth(Security);

            this.WhenPositionChanged()
                .Do(() =>
                {
                    if (Math.Abs(Position) >= Volume)
                    {
                        Stop();
                    }
                })
                .Apply(this);

            if (_securityToClose == null)
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
                Strategy mqsChild = null;

                Connector.RegisterMarketDepth(_securityToClose);

                var mqsStartRule = _securityToClose.WhenMarketDepthChanged(Connector)
                    .Do(md =>
                    {
                        if (_securityDesirableDirection == PriceDirection.Up && md.BestBid.Price >= _priceToClose
                        || _securityDesirableDirection == PriceDirection.Down && md.BestAsk.Price <= _priceToClose)
                        {
                            mqsChild = new MarketQuoterStrategy(_strategyOrderSide, Volume, Security.GetMarketPrice(_strategyOrderSide, Connector));
                            //TODO пока делаем по любой цене, как только сработает условие

                            mqsChild.WhenStopped()
                                .Do(this.Stop)
                                .Once()
                                .Apply(this);

                            ChildStrategies.Add(mqsChild);
                        }
                    })
                    .Until(() => mqsChild != null)
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