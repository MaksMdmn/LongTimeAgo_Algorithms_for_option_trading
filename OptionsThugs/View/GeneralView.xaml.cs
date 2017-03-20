using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using DevExpress.Xpf.Editors.Internal;
using Ecng.Common;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugs.Common;
using OptionsThugs.Model;
using StockSharp.Algo.Derivatives;
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
        private MyQuotingStrategy _strategy;
        private OptionDeskModel _optionDeskModel;


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
            decimal sign = -1;
            _strategy = CreateNewLQStrategy(Sides.Sell, 4, conn.SelectedSecurity.PriceStep.Value * sign, 0);


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

        private MyLimitQuotingStrategy CreateNewLQStrategy(Sides side, decimal volume, decimal priceShift, decimal stopQuote)
        {
            MyLimitQuotingStrategy strg = new MyLimitQuotingStrategy(side, volume, priceShift, stopQuote)
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
    }
}