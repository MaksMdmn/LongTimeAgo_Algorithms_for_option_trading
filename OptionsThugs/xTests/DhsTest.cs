using System;
using System.Collections.Generic;
using Ecng.Collections;
using OptionsThugs.Model;
using OptionsThugs.Model.Common;
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Logging;

namespace OptionsThugs.xTests
{
    public class DhsTest : BaseStrategyTest
    {
        public DhsTest(LogManager logManager, Connector stConnector, Portfolio stPortfolio, Security sSecurity) 
            : base(logManager, stConnector, stPortfolio, sSecurity)
        {
        }

        public void CreateNewDhstrategy(decimal futuresPosition, SynchronizedDictionary<Security, decimal> optionsPositions)
        {
            StrategyForTest = new DeltaHedgerStrategy(futuresPosition, optionsPositions);

            List<Security> securitiesToReg = new List<Security>();

            securitiesToReg.AddRange(optionsPositions.Keys);
            securitiesToReg.Add(StSecurity);

            StrategyForTest.SetStrategyEntitiesForWork(StConnector, StSecurity, StPortfolio);
            StrategyForTest.RegisterStrategyEntitiesForWork(
                securitiesToReg.ToArray(),
                new Security[] { StSecurity },
                new Portfolio[] { StPortfolio });
        }

        public void  CreateNewDhstrategy(decimal futuresPosition, SynchronizedDictionary<Security, decimal> optionsPositions,
            decimal deltaStep, decimal deltaBuffer)
        {
            CreateNewDhstrategy(futuresPosition, optionsPositions);

            var dhs = StrategyForTest as DeltaHedgerStrategy;

            if (dhs == null)
                throw new InvalidCastException("problem with strategy casting, check test method");

            dhs.DeltaStep = deltaStep;
            dhs.DeltaBuffer = deltaBuffer;
        }
        public void  CreateNewDhstrategy(decimal futuresPosition, SynchronizedDictionary<Security, decimal> optionsPositions,
            PriceHedgeLevel[] priceLevelsForHedge)
        {
            CreateNewDhstrategy(futuresPosition, optionsPositions);

            var dhs = StrategyForTest as DeltaHedgerStrategy;

            if (dhs == null)
                throw new InvalidCastException("problem with strategy casting, check test method");

            dhs.PriceLevelsForHedge = priceLevelsForHedge;
        }


        public void  CreateNewDhstrategy(decimal futuresPosition, SynchronizedDictionary<Security, decimal> optionsPositions,
            decimal deltaStep, PriceHedgeLevel[] priceLevelsForHedge)
        {
            CreateNewDhstrategy(futuresPosition, optionsPositions);

            var dhs = StrategyForTest as DeltaHedgerStrategy;

            if (dhs == null)
                throw new InvalidCastException("problem with strategy casting, check test method");

            dhs.DeltaStep = deltaStep;
            dhs.PriceLevelsForHedge = priceLevelsForHedge;

        }

        public void  CreateNewDhstrategy(decimal futuresPosition, SynchronizedDictionary<Security, decimal> optionsPositions,
            decimal deltaStep, decimal minFuturesPositionVal, decimal maxFuturesPositionVal)
        {
            CreateNewDhstrategy(futuresPosition, optionsPositions);

            var dhs = StrategyForTest as DeltaHedgerStrategy;

            if (dhs == null)
                throw new InvalidCastException("problem with strategy casting, check test method");

            dhs.DeltaStep = deltaStep;
            dhs.MinFuturesPositionVal = minFuturesPositionVal;
            dhs.MaxFuturesPositionVal = maxFuturesPositionVal;
        }
    }
}
