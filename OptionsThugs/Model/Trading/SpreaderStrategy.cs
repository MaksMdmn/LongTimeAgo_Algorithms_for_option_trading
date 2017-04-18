using System;
using System.Diagnostics;
using System.Globalization;
using OptionsThugs.Model.Common;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model.Trading
{
    public class SpreaderStrategy : PrimaryStrategy
    {
        private readonly object _syncRoot = new object();

        private readonly decimal _spread;
        private readonly decimal _lot;
        private readonly decimal _limitedFuturesValue;
        private readonly Sides _sideForEnterToPosition;

        private LimitQuoterStrategy _enterStrategy;
        private LimitQuoterStrategy _leaveStrategy;

        private decimal _lastQuoterPrice;
        private decimal _lastQuoterSpreadBuyPart;
        private decimal _lastQuoterSpreadSellPart;

        private volatile bool _isEnterActivated;
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

        public SpreaderStrategy(decimal currentPosition, decimal currentPositionPrice, decimal spread, decimal lot, Sides sideForEnterToPosition)
            : this(currentPosition, currentPositionPrice, spread, lot, sideForEnterToPosition, 0) { }


        public SpreaderStrategy(decimal currentPosition, decimal currentPositionPrice, decimal spread, decimal lot,
            Sides sideForEnterToPosition, decimal absMaxFuturesNumber)
        {
            if (absMaxFuturesNumber < 0)
                throw new ArgumentException("limitation of futures positions is established by value >=0 :" + absMaxFuturesNumber);

            TenRepresentationOfDecimalNumbers = 10000;

            _spread = spread;
            _lot = lot;
            _sideForEnterToPosition = sideForEnterToPosition;
            CurrentPoisition = currentPosition;
            CurrentPositionMoney = currentPosition * currentPositionPrice * -1;

            _isEnterActivated = false;
            _isLeaverActivated = false;

            if (sideForEnterToPosition == Sides.Sell)
                _limitedFuturesValue = absMaxFuturesNumber != 0 ? absMaxFuturesNumber * -1 : decimal.MinValue;
            else
                _limitedFuturesValue = absMaxFuturesNumber != 0 ? absMaxFuturesNumber : decimal.MaxValue;
        }


        protected override void OnStarted()
        {
            if (_spread <= 0) throw new ArgumentException("Spread cannot be below zero: " + _spread);
            if (_lot <= 0) throw new ArgumentException("Lot cannot be below zero: " + _lot);
            if (Security.PriceStep == null) throw new ArgumentException("Cannot read security price set, probably data still loading... :" + Security.PriceStep);

            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    if (!_isEnterActivated && md.CheckIfSpreadExist())
                    {
                        _isEnterActivated = true;

                        var sign = _sideForEnterToPosition == Sides.Buy ? 1 : -1;
                        var size = _lot.ShrinkSizeToTrade(_sideForEnterToPosition, CurrentPoisition, _limitedFuturesValue);
                        var step = md.BestPair.SpreadPrice.CheckIfValueNullThenZero() > _spread
                            ? Security.PriceStep.CheckIfValueNullThenZero() * sign
                            : 0;
                        var price = md.CalculateWorstLimitSpreadPrice(_sideForEnterToPosition, _spread);

                        if (size <= 0)
                        {
                            _isEnterActivated = false;
                        }
                        else
                        {
                            _enterStrategy = new LimitQuoterStrategy(_sideForEnterToPosition, size, step, price)
                            {
                                IsLimitOrdersAlwaysRepresent = true
                            };

                            AssignEnterRulesAndStart();
                        }
                    }

                    if (!_isLeaverActivated && CurrentPoisition != 0)
                    {
                        _isLeaverActivated = true;

                        var side = CurrentPoisition > 0 ? Sides.Sell : Sides.Buy;
                        var sign = side == Sides.Buy ? 1 : -1;
                        var size = CurrentPoisition.PrepareSizeToTrade();
                        var step = md.BestPair.SpreadPrice.CheckIfValueNullThenZero() > _spread
                            ? Security.PriceStep.CheckIfValueNullThenZero() * sign
                            : 0;
                        var price = CalculateExitPositionPrice();

                        _leaveStrategy = new LimitQuoterStrategy(side, size, step, price)
                        {
                            IsLimitOrdersAlwaysRepresent = true
                        };


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

        private void AssignEnterRulesAndStart()
        {
            if (_enterStrategy == null)
                throw new NullReferenceException("strategy, when was trying to assign enter rules");

            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    if (_lastQuoterPrice == 0)
                        return;

                    if (md.BestBid == null || md.BestAsk == null)
                        return;

                    var bestQuote = md.GetSuitableBestLimitQuote(_enterStrategy.QuotingSide);

                    if (_enterStrategy.QuotePriceShift != 0)
                    {
                        if (_lastQuoterPrice != bestQuote.Price ||
                            md.BestPair.SpreadPrice != null && md.BestPair.SpreadPrice.Value <= _spread)
                        {
                            _lastQuoterPrice = 0;

                            _enterStrategy.SafeStop();
                        }
                    }

                    if (_enterStrategy.QuotePriceShift == 0 && CheckIfSpreadChangedOrItsParts(md.BestPair))
                    {
                        _enterStrategy.SafeStop();
                    }

                })
                .Until(() => !_isEnterActivated)
                .Apply(this);

            _enterStrategy.WhenOrderRegistered()
                .Do(o =>
                {
                    _lastQuoterPrice = o.Price;
                })
                .Until(() => !_isEnterActivated)
                .Apply(this);

            _enterStrategy.WhenNewMyTrade()
                .Do(mt =>
                {
                    var sign = 1;

                    if (_enterStrategy.QuotingSide == Sides.Sell)
                        sign = -1;

                    PositionAndMoneySyncIncrementation(mt.Trade.Volume * sign, mt.Trade.Volume * mt.Trade.Price * sign * -1);

                    _leaveStrategy.SafeStop();
                })
                .Until(() => !_isEnterActivated)
                .Apply(this);

            _enterStrategy.WhenStopping()
                .Do(() =>
                {
                    _isEnterActivated = false;

                    ChildStrategies.Remove(_enterStrategy);
                })
                .Until(() => !_isEnterActivated)
                .Apply(this);


            MarkStrategyLikeChild(_enterStrategy);
            ChildStrategies.Add(_enterStrategy);
        }

        private void AssignLeaveRulesAndStart()
        {
            if (_leaveStrategy == null)
                throw new NullReferenceException("leaveStrategy");

            _leaveStrategy.WhenNewMyTrade()
                .Do(mt =>
                {
                    var sign = 1;

                    if (_leaveStrategy.QuotingSide == Sides.Sell)
                        sign = -1;

                    PositionAndMoneySyncIncrementation(mt.Trade.Volume * sign, mt.Trade.Volume * mt.Trade.Price * sign * -1);

                    _enterStrategy.SafeStop();
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

        private void PositionAndMoneySyncIncrementation(decimal addedPosValue, decimal addedMoneyValue)
        {
            lock (_syncRoot)
            {
                CurrentPoisition += addedPosValue;
                CurrentPositionMoney += addedMoneyValue;

                if (CurrentPoisition == 0)
                    CurrentPositionMoney = 0;
            }
        }

        private decimal CalculateExitPositionPrice()
        {
            return Security.ShrinkPrice(CurrentPoisition > 0
                ? Math.Round(CurrentPositionMoney / CurrentPoisition, 4) * -1 + _spread
                : Math.Round(CurrentPositionMoney / CurrentPoisition, 4) * -1 - _spread);
        }

        private bool CheckIfSpreadChangedOrItsParts(MarketDepthPair bestPair)
        {
            var result = bestPair.Bid.Price != _lastQuoterSpreadBuyPart
                || bestPair.Ask.Price != _lastQuoterSpreadSellPart;

            if (result)
            {
                _lastQuoterSpreadBuyPart = bestPair.Bid.Price;
                _lastQuoterSpreadSellPart = bestPair.Ask.Price;
            }

            return result;
        }
    }
}