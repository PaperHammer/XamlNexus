using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Common.Utils.DI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Winui3_Wpf_XamlNexus.Common.Utils.ThreadContext;
using Winui3_Wpf_XamlNexus.UIComponent.Attributes;
using Winui3_Wpf_XamlNexus.UIComponent.Activation;
using Winui3_Wpf_XamlNexus.UIComponent.Navigation;
using Winui3_Wpf_XamlNexus.UIComponent.Templates;

namespace Winui3_Wpf_XamlNexus.UIComponent.Utils.Extensions {
    public static class ArcNavigationContentViewExtension {
        public static void ArcNavigate(this ArcNavigationContentView navView, Type targetPageType,
            FrameworkPayload? payload = null, ArcNavigationOptions? options = null) {
            CrossThreadInvoker.InvokeOnUIThread(async () => {
                try { await navView.ArcNavigateAsync(targetPageType, payload, options); }
                catch (Exception ex) { ArcLog.GetLogger<ArcNavigationContentView>().Error(ex); }
            });
        }

        public static async Task<bool> ArcNavigateAsync(this ArcNavigationContentView navView, Type targetPageType,
            FrameworkPayload? payload = null, ArcNavigationOptions? options = null, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(navView);
            ArgumentNullException.ThrowIfNull(targetPageType);
            if (!typeof(ArcPage).IsAssignableFrom(targetPageType) || targetPageType.IsAbstract)
                throw new ArgumentException("The target must be a concrete ArcPage.", nameof(targetPageType));
            if (options is not null && options.Transition != ArcNavigationTransition.Fade)
                throw new ArgumentOutOfRangeException(nameof(options), options.Transition, "Unsupported navigation transition.");
            bool accepted = false;
            await navView.DispatcherQueue.EnqueueOrInvokeAsync(async () => {
                accepted = await navView.LeaveRequest.TryRunAsync(async () => {
                    cancellationToken.ThrowIfCancellationRequested();
                    var oldPage = GetActivePage(navView.PageMap);
                    if (oldPage?.ArcType == targetPageType && payload is null) return true;
                    if (oldPage != null && !await oldPage.CanLeaveAsync(ArcLeaveReason.Navigation, cancellationToken)) return false;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!ReferenceEquals(oldPage, GetActivePage(navView.PageMap))) return false;
                    // Construct and activate the target only after leave approval.
                    bool created = false;
                    var newPage = oldPage?.ArcType == targetPageType ? oldPage
                        : GetBackgroundPage(navView.PageMap, targetPageType);
                    newPage ??= ResolvePageInstance(targetPageType, out created);
                    NavigateRoot(navView, oldPage, newPage, payload, options, created, cancellationToken);
                    return true;
                });
            });
            return accepted;
        }

        public static async Task<bool> ArcCanLeaveAsync(this ArcNavigationContentView navView, ArcLeaveReason reason,
            CancellationToken cancellationToken = default) {
            bool accepted = false;
            await navView.DispatcherQueue.EnqueueOrInvokeAsync(async () => {
                accepted = await navView.LeaveRequest.TryRunAsync(async () => {
                    var pages = reason == ArcLeaveReason.WindowClose
                        ? navView.PageMap.Values.Where(page => page.Status != ArcPageStatus.Stopped).Distinct().ToArray()
                        : new[] { GetActivePage(navView.PageMap) }.OfType<ArcPage>().ToArray();
                    foreach (var page in pages) {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!await page.CanLeaveAsync(reason, cancellationToken)) return false;
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    return true;
                });
            });
            return accepted;
        }

        private static void NavigateRoot(ArcNavigationContentView navView, ArcPage? oldPage,
            ArcPage newPage, FrameworkPayload? payload, ArcNavigationOptions? options,
            bool created, CancellationToken cancellationToken) {
            var state = newPage.CaptureNavigationState();
            var incomingValues = payload is null ? null : new Dictionary<string, object?>(payload.GetRawData());
            bool mapped = navView.PageMap.ContainsKey(newPage.ArcType);
            bool attached = navView.ContentGrid.Children.Contains(newPage);
            Storyboard? animation = null;
            try {
                // Preserve payload identity on success; restore its dictionary if entry fails.
                bool incomingActivation = payload?.ContainsKey(BusinessActivationRequest.PayloadKey) == true;
                var nextPayload = payload.Merge(newPage.Payload, overwrite: false);
                if (payload is not null && !incomingActivation)
                    nextPayload.Set(BusinessActivationRequest.PayloadKey, null);
                if (options is not null) newPage.Opacity = 0;
                if (!mapped) navView.PageMap.Add(newPage.ArcType, newPage);
                if (!attached) navView.ContentGrid.Children.Add(newPage);
                newPage.EnterForNavigation(nextPayload);
                cancellationToken.ThrowIfCancellationRequested();
                if (options is not null) {
                    animation = CreateEnterAnimation(newPage, options.Transition);
                    animation.Begin();
                }
                newPage.CommitNavigationEntry();
            }
            catch {
                RollbackStep(() => animation?.Stop());
                if (!mapped) RollbackStep(() => {
                    if (navView.PageMap.TryGetValue(newPage.ArcType, out var entry) && ReferenceEquals(entry, newPage))
                        navView.PageMap.Remove(newPage.ArcType);
                });
                if (!attached) RollbackStep(() => {
                    if (navView.ContentGrid.Children.Contains(newPage)) navView.ContentGrid.Children.Remove(newPage);
                });
                if (created) RollbackStep(newPage.DiscardFailedNavigation);
                else RollbackStep(() => newPage.RestoreNavigationState(state));
                if (payload is not null && incomingValues is not null)
                    RollbackStep(() => payload.RestoreData(incomingValues));
                throw; // Keep the entry failure, even if cleanup also failed.
            }
            // Departure is committed only after all entry preparation succeeds.
            HandlePageStatus(navView, oldPage, newPage);
        }

        private static void RollbackStep(Action action) {
            try { action(); }
            catch (Exception exception) {
                ArcLog.GetLogger<ArcNavigationContentView>().Error("Navigation rollback cleanup failed", exception);
            }
        }

        private static void HandlePageStatus(ArcNavigationContentView navView, ArcPage? oldPage, ArcPage newPage) {
            // Activation belongs to this navigation, not to a previous page's delayed exit.
            newPage.SetActiveStatus();
            if (oldPage == null || ReferenceEquals(oldPage, newPage)) return;

            oldPage.NavigateExit(
                beforeLeave: () => {
                    // 如果在等待期间 oldPage 被复活（变为 PreActive 或 Active），不能移除 / Do not remove oldPage if it was reactivated as PreActive or Active while awaiting.
                    if (oldPage.Status == ArcPageStatus.Active || oldPage.Status == ArcPageStatus.PreActive) {
                        return;
                    }

                    navView.PageMap.Remove(oldPage.ArcType);
                    CrossThreadInvoker.InvokeOnUIThread(() => {
                        if (navView.ContentGrid.Children.Contains(oldPage)) {
                            navView.ContentGrid.Children.Remove(oldPage);
                        }
                    });
                });
        }

        private static Storyboard CreateEnterAnimation(UIElement newPage, ArcNavigationTransition transition) {
            var sb = new Storyboard();

            switch (transition) {
                case ArcNavigationTransition.Fade:
                    var fade = new DoubleAnimation {
                        From = 0,
                        To = 1,
                        Duration = FadeAnimationCache.Duration,
                        EasingFunction = FadeAnimationCache.Ease
                    };
                    Storyboard.SetTarget(fade, newPage);
                    Storyboard.SetTargetProperty(fade, "Opacity");
                    sb.Children.Add(fade);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(transition), transition, "Unsupported navigation transition.");
            }

            return sb;
        }

        private static ArcPage ResolvePageInstance(Type type, out bool created) {
            created = false;
            bool keepAlive = type.GetCustomAttribute<KeepAliveAttribute>()?.Value == true;

            if (keepAlive) {
                var ctx = ArcPageContextManager.GetContext(type);
                if (ctx?.PageInstance is ArcPage page)
                    return page;
            }

            created = true;
            return AppObjectFactory.Create(type) as ArcPage
                   ?? throw new Exception($"Cannot create page {type.Name}");
        }

        private static ArcPage? GetActivePage(Dictionary<Type, ArcPage> map) {
            foreach (var kvp in map) {
                // 在快速切换时，上一个页面可能还处于 PreActive（动画中），它也应该被视为 oldPage / During rapid navigation, the previous page may still be PreActive (animating); treat it as oldPage too.
                if (kvp.Value.Status == ArcPageStatus.Active || kvp.Value.Status == ArcPageStatus.PreActive) {
                    return kvp.Value;
                }
            }

            return null;
        }

        private static ArcPage? GetBackgroundPage(Dictionary<Type, ArcPage> map, Type targetPageType) {
            foreach (var kvp in map) {
                if (kvp.Value.ArcType == targetPageType && kvp.Value.Status == ArcPageStatus.BackgroundRunning) {
                    return kvp.Value;
                }
            }

            return null;
        }
    }

    public enum ArcNavigationTransition {
        Fade,
        // Slide, DrillIn ... / 滑动、钻入等过渡效果。
    }

    public record ArcNavigationOptions(ArcNavigationTransition Transition);

    static class FadeAnimationCache {
        public const int DurationMs = 250;

        public static readonly Duration Duration =
            new(TimeSpan.FromMilliseconds(DurationMs));

        public static readonly ExponentialEase Ease =
            new() { EasingMode = EasingMode.EaseOut };

        public static DoubleAnimation CreateFadeIn() {
            return new DoubleAnimation {
                From = 0,
                To = 1,
                Duration = Duration,
                EasingFunction = Ease
            };
        }

        public static DoubleAnimation CreateFadeOut() {
            return new DoubleAnimation {
                From = 1,
                To = 0,
                Duration = Duration,
                EasingFunction = Ease
            };
        }
    }    
}
