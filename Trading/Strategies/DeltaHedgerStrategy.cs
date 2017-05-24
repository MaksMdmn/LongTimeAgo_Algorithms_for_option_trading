using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ecng.Collections;
using Microsoft.Practices.ObjectBuilder2;
using StockSharp.Algo;
using StockSharp.Algo.Derivatives;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;
using Trading.Common;

namespace Trading.Strategies
{
    public class DeltaHedgerStrategy : PrimaryStrategy
    {
        private readonly SynchronizedDictionary<Security, decimal> _optionsPositions;
        private decimal _futuresPosition;
        private List<PriceHedgeLevel> _priceLevelsForHedge;

        private decimal _totalDelta;
        private bool _isPriceLevelsForHedgeInitialized;
        private volatile bool _isDeltaHedging;
        //Security = Underlying assets (future for hedge)

        public decimal MaxFuturesPositionVal { get; set; }

        public decimal MinFuturesPositionVal { get; set; }

        public decimal DeltaStep { get; set; }

        public decimal DeltaBuffer { get; set; }

        public List<PriceHedgeLevel> PriceLevelsForHedge { get; private set; }

        public DeltaHedgerStrategy(decimal futuresPosition, SynchronizedDictionary<Security, decimal> optionsPositions)
        {
            _futuresPosition = futuresPosition;
            _optionsPositions = optionsPositions;
            DeltaStep = 1;
            DeltaBuffer = 0;
            _priceLevelsForHedge = null;
            MinFuturesPositionVal = decimal.MinValue;
            MaxFuturesPositionVal = decimal.MaxValue;

            _isPriceLevelsForHedgeInitialized = false;

            _isDeltaHedging = false;
            _totalDelta = 0;
        }

        public void AddHedgeLevel(PriceDirection direction, decimal value)
        {
            if (value <= 0)
                throw new ArgumentException("all prices must be above zero: " + value);

            if (PriceLevelsForHedge == null)
                PriceLevelsForHedge = new List<PriceHedgeLevel>();

            PriceLevelsForHedge.Add(new PriceHedgeLevel(direction, value));

            if (!_isPriceLevelsForHedgeInitialized && ProcessState == ProcessStates.Stopped)
                _isPriceLevelsForHedgeInitialized = true;
        }

        public void AddHedgeLevels(List<PriceHedgeLevel> levels)
        {
            CheckIfPriceArrOkay(_priceLevelsForHedge);

            if (PriceLevelsForHedge == null)
                PriceLevelsForHedge = new List<PriceHedgeLevel>();

            PriceLevelsForHedge.AddRange(levels);

            if (!_isPriceLevelsForHedgeInitialized && ProcessState == ProcessStates.Stopped)
                _isPriceLevelsForHedgeInitialized = true;
        }

        public void RemoveHedgeLevel(decimal value)
        {
            if (PriceLevelsForHedge == null)
                return;

            var searchIndex = -1;
            for (var i = 0; i < PriceLevelsForHedge.Count; i++)
            {
                if (PriceLevelsForHedge[i].Price == value)
                {
                    searchIndex = i;
                    break;
                }
            }

            if (searchIndex == -1)
                return;

            PriceLevelsForHedge.RemoveAt(searchIndex);
        }

        public void ClearHedgeLevels()
        {
            PriceLevelsForHedge?.Clear();
        }

        protected override void OnStarted()
        {
            List<Security> securitiesToReg = new List<Security>();
            securitiesToReg.AddRange(_optionsPositions.Keys);
            securitiesToReg.Add(Security);

            DoStrategyPreparation(securitiesToReg.ToArray(), new Security[] { Security }, new Portfolio[] { Portfolio });

            if (DeltaStep < 0) throw new ArgumentException("DeltaStep cannot be below zero: " + DeltaStep);

            var md = GetMarketDepth(Security);

            Security.WhenMarketDepthChanged(Connector)
                .Or(Connector.WhenIntervalElapsed(PrimaryStrategy.AutoUpdatePeriod))
                .Do(() =>
                {
                    try
                    {

                        if (!_isDeltaHedging
                            && IsTradingTime())
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
                                    if (MyTradingHelper.CheckIfWasCrossedByPrice(level, futuresQuote.Price))
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
                    }
                    catch (Exception e1)
                    {
                        this.AddErrorLog($"exception: {e1.Message}");
                        Stop();
                    }

                })
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

            return delta;
        }

        private void DoHedge(decimal currentDelta, decimal deltaStep)
        {
            QuoterStrategy mqs = null;

            var hedgeSize = (currentDelta / deltaStep).PrepareSizeToTrade();

            if (currentDelta > 0)
            {
                hedgeSize = hedgeSize.ShrinkSizeToTrade(Sides.Sell, _futuresPosition,
                    MinFuturesPositionVal);

                if (hedgeSize <= 0)
                    return;

                mqs = new MarketQuoterStrategy(Sides.Sell, hedgeSize, Security.GetMarketPrice(Sides.Sell));
                _futuresPosition -= hedgeSize;
            }

            if (currentDelta < 0)
            {
                hedgeSize = hedgeSize.ShrinkSizeToTrade(Sides.Buy, _futuresPosition,
                    MaxFuturesPositionVal);

                if (hedgeSize <= 0)
                    return;

                mqs = new MarketQuoterStrategy(Sides.Buy, hedgeSize, Security.GetMarketPrice(Sides.Buy));
                _futuresPosition += hedgeSize;
            }

            MarkStrategyLikeChild(mqs);
            ChildStrategies.Add(mqs);
        }


        private void CheckIfPriceArrOkay(List<PriceHedgeLevel> priceLevels)
        {
            if (priceLevels == null)
                throw new NullReferenceException("priceLevels");

            if (priceLevels.Count == 0)
                throw new ArgumentException("prices array is empty.");

            foreach (var level in priceLevels)
            {
                if (level == null)
                    throw new NullReferenceException("level cannot be null");

                if (level.Price <= 0)
                    throw new ArgumentException("all prices must be above zero: " + level);
            }
        }

        public override string ToString()
        {
            return $"{nameof(MaxFuturesPositionVal)}: {MaxFuturesPositionVal}, " +
                   $"{nameof(MinFuturesPositionVal)}: {MinFuturesPositionVal}, " +
                   $"{nameof(DeltaStep)}: {DeltaStep}, " +
                   $"{nameof(DeltaBuffer)}: {DeltaBuffer}, " +
                   $"hedge levels: {PriceLevelsForHedge?.Select(phl => phl.Direction + " " + phl.Price + " ")} "
                   + base.ToString();
        }
    }
}