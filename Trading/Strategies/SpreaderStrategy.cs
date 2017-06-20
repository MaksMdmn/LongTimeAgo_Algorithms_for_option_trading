using System;
using System.Diagnostics;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;
using Trading.Common;

namespace Trading.Strategies
{
    public class SpreaderStrategy : PrimaryStrategy
    {
        private readonly object _syncRoot = new object();

        private readonly decimal _spread;
        private readonly decimal _lot;
        private readonly Sides _sideForEnterToPosition;

        private LimitQuoterStrategy _enterStrategy;
        private LimitQuoterStrategy _leaveStrategy;
        private MarketDepth md;

        private decimal _lastQuoterPrice;
        private decimal _lastQuoterSpreadBuyPart;
        private decimal _lastQuoterSpreadSellPart;

        private volatile bool _isEnterActivated;
        private volatile bool _isLeaverActivated;

        private volatile int _syncPosition;
        private volatile int _syncMoneyIntegerPart;
        private volatile int _syncMoneyDecimalPart;

        public decimal LimitedFuturesValueAbs { get; set; }

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

        public SpreaderStrategy(decimal currentPosition, decimal currentPositionPrice, decimal spread, decimal lot,
            Sides sideForEnterToPosition)
        {
            TenRepresentationOfDecimalNumbers = 10000;

            _spread = spread;
            _lot = lot;
            _sideForEnterToPosition = sideForEnterToPosition;
            CurrentPoisition = currentPosition;
            CurrentPositionMoney = currentPosition * currentPositionPrice * -1;

            _isEnterActivated = false;
            _isLeaverActivated = false;

            LimitedFuturesValueAbs = 0;
        }


        protected override void OnStarted()
        {
            DoStrategyPreparation(new Security[] { Security }, new Security[] { Security }, new Portfolio[] { Portfolio });

            if (_spread <= 0) throw new ArgumentException("Spread cannot be below zero: " + _spread);
            if (_lot <= 0) throw new ArgumentException("Lot cannot be below zero: " + _lot);
            if (Security.PriceStep == null) throw new ArgumentException("Cannot read security price set, probably data still loading... :" + Security.PriceStep);
            if (LimitedFuturesValueAbs < 0)
                throw new ArgumentException("limitation of futures positions is established by value >=0 : " + LimitedFuturesValueAbs);

            if (_sideForEnterToPosition == Sides.Sell)
                LimitedFuturesValueAbs = LimitedFuturesValueAbs != 0 ? LimitedFuturesValueAbs * -1 : decimal.MinValue;
            else
                LimitedFuturesValueAbs = LimitedFuturesValueAbs != 0 ? LimitedFuturesValueAbs : decimal.MaxValue;

            md = GetMarketDepth(Security);
            TimingController.SetTimingMethod(SpreadingProcess);

            Security.WhenMarketDepthChanged(Connector)
                .Do(() =>
                {
                    TimingController.TimingMethodHappened();
                    SpreadingProcess();
                })
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

        public override void PrimaryStopping()
        {
            _enterStrategy?.MarkPlaceAsPrimaryStoppingStarted();
            _leaveStrategy?.MarkPlaceAsPrimaryStoppingStarted();
            //TODO дичь, но проверить - бывает залипает заявка: проскакивает между остановкой стратегии и приходом ИД ордера с биржи - стратегия удаляется, ордер остаётся.
            base.PrimaryStopping();
        }

        private void SpreadingProcess() {
            try
            {
                if (!IsTradingTime())
                    return;

                if (!_isEnterActivated && md.CheckIfSpreadExist())
                {
                    _isEnterActivated = true;

                    var sign = _sideForEnterToPosition == Sides.Buy ? 1 : -1;
                    var size = _lot.ShrinkSizeToTrade(_sideForEnterToPosition, CurrentPoisition,
                        LimitedFuturesValueAbs);
                    var step = md.BestPair.SpreadPrice.CheckIfValueNullThenZero() > _spread
                        ? Security.PriceStep.Value * sign
                        : 0;
                    var price = md.CalculateWorstLimitSpreadPrice(_sideForEnterToPosition, _spread,
                        Security.PriceStep.Value);

                    if (size <= 0 || price <= 0)
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
                        ? Security.PriceStep.Value * sign
                        : 0;
                    var price = CalculateExitPositionPrice();

                    if (price <= 0)
                        throw new ArgumentException("Impossible to continue work, exit price <=0");

                    _leaveStrategy = new LimitQuoterStrategy(side, size, step, price)
                    {
                        IsLimitOrdersAlwaysRepresent = true
                    };


                    AssignLeaveRulesAndStart();
                }
            }
            catch (Exception e1)
            {
                this.AddErrorLog($"exception: {e1.Message}");
                PrimaryStopping();
            }
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

            _enterStrategy.PrimaryStrategyStopped += () =>
            {
                ChildStrategies.Remove(_enterStrategy);

                _isEnterActivated = false;
            };


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

            _leaveStrategy.PrimaryStrategyStopped += () =>
            {
                ChildStrategies.Remove(_leaveStrategy);

                _isLeaverActivated = false;
            };

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


        public override string ToString()
        {
            return $"{nameof(_spread)}: {_spread}, " +
                   $"{nameof(_lot)}: {_lot}, " +
                   $"{nameof(_sideForEnterToPosition)}: {_sideForEnterToPosition}, " +
                   $"{nameof(LimitedFuturesValueAbs)}: {LimitedFuturesValueAbs}, " +
                   $"{nameof(TenRepresentationOfDecimalNumbers)}: {TenRepresentationOfDecimalNumbers} "
                   + base.ToString();
        }
    }
}