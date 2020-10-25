using System;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.Workshop {
    public static class WorkshopUtils {
        public static void OpenSubPage(this FrameworkElement sender, Uri uri, string linkName, string linkIcon, string linkTag) {
            var group = sender?.GetParent<ModernWindow>()?.CurrentLinkGroup as LinkGroupFilterable;
            if (group == null) {
                NavigationCommands.GoToPage.Execute(uri, sender);
            } else {
                for (var i = group.Links.Count - 1; i >= 0; i--) {
                    var link = group.Links[i];
                    if (link.Tag == linkTag || link.Tag == @"selected" && linkTag == @"category" /* TODO */) {
                        group.Links.RemoveAt(i);
                    }
                }
                var currentLinkIndex = group.Links.IndexOf(group.SelectedLink);
                var newLink = new Link {
                    DisplayName = linkName,
                    Source = uri,
                    Icon = linkIcon != null ? new BetterImage { Filename = linkIcon } : null,
                    Tag = linkTag,
                    IsTemporary = true
                };
                if (currentLinkIndex != -1) {
                    group.Links.Insert(currentLinkIndex + 1, newLink);
                } else {
                    group.Links.Add(newLink);
                }
                group.SelectedLink = newLink;
            }
        }
    }
}