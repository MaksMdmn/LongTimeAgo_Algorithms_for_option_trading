using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OptionsThugs.Model;
using OptionsThugs.Model.Trading;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;

namespace OptionsThugs.xTests
{
    public class LqsTest : BaseStrategyTest
    {
        public LqsTest(LogManager logManager, Connector stConnector, Portfolio stPortfolio, Security sSecurity) : base(logManager, stConnector, stPortfolio, sSecurity)
        {
        }

        public void CreateNewLqsStrategy(Sides side, decimal volume, decimal priceShift, decimal stopQuote)
        {
            StrategyForTest = new LimitQuoterStrategy(side, volume, priceShift, stopQuote);

            StrategyForTest.SetStrategyEntitiesForWork(StConnector, StSecurity, StPortfolio);
            StrategyForTest.RegisterStrategyEntitiesForWork(
                new Security[] { },
                new Security[] { StSecurity },
                new Portfolio[] { StPortfolio });
        }

        public void CreateNewLqsStrategy(Sides side, decimal volume, decimal priceShift)
        {
            CreateNewLqsStrategy(side, volume, priceShift, 0);
        }
    }
}
