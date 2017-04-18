using OptionsThugs.Model.Trading;
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;

namespace OptionsThugs.xTests
{
    public class SprTest : BaseStrategyTest
    {
        public SprTest(LogManager logManager, Connector stConnector, Portfolio stPortfolio, Security sSecurity) : base(logManager, stConnector, stPortfolio, sSecurity)
        {
        }

        public void CreateNewSprStrategy(decimal currentPosition, decimal currentPositionPrice, decimal spread, decimal lot,
            Sides sideForEnterToPosition, decimal absMaxFuturesNumber)
        {
            StrategyForTest = new SpreaderStrategy(currentPosition, currentPositionPrice, spread, lot,
                sideForEnterToPosition, absMaxFuturesNumber);

            StrategyForTest.SetStrategyEntitiesForWork(StConnector, StSecurity, StPortfolio);
            StrategyForTest.RegisterStrategyEntitiesForWork(
                new Security[] { StSecurity },
                new Security[] { StSecurity },
                new Portfolio[] { StPortfolio });
        }

        public void CreateNewSprStrategy(decimal currentPosition, decimal currentPositionPrice, decimal spread, decimal lot,
            Sides sideForEnterToPosition)
        {
            CreateNewSprStrategy(currentPosition, currentPositionPrice, spread, lot, 
                sideForEnterToPosition, 0);
        }
    }
}
