using System;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugs.Model.Primary;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Strategies.Quoting;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class SpreaderStrategy : PrimaryStrategy
    {
        private readonly decimal _minSpread;
        private readonly decimal _lot;
        private readonly decimal _longPosSize;
        private readonly decimal _shortPosSize;
        private readonly bool _isLimitOrdersAlwaysRepresent;

        private volatile bool _isBuyPartActive;
        private volatile bool _isSellPartActive;

        public SpreaderStrategy(decimal minSpread, decimal minPos, decimal maxPos)
            : this(minSpread, minPos, maxPos, 1, true) { }

        public SpreaderStrategy(decimal minSpread, decimal shortPosSize, decimal longPosSize, decimal lot,
            bool isLimitOrdersAlwaysRepresent)
        {
            _minSpread = minSpread;
            _longPosSize = shortPosSize;
            _shortPosSize = longPosSize;
            _lot = lot;
            _isLimitOrdersAlwaysRepresent = isLimitOrdersAlwaysRepresent;

            _isBuyPartActive = false;
            _isSellPartActive = false;

            CancelOrdersWhenStopping = true;
            CommentOrders = true;
            DisposeOnStop = false;
            MaxErrorCount = 10;
            OrdersKeepTime = TimeSpan.Zero;
        }

        protected override void OnStarted()
        {
            if (Connector == null || Security == null || Portfolio == null) return;

            Connector.RegisterMarketDepth(Security);

            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    var currentSpread = md.BestAsk.Price - md.BestBid.Price;

                    if (currentSpread >= _minSpread)
                    {
                        if (_isLimitOrdersAlwaysRepresent)
                        {
                            //Cancel Active Orders THIS strategy (parent)
                        }

                        //TODO: notify about pos change at someone child strategy and calc it here. 
                        //TODO: after that remove both - recalc max short long positions and create new strategies.
                        //TODO: before stopping/removing child strategies we must be sure that we get actual pos value!!! (override onstopping/onstopped may be)
                        ProcessBuyPart();
                        ProcessSellPart();
                    }
                    else
                    {
                        //TODO check if strategies'll stopped through .Clear()
                        if (_isLimitOrdersAlwaysRepresent)
                        {
                            //Place two orders on buy sell and calc price for them
                        }
                        else
                        {
                            ChildStrategies.Clear();
                            _isBuyPartActive = false;
                            _isSellPartActive = false;
                        }
                    }



                })
                .Apply(this);


            base.OnStarted();
        }

        private void ProcessSellPart()
        {
            if (_isSellPartActive)
            {
                var sellPartStrategy = new LimitQuoterStrategy(Sides.Sell, CalculateSuitableAbsLot(Sides.Sell), -Security.PriceStep.Value);
                ChildStrategies.Add(sellPartStrategy);

                _isSellPartActive = true;
            }

        }

        private void ProcessBuyPart()
        {
            if (!_isBuyPartActive)
            {
                var buyPartStrategy = new LimitQuoterStrategy(Sides.Buy, CalculateSuitableAbsLot(Sides.Buy), Security.PriceStep.Value);
                ChildStrategies.Add(buyPartStrategy);

                _isBuyPartActive = true;
            }
        }

        private decimal CalculateSuitableAbsLot(Sides side)
        {
            decimal diff;
            if (side == Sides.Buy)
            {
                diff = Math.Abs(_longPosSize - Position);
            }
            else
            {
                diff = Math.Abs(_shortPosSize - Position);
            }

            return diff >= _lot ? _lot : diff;
        }
    }
}