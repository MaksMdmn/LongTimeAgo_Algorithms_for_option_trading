using System;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using Trading.Common;

namespace Trading.Strategies
{
    public class PositionCloserStrategy : PrimaryStrategy
    {
        private readonly decimal _priceToClose;
        private readonly PriceDirection _securityDesirableDirection;
        private readonly Sides _strategyOrderSide;

        public Security SecurityWithSignalToClose { get; set; }

        public PositionCloserStrategy(decimal priceToClose, PriceDirection securityDesirableDirection, decimal positionToClose)
        {
            _priceToClose = priceToClose;
            _securityDesirableDirection = securityDesirableDirection;
            _strategyOrderSide = positionToClose > 0 ? Sides.Sell : Sides.Buy;
            Volume = Math.Abs(positionToClose);
        }

        protected override void OnStarted()
        {
            if (SecurityWithSignalToClose == null)
                DoStrategyPreparation(new Security[] { }, new Security[] { Security }, new Portfolio[] { Portfolio });
            else
                DoStrategyPreparation(new Security[] { }, new Security[] { Security, SecurityWithSignalToClose }, new Portfolio[] { Portfolio });


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

            if (SecurityWithSignalToClose == null)
            {
                var md = GetMarketDepth(Security);

                Security.WhenMarketDepthChanged(Connector)
                    .Or(Connector.WhenIntervalElapsed(PrimaryStrategy.AutoUpdatePeriod))
                    .Do(() =>
                    {
                        //не проверяем время, т.к. правило выполняется Once()

                        var mqs = new MarketQuoterStrategy(_strategyOrderSide, Volume, _priceToClose);

                        mqs.WhenStopped()
                            .Do(this.Stop)
                            .Once()
                            .Apply(this);

                        MarkStrategyLikeChild(mqs);
                        ChildStrategies.Add(mqs);
                    })
                    .Once()
                    .Apply(this);
            }
            else
            {
                PrimaryStrategy mqs = null;

                var md = GetMarketDepth(SecurityWithSignalToClose);

                var mqsStartRule = SecurityWithSignalToClose
                    .WhenMarketDepthChanged(Connector)
                    .Or(Connector.WhenIntervalElapsed(PrimaryStrategy.AutoUpdatePeriod))
                    .Do(() =>
                    {
                        if (!IsTradingTime())
                            return;

                            //котировки специально развернуты неверно - как только была сделка на графике (ударили в аск или налили в бид) - закрываемся
                            if (_securityDesirableDirection == PriceDirection.Up && md.BestAsk.Price >= _priceToClose
                        || _securityDesirableDirection == PriceDirection.Down && md.BestBid.Price <= _priceToClose)
                        {
                                // пока делаем по любой цене, как только сработает условие
                                mqs = new MarketQuoterStrategy(_strategyOrderSide, Volume, Security.GetMarketPrice(_strategyOrderSide));

                            mqs.WhenStopped()
                                .Do(this.Stop)
                                .Once()
                                .Apply(this);

                            MarkStrategyLikeChild(mqs);
                            ChildStrategies.Add(mqs);
                        }
                    })
                    .Until(() => mqs != null)
                    .Apply(this);

                this.WhenStopping()
                    .Do(() => { /*NOP*/})
                    .Apply(this)
                    .Exclusive(mqsStartRule);
            }

            base.OnStarted();


        }

        public override string ToString()
        {
            return $"{nameof(_priceToClose)}: {_priceToClose}, " +
                   $"{nameof(_securityDesirableDirection)}: {_securityDesirableDirection}, " +
                   $"{nameof(_strategyOrderSide)}: {_strategyOrderSide}, " +
                   $"security with signal: {SecurityWithSignalToClose?.Code} "
                   + base.ToString();
        }
    }
}