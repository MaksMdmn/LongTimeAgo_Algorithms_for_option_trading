using System;
using System.Diagnostics;
using System.Globalization;
using OptionsThugs.Model.Common;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.Messages;

namespace OptionsThugs.Model.Trading
{
    public class SpreaderStrategy : PrimaryStrategy
    {
        private readonly object _syncRoot = new object();

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

        public decimal TenRepresentationOfDecimalNumbers { get; set; }

        public decimal CurrentPoisition
        {
            get
            {
                return _syncPosition;
            }
            private set
            {
                _syncPosition = Convert.ToInt32(value);
            }
        }

        public decimal CurrentPositionMoney
        {
            get
            {
                return _syncMoneyIntegerPart + _syncMoneyDecimalPart / TenRepresentationOfDecimalNumbers;
            }
            private set
            {
                var integerPart = decimal.Truncate(value);

                _syncMoneyIntegerPart = (int)integerPart;
                _syncMoneyDecimalPart = (int)((value - integerPart) * TenRepresentationOfDecimalNumbers);
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
            CurrentPositionMoney = currentPosition * currentPositionPrice * -1;

            TenRepresentationOfDecimalNumbers = 10000;

            switch (sideForEnterToPosition)
            {
                case DealDirection.Buy:
                    _isBuyerActivated = false;
                    _isSellerActivated = true; //never will be executed;
                    _isLeaverActivated = false;
                    break;
                case DealDirection.Sell:
                    _isBuyerActivated = true; //never will be executed;
                    _isSellerActivated = false;
                    _isLeaverActivated = false;
                    break;
                case DealDirection.Both:
                    _isBuyerActivated = false;
                    _isSellerActivated = false;
                    _isLeaverActivated = false;
                    break;
            }
        }


        protected override void OnStarted()
        {
            if (_spread <= 0) throw new ArgumentException("Spread cannot be below zero: " + _spread);
            if (_lot <= 0) throw new ArgumentException("Lot cannot be below zero: " + _lot);
            if (Security.PriceStep == null) throw new ArgumentException("Cannot read security price set, probably data still loading... :" + Security.PriceStep);

            if (_minFuturesPositionVal > 0) throw new ArgumentException("because of zero-based position idea, " +
                                                                        "min position should be below or equal to zero: " + _minFuturesPositionVal);
            if (_maxFuturesPositionVal < 0) throw new ArgumentException("because of zero-based position idea, " +
                                                                        "max position should be above or equal to zero: " + _maxFuturesPositionVal);

            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    if (!_isBuyerActivated && md.CheckIfSpreadExist())
                    {
                        _isBuyerActivated = true;

                        var size = CurrentPoisition == 0
                            ? _lot.ShrinkSizeToTrade(Sides.Buy, CurrentPoisition, _maxFuturesPositionVal)
                            : CalculateTradeSizeIfInPosition(Sides.Buy);

                        if (size <= 0)
                        {
                            _isBuyerActivated = false;
                        }
                        else
                        {
                            _buyerStrategy = new LimitQuoterStrategy(
                                Sides.Buy,
                                size,
                                md.BestPair.SpreadPrice.CheckIfValueNullThenZero() > _spread ? Security.PriceStep.CheckIfValueNullThenZero() : 0,
                                md.CalculateWorstLimitSpreadPrice(Sides.Buy, _spread));

                            _buyerStrategy.IsLimitOrdersAlwaysRepresent = true;

                            AssignEnterRulesAndStart(_buyerStrategy);
                        }
                    }

                    if (!_isSellerActivated && md.CheckIfSpreadExist())
                    {
                        _isSellerActivated = true;

                        var size = CurrentPoisition == 0
                            ? _lot.ShrinkSizeToTrade(Sides.Sell, CurrentPoisition, _minFuturesPositionVal)
                            : CalculateTradeSizeIfInPosition(Sides.Sell);

                        if (size <= 0)
                        {
                            _isSellerActivated = false;
                        }
                        else
                        {

                            _sellerStrategy = new LimitQuoterStrategy(
                            Sides.Sell,
                            size,
                            md.BestPair.SpreadPrice.CheckIfValueNullThenZero() > _spread ? Security.PriceStep.CheckIfValueNullThenZero() * -1 : 0,
                            md.CalculateWorstLimitSpreadPrice(Sides.Sell, _spread));

                            _sellerStrategy.IsLimitOrdersAlwaysRepresent = true;

                            AssignEnterRulesAndStart(_sellerStrategy);
                        }
                    }

                    if (!_isLeaverActivated && CurrentPoisition != 0)
                    {
                        _isLeaverActivated = true;

                        var side = CurrentPoisition > 0 ? Sides.Sell : Sides.Buy;
                        var sign = side == Sides.Buy ? 1 : -1;

                        _leaveStrategy = new LimitQuoterStrategy(
                            side,
                            CurrentPoisition.PrepareSizeToTrade(),
                            md.BestPair.SpreadPrice.CheckIfValueNullThenZero() > _spread ? Security.PriceStep.CheckIfValueNullThenZero() * sign : 0,
                            CalculateExitPositionPrice());

                        _leaveStrategy.IsLimitOrdersAlwaysRepresent = true;

                        AssignLeaveRulesAndStart();
                    }
                })
                .Until(() => ProcessState == ProcessStates.Stopping)
                .Apply(this);

            this.WhenStopping()
                .Do(() =>
                {
                    ChildStrategies.Clear();
                })
                .Once()
                .Apply(this);

            base.OnStarted();
        }

        private void AssignEnterRulesAndStart(LimitQuoterStrategy enterStrategy)
        {
            Debug.WriteLine("assign new strategy: {0}, spread is: {1}", enterStrategy.QuotingSide, Security.BestPair.SpreadPrice);

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
                    {
                        _lastBuyQuoterPrice = bestPair.Bid.Price;
                        _lastSellQuoterPrice = bestPair.Ask.Price;

                        enterStrategy.Stop();
                    }

                })
                .Until(() => enterStrategy.QuotingSide == Sides.Buy ? !_isBuyerActivated : !_isSellerActivated)
                .Apply(this);

            enterStrategy.WhenNewMyTrade()
                .Do(mt =>
                {
                    var sign = 1;

                    if (enterStrategy.QuotingSide == Sides.Sell)
                        sign = -1;

                    PositionAndMoneyAsyncIncrementation(mt.Trade.Volume * sign, mt.Trade.Volume * mt.Trade.Price * sign * -1);

                    _leaveStrategy?.Stop();
                })
                .Until(() => enterStrategy.QuotingSide == Sides.Buy ? !_isBuyerActivated : !_isSellerActivated)
                .Apply(this);

            enterStrategy.WhenStopping()
                .Do(() =>
                {
                    if (enterStrategy.QuotingSide == Sides.Buy)
                        _isBuyerActivated = false;
                    else
                        _isSellerActivated = false;

                    ChildStrategies.Remove(enterStrategy);
                })
                .Until(() => enterStrategy.QuotingSide == Sides.Buy ? !_isBuyerActivated : !_isSellerActivated)
                .Apply(this);


            MarkStrategyLikeChild(enterStrategy);
            ChildStrategies.Add(enterStrategy);
        }

        private void AssignLeaveRulesAndStart()
        {
            Debug.WriteLine("new leaver");
            if (_leaveStrategy == null)
                throw new NullReferenceException("leaveStrategy");

            _leaveStrategy.WhenNewMyTrade()
                .Do(mt =>
                {
                    var sign = 1;

                    if (_leaveStrategy.QuotingSide == Sides.Sell)
                        sign = -1;

                    PositionAndMoneyAsyncIncrementation(mt.Trade.Volume * sign, mt.Trade.Volume * mt.Trade.Price * sign * -1);
                })
                .Until(() => !_isLeaverActivated)
                .Apply(this);

            _leaveStrategy.WhenStopping()
                .Do(() =>
                {
                    _isLeaverActivated = false;

                    ChildStrategies.Remove(_leaveStrategy);
                })
                .Until(() => !_isLeaverActivated)
                .Apply(this);

            MarkStrategyLikeChild(_leaveStrategy);
            ChildStrategies.Add(_leaveStrategy);
        }

        private void PositionAndMoneyAsyncIncrementation(decimal addedPosValue, decimal addedMoneyValue)
        {
            lock (_syncRoot)
            {
                CurrentPoisition += addedPosValue;
                CurrentPositionMoney += addedMoneyValue;
                Debug.WriteLine("new pos: {0}", CurrentPoisition);
                Debug.WriteLine("new money: {0}", CurrentPositionMoney);

                if (CurrentPoisition == 0)
                {
                    Debug.WriteLine("money set to zero");
                    CurrentPositionMoney = 0;
                }
            }
        }

        private decimal CalculateExitPositionPrice()
        {
            Debug.Print("new exit-price: " + Security.ShrinkPrice(CurrentPoisition > 0
                                ? Math.Round(CurrentPositionMoney / CurrentPoisition, 4) * -1 + _spread
                                : Math.Round(CurrentPositionMoney / CurrentPoisition, 4) * -1 - _spread)
                            .ToString(CultureInfo.InvariantCulture));

            return Security.ShrinkPrice(CurrentPoisition > 0
                ? Math.Round(CurrentPositionMoney / CurrentPoisition, 4) * -1 + _spread
                : Math.Round(CurrentPositionMoney / CurrentPoisition, 4) * -1 - _spread);
        }

        private decimal CalculateTradeSizeIfInPosition(Sides dealSide)
        {
            if (CurrentPoisition == 0) throw new ArgumentException("Cannot use such a method, cause position is zero.");

            decimal result;

            if (CurrentPoisition > 0)
            {
                if (dealSide == Sides.Buy)
                {
                    result = _maxFuturesPositionVal - CurrentPoisition;
                }
                else
                    result = _minFuturesPositionVal + CurrentPoisition;
            }
            else
            {
                if (dealSide == Sides.Buy)
                    result = _maxFuturesPositionVal + CurrentPoisition;
                else
                    result = _minFuturesPositionVal - CurrentPoisition;
            }

            return result > 0 ? result : 0;
        }
    }
}