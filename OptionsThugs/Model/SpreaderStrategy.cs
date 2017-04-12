using System;
using System.Diagnostics;
using OptionsThugs.Model.Common;
using OptionsThugs.Model.Primary;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class SpreaderStrategy : PrimaryStrategy
    {
        private readonly decimal _spread;
        private readonly decimal _lot;
        private readonly decimal _minFuturesPositionVal;
        private readonly decimal _maxFuturesPositionVal;
        private readonly DealDirection _sideForEnterToPosition;

        private LimitQuoterStrategy _buyerStrategy;
        private LimitQuoterStrategy _sellerStrategy;
        private LimitQuoterStrategy _leaveStrategy;

        private decimal _lastBuyQuoterPrice;
        private decimal _lastSellQuoterPrice;

        private volatile bool _isBuyerActivated;
        private volatile bool _isSellerActivated;
        private volatile bool _isLeaverActivated;

        private volatile int _syncPosition;
        private volatile int _syncMoneyIntegerPart;
        private volatile int _syncMoneyDecimalPart;
        private volatile int _syncPriceIntegerPart;
        private volatile int _syncPriceDecimalPart;

        private decimal _syncMultipleTenValueOfDecimalNumbers = 100; //TODO in property?

        private decimal CurrentPoisition
        {
            get { return _syncPosition; }
            set { _syncPosition = Convert.ToInt32(value); }
        }

        private decimal CurrentPositionMoney
        {
            get { return _syncMoneyIntegerPart + _syncMoneyDecimalPart / _syncMultipleTenValueOfDecimalNumbers; }
            set
            {
                var integerPart = decimal.Truncate(value);

                _syncMoneyIntegerPart = (int)integerPart;
                _syncMoneyDecimalPart = (int)((value - integerPart) * _syncMultipleTenValueOfDecimalNumbers);
            }
        }

        private decimal CurrentPositionPrice
        {
            get { return _syncPriceIntegerPart + _syncPriceDecimalPart / _syncMultipleTenValueOfDecimalNumbers; }
            set
            {
                var integerPart = decimal.Truncate(value);

                _syncPriceIntegerPart = (int)integerPart;
                _syncPriceDecimalPart = (int)((value - integerPart) * _syncMultipleTenValueOfDecimalNumbers);
            }
        }
        public SpreaderStrategy(decimal currentPosition, decimal currentPositionPrice, decimal spread, decimal lot, DealDirection sideForEnterToPosition)
            : this(currentPosition, currentPositionPrice, spread, lot, sideForEnterToPosition, decimal.MinValue, decimal.MaxValue) { }


        public SpreaderStrategy(decimal currentPosition, decimal currentPositionPrice, decimal spread, decimal lot,
            DealDirection sideForEnterToPosition, decimal minFuturesPositionVal, decimal maxFuturesPositionVal)
        {
            _spread = spread;
            _lot = lot;
            _minFuturesPositionVal = minFuturesPositionVal;
            _maxFuturesPositionVal = maxFuturesPositionVal;
            _sideForEnterToPosition = sideForEnterToPosition;
            CurrentPoisition = currentPosition;
            CurrentPositionPrice = currentPositionPrice;
            CurrentPositionMoney = currentPosition * currentPositionPrice * -1;
        }


        protected override void OnStarted()
        {
            //TODO DO NOT FORGET reg security and MD!!!!!

            if (_spread <= 0) throw new ArgumentException("Spread cannot be below zero: " + _spread);
            if (_lot <= 0) throw new ArgumentException("Lot cannot be below zero: " + _lot);
            if (Security.PriceStep == null) throw new ArgumentException("Cannot read security price set, probably data still loading... :" + Security.PriceStep);

            switch (_sideForEnterToPosition)
            {
                case DealDirection.Buy:
                    _buyerStrategy = new LimitQuoterStrategy();
                    _isBuyerActivated = true;
                    break;
                case DealDirection.Sell:
                    _sellerStrategy = new LimitQuoterStrategy();
                    _isSellerActivated = true;
                    break;
                case DealDirection.Both:
                    _buyerStrategy = new LimitQuoterStrategy();
                    _sellerStrategy = new LimitQuoterStrategy();
                    _isBuyerActivated = true;
                    _isSellerActivated = true;
                    break;
            }

            if (_position != 0)
            {
                _leaveStrategy = CreateQuoter(_position > 0 ? Sides.Sell : Sides.Buy,
                    _position,
                    _positionPrice,
                    Security.PriceStep.CheckIfValueNullThenZero());
                _isLeaverActivated = true;
            }

            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    if (!_isBuyerActivated)
                    {
                    }

                    if (!_isSellerActivated)
                    {
                    }

                    if (!_isLeaverActivated)
                    {
                    }
                })
                .Until(() => ProcessState == ProcessStates.Stopping)
                .Apply(this);

            this.WhenStopping()
                .Do(() =>
                {
                    Debug.WriteLine("SS stoping, try to clean all childs");
                    ChildStrategies.Clear();
                    Debug.WriteLine("SS stopped");
                })
                .Once()
                .Apply(this);

            base.OnStarted();
        }

        private LimitQuoterStrategy CreateQuoter(Sides quoteSide, decimal quoteSize, decimal worstPrice, decimal quoteStep)
        {
            //TODO calculate price step depend on spread and set it here;

            var strategy = new LimitQuoterStrategy(quoteSide, quoteSize, quoteStep, worstPrice)
            {
                IsLimitOrdersAlwaysRepresent = true
            };



            return strategy;
        }

        private void AssignEnterPosRules(LimitQuoterStrategy enterStrategy)
        {
            if (enterStrategy == null)
                throw new NullReferenceException("strategy, when was trying to assign enter rules");

            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    var bestPair = md.BestPair;

                    if (bestPair.Bid == null || bestPair.Ask == null)
                        return;

                    var currentSpread = bestPair.Ask.Price - bestPair.Bid.Price;

                    if (currentSpread <= 0)
                        return;

                    if (_lastBuyQuoterPrice != bestPair.Bid.Price
                    || _lastSellQuoterPrice != bestPair.Ask.Price)
                        enterStrategy.Stop();

                })
                .Until(() => enterStrategy.ProcessState == ProcessStates.Stopping)
                .Apply(this);

            enterStrategy.WhenNewMyTrade()
                .Do(mt =>
                {
                    var sign = 1;

                    if (mt.Trade.OrderDirection == Sides.Sell)
                        sign = -1;

                    _syncPosition += Convert.ToInt32(mt.Trade.Volume);

                    CurrentPoisition += mt.Trade.Volume * sign;
                    CurrentPositionMoney += mt.Trade.Volume * mt.Trade.Price * sign * -1;
                    CurrentPositionPrice = Math.Round(CurrentPositionMoney / CurrentPoisition) * -1;
                })
                .Until(() => enterStrategy.ProcessState == ProcessStates.Stopping)
                .Apply(this);

            enterStrategy.WhenStopping()
                .Do(() =>
                {
                    //TODO last check pos?
                    if (enterStrategy.QuotingSide == Sides.Buy)
                        _isBuyerActivated = false;
                    else
                        _isSellerActivated = false;

                    ChildStrategies.Remove(enterStrategy);
                })
                .Until(() => enterStrategy.ProcessState == ProcessStates.Stopping)
                .Apply(this);


            MarkStrategyLikeChild(enterStrategy);
            ChildStrategies.Add(enterStrategy);
        }

        private void AssignLeaveRules()
        {
            if (_leaveStrategy == null)
                throw new NullReferenceException("leaveStrategy");

            //TODO When cur volatile pos changed...  _leaverStrategy.Stop();

            _leaveStrategy.WhenPositionChanged()
                .Do(() =>
                {
                    //TODO
                })
                .Until(() => _leaveStrategy.ProcessState == ProcessStates.Stopping)
                .Apply(this);

            _leaveStrategy.WhenStopping()
                .Do(() =>
                {
                    //TODO last check pos?
                    _isLeaverActivated = false;

                    ChildStrategies.Remove(_leaveStrategy);
                })
                .Until(() => _leaveStrategy.ProcessState == ProcessStates.Stopping)
                .Apply(this);


            MarkStrategyLikeChild(_leaveStrategy);
            ChildStrategies.Add(_leaveStrategy);
        }

        //private void DoEnterPart(MarketDepthPair bestPair)
        //{
        //    if (bestPair.Bid == null || bestPair.Ask == null) return;

        //    var currentSpread = bestPair.Ask.Price - bestPair.Bid.Price;

        //    if (currentSpread <= 0) return;

        //    if (_minFuturesPositionVal >= _currentPosition || _currentPosition >= _maxFuturesPositionVal) return;

        //    if (currentSpread > _spread)
        //        PickUpQuoters(bestPair, Security.PriceStep.CheckIfValueNullThenZero(), false);

        //    if (currentSpread < _spread)
        //        PickUpQuoters(bestPair, Security.PriceStep.CheckIfValueNullThenZero(), true);

        //    if (currentSpread == _spread)
        //        PickUpQuoters(bestPair, 0, false);
        //}

        //private void DoExitPart(MarketDepthPair bestPair)
        //{
        //    //TODO pos must be volatile or smth
        //    //TODO calc pos price 100% (desirable do it in some event or smth)
        //    //TODO it's must be fast exit - recreate strategy if MD changed and always represent limit order 
        //    //TODO                 (or mb just create my limit order?, but this is a lot of shit)

        //    if (!_isTryingToExit
        //        && CheckIfExitPriceFine(bestPair)
        //        && _currentPosition != 0)
        //    {
        //        RunNewQuoter(_currentPosition > 0 ? Sides.Sell : Sides.Buy,
        //            bestPair, Security.PriceStep.CheckIfValueNullThenZero(), true, true);
        //    }
        //}

        //private void PickUpQuoters(MarketDepthPair bestPair, decimal absQuotingStepValue, bool limitOrdersAlwaysPlaced)
        //{
        //    switch (_sideForEnterToPosition)
        //    {
        //        case DealDirection.Buy:
        //            if (!_isTryingToBuy)
        //                RunNewQuoter(Sides.Buy, bestPair, absQuotingStepValue, limitOrdersAlwaysPlaced);
        //            break;
        //        case DealDirection.Sell:
        //            if (!_isTryingToSell)
        //                RunNewQuoter(Sides.Sell, bestPair, absQuotingStepValue, limitOrdersAlwaysPlaced);
        //            break;
        //        case DealDirection.Both:
        //            if (!_isTryingToBuy)
        //                RunNewQuoter(Sides.Buy, bestPair, absQuotingStepValue, limitOrdersAlwaysPlaced);
        //            if (!_isTryingToSell)
        //                RunNewQuoter(Sides.Sell, bestPair, absQuotingStepValue, limitOrdersAlwaysPlaced);
        //            break;
        //    }
        //}

        //private void RunNewQuoter(Sides side, MarketDepthPair bestPair, decimal quotingStep, bool limitOrdersAlwaysPlaced, bool positionIsTradeSize = false)
        //{
        //    var sign = side == Sides.Buy ? 1 : -1;

        //    LimitQuoterStrategy tempQuoter;
        //    decimal quotingVolume;
        //    decimal worstQuotingPrice;
        //    decimal orientiedSize = positionIsTradeSize ? Math.Abs(_currentPosition) : _lot;


        //    if (side == Sides.Buy)
        //    {
        //        quotingVolume = orientiedSize.ShrinkSizeToTrade(side, _currentPosition, _maxFuturesPositionVal);
        //        worstQuotingPrice = bestPair.Ask.Price - _spread;
        //    }
        //    else
        //    {
        //        quotingVolume = orientiedSize.ShrinkSizeToTrade(side, _currentPosition, _minFuturesPositionVal);
        //        worstQuotingPrice = bestPair.Bid.Price + _spread;
        //    }

        //    tempQuoter = new LimitQuoterStrategy(side, quotingVolume, quotingStep * sign, worstQuotingPrice)
        //    {
        //        IsLimitOrdersAlwaysRepresent = limitOrdersAlwaysPlaced
        //    };

        //    Debug.WriteLine("Creating new LQS: vol: {0}, worstPrice: {1}, side: {2}, step: {3}, isAlwaysPlaced: {4}, IsPos=Trade: {5}",
        //        quotingVolume, worstQuotingPrice, side, quotingStep,
        //        limitOrdersAlwaysPlaced, positionIsTradeSize);

        //    tempQuoter.WhenNewMyTrade()
        //        .Do(mt =>
        //        {
        //            _currentPosition += mt.Trade.Volume * sign;
        //            _currentMoney += mt.Trade.Volume * mt.Trade.Price * sign * -1;
        //            _enteredPrice = Math.Round(_currentMoney / _currentPosition, 4) * -1;

        //            Debug.Print("entered price: " + _enteredPrice);
        //        })
        //        .Apply(this);

        //    tempQuoter.WhenStopped()
        //        .Do(() =>
        //        {
        //            Debug.WriteLine("tq removing");
        //            if (tempQuoter.QuotingSide == Sides.Buy)
        //                _isTryingToBuy = false;
        //            else
        //                _isTryingToSell = false;

        //            ChildStrategies.Remove(tempQuoter);

        //            tempQuoter = null;

        //            Debug.WriteLine("tq removed");
        //        })
        //        .Once()
        //        .Apply(this);

        //    Security.WhenMarketDepthChanged(Connector)
        //        .Do(md =>
        //        {
        //            if (md.BestBid == null || md.BestAsk == null || md.BestPair.SpreadPrice < _spread)
        //            {
        //                Debug.WriteLine("tq stopping");
        //                tempQuoter.Stop();
        //                Debug.WriteLine("tq stopped");
        //            }
        //        })
        //        .Until(() => tempQuoter == null)
        //        .Apply(this);

        //    if (side == Sides.Buy)
        //    {
        //        _buyer = tempQuoter;
        //        _isTryingToBuy = true;
        //    }
        //    else
        //    {
        //        _seller = tempQuoter;
        //        _isTryingToSell = true;
        //    }

        //    MarkStrategyLikeChild(tempQuoter);
        //    ChildStrategies.Add(tempQuoter);

        //    Debug.WriteLine("LQS created like above");
        //}


        //private bool CheckIfExitPriceFine(MarketDepthPair bestPair)
        //{
        //    if (_enteredPrice == 0 || _currentPosition == 0)
        //        return false;

        //    if (_currentPosition > 0)
        //        return _enteredPrice + _spread <= bestPair.Bid.Price;

        //    if (_currentPosition < 0)
        //        return _enteredPrice - _spread >= bestPair.Ask.Price;

        //    return false;
        //}
    }
}