using System;
using System.Diagnostics;
using OptionsThugs.Model.Common;
using OptionsThugs.Model.Primary;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class SpreaderStrategy : PrimaryStrategy
    {
        private readonly object _locker = new object();

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

        private decimal _syncMultipleTenValueOfDecimalNumbers = 100; //TODO in property?

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
                return _syncMoneyIntegerPart + _syncMoneyDecimalPart / _syncMultipleTenValueOfDecimalNumbers; ;
            }
            private set
            {
                var integerPart = decimal.Truncate(value);

                _syncMoneyIntegerPart = (int)integerPart;
                _syncMoneyDecimalPart = (int)((value - integerPart) * _syncMultipleTenValueOfDecimalNumbers);
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
        }


        protected override void OnStarted()
        {
            //TODO DO NOT FORGET reg security and MD!!!!!

            if (_spread <= 0) throw new ArgumentException("Spread cannot be below zero: " + _spread);
            if (_lot <= 0) throw new ArgumentException("Lot cannot be below zero: " + _lot);
            if (Security.PriceStep == null) throw new ArgumentException("Cannot read security price set, probably data still loading... :" + Security.PriceStep);

            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    if (!_isBuyerActivated && md.CheckIfSpreadExist())
                    {
                        _buyerStrategy = new LimitQuoterStrategy(
                            Sides.Buy,
                            _lot.ShrinkSizeToTrade(Sides.Buy, CurrentPoisition, _maxFuturesPositionVal),
                            md.BestPair.SpreadPrice.CheckIfValueNullThenZero() > _spread ? Security.PriceStep.CheckIfValueNullThenZero() : 0,
                            md.CalculateWorstLimitSpreadPrice(Sides.Buy, _spread));

                        _buyerStrategy.IsLimitOrdersAlwaysRepresent = true;

                        AssignEnterRulesAndStart(_buyerStrategy);
                    }

                    if (!_isSellerActivated && md.CheckIfSpreadExist())
                    {
                        _sellerStrategy = new LimitQuoterStrategy(
                            Sides.Sell,
                            _lot.ShrinkSizeToTrade(Sides.Sell, CurrentPoisition, _minFuturesPositionVal),
                            md.BestPair.SpreadPrice.CheckIfValueNullThenZero() > _spread ? Security.PriceStep.CheckIfValueNullThenZero() : 0,
                            md.CalculateWorstLimitSpreadPrice(Sides.Sell, _spread));

                        _sellerStrategy.IsLimitOrdersAlwaysRepresent = true;

                        AssignEnterRulesAndStart(_sellerStrategy);
                    }

                    if (!_isLeaverActivated && CurrentPoisition != 0)
                    {
                        var side = CurrentPoisition > 0 ? Sides.Sell : Sides.Buy;

                        _leaveStrategy = new LimitQuoterStrategy(
                            side,
                            CurrentPoisition.PrepareSizeToTrade(),
                            md.BestPair.SpreadPrice.CheckIfValueNullThenZero() > _spread ? Security.PriceStep.CheckIfValueNullThenZero() : 0,
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
                    Debug.WriteLine("SS stoping, try to clean all childs");
                    ChildStrategies.Clear();
                    Debug.WriteLine("SS stopped");
                })
                .Once()
                .Apply(this);

            base.OnStarted();
        }

        private void AssignEnterRulesAndStart(LimitQuoterStrategy enterStrategy)
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
                    {
                        _lastBuyQuoterPrice = bestPair.Bid.Price;
                        _lastSellQuoterPrice = bestPair.Ask.Price;

                        enterStrategy.Stop();
                    }

                })
                .Until(() => enterStrategy.ProcessState == ProcessStates.Stopping)
                .Apply(this);

            enterStrategy.WhenNewMyTrade()
                .Do(mt =>
                {
                    var sign = 1;

                    if (mt.Trade.OrderDirection == Sides.Sell)
                        sign = -1;

                    PositionSyncIncrement(mt.Trade.Volume * sign);
                    PositionMoneySyncIncrement(mt.Trade.Volume * mt.Trade.Price * sign * -1);

                    _leaveStrategy?.Stop();
                })
                .Until(() => enterStrategy.ProcessState == ProcessStates.Stopping)
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
                .Until(() => enterStrategy.ProcessState == ProcessStates.Stopping)
                .Apply(this);


            MarkStrategyLikeChild(enterStrategy);
            ChildStrategies.Add(enterStrategy);
        }

        private void AssignLeaveRulesAndStart()
        {
            if (_leaveStrategy == null)
                throw new NullReferenceException("leaveStrategy");

            _leaveStrategy.WhenStopping()
                .Do(() =>
                {
                    _isLeaverActivated = false;

                    ChildStrategies.Remove(_leaveStrategy);
                })
                .Until(() => _leaveStrategy.ProcessState == ProcessStates.Stopping)
                .Apply(this);


            MarkStrategyLikeChild(_leaveStrategy);
            ChildStrategies.Add(_leaveStrategy);
        }

        private void PositionSyncIncrement(decimal addedValue)
        {
            lock (_locker)
            {
                CurrentPoisition += addedValue;
            }
        }

        private void PositionMoneySyncIncrement(decimal addedValue)
        {
            lock (_locker)
            {
                CurrentPositionMoney += addedValue;
            }
        }

        private decimal CalculateExitPositionPrice()
        {
            return CurrentPoisition > 0
                ? Math.Round(CurrentPositionMoney / CurrentPoisition, 4) * -1 + _spread
                : Math.Round(CurrentPositionMoney / CurrentPoisition, 4) * -1 - _spread;
        }
    }
}