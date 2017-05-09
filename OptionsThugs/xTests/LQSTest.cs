using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;
using Trading.Strategies;

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
        }

        public void CreateNewLqsStrategy(Sides side, decimal volume, decimal priceShift)
        {
            CreateNewLqsStrategy(side, volume, priceShift, 0);
        }
    }
}
