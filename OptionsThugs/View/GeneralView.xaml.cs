using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using DevExpress.Xpf.Editors.Internal;
using Ecng.Common;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugs.Model;
using OptionsThugs.Model.Common;
using OptionsThugs.Model.Primary;
using StockSharp.Algo.Derivatives;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;
using StockSharp.Xaml;

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
                _strategy.Stop();
            }
            else
            {
                _strategy.Start();
            }
            _testFlag = !_testFlag;
        }

        private void PrepareStrategy(object sender, RoutedEventArgs e)
        {
            decimal sign = 1;
            //_strategy = CreateNewLQStrategy(Sides.Buy, 15, conn.SelectedSecurity.PriceStep.Value * sign, 0);
            //_strategy = CreateNewMQStrategy(Sides.Sell, 20, 58455);
            //_strategy = CreateNewCondtrategy(16450, 13, MyConditionalClosePosStrategy.PriceDirection.Down, _sec2);

            List<Security> tempOptions = new List<Security>();

            conn.SafeConnection.Connector.Positions.ForEach(p =>
            {
                if (p.Security.Type == SecurityTypes.Option)
                {
                    tempOptions.Add(p.Security);
                }
            });

            _strategy = CreateNewDHStrategy(1, tempOptions);


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
    }
}