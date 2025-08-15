using System;
using System.Windows.Input;

namespace SimTools.Helpers
{
    /// <summary>
    /// A small helper that lets WPF buttons/menus call your code.
    /// Use it when you want to bind a button to a method.
    /// - Pass the method to run (Execute).
    /// - Optionally pass a check method to enable/disable the button (CanExecute).
    /// Best for quick actions. For long-running work, use an async command class instead.
    /// </summary>

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action execute)
            : this(o => execute(), null) { }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
