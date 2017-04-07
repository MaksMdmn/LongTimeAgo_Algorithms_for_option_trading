using System;
using System.Diagnostics;
using Ecng.Collections;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugs.Model.Common;
using OptionsThugs.Model.Primary;
using StockSharp.Algo;
using StockSharp.Algo.Derivatives;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class DeltaHedgerStrategy : PrimaryStrategy
    {
        private readonly SynchronizedDictionary<Security, decimal> _optionsPositions;
        private decimal _futuresPosition;
        private PriceHedgeLevel[] _priceLevelsForHedge;

        private decimal _totalDelta;
        private bool _isPriceLevelsForHedgeInitialized;
        private volatile bool _isDeltaHedging;
        //Security = Underlying assets (future for hedge)

        public decimal MaxFuturesPositionVal { get; set; }

        public decimal MinFuturesPositionVal { get; set; }

        public decimal DeltaStep { get; set; }

        public decimal DeltaBuffer { get; set; }

        public PriceHedgeLevel[] PriceLevelsForHedge
        {
            get { return _priceLevelsForHedge; }
            set
            {
                _priceLevelsForHedge = value;
                CheckIfPriceArrOkay(_priceLevelsForHedge);
            }
        }

        public DeltaHedgerStrategy(decimal futuresPosition, SynchronizedDictionary<Security, decimal> optionsPositions)
            : this(futuresPosition, optionsPositions, 1, 0, null, decimal.MinValue, decimal.MaxValue) //TODO min max is it ok?
        { }


        private DeltaHedgerStrategy(decimal futuresPosition, SynchronizedDictionary<Security, decimal> optionsPositions,
            decimal deltaStep, decimal deltaBuffer, PriceHedgeLevel[] priceLevelsForHedge,
            decimal minFuturesPositionVal, decimal maxFuturesPositionVal)
        {
            _futuresPosition = futuresPosition;
            _optionsPositions = optionsPositions;
            DeltaStep = deltaStep;
            DeltaBuffer = deltaBuffer;
            _priceLevelsForHedge = priceLevelsForHedge;
            MinFuturesPositionVal = minFuturesPositionVal;
            MaxFuturesPositionVal = maxFuturesPositionVal;

            if (_priceLevelsForHedge == null)
                _isPriceLevelsForHedgeInitialized = false;
            else
                CheckIfPriceArrOkay(_priceLevelsForHedge);

            _isDeltaHedging = false;
            _totalDelta = 0;
        }


        protected override void OnStarted()
        {
            if (DeltaStep < 0) throw new ArgumentException("DeltaStep cannot be below zero: " + DeltaStep); ;

            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    if (!_isDeltaHedging)
                    {
                        _isDeltaHedging = true;

                        var futuresQuote = _totalDelta >= 0 ? md.BestBid : md.BestAsk;

                        if (futuresQuote == null)
                        {
                            _isDeltaHedging = false;
                            return;
                        }

                        if (_isPriceLevelsForHedgeInitialized)
                        {
                            _priceLevelsForHedge.ForEach(level =>
                            {
                                if (level.CheckIfWasCrossedByPrice(futuresQuote.Price))
                                {
                                    DoHedge(CalcPosDelta(), 1);
                                }
                            });
                        }

                        _totalDelta = CalcPosDelta();

                        if (DeltaStep != 0
                            && Math.Abs(_totalDelta / DeltaStep) >= 1
                            && _totalDelta != 0)
                        {
                            DoHedge(_totalDelta, DeltaStep);
                        }

                        _isDeltaHedging = false;
                    }

                })
                .Until(() => ProcessState == ProcessStates.Stopping) // может лишнее
                .Apply(this);


            base.OnStarted();
        }

        private decimal CalcPosDelta()
        {
            decimal delta = 0M;

            delta += _futuresPosition;

            delta += DeltaBuffer;

            _optionsPositions.ForEach(pair =>
            {
                decimal? futPrice = null;

                if (pair.Value > 0)
                    futPrice = Security.BestBid?.Price;
                if (pair.Value < 0)
                    futPrice = Security.BestAsk?.Price;

                var bs = new BlackScholes(pair.Key, Security, Connector);

                delta += bs.Delta(DateTimeOffset.Now, null, futPrice).CheckIfValueNullThenZero() * pair.Value;
            });

            Debug.WriteLine(delta);

            return delta;
        }

        private void DoHedge(decimal currentDelta, decimal deltaStep)
        {
            QuoterStrategy mqs = null;

            var hedgeSize = (currentDelta / deltaStep).PrepareSizeToTrade();

            if (currentDelta > 0)
            {
                if (_futuresPosition <= MinFuturesPositionVal)
                    return;

                if (_futuresPosition - hedgeSize < MinFuturesPositionVal)
                    hedgeSize = (MinFuturesPositionVal - _futuresPosition).PrepareSizeToTrade();

                if (hedgeSize <= 0)
                    return;

                mqs = new MarketQuoterStrategy(Sides.Sell, hedgeSize, Security.GetMarketPrice(Sides.Sell));
                _futuresPosition -= hedgeSize;
            }

            if (currentDelta < 0)
            {
                if (_futuresPosition >= MaxFuturesPositionVal)
                    return;

                if (_futuresPosition + hedgeSize > MaxFuturesPositionVal)
                    hedgeSize = (MaxFuturesPositionVal - _futuresPosition).PrepareSizeToTrade();

                if (hedgeSize <= 0)
                    return;

                mqs = new MarketQuoterStrategy(Sides.Buy, hedgeSize, Security.GetMarketPrice(Sides.Buy));
                _futuresPosition += hedgeSize;
            }

            MarkStrategyLikeChild(mqs);
            ChildStrategies.Add(mqs);
        }


        private void CheckIfPriceArrOkay(PriceHedgeLevel[] priceLevels)
        {
            if (priceLevels == null)
                throw new NullReferenceException("priceLevels");

            if (priceLevels.Length == 0)
                throw new ArgumentException("prices array is empty.");

            foreach (var level in priceLevels)
            {
                if (level == null)
                    throw new NullReferenceException("level cannot be null");

                if (level.Price <= 0)
                    throw new ArgumentException("all prices must be above zero: " + level);
            }

            _isPriceLevelsForHedgeInitialized = true;
        }
    }
}