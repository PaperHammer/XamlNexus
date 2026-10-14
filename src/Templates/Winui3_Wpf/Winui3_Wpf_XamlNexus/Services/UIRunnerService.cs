using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Common.Utils.IPC;
using Winui3_Wpf_XamlNexus.Common.Utils.PInvoke;
using Winui3_Wpf_XamlNexus.Core.Monitor;
using Winui3_Wpf_XamlNexus.Lang;
using Winui3_Wpf_XamlNexus.Services.Interfaces;
using UAC = UACHelper.UACHelper;

namespace Winui3_Wpf_XamlNexus.Services {
    public partial class UIRunnerService : IUIRunnerService {
        public event EventHandler<MessageType>? UISendCmd;

        public UIRunnerService(IMonitorManager monitorManager) {
            _monitorManager = monitorManager;

            if (UAC.IsElevated) {
                ArcLog.GetLogger<UIRunnerService>().Warn("Process is running elevated, UI may not function properly.");
            }

            if (Consts.ApplicationType.IsMSIX) {
                _workingDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\"));
                _fileName = Path.Combine(_workingDir, Consts.ModuleName.UI);
            }
            else {
                _workingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Consts.WorkingDir.UI);
                _fileName = Path.Combine(_workingDir, Consts.ModuleName.UI);
            }
        }

        public void ShowUI() {
            lock (_processSync) {
                if (_hostShutdownRequested || App.IsShuttingDown) return;
                if (_processUI != null) {
                    try {
                        ArcLog.GetLogger<UIRunnerService>().Warn("UI is already running");
                        UISendCmd?.Invoke(this, MessageType.cmd_active);
                    }
                    catch (Exception e) {
                        ArcLog.GetLogger<UIRunnerService>().Error(e);
                    }
                }
                else {
                    try {
                        _processUI = new Process {
                            StartInfo = new ProcessStartInfo {
                                FileName = _fileName,
                                RedirectStandardInput = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = false,
                                UseShellExecute = false,
                                WorkingDirectory = _workingDir,
                            },
                            EnableRaisingEvents = true
                        };
                        if (_startupArguments is not null)
                            foreach (string argument in _startupArguments)
                                _processUI.StartInfo.ArgumentList.Add(argument);
                        _processUI.Exited += Proc_UI_Exited;
                        _processUI.OutputDataReceived += Proc_OutputDataReceived;
                        _processUI.Start();
                        _startupArguments = null; // Do not replay a startup destination after UI restart.
                        _processIds[_processUI] = _processUI.Id;
                        App.Jobs.AddProcess(_processUI.Id);

                        _processUI.BeginOutputReadLine();
                        //_processUI.BeginErrorReadLine();
                    }
                    catch (Exception e) {
                        ArcLog.GetLogger<UIRunnerService>().Error(e);
                        _processUI = null;
                        if (Environment.GetCommandLineArgs().Contains("--xamlnexus-run")) throw;
                        _ = MessageBox.Show(
                            $"{LanguageManager.Instance["UIRunnerService_ExceptionGeneral"]}\nEXCEPTION:\n{e.Message}",
                            LanguageManager.Instance["Common_TextError"],
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }

        public void RestartUI() => RunRequest(RestartUIAsync());
        public void CloseUI() => RunRequest(CloseUIAsync());

        private static async void RunRequest(Task<bool> request) {
            try { await request; }
            catch (Exception exception) { ArcLog.GetLogger<UIRunnerService>().Error(exception); }
        }

        public async Task<bool> CloseUIAsync(CancellationToken cancellationToken = default) {
            Process? process;
            lock (_processSync) process = _processUI;
            if (process is null) return true;
            int processId;
            try {
                processId = process.Id;
                if (process.HasExited) return true;
            }
            catch (InvalidOperationException) { return true; }
            return await _closeRequests.RequestAsync(processId, async id => {
                try {
                    await process.StandardInput.WriteLineAsync(UIHostControl.Request(id));
                    await process.StandardInput.FlushAsync(cancellationToken);
                    if (process.HasExited) _closeRequests.ProcessExited(processId);
                }
                catch (InvalidOperationException) when (!ReferenceEquals(_processUI, process)) {
                    _closeRequests.ProcessExited(processId);
                }
                catch (IOException) when (!ReferenceEquals(_processUI, process)) { _closeRequests.ProcessExited(processId); }
            }, cancellationToken);
        }

        public async Task<bool> PrepareShutdownAsync(CancellationToken cancellationToken = default) {
            lock (_processSync) _hostShutdownRequested = true;
            bool approved = false;
            try { return approved = await CloseUIAsync(cancellationToken); }
            finally {
                if (!approved) { lock (_processSync) _hostShutdownRequested = false; }
            }
        }

        public async Task<bool> RestartUIAsync(CancellationToken cancellationToken = default) {
            if (!await _restartGate.WaitAsync(0, cancellationToken)) return false;
            if (_hostShutdownRequested) { _restartGate.Release(); return false; }
            _restartPending = true;
            try {
                if (!await CloseUIAsync(cancellationToken)) return false;
                ShowUI();
                return true;
            }
            finally { _restartPending = false; _restartGate.Release(); }
        }

        public nint GetUIHwnd() {
            if (_processUI == null) {
                return default;
            }

            return _processUI.MainWindowHandle;
        }

        public bool IsVisibleUI => _processUI != null && Native.IsWindowVisible(_processUI.MainWindowHandle);

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            //When the redirected stream is closed, a null line is sent to the event handler.
            if (!string.IsNullOrEmpty(e.Data)) {
                if (sender is Process process && _processIds.TryGetValue(process, out int id) &&
                    _closeRequests.Receive(id, e.Data)) return;
                //Ref: https://github.com/cyanfish/grpc-dotnet-namedpipes/issues/8
                ArcLog.GetLogger<UIRunnerService>().Info($"UI: {e.Data}");
            }
        }

        private void Proc_UI_Exited(object? sender, EventArgs e) {
            if (sender is not Process process || !ReferenceEquals(process, _processUI)) return;
            int exitCode = process.ExitCode;
            int processId = process.Id;
            process.Exited -= Proc_UI_Exited;
            process.OutputDataReceived -= Proc_OutputDataReceived;
            bool restarting = _restartPending;
            lock (_processSync) { if (ReferenceEquals(_processUI, process)) _processUI = null; }
            _closeRequests.ProcessExited(processId);
            _ = Task.Run(async () => {
                try { await process.WaitForExitAsync(); }
                finally { _processIds.TryRemove(process, out _); process.Dispose(); }
            });
            if (!restarting && Environment.GetCommandLineArgs().Contains("--xamlnexus-run")) {
                _ = Application.Current.Dispatcher.BeginInvoke(new Action(async () => {
                    if (await App.RequestShutDownAsync()) Environment.Exit(exitCode);
                }));
            }
        }

        #region helpers
        private void SetWindowRect(Process proc, Native.RECT rect) {
            ArgumentNullException.ThrowIfNull(proc);

            while (proc.WaitForInputIdle(-1) != true || proc.MainWindowHandle == IntPtr.Zero) {
                proc.Refresh();
            }

            Native.SetWindowPos(proc.MainWindowHandle,
                0,
                rect.Left,
                rect.Top,
                (rect.Right - rect.Left),
                (rect.Bottom - rect.Top),
                (int)Native.SetWindowPosFlags.SWP_SHOWWINDOW);

            //Monitor disconnected fallback.
            if (!IsOnScreen(proc.MainWindowHandle)) {
                Native.SetWindowPos(proc.MainWindowHandle,
                         0,
                         _monitorManager.PrimaryMonitor.Bounds.Left + 50,
                         _monitorManager.PrimaryMonitor.Bounds.Top + 50,
                         0,
                         0,
                         (int)Native.SetWindowPosFlags.SWP_NOSIZE);
            }
        }

        /// <summary>
        /// 检查窗口是否完全位于屏幕区域之外
        /// </summary>
        private bool IsOnScreen(IntPtr hwnd) {
            if (Native.GetWindowRect(hwnd, out Native.RECT winRect) != 0) {
                var rect = new Rectangle(
                    winRect.Left,
                    winRect.Top,
                    (winRect.Right - winRect.Left),
                    (winRect.Bottom - winRect.Top));
                return _monitorManager.Monitors.Any(s => s.WorkingArea.IntersectsWith(rect));
            }
            return false;
        }
        #endregion

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    try {
                        _processUI?.Kill();
                    }
                    catch { }
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly System.Collections.Concurrent.ConcurrentDictionary<Process, int> _processIds = new();
        private readonly UICloseCoordinator _closeRequests = new();
        private readonly SemaphoreSlim _restartGate = new(1, 1);
        private volatile bool _restartPending;
        private volatile bool _hostShutdownRequested;
        private readonly object _processSync = new();
        private Process? _processUI;
        private string[]? _startupArguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        private readonly IMonitorManager _monitorManager;
        private readonly string _fileName, _workingDir;
    }
}
