using System.Windows;
using OptionsThugs.Model;
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
        private bool _test1Time = false;
        private readonly LogManager _logManager = new LogManager();
        private QuotingStrategy _strategy;

        public GeneralView()
        {
            InitializeComponent();
            connection.SetupDefaultQuikLuaConnAndDisconn();

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
            _strategy = new LimitQuotingStrategy(Sides.Sell, 10, -1)
            {
                Connector = connection.SafeConnection.Connector,
                Security = connection.SelectedSecurity,
                Portfolio = connection.SelectedPortfolio,
            };

            _logManager.Sources.Add(_strategy);
        }
    }
}