using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Common.Utils.ThreadContext;
using Winui3_Wpf_XamlNexus.UIComponent.Attributes;
using Winui3_Wpf_XamlNexus.UIComponent.Navigation;
using Winui3_Wpf_XamlNexus.UIComponent.Context;
using Winui3_Wpf_XamlNexus.UIComponent.Utils;

namespace Winui3_Wpf_XamlNexus.UIComponent.Templates {
    public abstract class ArcPage : Page {
        public virtual ArcPageContext? ArcContext { get; set; } = null!;
        public abstract Type ArcType { get; }
        /// <summary>
        /// 页面是否被导航系统保活
        /// </summary>
        public bool KeepAlive => _keepAlive.Value;
        public bool IsPreLeaved => Volatile.Read(ref _isPreLeaved) == 1;
        /// <summary>
        /// 该类型是否会存在多个实例（同类型多实例无法使用 ArcPageContext 管理器）
        /// </summary>
        protected virtual bool IsMultiInstance => false;
        protected ArcPageContextKey ContextKey => GetContextKey();
        public FrameworkPayload? Payload { get; protected set; }
        public ArcPageStatus Status { get; protected set; }

        protected ArcPage() {
            ArcContext = new ArcPageContext(this);
            this.Loaded += ArcPage_Loaded;
            this.Unloaded += ArcPage_Unloaded;
            _keepAlive = new Lazy<bool>(() => ArcType.GetCustomAttribute<KeepAliveAttribute>()?.Value == true);
        }

        private void ArcPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            EnsureContextRegistered();
        }

        protected void ArcPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            DestroyOnce();
        }

        /// <summary>Called before navigation or window close changes page state. Return false to retain the page.</summary>
        public virtual Task<bool> CanLeaveAsync(ArcLeaveReason reason, CancellationToken cancellationToken = default) => Task.FromResult(true);

        #region async life-cycle hooks
        public void NavigateEnter(FrameworkPayload? payload) {
            _exitCts?.Cancel();
            Volatile.Write(ref _isPreLeaved, 0);
            OnEnter(payload);
        }

        // Compatibility entry point for existing navigation callers.
        public async void NavigateExit(Action? beforeLeave = null, Action? afterDestoried = null) {
            try {
                await NavigateExitAsync(beforeLeave, afterDestoried);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ArcPage>().Error(ex);
            }
        }

        /// <summary>
        /// Await page exit on the UI thread. Returns false when re-entry or a newer exit supersedes it.
        /// Hook failures propagate to the caller; retained pages remain in the visual tree.
        /// </summary>
        public async Task<bool> NavigateExitAsync(Action? beforeLeave = null, Action? afterDestoried = null) {
            _exitCts?.Cancel();
            var exit = new CancellationTokenSource();
            _exitCts = exit;
            var token = exit.Token;
            try {
                if (Volatile.Read(ref _isDestroyed) != 0) return false;
                Status = ArcPageStatus.BackgroundRunning;
                await OnPreLeaveAsync(token).WaitAsync(token);
                token.ThrowIfCancellationRequested();
                if (Volatile.Read(ref _isDestroyed) != 0) return false;
                Volatile.Write(ref _isPreLeaved, 1);

                if (!KeepAlive) {
                    // Leave hooks still have their context and data before visual-tree removal.
                    await OnLeaveAsync(token).WaitAsync(token);
                    token.ThrowIfCancellationRequested();
                    if (Volatile.Read(ref _isDestroyed) != 0) return false;
                    beforeLeave?.Invoke();
                    DestroyOnce();
                    afterDestoried?.Invoke();
                }
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                return false;
            }
            finally {
                if (ReferenceEquals(_exitCts, exit)) _exitCts = null;
                exit.Dispose();
            }
        }

        /// <summary>
        /// 页面进入
        /// </summary>
        protected virtual void OnEnter(FrameworkPayload? payload) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                Status = ArcPageStatus.PreActive;
                this.Translation = new System.Numerics.Vector3(0, 0, 0);
                this.Opacity = 1;
                this.IsHitTestVisible = true;
                this.Payload = payload;
                if (ArcContext != null) ArcContext.IsActive = true;
            });
        }

        /// <summary>Override to propagate exit cancellation into page operations; legacy overrides remain supported.</summary>
        protected virtual Task OnPreLeaveAsync(CancellationToken cancellationToken) => OnPreLeaveAsync();

        protected virtual async Task OnPreLeaveAsync() {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                this.Opacity = 0.0;
                this.IsHitTestVisible = false;
                this.Translation = new System.Numerics.Vector3(0, 10000, 0);
                Canvas.SetZIndex(this, 0);
            });

            if (ArcContext != null) {
                ArcContext.IsActive = false;
                await ArcContext.KeepAliveBlocking.WaitAsync();
            }
        }

        /// <summary>
        /// 页面离开
        /// </summary>
        protected virtual Task OnLeaveAsync(CancellationToken cancellationToken) => OnLeaveAsync();

        protected virtual Task OnLeaveAsync() {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 页面销毁
        /// </summary>
        protected virtual void OnDestroy() {
            Status = ArcPageStatus.Stopped;
            Payload = null;
            DataContext = null;
            UnregisterContext();
            ArcContext = null;
        }
        #endregion

        #region utils
        private void EnsureContextRegistered() {
            if (ArcContext is null)
                return;

            ArcContext.IsActive = true;

            var key = GetContextKey();
            if (!ArcPageContextManager.HasContext(key)) {
                ArcPageContextManager.RegisterContext(key, ArcContext);
            }
        }

        private void UnregisterContext() {
            if (ArcContext is null)
                return;

            ArcContext.IsActive = false;

            var key = GetContextKey();
            ArcPageContextManager.UnregisterContext(key);
        }

        /// <summary>
        /// 为单实例 / 多实例生成统一的 ContextKey。
        /// 多实例依赖 TimeSpan，单实例不依赖。
        /// </summary>
        private ArcPageContextKey GetContextKey() {
            return IsMultiInstance
                ? new ArcPageContextKey(ArcType, _timeSpan)
                : new ArcPageContextKey(ArcType);
        }

        internal void SetActiveStatus() {
            Status = ArcPageStatus.Active;
        }
        #endregion

        private void DestroyOnce() {
            if (Interlocked.Exchange(ref _isDestroyed, 1) == 0) OnDestroy();
        }

        private int _isDestroyed;
        private int _isPreLeaved;
        private readonly long _timeSpan = DateTime.UtcNow.Ticks;
        private readonly Lazy<bool> _keepAlive;
        private CancellationTokenSource? _exitCts;
    }

    public enum ArcPageStatus {
        /// <summary>
        /// [不可用/未加载]
        /// 页面不在视觉树中 (Grid.Children 不包含此页面)。
        /// 此时页面对象可能已被销毁，或者仅存在于缓存字典中但未挂载。
        /// </summary>
        Stopped,

        /// <summary>
        /// Represents a state indicating that an entity is not yet active but is prepared to become active.
        /// </summary>
        PreActive,

        /// <summary>
        /// [正常运行]
        /// 页面在视觉树中，完全可见，且可以响应用户交互。
        /// 对应：Opacity=1, IsHitTestVisible=True, ZIndex=最高
        /// </summary>
        Active,

        /// <summary>
        /// [被隐藏/后台运行]
        /// 页面依然在视觉树中，UI 线程仍在渲染它（动画、WebView 均在运行），
        /// 但用户看不见，且无法点击。
        /// 对应：Opacity=0, IsHitTestVisible=False, ZIndex=较低
        /// </summary>
        BackgroundRunning
    }
}
