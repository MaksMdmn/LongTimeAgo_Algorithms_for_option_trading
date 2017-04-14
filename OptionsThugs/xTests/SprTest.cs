using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OptionsThugs.Model;
using OptionsThugs.Model.Common;
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Logging;

namespace OptionsThugs.xTests
{
    public class SprTest : BaseStrategyTest
    {
        public SprTest(LogManager logManager, Connector stConnector, Portfolio stPortfolio, Security sSecurity) : base(logManager, stConnector, stPortfolio, sSecurity)
        {
        }

        public void CreateNewSprStrategy(decimal currentPosition, decimal currentPositionPrice, decimal spread, decimal lot,
            DealDirection sideForEnterToPosition, decimal minFuturesPositionVal, decimal maxFuturesPositionVal)
        {
            StrategyForTest = new SpreaderStrategy(currentPosition, currentPositionPrice, spread, lot, 
                sideForEnterToPosition, minFuturesPositionVal, maxFuturesPositionVal);

            StrategyForTest.SetStrategyEntitiesForWork(StConnector, StSecurity, StPortfolio);
            StrategyForTest.RegisterStrategyEntitiesForWork(
                new Security[] { StSecurity },
                new Security[] { StSecurity },
                new Portfolio[] { StPortfolio });
        }

        public void CreateNewSprStrategy(decimal currentPosition, decimal currentPositionPrice, decimal spread, decimal lot,
            DealDirection sideForEnterToPosition)
        {
            CreateNewSprStrategy(currentPosition, currentPositionPrice, spread, lot, 
                sideForEnterToPosition, decimal.MinValue, decimal.MaxValue);
        }
    }
}
