using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ModernMenu : Control {
        private readonly Dictionary<string, ReadOnlyLinkGroupCollection> _groupMap = new Dictionary<string, ReadOnlyLinkGroupCollection>();
        private bool _isSelecting;

        static ModernMenu() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ModernMenu), new FrameworkPropertyMetadata(typeof(ModernMenu)));
        }

        public ModernMenu() {
            InputBindings.AddRange(new[] {
                new InputBinding(new DelegateCommand(NewTab), new KeyGesture(Key.T, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(CloseTab), new KeyGesture(Key.W, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(CloseTab), new KeyGesture(Key.F4, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(RestoreTab), new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(new DelegateCommand(FocusCurrentTab), new KeyGesture(Key.F6)),
                new InputBinding(new DelegateCommand(FocusCurrentTab), new KeyGesture(Key.L, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(NextTab), new KeyGesture(Key.Tab, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(PreviousTab), new KeyGesture(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift))
            });

            foreach (var i in Enumerable.Range(0, 9)) {
                InputBindings.Add(new InputBinding(new DelegateCommand(() => SwitchTab(i, false)), new KeyGesture(Key.D1 + i, ModifierKeys.Control)));
                InputBindings.Add(new InputBinding(new DelegateCommand(() => SwitchSection(i, false)),
                        new KeyGesture(Key.D1 + i, ModifierKeys.Alt | ModifierKeys.Control)));
            }

            Loaded += OnLoaded;
        }

        public static readonly RoutedEvent InitializeEvent = EventManager.RegisterRoutedEvent(nameof(Initialize), RoutingStrategy.Bubble,
                typeof(EventHandler<InitializeEventArgs>), typeof(ModernMenu));

        public event EventHandler<InitializeEventArgs> Initialize {
            add => AddHandler(InitializeEvent, value);
            remove => RemoveHandler(InitializeEvent, value);
        }

        public class InitializeEventArgs : RoutedEventArgs {
            public InitializeEventArgs(RoutedEvent routedEvent, Uri loadedUri) : base(routedEvent) {
                LoadedUri = loadedUri;
            }

            [CanBeNull]
            public Uri LoadedUri { get; }
        }

        private bool _skipLoading;

        public void SkipLoading() {
            _skipLoading = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            foreach (var linkGroup in LinkGroups) {
                linkGroup.Initialize();
            }

            if (!_skipLoading) {
                var saved = ValuesStorage.Get<Uri>($"{SaveKey}_link");
                if (!SelectUriIfLinkExists(saved)) {
                    Logging.Debug("Can’t find link: " + saved);
                    SelectUriIfLinkExists(DefaultSource);
                }
            }
        }

        #region Browser-like commands
        private void NewTab() {
            if (!(SelectedLinkGroup is LinkGroupFilterable) || _subMenuListBox == null || VisualExtension.IsInputFocused()) return;
            var textBox = _subMenuListBox.ItemContainerGenerator
                                         .ContainerFromIndex(_subMenuListBox.Items.Count - 1)?.FindChild<TextBox>("NameTextBox");
            if (textBox == null) return;
            textBox.Focus();
            textBox.SelectAll();
        }

        private void CloseTab() {
            if (!(SelectedLinkGroup is LinkGroupFilterable) || _subMenuListBox == null || VisualExtension.IsInputFocused()) return;
            _subMenuListBox.ItemContainerGenerator
                           .ContainerFromIndex(_subMenuListBox.SelectedIndex)?.FindChild<Button>("CloseButton")?.Command?.Execute(null);
            _subMenuListBox.Focus();
        }

        private void RestoreTab() {
            if (!(SelectedLinkGroup is LinkGroupFilterable) || _subMenuListBox == null || VisualExtension.IsInputFocused()) return;
            ((LinkGroupFilterable)SelectedLinkGroup).RestoreLastClosed();
            _subMenuListBox.Focus();
        }

        private void FocusCurrentTab() {
            if (!(SelectedLinkGroup is LinkGroupFilterable) || _subMenuListBox == null || VisualExtension.IsInputFocused()) return;
            _subMenuListBox.ItemContainerGenerator
                           .ContainerFromIndex(_subMenuListBox.SelectedIndex)?.FindChild<TextBox>("NameTextBox")?.Focus();
        }

        private void SwitchTab(int index, bool cycle) {
            if (_subMenuListBox == null || VisualExtension.IsInputFocused()) return;
            var count = _subMenuListBox.Items.Count - (SelectedLinkGroup is LinkGroupFilterable ? 1 : 0);
            _subMenuListBox.SelectedIndex = index >= count ? cycle ? 0 : count - 1 :
                    index < 0 ? cycle ? count - 1 : 0 : index;
            _subMenuListBox.Focus();
        }

        private void SwitchSection(int index, bool cycle) {
            if (_subMenuListBox == null || VisualExtension.IsInputFocused()) return;
            var count = VisibleLinkGroups.Count;
            var group = VisibleLinkGroups.ElementAtOrDefault(index >= count ? cycle ? 0 : count - 1 :
                    index < 0 ? cycle ? count - 1 : 0 : index);
            if ((group as LinkGroupFilterable)?.IsEnabled == false) return;
            SelectedLink = group?.SelectedLink ?? SelectedLink;
        }

        private void NextTab() {
            if (_subMenuListBox == null || VisualExtension.IsInputFocused()) return;
            SwitchTab(_subMenuListBox.SelectedIndex + 1, true);
        }

        private void PreviousTab() {
            if (_subMenuListBox == null || VisualExtension.IsInputFocused()) return;
            SwitchTab(_subMenuListBox.SelectedIndex - 1, true);
        }
        #endregion

        #region LinkGroups
        public static readonly DependencyProperty LinkGroupsProperty = DependencyProperty.Register("LinkGroups", typeof(LinkGroupCollection),
                typeof(ModernMenu), new PropertyMetadata(new LinkGroupCollection(), OnLinkGroupsChanged));

        public LinkGroupCollection LinkGroups {
            get => (LinkGroupCollection)GetValue(LinkGroupsProperty);
            set => SetValue(LinkGroupsProperty, value);
        }

        private static void OnLinkGroupsChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernMenu)o).OnLinkGroupsChanged((LinkGroupCollection)e.OldValue, (LinkGroupCollection)e.NewValue);
        }

        private void OnLinkGroupsChanged(LinkGroupCollection oldValue, LinkGroupCollection newValue) {
            if (oldValue != null) {
                oldValue.CollectionChanged -= OnLinkGroupsCollectionChanged;
            }

            if (newValue != null) {
                newValue.CollectionChanged += OnLinkGroupsCollectionChanged;
            }

            RebuildMenu(newValue);
        }
        #endregion

        #region SelectedLinkGroup
        public static readonly DependencyProperty SelectedLinkGroupProperty = DependencyProperty.Register("SelectedLinkGroup", typeof(LinkGroup),
                typeof(ModernMenu), new PropertyMetadata(OnSelectedLinkGroupChanged));

        [CanBeNull]
        public LinkGroup SelectedLinkGroup => (LinkGroup)GetValue(SelectedLinkGroupProperty);

        [CanBeNull]
        public LinkGroupFilterable SelectedLinkGroupFilterable => SelectedLinkGroup as LinkGroupFilterable;

        private static void OnSelectedLinkGroupChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernMenu)o).OnSelectedLinkGroupChanged((LinkGroup)e.OldValue, (LinkGroup)e.NewValue);
        }

        private void OnSelectedLinkGroupChanged(LinkGroup oldValue, LinkGroup newValue) {
            if (oldValue != null) {
                oldValue.PropertyChanged -= OnGroupPropertyChanged;
            }

            if (newValue != null) {
                newValue.PropertyChanged += OnGroupPropertyChanged;
                SelectedLink = newValue.SelectedLink;
            } else {
                SelectedLink = null;
            }
        }

        private void OnGroupPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(LinkGroup.SelectedLink)) return;
            SelectedLink = (sender as LinkGroup)?.SelectedLink;
        }
        #endregion

        #region SelectedLink
        public static readonly DependencyProperty SelectedLinkProperty = DependencyProperty.Register(nameof(SelectedLink), typeof(Link),
                typeof(ModernMenu), new PropertyMetadata(OnSelectedLinkChanged));

        public Link SelectedLink {
            get => (Link)GetValue(SelectedLinkProperty);
            set => SetValue(SelectedLinkProperty, value);
        }

        private static void OnSelectedLinkChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernMenu)o).OnSelectedLinkChanged((Link)e.OldValue, (Link)e.NewValue);
        }

        private void OnSelectedLinkChanged(Link oldValue, Link newValue) {
            if (oldValue != null) {
                oldValue.PropertyChanged -= OnLinkPropertyChanged;
            }

            if (newValue != null) {
                newValue.PropertyChanged += OnLinkPropertyChanged;
            }

            SelectedSource = newValue?.NonSelectable == false ? newValue.Source : null;

            if (newValue == null || SaveKey == null || newValue.NonSelectable) return;
            var group = (from g in LinkGroups
                         where g.Links.Contains(newValue)
                         select g).FirstOrDefault();
            if (group != null) {
                group.SelectedLink = newValue;
                ValuesStorage.Set($"{SaveKey}__{group.GroupKey}", newValue.Source);
            }
            ValuesStorage.Set($"{SaveKey}_link", newValue.Source);
        }
        #endregion

        #region SelectedSource
        public static readonly DependencyProperty SelectedSourceProperty = DependencyProperty.Register("SelectedSource", typeof(Uri),
                typeof(ModernMenu), new PropertyMetadata(OnSelectedSourceChanged));

        public Uri SelectedSource {
            get => (Uri)GetValue(SelectedSourceProperty);
            set => SetValue(SelectedSourceProperty, value);
        }

        private static void OnSelectedSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernMenu)o).OnSelectedSourceChanged((Uri)e.OldValue, (Uri)e.NewValue);
        }

        private void OnSelectedSourceChanged(Uri oldValue, Uri newValue) {
            if (_isSelecting) return;
            if (newValue?.Equals(oldValue) == true) return;
            UpdateSelection();
        }
        #endregion

        #region VisibleLinkGroups
        private static readonly DependencyPropertyKey VisibleLinkGroupsPropertyKey = DependencyProperty.RegisterReadOnly("VisibleLinkGroups",
                typeof(ReadOnlyLinkGroupCollection), typeof(ModernMenu), null);

        public static readonly DependencyProperty VisibleLinkGroupsProperty = VisibleLinkGroupsPropertyKey.DependencyProperty;

        public ReadOnlyLinkGroupCollection VisibleLinkGroups => (ReadOnlyLinkGroupCollection)GetValue(VisibleLinkGroupsProperty);
        #endregion

        private ListBox _subMenuListBox;

        public override void OnApplyTemplate() {
            if (_subMenuListBox != null) {
                _subMenuListBox.Drop -= OnDrop;
            }

            base.OnApplyTemplate();

            _subMenuListBox = GetTemplateChild("PART_SubMenu") as ListBox;
            if (_subMenuListBox != null) {
                _subMenuListBox.Drop += OnDrop;
            }
        }

        private void OnDrop(object sender, DragEventArgs e) {
            var destination = (ListBox)sender;
            var widget = e.Data.GetData(LinkInput.DraggableFormat) as LinkInput;
            var source = e.Data.GetData(Draggable.SourceFormat) as ItemsControl;

            if (widget == null || source == null || !ReferenceEquals(source, _subMenuListBox) || !(SelectedLinkGroup is LinkGroupFilterable group)) {
                e.Effects = DragDropEffects.None;
                return;
            }

            e.Effects = DragDropEffects.Move;
            group.OnDrop(widget, destination.GetMouseItemIndex());
        }

        private bool SelectUriIfLinkExists(Uri uri, string groupKey = null) {
            if (uri != null) {
                RaiseEvent(new InitializeEventArgs(InitializeEvent, uri));
                var selected = LinkGroups.Where(x => groupKey == null || x.GroupKey == groupKey)
                                         .SelectMany(g => g.Links).FirstOrDefault(t => t.Source?.ToString() == uri.ToString());
                if (selected != null) {
                    SelectedLink = selected;
                    return true;
                }
            }

            return false;
        }

        public void SwitchToGroupByKey(string key) {
            if (SaveKey == null || !SelectUriIfLinkExists(ValuesStorage.Get<Uri>($"{SaveKey}__{key}"), key)) {
                SelectedLink = (from g in LinkGroups
                                where g.GroupKey == key
                                from l in g.Links
                                select l).FirstOrDefault();
            }
        }

        private void OnLinkPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName != nameof(Link.Source)) return;
            var link = sender as Link;
            SelectedSource = link?.Source;

            if (link == null || SaveKey == null || link.NonSelectable) return;
            var group = (from g in LinkGroups
                         where g.Links.Contains(link)
                         select g).FirstOrDefault();
            if (group != null) {
                ValuesStorage.Set($"{SaveKey}__{group.GroupKey}", link.Source);
            }
            ValuesStorage.Set($"{SaveKey}_link", link.Source);
        }

        private void OnLinkGroupsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            RebuildMenu((LinkGroupCollection)sender);
        }

        private void RebuildMenu(LinkGroupCollection groups) {
            _groupMap.Clear();
            if (groups != null) {
                // fill the group map based on group key
                foreach (var group in groups) {
                    if (!_groupMap.TryGetValue(group.GroupKey, out var groupCollection)) {
                        // create a new collection for this group key
                        groupCollection = new ReadOnlyLinkGroupCollection(new LinkGroupCollection());
                        _groupMap.Add(group.GroupKey, groupCollection);
                    }

                    // add the group
                    groupCollection.List.Add(group);
                }
            }

            // update current selection
            UpdateSelection();
        }

        private void UpdateSelection() {
            if (!IsLoaded) return;

            LinkGroup selectedGroup = null;
            Link selectedLink = null;

            if (LinkGroups != null) {
                // find the current select group and link based on the selected source
                var linkInfo = (from g in LinkGroups
                                from l in g.Links
                                where l.Source == SelectedSource
                                select new {
                                    Group = g,
                                    Link = l
                                }).FirstOrDefault();

                if (linkInfo != null) {
                    selectedGroup = linkInfo.Group;
                    selectedLink = linkInfo.Link;
                } else {
                    // could not find link and group based on selected source, fall back to selected link group
                    selectedGroup = SelectedLinkGroup;

                    // if selected group doesn’t exist in available groups, select first group
                    if (LinkGroups.All(g => g != selectedGroup)) {
                        selectedGroup = LinkGroups.FirstOrDefault();
                    }
                }
            }

            ReadOnlyLinkGroupCollection groups = null;
            if (selectedGroup != null) {
                // ensure group itself maintains the selected link
                if (selectedLink == null) {
                    /* very questionable place */
                    selectedLink = selectedGroup.SelectedLink;
                } else {
                    selectedGroup.SelectedLink = selectedLink;
                }

                // find the collection this group belongs to
                var groupKey = selectedGroup.GroupKey;
                _groupMap.TryGetValue(groupKey, out groups);
            }

            _isSelecting = true;
            // update selection
            SetValue(VisibleLinkGroupsPropertyKey, groups);
            SetCurrentValue(SelectedLinkGroupProperty, selectedGroup);
            SetCurrentValue(SelectedLinkProperty, selectedLink);
            _isSelecting = false;
        }

        public static readonly DependencyProperty SaveKeyProperty = DependencyProperty.Register(nameof(SaveKey), typeof(string),
                typeof(ModernMenu));

        public string SaveKey {
            get => (string)GetValue(SaveKeyProperty);
            set => SetValue(SaveKeyProperty, value);
        }

        public static readonly DependencyProperty DefaultSourceProperty = DependencyProperty.Register(nameof(DefaultSource), typeof(Uri),
                typeof(ModernMenu));

        public Uri DefaultSource {
            get => (Uri)GetValue(DefaultSourceProperty);
            set => SetValue(DefaultSourceProperty, value);
        }
    }
}