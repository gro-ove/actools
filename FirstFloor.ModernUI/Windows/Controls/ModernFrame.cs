using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;
using FirstFloor.ModernUI.Windows.Navigation;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ModernFrame : ContentControl {
        public static readonly DependencyProperty TransitionNameProperty = DependencyProperty.Register("TransitionName", typeof(string),
            typeof(ModernFrame));

        public static readonly DependencyProperty KeepAliveProperty = DependencyProperty.RegisterAttached("KeepAlive", typeof(bool?),
            typeof(ModernFrame), new PropertyMetadata(null));
        public static readonly DependencyProperty KeepContentAliveProperty = DependencyProperty.RegisterAttached("KeepContentAlive", typeof(bool),
            typeof(ModernFrame), new FrameworkPropertyMetadata(false,
                    FrameworkPropertyMetadataOptions.Inherits, OnKeepContentAliveChanged));
        public static readonly DependencyProperty ContentLoaderProperty = DependencyProperty.Register("ContentLoader", typeof(IContentLoader),
            typeof(ModernFrame), new PropertyMetadata(new DefaultContentLoader(), OnContentLoaderChanged));
        private static readonly DependencyPropertyKey IsLoadingContentPropertyKey = DependencyProperty.RegisterReadOnly("IsLoadingContent", typeof(bool),
            typeof(ModernFrame), new PropertyMetadata(false));
        public static readonly DependencyProperty IsLoadingContentProperty = IsLoadingContentPropertyKey.DependencyProperty;
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(Uri),
            typeof(ModernFrame), new PropertyMetadata(OnSourceChanged));

        public event EventHandler<FragmentNavigationEventArgs> FragmentNavigation;
        public event EventHandler<NavigatingCancelEventArgs> Navigating;
        public event EventHandler<NavigationEventArgs> Navigated;
        public event EventHandler<NavigationFailedEventArgs> NavigationFailed;

        private readonly Stack<Uri> _history = new Stack<Uri>(), _future = new Stack<Uri>();
        private readonly Dictionary<Uri, object> _contentCache = new Dictionary<Uri, object>();
        private readonly List<WeakReference<ModernFrame>> _childFrames = new List<WeakReference<ModernFrame>>();

        private CancellationTokenSource _tokenSource;
        private bool _isNavigatingHistory, _isNavigatingFuture;
        private bool _isResetSource;

        public ModernFrame() {
            DefaultStyleKey = typeof(ModernFrame);

            CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseBack, OnBrowseBack, OnCanBrowseBack));
            CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseForward, OnBrowseForward, OnCanBrowseForward));
            CommandBindings.Add(new CommandBinding(NavigationCommands.Refresh, OnRefresh, OnCanRefresh));
            CommandBindings.Add(new CommandBinding(NavigationCommands.GoToPage, OnGoToPage, OnCanGoToPage));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopy, OnCanCopy));

            Loaded += OnLoaded;
        }

        public static bool OptionDisableTransitionAnimation = false;
        public static string OptionTransitionName = "ModernUITransition";

        public override void OnApplyTemplate() {
            if (TransitionName == null) {
                TransitionName = OptionDisableTransitionAnimation ? "Normal" : OptionTransitionName;
            }

            base.OnApplyTemplate();
        }

        private static void OnKeepContentAliveChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            (o as ModernFrame)?.OnKeepContentAliveChanged((bool)e.NewValue);
        }

        private void OnKeepContentAliveChanged(bool keepAlive) {
            // clear content cache
            _contentCache.Clear();
        }

        private static void OnContentLoaderChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue == null) {
                // null values for content loader not allowed
                throw new ArgumentNullException("ContentLoader");
            }
        }

        private static void OnSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernFrame)o).OnSourceChanged((Uri)e.OldValue, (Uri)e.NewValue);
        }

        private void OnSourceChanged(Uri oldValue, Uri newValue) {
            // if resetting source or old source equals new, don’t do anything
            if (_isResetSource || newValue != null && newValue.Equals(oldValue)) {
                return;
            }

            // handle fragment navigation
            string newFragment;
            var oldValueNoFragment = NavigationHelper.RemoveFragment(oldValue);
            var newValueNoFragment = NavigationHelper.RemoveFragment(newValue, out newFragment);

            if (newValueNoFragment != null && newValueNoFragment.Equals(oldValueNoFragment)) {
                // fragment navigation
                var args = new FragmentNavigationEventArgs {
                    Fragment = newFragment
                };

                OnFragmentNavigation(Content as IContent, args);
            } else {
                var navType = _isNavigatingHistory ? NavigationType.Back :
                    _isNavigatingFuture ? NavigationType.Forward :
                    NavigationType.New;

                // only invoke CanNavigate for new navigation
                if (!_isNavigatingHistory && !_isNavigatingFuture && !CanNavigate(oldValue, newValue, navType)) {
                    return;
                }

                Navigate(oldValue, newValue, navType);
            }
        }

        private bool CanNavigate(Uri oldValue, Uri newValue, NavigationType navigationType) {
            var cancelArgs = new NavigatingCancelEventArgs {
                Frame = this,
                Source = newValue,
                IsParentFrameNavigating = true,
                NavigationType = navigationType,
                Cancel = false
            };
            OnNavigating(Content as IContent, cancelArgs);

            // check if navigation cancelled
            if (!cancelArgs.Cancel) return true;

            Debug.WriteLine("Cancelled navigation from '{0}' to '{1}'", oldValue, newValue);

            if (Source != oldValue) {
                // enqueue the operation to reset the source back to the old value
                Dispatcher.BeginInvoke((Action)(() => {
                    _isResetSource = true;
                    SetCurrentValue(SourceProperty, oldValue);
                    _isResetSource = false;
                }));
            }
            return false;
        }

        public static bool OptionUseSyncNavigation = false;

        private void Navigate(Uri oldValue, Uri newValue, NavigationType navigationType) {
            if (OptionUseSyncNavigation) {
                NavigateSync(oldValue, newValue, navigationType);
            } else {
                NavigateAsync(oldValue, newValue, navigationType);
            }
        }

        private void NavigateSync(Uri oldValue, Uri newValue, NavigationType navigationType) {
            Debug.WriteLine("Navigating from '{0}' to '{1}'", oldValue, newValue);

            // set IsLoadingContent state
            SetValue(IsLoadingContentPropertyKey, true);

            // cancel previous load content task (if any)
            // note: no need for thread synchronization, this code always executes on the UI thread
            if (_tokenSource != null) {
                _tokenSource.Cancel();
                _tokenSource = null;
            }

            // push previous source onto the history stack (only for new navigation types)
            if (oldValue != null) {
                switch (navigationType) {
                    case NavigationType.New:
                        _history.Push(oldValue);
                        _future.Clear();
                        break;
                    case NavigationType.Back:
                        _future.Push(oldValue);
                        break;
                    case NavigationType.Forward:
                        _history.Push(oldValue);
                        break;
                }
            }

            object newContent = null;

            if (newValue != null) {
                // content is cached on uri without fragment
                var newValueNoFragment = NavigationHelper.RemoveFragment(newValue);

                if (navigationType == NavigationType.Refresh || !_contentCache.TryGetValue(newValueNoFragment, out newContent)) {
#if !DEBUG
                    try {
#endif
                    newContent = ContentLoader.LoadContent(newValue);

                    if (ShouldKeepContentAlive(newContent)) {
                        // keep the new content in memory
                        Debug.WriteLine("KEEP CONTENT ALIVE: " + newValue);
                        _contentCache[newValueNoFragment] = newContent;
                    }

                    SetContent(newValue, navigationType, newContent, false);
#if !DEBUG
                    } catch (Exception e) {
                        // raise failed event
                        var failedArgs = new NavigationFailedEventArgs {
                            Frame = this,
                            Source = newValue,
                            Error = e?.InnerException,
                            Handled = false
                        };

                        OnNavigationFailed(failedArgs);

                        // if not handled, show error as content
                        newContent = failedArgs.Handled ? null : failedArgs.Error;

                        SetContent(newValue, navigationType, newContent, true);
                    }
#endif
                }
            }

            // newValue is null or newContent was found in the cache
            SetContent(newValue, navigationType, newContent, false);
        }

        private void NavigateAsync(Uri oldValue, Uri newValue, NavigationType navigationType) {
            Debug.WriteLine("Navigating from '{0}' to '{1}'", oldValue, newValue);

            // set IsLoadingContent state
            SetValue(IsLoadingContentPropertyKey, true);

            // cancel previous load content task (if any)
            // note: no need for thread synchronization, this code always executes on the UI thread
            if (_tokenSource != null) {
                _tokenSource.Cancel();
                _tokenSource = null;
            }

            // push previous source onto the history stack (only for new navigation types)
            if (oldValue != null) {
                switch (navigationType) {
                    case NavigationType.New:
                        _history.Push(oldValue);
                        _future.Clear();
                        break;
                    case NavigationType.Back:
                        _future.Push(oldValue);
                        break;
                    case NavigationType.Forward:
                        _history.Push(oldValue);
                        break;
                }
            }

            object newContent = null;

            if (newValue != null) {
                // content is cached on uri without fragment
                var newValueNoFragment = NavigationHelper.RemoveFragment(newValue);

                if (navigationType == NavigationType.Refresh || !_contentCache.TryGetValue(newValueNoFragment, out newContent)) {
                    var localTokenSource = new CancellationTokenSource();
                    _tokenSource = localTokenSource;
                    // load the content (asynchronous!)
                    var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
                    var task = ContentLoader.LoadContentAsync(newValue, _tokenSource.Token);

                    task.ContinueWith(t => {
                        try {
                            if (t.IsCanceled || localTokenSource.IsCancellationRequested) {
                                Debug.WriteLine("Cancelled navigation to '{0}'", newValue);
                            } else if (t.IsFaulted) {
                                Logging.Warning("[FRAME] Navigation failed: " + t.Exception?.InnerException);

                                // raise failed event
                                var failedArgs = new NavigationFailedEventArgs {
                                    Frame = this,
                                    Source = newValue,
                                    Error = t.Exception?.InnerException,
                                    Handled = false
                                };

                                OnNavigationFailed(failedArgs);

                                // if not handled, show error as content
                                newContent = failedArgs.Handled ? null : failedArgs.Error;

                                SetContent(newValue, navigationType, newContent, true);
                            } else {
                                newContent = t.Result;
                                if (ShouldKeepContentAlive(newContent)) {
                                    // keep the new content in memory
                                    Debug.WriteLine("KEEP CONTENT ALIVE: " + newValue);
                                    _contentCache[newValueNoFragment] = newContent;
                                }

                                SetContent(newValue, navigationType, newContent, false);
                            }
                        } finally {
                            // clear global tokenSource to avoid a Cancel on a disposed object
                            if (_tokenSource == localTokenSource) {
                                _tokenSource = null;
                            }

                            // and dispose of the local tokensource
                            localTokenSource.Dispose();
                        }
                    }, scheduler);
                    return;
                }
            }

            // newValue is null or newContent was found in the cache
            SetContent(newValue, navigationType, newContent, false);
        }

        private void SetContent(Uri newSource, NavigationType navigationType, object newContent, bool contentIsError) {
            var oldContent = Content as IContent;

            // assign content
            Content = newContent;

            // do not raise navigated event when error
            if (!contentIsError) {
                var args = new NavigationEventArgs {
                    Frame = this,
                    Source = newSource,
                    Content = newContent,
                    NavigationType = navigationType
                };

                OnNavigated(oldContent, newContent as IContent, args);
            }

            // set IsLoadingContent to false
            SetValue(IsLoadingContentPropertyKey, false);

            if (contentIsError) return;

            // and raise optional fragment navigation events
            string fragment;
            NavigationHelper.RemoveFragment(newSource, out fragment);
            if (fragment == null) return;

            // fragment navigation
            var fragmentArgs = new FragmentNavigationEventArgs {
                Fragment = fragment
            };

            OnFragmentNavigation(newContent as IContent, fragmentArgs);
        }

        private IEnumerable<ModernFrame> GetChildFrames() {
            var refs = _childFrames.ToArray();
            foreach (var r in refs) {
                var valid = false;
                ModernFrame frame;

                if (r.TryGetTarget(out frame)) {
                    // check if frame is still an actual child (not the case when child is removed, but not yet garbage collected)
                    if (ReferenceEquals(NavigationHelper.FindFrame(null, frame), this)) {
                        valid = true;
                        yield return frame;
                    }
                }

                if (!valid) {
                    _childFrames.Remove(r);
                }
            }
        }

        private void OnFragmentNavigation(IContent content, FragmentNavigationEventArgs e) {
            // invoke optional IContent.OnFragmentNavigation
            content?.OnFragmentNavigation(e);

            // raise the FragmentNavigation event
            FragmentNavigation?.Invoke(this, e);
        }

        private void OnNavigating(IContent content, NavigatingCancelEventArgs e) {
            // first invoke child frame navigation events
            foreach (var f in GetChildFrames()) {
                f.OnNavigating(f.Content as IContent, e);
            }

            e.IsParentFrameNavigating = !ReferenceEquals(e.Frame, this);

            // invoke IContent.OnNavigating (only if content implements IContent)
            content?.OnNavigatingFrom(e);

            // raise the Navigating event
            Navigating?.Invoke(this, e);
        }

        private void OnNavigated(IContent oldContent, IContent newContent, NavigationEventArgs e) {
            // invoke IContent.OnNavigatedFrom and OnNavigatedTo
            oldContent?.OnNavigatedFrom(e);
            newContent?.OnNavigatedTo(e);

            // raise the Navigated event
            Navigated?.Invoke(this, e);
        }

        private void OnNavigationFailed(NavigationFailedEventArgs e) {
            NavigationFailed?.Invoke(this, e);
        }

        private bool HandleRoutedEvent(CanExecuteRoutedEventArgs args) {
            var originalSource = args.OriginalSource as DependencyObject;
            return originalSource != null &&
                ReferenceEquals(originalSource.AncestorsAndSelf().OfType<ModernFrame>().FirstOrDefault(), this);
        }

        private void OnCanBrowseForward(object sender, CanExecuteRoutedEventArgs e) {
            if (HandleRoutedEvent(e)) {
                e.CanExecute = _future.Count > 0;
            }
        }

        private void OnCanBrowseBack(object sender, CanExecuteRoutedEventArgs e) {
            // only enable browse back for source frame, do not bubble
            if (HandleRoutedEvent(e)) {
                if (_history.Count > 0) {
                    e.CanExecute = true;
                } else {
                    var ts = TopSource;
                    e.CanExecute = ts != null && ts != Source;
                }
            }
        }

        private void OnCanCopy(object sender, CanExecuteRoutedEventArgs e) {
            if (HandleRoutedEvent(e)) {
                e.CanExecute = Content != null;
            }
        }

        private void OnCanGoToPage(object sender, CanExecuteRoutedEventArgs e) {
            if (HandleRoutedEvent(e)) {
                e.CanExecute = e.Parameter is string || e.Parameter is Uri;
            }
        }

        private void OnCanRefresh(object sender, CanExecuteRoutedEventArgs e) {
            if (HandleRoutedEvent(e)) {
                e.CanExecute = Source != null;
            }
        }

        private void OnBrowseBack(object target, ExecutedRoutedEventArgs e) {
            if (!_history.Any() && TopSource == null) return;

            var oldValue = Source;
            var newValue = _history.Count > 0 ? _history.Peek() : TopSource;
            // do not remove just yet, navigation may be cancelled

            if (!CanNavigate(oldValue, newValue, NavigationType.Back)) return;
            _isNavigatingHistory = true;
            SetCurrentValue(SourceProperty, _history.Count > 0 ? _history.Pop() : TopSource);
            _isNavigatingHistory = false;
        }

        private void OnBrowseForward(object target, ExecutedRoutedEventArgs e) {
            if (!_history.Any()) return;

            var oldValue = Source;
            var newValue = _future.Peek();
            // do not remove just yet, navigation may be cancelled

            if (!CanNavigate(oldValue, newValue, NavigationType.Forward)) return;
            _isNavigatingFuture = true;
            SetCurrentValue(SourceProperty, _future.Pop());
            _isNavigatingFuture = false;
        }

        private void OnGoToPage(object target, ExecutedRoutedEventArgs e) {
            var newValue = NavigationHelper.ToUri(e.Parameter);
            SetCurrentValue(SourceProperty, newValue);
        }

        private void OnRefresh(object target, ExecutedRoutedEventArgs e) {
            if (CanNavigate(Source, Source, NavigationType.Refresh)) {
                Navigate(Source, Source, NavigationType.Refresh);
            }
        }

        private void OnCopy(object target, ExecutedRoutedEventArgs e) {
            // copies the string representation of the current content to the clipboard
            Clipboard.SetText(Content.ToString());
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            NavigationHelper.FindFrame(NavigationHelper.FrameParent, this)?.RegisterChildFrame(this);
        }

        private void RegisterChildFrame(ModernFrame frame) {
            // do not register existing frame
            if (GetChildFrames().Contains(frame)) return;
            _childFrames.Add(new WeakReference<ModernFrame>(frame));
        }

        private bool ShouldKeepContentAlive(object content) {
            var o = content as DependencyObject;
            if (o == null) return KeepContentAlive;
            var result = GetKeepAlive(o);

            // if a value exists for given content, use it
            return result ?? KeepContentAlive;
            // otherwise let the ModernFrame decide
        }

        public static bool? GetKeepAlive(DependencyObject o) {
            if (o == null) {
                throw new ArgumentNullException(nameof(o));
            }
            return (bool?)o.GetValue(KeepAliveProperty);
        }

        public static void SetKeepAlive(DependencyObject o, bool? value) {
            if (o == null) {
                throw new ArgumentNullException(nameof(o));
            }
            o.SetValue(KeepAliveProperty, value);
        }

        public static bool GetKeepContentAlive(DependencyObject o) {
            if (o == null) {
                throw new ArgumentNullException(nameof(o));
            }
            return (bool)o.GetValue(KeepContentAliveProperty);
        }

        public static void SetKeepContentAlive(DependencyObject o, bool value) {
            if (o == null) {
                throw new ArgumentNullException(nameof(o));
            }
            o.SetValue(KeepContentAliveProperty, value);
        }

        public bool KeepContentAlive {
            get { return (bool)GetValue(KeepContentAliveProperty); }
            set { SetValue(KeepContentAliveProperty, value); }
        }

        public IContentLoader ContentLoader {
            get { return (IContentLoader)GetValue(ContentLoaderProperty); }
            set { SetValue(ContentLoaderProperty, value); }
        }

        public bool IsLoadingContent => (bool)GetValue(IsLoadingContentProperty);

        public Uri Source {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public string TransitionName {
            get { return (string)GetValue(TransitionNameProperty); }
            set { SetValue(TransitionNameProperty, value); }
        }

        public Uri TopSource {
            get { return (Uri)GetValue(TopSourceProperty); }
            set { SetValue(TopSourceProperty, value); }
        }

        public static readonly DependencyProperty TopSourceProperty = DependencyProperty.RegisterAttached(nameof(TopSource), typeof(Uri),
                typeof(ModernFrame),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.Inherits));

        public static Uri GetTopSource(DependencyObject o) {
            if (o == null) {
                throw new ArgumentNullException(nameof(o));
            }
            return (Uri)o.GetValue(TopSourceProperty);
        }

        public static void SetTopSource(DependencyObject o, Uri value) {
            if (o == null) {
                throw new ArgumentNullException(nameof(o));
            }
            o.SetValue(TopSourceProperty, value);
        }
    }
}
