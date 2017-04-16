using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OptionsThugs.Model.Trading;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;

namespace OptionsThugs.xTests
{
    public abstract class BaseStrategyTest
    {
        protected readonly Connector StConnector;
        protected readonly Portfolio StPortfolio;
        protected readonly Security StSecurity;

        private readonly LogManager _logManager;

        private bool _isStrategyRunning;
        private PrimaryStrategy _strategyForTest;

        public PrimaryStrategy StrategyForTest
        {
            get
            {
                return _strategyForTest;
            }
            protected set
            {
                _strategyForTest = value;
                PrepareStrategyForTest();
            }
        }

        protected BaseStrategyTest(LogManager logManager, Connector stConnector, Portfolio stPortfolio, Security sSecurity)
        {
            _logManager = logManager;
            StConnector = stConnector;
            StPortfolio = stPortfolio;
            StSecurity = sSecurity;

            _isStrategyRunning = false;
        }

        public void StartStopStrategyForTest()
        {
            if (StrategyForTest == null) throw new NullReferenceException("StrategyForTest");

            if (_isStrategyRunning)
                StrategyForTest.Stop();
            else
                StrategyForTest.Start();

            _isStrategyRunning = !_isStrategyRunning;
        }

        private void PrepareStrategyForTest()
        {
            _logManager?.Sources.Add(StrategyForTest);

            StrategyForTest.ProcessStateChanged += st => { Debug.WriteLine(st.ProcessState); };
        }

    }
}
