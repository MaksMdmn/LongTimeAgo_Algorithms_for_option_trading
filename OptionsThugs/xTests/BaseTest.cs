using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;

namespace OptionsThugs.xTests
{
    public class BaseTest
    {
        protected readonly LogManager _logManager;
        protected readonly Connector _connector;
        protected readonly Portfolio _portfolio;
        protected readonly Security _security;

        public Strategy Strategy { get; protected set; }

        public BaseTest(LogManager logManager, Connector connector, Portfolio portfolio, Security security)
        {
            _logManager = logManager;
            _connector = connector;
            _portfolio = portfolio;
            _security = security;
        }

        protected void ImportantPreparations(Strategy s)
        {
            s.Connector = _connector;
            s.Portfolio = _portfolio;
            s.Security = _security;

            _logManager.Sources.Add(s);

            s.ProcessStateChanged += st => { Debug.WriteLine(st.ProcessState); };

            Strategy = s;
        }

    }
}
