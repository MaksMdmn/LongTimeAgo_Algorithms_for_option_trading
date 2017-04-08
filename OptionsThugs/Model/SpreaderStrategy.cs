using System;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugs.Model.Common;
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
        private readonly decimal _spread;
        private readonly decimal _lot;
        private readonly decimal _minFuturesPositionVal;
        private readonly decimal _maxFuturesPositionVal;
        private readonly DealDirection _enterPositionSide;

        private MarketDepth _marketDepth;
        private decimal _currentPosition;
        private decimal _enteredPrice;
        private LimitQuoterStrategy _buyer;
        private LimitQuoterStrategy _seller;

        private volatile bool _isTryingToBuy;
        private volatile bool _isTryingToSell;
        private volatile bool _isTryingToExit;

        public SpreaderStrategy(decimal currentPosition, decimal spread, decimal lot, DealDirection enterPositionSide)
            : this(currentPosition, spread, lot, enterPositionSide, decimal.MinValue, decimal.MaxValue) { }


        public SpreaderStrategy(decimal currentPosition, decimal spread, decimal lot,
            DealDirection enterPositionSide, decimal minFuturesPositionVal, decimal maxFuturesPositionVal)
        {
            _spread = spread;
            _lot = lot;
            _minFuturesPositionVal = minFuturesPositionVal;
            _maxFuturesPositionVal = maxFuturesPositionVal;
            _enterPositionSide = enterPositionSide;
            _currentPosition = currentPosition;
        }


        protected override void OnStarted()
        {
            //TODO DO NOT FORGET reg security and MD!!!!!

            if (_spread <= 0) throw new ArgumentException("Spread cannot be below zero: " + _spread);
            if (_lot <= 0) throw new ArgumentException("Lot cannot be below zero: " + _lot);
            if (Security.PriceStep == null) throw new ArgumentException("Cannot read security price set, probably data still loading... :" + Security.PriceStep.Value);

            _marketDepth = Connector.GetMarketDepth(Security);
            var minSpreadChange = Security.PriceStep.Value;

            _marketDepth.WhenSpreadMore(minSpreadChange)
                .Or(_marketDepth.WhenSpreadLess(minSpreadChange))
                .Do(() =>
                {
                    if (_currentPosition == 0)
                    {
                        DoEnterPart(_marketDepth.BestPair);
                    }
                    else
                    {
                        DoExitPart(_marketDepth.BestPair);
                        DoEnterPart(_marketDepth.BestPair);
                    }
                })
                .Until(() => ProcessState == ProcessStates.Stopping)
                .Apply(this);

            base.OnStarted();
        }

        private void DoEnterPart(MarketDepthPair bestPair)
        {
            if (bestPair.Bid == null || bestPair.Ask == null) return;

            var currentSpread = bestPair.Ask.Price - bestPair.Bid.Price;

            if (currentSpread <= 0) return;

            if (_minFuturesPositionVal >= _currentPosition || _currentPosition >= _maxFuturesPositionVal) return;

            if (currentSpread > _spread)
                PickUpQuoters(bestPair, Security.PriceStep.CheckIfValueNullThenZero(), false);

            if (currentSpread < _spread)
                PickUpQuoters(bestPair, Security.PriceStep.CheckIfValueNullThenZero(), true);

            if (currentSpread == _spread)
                PickUpQuoters(bestPair, 0, false);
        }

        private void DoExitPart(MarketDepthPair bestPair)
        {
            //TODO pos must be volatile or smth
            //TODO calc pos price 100% (desirable do it in some event or smth)
            //TODO it's must be fast exit - recreate strategy if MD changed and always represent limit order 
            //TODO                 (or mb just create my limit order?, but this is a lot of shit)

            if (!_isTryingToExit)
            {

            }
        }

        private void PickUpQuoters(MarketDepthPair bestPair, decimal absQuotingStepValue, bool limitOrdersAlwaysPlaced)
        {
            switch (_enterPositionSide)
            {
                case DealDirection.Buy:
                    if (!_isTryingToBuy)
                        RunNewQuoter(Sides.Buy, bestPair, absQuotingStepValue, limitOrdersAlwaysPlaced);
                    break;
                case DealDirection.Sell:
                    if (!_isTryingToSell)
                        RunNewQuoter(Sides.Sell, bestPair, absQuotingStepValue, limitOrdersAlwaysPlaced);
                    break;
                case DealDirection.Both:
                    if (!_isTryingToBuy)
                        RunNewQuoter(Sides.Buy, bestPair, absQuotingStepValue, limitOrdersAlwaysPlaced);
                    if (!_isTryingToSell)
                        RunNewQuoter(Sides.Sell, bestPair, absQuotingStepValue, limitOrdersAlwaysPlaced);
                    break;
            }
        }

        private void RunNewQuoter(Sides side, MarketDepthPair bestPair, decimal quotingStep, bool limitOrdersAlwaysPlaced)
        {
            var sign = side == Sides.Buy ? 1 : -1;

            LimitQuoterStrategy tempQuoter;
            decimal quotingVolume;
            decimal worstQuotingPrice;

            if (side == Sides.Buy)
            {
                quotingVolume = _lot.ShrinkSizeToTrade(side, _currentPosition, _maxFuturesPositionVal);
                worstQuotingPrice = bestPair.Ask.Price - _spread;
            }
            else
            {
                quotingVolume = _lot.ShrinkSizeToTrade(side, _currentPosition, _minFuturesPositionVal);
                worstQuotingPrice = bestPair.Bid.Price + _spread;
            }

            tempQuoter = new LimitQuoterStrategy(side, quotingVolume, quotingStep * sign, worstQuotingPrice)
            {
                IsLimitOrdersAlwaysRepresent = limitOrdersAlwaysPlaced
            };

            tempQuoter.WhenNewMyTrade()
                .Do(mt =>
                {
                    _currentPosition += mt.Trade.Volume * sign;
                })
                .Apply(this);

            tempQuoter.WhenStopped()
                .Do(() =>
                {
                    if (tempQuoter.QuotingSide == Sides.Buy)
                        _isTryingToBuy = false;
                    else
                        _isTryingToSell = false;

                    ChildStrategies.Remove(tempQuoter);

                    tempQuoter = null;
                });

            _marketDepth.WhenChanged()
                .Do(() =>
                {
                    if (_marketDepth.BestBid == null || _marketDepth.BestAsk == null || _marketDepth.BestPair.SpreadPrice < _spread)
                        tempQuoter.Stop();
                })
                .Until(() => tempQuoter == null)
                .Apply(this);

            if (side == Sides.Buy)
            {
                _buyer = tempQuoter;
                _isTryingToBuy = true;
            }
            else
            {
                _seller = tempQuoter;
                _isTryingToSell = true;
            }

            MarkStrategyLikeChild(tempQuoter);
            ChildStrategies.Add(tempQuoter);
        }


        private bool CheckIfExitPriceFine(MarketDepthPair bestPair)
        {
            if (_enteredPrice == 0 || _currentPosition == 0)
                return false;

            if (_currentPosition > 0)
                return _enteredPrice + _spread <= bestPair.Bid.Price;

            if (_currentPosition < 0)
                return _enteredPrice - _spread >= bestPair.Ask.Price;

            return false;
        }
    }
}