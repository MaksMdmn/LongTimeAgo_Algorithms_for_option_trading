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
        private readonly LogManager _logManager = new LogManager();
        private OptionDeskModel _optionDeskModel;
        private Security _sec2;

        private BaseStrategyTest _strategyTest;


        public GeneralView()
        {
            InitializeComponent();
            conn.SetupDefaultQuikLuaConnAndDisconn();

            _logManager.Listeners.Add(new GuiLogListener(myMon));
            _logManager.Listeners.Add(new FileLogListener("log.txt"));

            conn.SafeConnection.Connector.NewPosition += p => Debug.WriteLine(p + " #: " + p.CurrentValue);
        }

        private void StartStopClick(object sender, RoutedEventArgs e)
        {
            _strategyTest.StartStopStrategyForTest();
        }

        private void PrepareStrategy(object sender, RoutedEventArgs e)
        {
            _strategyTest = new SprTest(_logManager, conn.SafeConnection.Connector, conn.SelectedPortfolio, conn.SelectedSecurity);

            var sprTest = _strategyTest as SprTest;
            var futPos = conn.SafeConnection.Connector.GetSecurityPosition(conn.SelectedPortfolio, conn.SelectedSecurity);
            var spread = conn.SelectedSecurity.PriceStep.CheckIfValueNullThenZero() * 4;
            var lot = 5;
            var side = DealDirection.Buy;
            var minPos = -6;
            var maxPos = 8;

            sprTest.CreateNewSprStrategy(futPos, spread, lot, side, minPos, maxPos);

            #region test DHS
            //_strategyTest = new DhsTest(_logManager, conn.SafeConnection.Connector, conn.SelectedPortfolio, conn.SelectedSecurity);

            //var dhsTest = _strategyTest as DhsTest;

            //var futPos = conn.SafeConnection.Connector.GetSecurityPosition(conn.SelectedPortfolio, conn.SelectedSecurity);
            //var optPos = conn.SafeConnection.Connector.GetSecuritiesPositions(
            //    conn.SelectedPortfolio,
            //    conn.SafeConnection.Connector.GetSecuritiesWithPositions(SecurityTypes.Option));
            //var hedgeLevels = new PriceHedgeLevel[]
            //{
            //    new PriceHedgeLevel(PriceDirection.Down, 57800)
            //};
            //var deltaStep = 1;
            //var deltaBuffer = 0;

            //var minFutPos = -2;
            //var maxFutPos = 3;


            //dhsTest.CreateNewDhstrategy(futPos, optPos);
            //dhsTest.CreateNewDhstrategy(futPos, optPos, deltaStep, hedgeLevels);
            //dhsTest.CreateNewDhstrategy(futPos, optPos, deltaStep, deltaBuffer);
            //dhsTest.CreateNewDhstrategy(futPos, optPos, deltaStep, minFutPos, maxFutPos);
            #endregion

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
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {

        }
    }
}