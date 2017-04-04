using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OptionsThugs.Model.Primary;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;

namespace OptionsThugs.xTests
{
    public class LqsTest : BaseTest
    {
        public LqsTest(LogManager logManager, Connector connector, Portfolio portfolio, Security security)
            : base(logManager, connector, portfolio, security)
        {
        }

        public void CreateNewLqsStrategy(Sides side, decimal volume, decimal priceShift, decimal stopQuote)
        {
            LimitQuoterStrategy strg = new LimitQuoterStrategy(side, volume, priceShift, stopQuote);

            ImportantPreparations(strg);
        }
    }
}
