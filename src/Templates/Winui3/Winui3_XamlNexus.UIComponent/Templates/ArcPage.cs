using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Winui3_XamlNexus.Common.Logging;
using Winui3_XamlNexus.Common.Utils.ThreadContext;
using Winui3_XamlNexus.UIComponent.Attributes;
using Winui3_XamlNexus.UIComponent.Context;
using Winui3_XamlNexus.UIComponent.Utils;

namespace Winui3_XamlNexus.UIComponent.Templates {
    public abstract class ArcPage : Page {
        public virtual ArcPageContext? ArcContext { get; set; } = null!;
        public abstract Type ArcType { get; }
        /// <summary>
        /// 页面是否被导航系统保活 / Whether the navigation system retains this page.
        /// </summary>
        public bool KeepAlive => _keepAlive.Value;
        public bool IsPreLeaved => Volatile.Read(ref _isPreLeaved) == 1;
        /// <summary>
        /// 该类型是否会存在多个实例（同类型多实例无法使用 ArcPageContext 管理器） / Whether this type allows multiple instances (same-type instances cannot use the ArcPageContext manager).
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
            OnDestroy();
        }

        #region async life-cycle hooks
        public void NavigateEnter(FrameworkPayload? payload) {
            Volatile.Write(ref _isPreLeaved, 0);
            OnEnter(payload);
        }

        public async void NavigateExit(Action? beforeLeave = null, Action? afterDestoried = null) {
            _exitCts?.Cancel(); //以此防范极其罕见的并发
            _exitCts = new CancellationTokenSource();
            var token = _exitCts.Token;

            try {
                Status = ArcPageStatus.BackgroundRunning;
                await OnPreLeaveAsync();

                if (!KeepAlive) {
                    beforeLeave?.Invoke();

                    if (token.IsCancellationRequested) return;

                    await OnLeaveAsync();
                    OnDestroy();

                    if (token.IsCancellationRequested) return;

                    afterDestoried?.Invoke();
                }
            }
            catch (OperationCanceledException) {
                // 被复活，忽略退出逻辑 / The page has been reactivated; skip leaving logic.
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ArcPage>().Error(ex);
            }
            finally {
                if (_exitCts != null && !_exitCts.IsCancellationRequested) {
                    _exitCts.Dispose();
                    _exitCts = null;
                }
            }
        }

        /// <summary>
        /// 页面进入 / The page is entering.
        /// </summary>
        protected virtual void OnEnter(FrameworkPayload? payload) {
            if (_exitCts != null) {
                _exitCts.Cancel();
                _exitCts.Dispose();
                _exitCts = null;
            }

            CrossThreadInvoker.InvokeOnUIThread(() => {
                Status = ArcPageStatus.PreActive;
                this.Translation = new System.Numerics.Vector3(0, 0, 0);
                this.Opacity = 1;
                this.IsHitTestVisible = true;
                this.Payload = payload;
            });
        }

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

            // 避免 JIT 优化代码顺序 / Prevent JIT optimization from reordering this code.
            Volatile.Write(ref _isPreLeaved, 1);
        }

        /// <summary>
        /// 页面离开 / The page is leaving.
        /// </summary>
        protected virtual Task OnLeaveAsync() {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 页面销毁 / Page cleanup.
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
        /// 为单实例 / 多实例生成统一的 ContextKey。 / Generate a consistent ContextKey for single-instance and multiple-instance pages.
        /// 多实例依赖 TimeSpan，单实例不依赖。 / Multiple instances use TimeSpan; a single instance does not.
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

        private int _isPreLeaved;
        private readonly long _timeSpan = DateTime.UtcNow.Ticks;
        private readonly Lazy<bool> _keepAlive;
        private CancellationTokenSource? _exitCts;
    }

    public enum ArcPageStatus {
        /// <summary>
        /// [不可用/未加载] / [Unavailable / unloaded]
        /// 页面不在视觉树中 (Grid.Children 不包含此页面)。 / The page is outside the visual tree (Grid.Children does not contain it).
        /// 此时页面对象可能已被销毁，或者仅存在于缓存字典中但未挂载。 / The page may have been cleaned up, or may exist only in the cache without being attached.
        /// </summary>
        Stopped,

        /// <summary>
        /// Represents a state indicating that an entity is not yet active but is prepared to become active. / 尚未激活，但已准备进入激活状态。
        /// </summary>
        PreActive,

        /// <summary>
        /// [正常运行] / [Active]
        /// 页面在视觉树中，完全可见，且可以响应用户交互。 / The page is in the visual tree, fully visible and interactive.
        /// 对应：Opacity=1, IsHitTestVisible=True, ZIndex=最高 / Corresponds to Opacity=1, IsHitTestVisible=True, and the highest ZIndex.
        /// </summary>
        Active,

        /// <summary>
        /// [被隐藏/后台运行] / [Hidden / running in the background]
        /// 页面依然在视觉树中，UI 线程仍在渲染它（动画、WebView 均在运行）， / The page remains in the visual tree, with UI rendering, animations and WebView still running,
        /// 但用户看不见，且无法点击。 / but it is invisible and cannot receive clicks.
        /// 对应：Opacity=0, IsHitTestVisible=False, ZIndex=较低 / Corresponds to Opacity=0, IsHitTestVisible=False, and a lower ZIndex.
        /// </summary>
        BackgroundRunning
    }
}
