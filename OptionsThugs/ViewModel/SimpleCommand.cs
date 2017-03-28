using System;
using System.Windows.Input;

namespace OptionsThugs.ViewModel
{
    public class SimpleCommand : ICommand
    {
        private readonly Action _action;

        public SimpleCommand(Action someAction)
        {
            _action = someAction;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public event EventHandler CanExecuteChanged;

        public void Execute(object parameter)
        {
            _action?.Invoke();
        }
    }
}