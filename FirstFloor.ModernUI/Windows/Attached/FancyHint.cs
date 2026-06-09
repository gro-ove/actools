using System;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace FirstFloor.ModernUI.Windows.Attached {
    public class FancyHint : NotifyPropertyChanged {
        public static TimeSpan OptionMinimumDelay = TimeSpan.FromMinutes(30d);
        public static bool OptionDebugMode = false;

        private static readonly string KeyStartup = "__fancyHint:startupId";
        private static readonly int StartupId;

        private static readonly string KeyLastShown = "__fancyHint:lastShown";
        private static DateTime _lastShown;

        static FancyHint() {
            StartupId = ValuesStorage.Get<int>(KeyStartup);
            ValuesStorage.Set(KeyStartup, StartupId + 1);
            _lastShown = ValuesStorage.Get(KeyLastShown, DateTime.MinValue);
        }

        private readonly string _keyShown, _keyAvailable;
        private readonly int _startupsDelay;
        private int _triggersDelay;
        private readonly bool _forced;

        public FancyHint(string id, string header, string description, double probability = 1d, bool availableByDefault = true,
                int startupsDelay = 2, int triggersDelay = 0, bool forced = false, bool closeOnResize = false) {
            Id = id;
            Header = header;
            Description = description;
            Probability = probability;
            CloseOnResize = closeOnResize;
            _keyShown = $"__fancyHint:shown:{id}";
            _keyAvailable = availableByDefault ? null : $"__fancyHint:available:{id}";

            _startupsDelay = startupsDelay;
            _triggersDelay = triggersDelay;
            _forced = forced;

            FancyHintsService.Register(this);
        }

        [ThreadStatic]
        private static Random _random;

        public static Random RandomInstance => _random ?? (_random = new Random(Guid.NewGuid().GetHashCode()));

        public string Id { get; }
        public string Header { get; }
        public string Description { get; }

        public double Probability { get; }
        public bool CloseOnResize { get; }

        private static void DebugMessage(string msg) {
#if DEBUG_
            Logging.Debug(msg);
#else
            if (OptionDebugMode) {
                Logging.Debug(msg);
            }
#endif
        }

        public async void Trigger(TimeSpan delay) {
            await Task.Delay(delay);
            ActionExtension.InvokeInMainThreadAsync(() => {
                DebugMessage($"{Id}: triggered");
                
                if (FancyHintAdorner.IsAnyShown) {
                    DebugMessage($"{Id}: something else is being shown right now");
                    return;
                }

                if (Shown) {
                    DebugMessage($"{Id}: already shown");
                    return;
                }

                if (!Available) {
                    DebugMessage($"{Id}: not available");
                    return;
                }

                if (!OptionDebugMode && RandomInstance.NextDouble() > Probability) {
                    DebugMessage($"{Id}: not now, random says so");
                    return;
                }

                if (_startupsDelay > StartupId) {
                    DebugMessage($"{Id}: waiting for a next start up ({_startupsDelay}, current: {StartupId})");
                    return;
                }

                if (_triggersDelay-- > 0) {
                    DebugMessage($"{Id}: waiting for a next ({_triggersDelay + 1}) trigger");
                    return;
                }

                if (!_forced && DateTime.Now - _lastShown < OptionMinimumDelay) {
                    DebugMessage($"{Id}: gap ({DateTime.Now - _lastShown}, required: {OptionMinimumDelay}) is too low");
                    return;
                }

                var args = new ShowHintEventArgs();
                Show?.Invoke(this, args);
                if (!args.Shown) {
                    DebugMessage($"{Id}: cancelled!");
                    return;
                }

                DebugMessage($"{Id}: shown");
                Shown = true;
                _lastShown = DateTime.Now;
                ValuesStorage.Set(KeyLastShown, _lastShown);
            });
        }

        public void Trigger() {
            Trigger(TimeSpan.FromMilliseconds(300));
        }

        public void MarkAsAvailable() {
            Available = true;
        }

        public void MarkAsUnnecessary() {
            Logging.Debug($"{Id}: unnecessary");
            Shown = true;
            Unnecessary?.Invoke(this, EventArgs.Empty);
        }

        private bool? _available;

        public bool Available {
            get => _keyAvailable == null || (_available ?? (_available = ValuesStorage.Get<bool>(_keyAvailable)).Value);
            private set {
                if (_keyAvailable == null || Equals(value, Available)) return;
                _available = value;
                ValuesStorage.Set(_keyAvailable, value);
                OnPropertyChanged();
            }
        }

        private bool? _shown;

        public bool Shown {
            get => _shown ?? (_shown = !OptionDebugMode && ValuesStorage.Get<bool>(_keyShown)).Value;
            private set {
                if (Equals(value, Shown)) return;
                _shown = value;
                ValuesStorage.Set(_keyShown, value);
                OnPropertyChanged();
            }
        }

        public event EventHandler<ShowHintEventArgs> Show;
        public event EventHandler<EventArgs> Unnecessary;
    }
}