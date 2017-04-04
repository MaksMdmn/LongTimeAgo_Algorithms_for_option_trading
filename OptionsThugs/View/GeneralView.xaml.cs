using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using DevExpress.Xpf.Editors.Internal;
using Ecng.Common;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugs.Model;
using OptionsThugs.Model.Common;
using OptionsThugs.Model.Primary;
using OptionsThugs.xTests;
using StockSharp.Algo.Derivatives;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;
using StockSharp.Xaml;
using StockSharp_TraderConnection;

namespace OptionsThugs.View
{
    /// <summary>
    /// Interaction logic for GeneralView.xaml
    /// </summary>
    public partial class GeneralView : Window
    {
        private bool _testFlag = false;
        private readonly LogManager _logManager = new LogManager();
        private Strategy _strategy;
        private OptionDeskModel _optionDeskModel;
        private Security _sec2;
        private BaseTest _strategyTest;


        public GeneralView()
        {
            InitializeComponent();
            conn.SetupDefaultQuikLuaConnAndDisconn();

            _logManager.Listeners.Add(new GuiLogListener(myMon));
            _logManager.Listeners.Add(new FileLogListener("log.txt"));

        }

        private void StartStopClick(object sender, RoutedEventArgs e)
        {
            if (_testFlag)
            {
                _strategyTest.Strategy.Stop();
            }
            else
            {
                _strategyTest.Strategy.Start();
            }
            _testFlag = !_testFlag;
        }

        private void PrepareStrategy(object sender, RoutedEventArgs e)
        {
            _strategyTest = new LqsTest(_logManager, conn.SafeConnection.Connector, conn.SelectedPortfolio, conn.SelectedSecurity);

            ((LqsTest)_strategyTest).CreateNewLqsStrategy(Sides.Buy, 10, 1, 57390);


            //decimal sign = 1;
            //_strategy = CreateNewLQStrategy(Sides.Buy, 15, conn.SelectedSecurity.PriceStep.Value * sign, 0);
            //_strategy = CreateNewMQStrategy(Sides.Sell, 20, 58455);
            //_strategy = CreateNewCondtrategy(16450, 13, MyConditionalClosePosStrategy.PriceDirection.Down, _sec2);

            //List<Security> tempOptions = new List<Security>();

            //conn.SafeConnection.Connector.Positions.ForEach(p =>
            //{
            //    if (p.Security.Type == SecurityTypes.Option)
            //    {
            //        tempOptions.Add(p.Security);
            //    }
            //});

            //_strategy = CreateNewDHStrategy(1, tempOptions);


            #region Test OptionDesk and OptionDeskModel

            //_optionDeskModel = new OptionDeskModel();

            //Desk.Model = _optionDeskModel;

            //_optionDeskModel.MarketDataProvider = conn.SafeConnection.Connector;
            //_optionDeskModel.UnderlyingAsset = conn.SelectedSecurity;

            //var securities =
            //    conn.SafeConnection.Connector.Lookup(new Security()
            //    {
            //        Type = SecurityTypes.Option,

            //    });

            //securities.ForEach(s =>
            //{
            //    if (s.UnderlyingSecurityId.CompareIgnoreCase(conn.SelectedSecurity.Id))
            //        _optionDeskModel.Add(s);
            //});

            //_optionDeskModel.Refresh();

            #endregion
        }

        private void Prepare2Click(object sender, RoutedEventArgs e)
        {
            _sec2 = conn.SelectedSecurity;
            //Position pos = conn.SafeConnection.Connector.GetPosition(conn.SelectedPortfolio, conn.SelectedSecurity);

            //conn.SafeConnection.Connector.PositionChanged += p =>
            //{
            //    if (p.Security.UnderlyingSecurityId.ContainsIgnoreCase("si"))
            //        MessageBox.Show(p.Security.ToString() + " new pos: " + p.CurrentValue);
            //};

        }

        private LimitQuoterStrategy CreateNewLQStrategy(Sides side, decimal volume, decimal priceShift, decimal stopQuote)
        {
            LimitQuoterStrategy strg = new LimitQuoterStrategy(side, volume, priceShift, stopQuote)
            {
                Connector = conn.SafeConnection.Connector,
                Security = conn.SelectedSecurity,
                Portfolio = conn.SelectedPortfolio,
            };

            strg.ProcessStateChanged += s =>
            {
                Debug.WriteLine(s.ProcessState);
            };
            _logManager.Sources.Add(strg);

            return strg;
        }

        private MarketQuoterStrategy CreateNewMQStrategy(Sides side, decimal volume, decimal targetPrice)
        {
            MarketQuoterStrategy strg = new MarketQuoterStrategy(side, volume, targetPrice)
            {
                Connector = conn.SafeConnection.Connector,
                Security = conn.SelectedSecurity,
                Portfolio = conn.SelectedPortfolio,
            };

            strg.ProcessStateChanged += s =>
            {
                Debug.WriteLine(s.ProcessState);
            };
            _logManager.Sources.Add(strg);

            return strg;
        }

        private PositionCloserStrategy CreateNewCondtrategy(decimal priceToClose,
            decimal sizeToClose,
            PriceDirection securityDesirableDirection = PriceDirection.None,
            Security securityToClose = null)
        {
            PositionCloserStrategy strg;

            if (securityDesirableDirection == PriceDirection.None)
            {
                strg = new PositionCloserStrategy(priceToClose, sizeToClose)
                {
                    Connector = conn.SafeConnection.Connector,
                    Security = conn.SelectedSecurity,
                    Portfolio = conn.SelectedPortfolio,
                };
            }
            else
            {
                strg = new PositionCloserStrategy(priceToClose, securityToClose, securityDesirableDirection, sizeToClose)
                {
                    Connector = conn.SafeConnection.Connector,
                    Security = conn.SelectedSecurity,
                    Portfolio = conn.SelectedPortfolio,
                };
            }

            strg.ProcessStateChanged += s =>
            {
                Debug.WriteLine(s.ProcessState);
            };
            _logManager.Sources.Add(strg);

            return strg;
        }

        private DeltaHedgerStrategy CreateNewDHStrategy(decimal deltaStep, List<Security> options)
        {
            DeltaHedgerStrategy strg;

            //strg = new DeltaHedgerStrategy(deltaStep, options); 

            var tempLevelsArr = new PriceHedgeLevel[]
            {
                new PriceHedgeLevel(PriceDirection.Up, 58090),
                new PriceHedgeLevel(PriceDirection.Down, 57945)
            };

            strg = new DeltaHedgerStrategy(-1, 1, 1, 0, tempLevelsArr, options);





            strg.Connector = conn.SafeConnection.Connector;
            strg.Security = conn.SelectedSecurity;
            strg.Portfolio = conn.SelectedPortfolio;




            strg.ProcessStateChanged += s =>
            {
                Debug.WriteLine(s.ProcessState);
            };

            _logManager.Sources.Add(strg);

            return strg;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Security opt = _sec2;

            conn.SafeConnection.Connector.RegisterSecurity(conn.SelectedSecurity);
            conn.SafeConnection.Connector.RegisterSecurity(opt);
            //TODO подумать, где регистрировать Секьюрити ИЛИ маркетдепзы?
            //TODO подумать, может надо регать ещё портфель?
            //TODO поменять это в своей архитектуре

            Thread.Sleep(2000);

            BlackScholes bs = new BlackScholes(opt, conn.SelectedSecurity, conn.SafeConnection.Connector);

            //var vol = GreeksCalculator.CalculateImpliedVolatility(OptionTypes.Call,
            //        conn.SelectedSecurity.BestAsk.Price, opt.Strike.Value, 20, 365, opt.BestAsk.Price, 0.5m);
            //var d1 = GreeksCalculator.Calculate_d1(conn.SelectedSecurity.BestAsk.Price, opt.Strike.Value, 20, 365, vol);
            //var dev = GreeksCalculator.GreeksDistribution(d1);

            //tb1.Text = bs.Delta(DateTimeOffset.Now, dev, conn.SelectedSecurity.BestAsk.Price).ToString();
            //tb2.Text = bs.Gamma(DateTimeOffset.Now, dev, conn.SelectedSecurity.BestAsk.Price).ToString();
            //tb3.Text = bs.Vega(DateTimeOffset.Now, dev, conn.SelectedSecurity.BestAsk.Price).ToString();
            //tb4.Text = bs.Theta(DateTimeOffset.Now, dev, conn.SelectedSecurity.BestAsk.Price).ToString();
            //tb5.Text = bs.Premium(DateTimeOffset.Now, dev, conn.SelectedSecurity.BestAsk.Price).ToString();
            //tb6.Text = bs.UnderlyingAsset.Id;
            //tb7.Text = bs.DefaultDeviation.ToString(CultureInfo.InvariantCulture);

            tb1.Text = bs.Delta(DateTimeOffset.Now).ToString();
            tb2.Text = bs.Gamma(DateTimeOffset.Now).ToString();
            tb3.Text = bs.Vega(DateTimeOffset.Now).ToString();
            tb4.Text = bs.Theta(DateTimeOffset.Now).ToString();
            tb5.Text = bs.Premium(DateTimeOffset.Now).ToString();
            tb6.Text = bs.UnderlyingAsset.Id;
            tb7.Text = bs.DefaultDeviation.ToString(CultureInfo.InvariantCulture);
        }
    }
}