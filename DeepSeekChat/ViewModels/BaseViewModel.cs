using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DeepSeekChat.ViewModels
{
    /// <summary>
    /// 通用的RelayCommand实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        private readonly Action<Exception> _errorHandler;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">是否可以执行</param>
        /// <param name="errorHandler">错误处理方法</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null, Action<Exception> errorHandler = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _errorHandler = errorHandler;
        }

        /// <summary>
        /// 是否可以执行
        /// </summary>
        public bool CanExecute(object parameter)
        {
            try
            {
                return _canExecute?.Invoke() ?? true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        public void Execute(object parameter)
        {
            try
            {
                if (CanExecute(parameter))
                {
                    _execute();
                }
            }
            catch (Exception ex)
            {
                _errorHandler?.Invoke(ex);
                throw;
            }
        }

        /// <summary>
        /// 触发CanExecuteChanged事件
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// CanExecuteChanged事件
        /// </summary>
        public event EventHandler CanExecuteChanged;
    }

    /// <summary>
    /// 支持参数的RelayCommand实现
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;
        private readonly Action<Exception> _errorHandler;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">是否可以执行</param>
        /// <param name="errorHandler">错误处理方法</param>
        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null, Action<Exception> errorHandler = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _errorHandler = errorHandler;
        }

        /// <summary>
        /// 是否可以执行
        /// </summary>
        public bool CanExecute(object parameter)
        {
            try
            {
                if (_canExecute == null)
                    return true;

                T typedParameter = default;
                if (parameter != null && parameter is T)
                {
                    typedParameter = (T)parameter;
                }
                else if (parameter == null && default(T) == null)
                {
                    // 允许空参数
                }
                else
                {
                    return false;
                }

                return _canExecute(typedParameter);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        public void Execute(object parameter)
        {
            try
            {
                if (CanExecute(parameter))
                {
                    T typedParameter = default;
                    if (parameter != null && parameter is T)
                    {
                        typedParameter = (T)parameter;
                    }
                    _execute(typedParameter);
                }
            }
            catch (Exception ex)
            {
                _errorHandler?.Invoke(ex);
                throw;
            }
        }

        /// <summary>
        /// 触发CanExecuteChanged事件
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// CanExecuteChanged事件
        /// </summary>
        public event EventHandler CanExecuteChanged;
    }

    /// <summary>
    /// 支持异步操作的RelayCommand
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private readonly Action<Exception> _errorHandler;
        private bool _isExecuting;

        /// <summary>
        /// 构造函数
        /// </summary>
        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null, Action<Exception> errorHandler = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _errorHandler = errorHandler;
        }

        /// <summary>
        /// 是否正在执行
        /// </summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 是否可以执行
        /// </summary>
        public bool CanExecute(object parameter)
        {
            return !IsExecuting && (_canExecute?.Invoke() ?? true);
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                IsExecuting = true;
                await _execute();
            }
            catch (Exception ex)
            {
                _errorHandler?.Invoke(ex);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// 触发CanExecuteChanged事件
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// CanExecuteChanged事件
        /// </summary>
        public event EventHandler CanExecuteChanged;
    }

    /// <summary>
    /// 支持异步操作和参数的RelayCommand
    /// </summary>
    public class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T, Task> _execute;
        private readonly Func<T, bool> _canExecute;
        private readonly Action<Exception> _errorHandler;
        private bool _isExecuting;

        /// <summary>
        /// 构造函数
        /// </summary>
        public AsyncRelayCommand(Func<T, Task> execute, Func<T, bool> canExecute = null, Action<Exception> errorHandler = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _errorHandler = errorHandler;
        }

        /// <summary>
        /// 是否正在执行
        /// </summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 是否可以执行
        /// </summary>
        public bool CanExecute(object parameter)
        {
            if (IsExecuting)
                return false;

            try
            {
                if (_canExecute == null)
                    return true;

                T typedParameter = default;
                if (parameter != null && parameter is T)
                {
                    typedParameter = (T)parameter;
                }
                else if (parameter == null && default(T) == null)
                {
                    // 允许空参数
                }
                else
                {
                    return false;
                }

                return _canExecute(typedParameter);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                IsExecuting = true;
                T typedParameter = default;
                if (parameter != null && parameter is T)
                {
                    typedParameter = (T)parameter;
                }
                await _execute(typedParameter);
            }
            catch (Exception ex)
            {
                _errorHandler?.Invoke(ex);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// 触发CanExecuteChanged事件
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// CanExecuteChanged事件
        /// </summary>
        public event EventHandler CanExecuteChanged;
    }

    /// <summary>
    /// MVVM ViewModel基类，实现INotifyPropertyChanged接口
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 设置属性值并通知变更
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="storage">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名</param>
        /// <returns>如果值已更改返回true</returns>
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 设置属性值并通知变更（带属性名验证）
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="storage">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名</param>
        /// <param name="onChanged">变更后回调</param>
        protected virtual void SetProperty<T>(ref T storage, T value, Action onChanged, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return;

            storage = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
        }

        /// <summary>
        /// 设置属性值并刷新所有命令
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="storage">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名</param>
        /// <returns>如果值已更改返回true</returns>
        protected virtual bool SetPropertyAndRefreshCommands<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            RefreshAllCommands();
            return true;
        }

        /// <summary>
        /// 触发属性变更事件
        /// </summary>
        /// <param name="propertyName">属性名</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 触发属性变更事件（用于多个属性）
        /// </summary>
        /// <param name="propertyNames">属性名数组</param>
        protected void OnPropertyChanged(params string[] propertyNames)
        {
            if (PropertyChanged != null)
            {
                foreach (var propertyName in propertyNames)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        /// <summary>
        /// 延迟通知属性变更（用于批量更新后）
        /// </summary>
        /// <param name="propertyName">属性名</param>
        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        /// <summary>
        /// 刷新所有命令的可执行状态
        /// </summary>
        protected virtual void RefreshAllCommands()
        {
            // 在派生类中实现具体的命令刷新逻辑
        }
    }

    /// <summary>
    /// 带有验证功能的ViewModel基类
    /// </summary>
    public abstract class ValidatableViewModel : BaseViewModel, IDataErrorInfo
    {
        private readonly Dictionary<string, string> _errors = new Dictionary<string, string>();

        /// <summary>
        /// 错误字典
        /// </summary>
        protected Dictionary<string, string> Errors => _errors;

        /// <summary>
        /// 是否有错误
        /// </summary>
        public bool HasErrors => _errors.Count > 0;

        /// <summary>
        /// 获取指定属性的错误信息
        /// </summary>
        public string this[string columnName]
        {
            get
            {
                if (_errors.ContainsKey(columnName))
                    return _errors[columnName];
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取总体错误信息
        /// </summary>
        public virtual string Error
        {
            get
            {
                if (_errors.Count == 0)
                    return string.Empty;

                return string.Join(Environment.NewLine, _errors.Values);
            }
        }

        /// <summary>
        /// 设置错误信息
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="error">错误信息</param>
        protected void SetError(string propertyName, string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                if (_errors.ContainsKey(propertyName))
                    _errors.Remove(propertyName);
            }
            else
            {
                _errors[propertyName] = error;
            }

            OnPropertyChanged(nameof(HasErrors));
            RefreshAllCommands(); // 错误状态变化时刷新命令
        }

        /// <summary>
        /// 清除所有错误
        /// </summary>
        protected void ClearErrors()
        {
            _errors.Clear();
            OnPropertyChanged(nameof(HasErrors));
            RefreshAllCommands(); // 错误状态变化时刷新命令
        }

        /// <summary>
        /// 验证属性值
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <returns>验证错误信息</returns>
        protected virtual string ValidateProperty(string propertyName)
        {
            return string.Empty;
        }

        /// <summary>
        /// 带验证的SetProperty方法
        /// </summary>
        protected virtual bool SetProperty<T>(ref T storage, T value,
            Func<T, string> validator, [CallerMemberName] string propertyName = null)
        {
            if (SetProperty(ref storage, value, propertyName))
            {
                var error = validator?.Invoke(value);
                SetError(propertyName, error);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 带验证和命令刷新的SetProperty方法
        /// </summary>
        protected virtual bool SetPropertyWithValidation<T>(ref T storage, T value,
            Func<T, string> validator, [CallerMemberName] string propertyName = null)
        {
            if (SetProperty(ref storage, value, propertyName))
            {
                var error = validator?.Invoke(value);
                SetError(propertyName, error);
                RefreshAllCommands();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 支持命令的ViewModel基类
    /// </summary>
    public abstract class CommandableViewModel : BaseViewModel
    {
        private readonly Dictionary<string, ICommand> _commands = new Dictionary<string, ICommand>();

        /// <summary>
        /// 注册命令
        /// </summary>
        /// <param name="name">命令名</param>
        /// <param name="command">命令实例</param>
        protected void RegisterCommand(string name, ICommand command)
        {
            _commands[name] = command;
        }

        /// <summary>
        /// 获取命令
        /// </summary>
        /// <param name="name">命令名</param>
        /// <returns>命令实例</returns>
        protected ICommand GetCommand(string name)
        {
            return _commands.ContainsKey(name) ? _commands[name] : null;
        }

        /// <summary>
        /// 获取所有命令
        /// </summary>
        protected IEnumerable<KeyValuePair<string, ICommand>> GetAllCommands()
        {
            return _commands;
        }

        /// <summary>
        /// 创建并注册RelayCommand
        /// </summary>
        protected ICommand CreateCommand(Action execute, Func<bool> canExecute = null, Action<Exception> errorHandler = null, string commandName = null)
        {
            var command = new RelayCommand(execute, canExecute, errorHandler);
            if (!string.IsNullOrEmpty(commandName))
            {
                RegisterCommand(commandName, command);
            }
            return command;
        }

        /// <summary>
        /// 创建并注册带参数的RelayCommand
        /// </summary>
        protected ICommand CreateCommand<T>(Action<T> execute, Func<T, bool> canExecute = null, Action<Exception> errorHandler = null, string commandName = null)
        {
            var command = new RelayCommand<T>(execute, canExecute, errorHandler);
            if (!string.IsNullOrEmpty(commandName))
            {
                RegisterCommand(commandName, command);
            }
            return command;
        }

        /// <summary>
        /// 创建并注册异步RelayCommand
        /// </summary>
        protected ICommand CreateAsyncCommand(Func<Task> execute, Func<bool> canExecute = null, Action<Exception> errorHandler = null, string commandName = null)
        {
            var command = new AsyncRelayCommand(execute, canExecute, errorHandler);
            if (!string.IsNullOrEmpty(commandName))
            {
                RegisterCommand(commandName, command);
            }
            return command;
        }

        /// <summary>
        /// 创建并注册带参数的异步RelayCommand
        /// </summary>
        protected ICommand CreateAsyncCommand<T>(Func<T, Task> execute, Func<T, bool> canExecute = null, Action<Exception> errorHandler = null, string commandName = null)
        {
            var command = new AsyncRelayCommand<T>(execute, canExecute, errorHandler);
            if (!string.IsNullOrEmpty(commandName))
            {
                RegisterCommand(commandName, command);
            }
            return command;
        }

        /// <summary>
        /// 更新所有命令的可执行状态
        /// </summary>
        protected override void RefreshAllCommands()
        {
            foreach (var command in _commands.Values)
            {
                if (command is RelayCommand relayCommand)
                {
                    relayCommand.RaiseCanExecuteChanged();
                }
                else if (command is RelayCommandEx relayCommandEx)
                {
                    relayCommandEx.RaiseCanExecuteChanged();
                }
                else if (command is RelayCommand<object> genericCommand)
                {
                    genericCommand.RaiseCanExecuteChanged();
                }
                else if (command is AsyncRelayCommand asyncCommand)
                {
                    asyncCommand.RaiseCanExecuteChanged();
                }
                else if (command is AsyncRelayCommand<object> asyncGenericCommand)
                {
                    asyncGenericCommand.RaiseCanExecuteChanged();
                }
            }
        }
    }

    /// <summary>
    /// 完整的ViewModel基类（包含所有功能）
    /// </summary>
    public abstract class ViewModelBase : CommandableViewModel, IDisposable
    {
        private bool _isBusy;
        private string _title;
        private bool _isDisposed;

        /// <summary>
        /// 是否繁忙（用于显示加载状态）
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetPropertyAndRefreshCommands(ref _isBusy, value);
        }

        /// <summary>
        /// 视图标题
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        protected ViewModelBase()
        {
            Title = GetType().Name.Replace("ViewModel", "");
        }

        /// <summary>
        /// 初始化方法（可在派生类中重写）
        /// </summary>
        public virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 卸载方法（可在派生类中重写）
        /// </summary>
        public virtual Task UninitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 显示消息
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">消息标题</param>
        public virtual Task ShowMessageAsync(string message, string title = "提示")
        {
            // 这里可以调用消息服务
            Console.WriteLine($"{title}: {message}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">消息标题</param>
        /// <returns>用户是否确认</returns>
        public virtual Task<bool> ShowConfirmationAsync(string message, string title = "确认")
        {
            // 这里可以调用对话框服务
            Console.WriteLine($"{title}: {message}");
            return Task.FromResult(true);
        }

        /// <summary>
        /// 执行安全操作（带异常处理）
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="errorMessage">错误消息前缀</param>
        protected async Task ExecuteSafeAsync(Func<Task> action, string errorMessage = "操作失败")
        {
            try
            {
                IsBusy = true;
                await action();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"{errorMessage}: {ex.Message}", "错误");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 执行安全操作（带返回值）
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="func">要执行的函数</param>
        /// <param name="errorMessage">错误消息前缀</param>
        /// <param name="defaultValue">默认值</param>
        protected async Task<T> ExecuteSafeAsync<T>(Func<Task<T>> func, string errorMessage = "操作失败", T defaultValue = default)
        {
            try
            {
                IsBusy = true;
                return await func();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"{errorMessage}: {ex.Message}", "错误");
                return defaultValue;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public virtual void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// 释放托管资源
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 释放托管资源
            }
            // 释放非托管资源
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~ViewModelBase()
        {
            Dispose(false);
        }
    }
}