using System.Windows.Input;

namespace SqliteShowcase.Models.Mvvm {
    public class RelayCommand : ICommand {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null) {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object? parameter) {
            _execute();
        }

        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class RelayCommand<T> : ICommand {
        private readonly Action<T> _execute;
        private readonly Predicate<T>? _canExecute;

        public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null) {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) {
            if (_canExecute is null)
                return true;

            return TryGetParameter(parameter, out T value) && _canExecute(value);
        }

        public void Execute(object? parameter) {
            if (!TryGetParameter(parameter, out T value))
                throw new ArgumentException($"Command parameter must be assignable to {typeof(T).FullName}.", nameof(parameter));

            _execute(value);
        }

        private static bool TryGetParameter(object? parameter, out T value) {
            if (parameter is T typedValue) {
                value = typedValue;
                return true;
            }

            if (parameter is null && default(T) is null) {
                value = default!;
                return true;
            }

            value = default!;
            return false;
        }

        public event EventHandler? CanExecuteChanged {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
