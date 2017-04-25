using System;
using System.Windows.Input;
using Ecng.Xaml;
using OptionsThugs.Model.Service;
using OptionsThugs.Model.Trading;
using OptionsThugs.View;
using StockSharp.Algo;
using StockSharp.Messages;


namespace OptionsThugs.ViewModel
{
    public class StrategyControlViewModel
    {
        private volatile bool _isStarted;
        private readonly StrategyStringCreator _creator;
        private readonly StrategyControlView _view;

        public PrimaryStrategy Strategy { get; private set; }
        public ICommand StartStopCommand { get; private set; }
        public ICommand PrepareCommand { get; private set; }

        public StrategyControlViewModel(StrategyTypes strategyType)
        {
            _isStarted = false;
            _creator = new StrategyStringCreator(strategyType);
            StartStopCommand = new DelegateCommand(StartStopExecute, CanStartStop);
            PrepareCommand = new DelegateCommand(PrepareExecute, CanPrepare);
            _view = new StrategyControlView();
            _view.Loaded += (sender, args) => { _view.testBox.Text = _creator.GetHelpDescription(); };

            _view.Show();
        }

        public void StartStopExecute(object obj)
        {

        }

        public bool CanStartStop(object obj)
        {
            if (Strategy == null) return false;

            if (_isStarted) return Strategy.ProcessState == ProcessStates.Started;

            return Strategy.ProcessState == ProcessStates.Stopped;
        }

        public void PrepareExecute(object obj)
        {
            //Read parametres from text row and validate them
            //_creator.CompleteStrategyFromString();
        }

        public bool CanPrepare(object obj)
        {
            return Strategy.Connector?.ConnectionState == ConnectionStates.Connected;
        }
    }
}
