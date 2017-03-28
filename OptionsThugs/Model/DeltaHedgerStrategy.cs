using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ecng.Collections;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugs.Model.Common;
using OptionsThugs.Model.Primary;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class DeltaHedgerStrategy : Strategy
    {
        private readonly decimal _maxFuturesPositionVal;
        private readonly decimal _minFuturesPositionVal;
        private readonly decimal _deltaStep;
        private readonly decimal _deltaBuffer;
        private readonly PriceHedgeLevel[] _priceLevelsForHedge;
        private readonly List<Security> _options;
        private readonly SynchronizedDictionary<Security, decimal> _optionPositions;
        private bool _isPriceLevelsForHedgeInitialized;
        private decimal _futuresPosition;
        private volatile bool _isDeltaHedging;

        //Security field = Underlying assets here (futures for hedge)


        public DeltaHedgerStrategy(decimal deltaStep, List<Security> options)
            : this(deltaStep, 0, options) { }

        public DeltaHedgerStrategy(decimal deltaStep, decimal deltaBuffer, List<Security> options) : this(0, 0, deltaStep, deltaBuffer, options) { }

        public DeltaHedgerStrategy(PriceHedgeLevel[] priceLevelsForHedge, List<Security> options)
            : this(0, priceLevelsForHedge, options) {}

        public DeltaHedgerStrategy(decimal deltaStep, PriceHedgeLevel[] priceLevelsForHedge, List<Security> options)
            : this(deltaStep, 0, priceLevelsForHedge, options){}
        
        public DeltaHedgerStrategy(decimal deltaStep, decimal deltaBuffer, PriceHedgeLevel[] priceLevelsForHedge,
            List<Security> options): this(0, 0, deltaStep, deltaBuffer, priceLevelsForHedge, options)
        {
            CheckIfPriceArrOkay(priceLevelsForHedge);
            _priceLevelsForHedge = priceLevelsForHedge;
            _isPriceLevelsForHedgeInitialized = true;
        }

        public DeltaHedgerStrategy(decimal minFuturesPositionVal, decimal maxFuturesPositionVal, decimal deltaStep,
            decimal deltaBuffer, PriceHedgeLevel[] priceLevelsForHedge, List<Security> options)
            : this(minFuturesPositionVal, maxFuturesPositionVal, deltaStep, deltaBuffer, options)
        {
            CheckIfPriceArrOkay(priceLevelsForHedge);
            _priceLevelsForHedge = priceLevelsForHedge;
            _isPriceLevelsForHedgeInitialized = true;
        }

        public DeltaHedgerStrategy(decimal minFuturesPositionVal, decimal maxFuturesPositionVal, decimal deltaStep, decimal deltaBuffer, List<Security> options)
        {
            _minFuturesPositionVal = minFuturesPositionVal;
            _maxFuturesPositionVal = maxFuturesPositionVal;
            _deltaStep = deltaStep;
            _deltaBuffer = deltaBuffer;
            _optionPositions = new SynchronizedDictionary<Security, decimal>();
            _futuresPosition = 0;
            _options = options;
            _isPriceLevelsForHedgeInitialized = false;
            _isDeltaHedging = false;
        }

        protected override void OnStarted()
        {
            if (Connector == null || Security == null || Portfolio == null) return;
            if (_deltaStep < 0) return;

            Connector.RegisterMarketDepth(Security);

            _futuresPosition = CheckIfValueNull(Connector.GetPosition(Portfolio, Security).CurrentValue);

            _options.ForEach(o =>
            {
                Connector.RegisterMarketDepth(o);

                decimal tempPosition = CheckIfValueNull(Connector.GetPosition(Portfolio, o).CurrentValue);

                _optionPositions[o] = tempPosition;
            });

            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    if (!_isDeltaHedging)
                    {
                        _isDeltaHedging = true;

                        if (Security.BestAsk == null)
                        {
                            _isDeltaHedging = false;
                            return; //TODO pzdc
                        }

                        if (_isPriceLevelsForHedgeInitialized)
                        {
                            _priceLevelsForHedge.ForEach(level =>
                            {
                                //TODO last price??
                                if (level.CheckIfWasCrossedByPrice(Security.BestAsk.Price))
                                {
                                    DoHedge(CalcPosDelta(), 1);
                                }
                            });
                        }

                        var currentDelta = CalcPosDelta();

                        if (_deltaStep != 0
                            && Math.Abs(currentDelta / _deltaStep) >= 1
                            && currentDelta != 0)
                        {
                            DoHedge(currentDelta, _deltaStep);
                        }

                        _isDeltaHedging = false;
                    }

                })
                .Until(() => this.ProcessState == ProcessStates.Stopping) // может лишнее
                .Apply(this);


            base.OnStarted();
        }

        private decimal CalcPosDelta()
        {
            decimal result = 0;

            result += _futuresPosition;

            result += _deltaBuffer;

            _optionPositions.ForEach(pair =>
            {
                //TODO pzdc polniy
                if (Security == null || Security.BestAsk == null) return;
                if (pair.Key == null || pair.Key.BestAsk == null || pair.Key.Strike == null) return;

                var vol = GreeksCalculator.CalculateImpliedVolatility(OptionTypes.Call,
                    Security.BestAsk.Price, pair.Key.Strike.Value, 23, 365, pair.Key.BestAsk.Price, 0.5m);
                var d1 = GreeksCalculator.Calculate_d1(Security.BestAsk.Price, pair.Key.Strike.Value, 23,
                    365, vol);
                var delta = GreeksCalculator.CalculateDelta(pair.Key.OptionType.Value, d1);

                result += CheckIfValueNull(delta) * pair.Value;

            });

            Debug.WriteLine("curdelta: " + result);

            return result;
        }

        private void DoHedge(decimal currentDelta, decimal deltaStep)
        {
            QuoterStrategy mqs = null;

            var hedgeSize = (currentDelta / deltaStep).PrepareSizeToTrade();

            if (currentDelta > 0)
            {
                if(_futuresPosition <= _minFuturesPositionVal)
                    return;

                if (_futuresPosition - hedgeSize < _minFuturesPositionVal)
                    hedgeSize = (_minFuturesPositionVal - _futuresPosition).PrepareSizeToTrade();

                mqs = new MarketQuoterStrategy(Sides.Sell, hedgeSize,
                    Security.GetMarketPrice(Sides.Sell, Connector));
                _futuresPosition -= hedgeSize;
            }

            if (currentDelta < 0)
            {
                if (_futuresPosition >= _maxFuturesPositionVal)
                    return;

                if (_futuresPosition + hedgeSize > _maxFuturesPositionVal)
                    hedgeSize = (_maxFuturesPositionVal - _futuresPosition).PrepareSizeToTrade();

                if (hedgeSize <= 0)
                    return;

                mqs = new MarketQuoterStrategy(Sides.Buy, hedgeSize,
                    Security.GetMarketPrice(Sides.Buy, Connector));
                _futuresPosition += hedgeSize;
            }

            ChildStrategies.Add(mqs);
        }


        private void CheckIfPriceArrOkay(PriceHedgeLevel[] priceLevels)
        {
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

        private decimal CheckIfValueNull(decimal? val)
        {
            return val == null ? 0 : val.Value;
        }
    }
}