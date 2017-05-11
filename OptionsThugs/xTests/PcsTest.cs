using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using Trading.Common;
using Trading.Strategies;

namespace OptionsThugs.xTests
{
    public class PcsTest : BaseStrategyTest
    {
        public PcsTest(LogManager logManager, Connector stConnector, Portfolio stPortfolio, Security sSecurity) : base(logManager, stConnector, stPortfolio, sSecurity)
        {
        }

        public void CreateNewCondtrategy(decimal priceToClose, Security securityToClose, PriceDirection securityDesirableDirection, decimal positionToClose)
        {
            //StrategyForTest = new PositionCloserStrategy(priceToClose, securityToClose, securityDesirableDirection, positionToClose);

            StrategyForTest.SetStrategyEntitiesForWork(StConnector, StSecurity, StPortfolio);
        }

        public void CreateNewCondtrategy(decimal priceToClose, decimal positionToClose)
        {
            CreateNewCondtrategy(priceToClose, null, PriceDirection.None, positionToClose);
        }
    }
}
