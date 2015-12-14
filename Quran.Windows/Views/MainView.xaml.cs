﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Quran.Core;
using Quran.Core.Data;
using Quran.Core.Properties;
using Quran.Core.Utils;
using Quran.Core.ViewModels;
using Quran.Windows.UI;
using Quran.Windows.Utils;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Quran.Windows.Views
{
    public partial class MainView
    {
        public MainViewModel ViewModel { get; set; }
        public SearchViewModel SearchViewModel { get; set; }
        public ObservableCollection<NavigationLink> NavigationLinks = new ObservableCollection<NavigationLink>();

        public MainView()
        {
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;
            ViewModel = QuranApp.MainViewModel;
            SearchViewModel = QuranApp.SearchViewModel;
            InitializeComponent();
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
        }

        // Load data for the ViewModel Items
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await ViewModel.Initialize();
            await SearchViewModel.Initialize();
            SurahViewSource.Source = ViewModel.GetGrouppedSurahItems();
            JuzViewSource.Source = ViewModel.GetGrouppedJuzItems();
            BookmarksViewSource.Source = ViewModel.GetGrouppedBookmarks();
            BuildLocalizedApplicationBar();
            await LittleWatson.CheckForPreviousException();

            // We set the state of the commands on the appbar
            SetCommandsVisibility(BookmarksListView);

            // Remove all back navigation options
            while (Frame.BackStack.Count() > 0)
            {
                Frame.BackStack.RemoveAt(Frame.BackStack.Count() - 1);
            }

            // Show welcome message
            await ShowWelcomeMessage();
            
            // Show prompt to download content if not all images exist
            if (!await FileUtils.HaveAllImages())
            {
                try
                {
                    await ViewModel.Download();
                }
                catch (Exception ex)
                {
                    await QuranApp.NativeProvider.ShowErrorMessageBox("Failed to download Quran Data: " + ex.Message);
                }
            }
        }

        private void NavigateToLastPage(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DetailsView));
        }


        private async Task ShowWelcomeMessage()
        {
            var versionFromConfig = new Version(SettingsUtils.Get<string>(Constants.PREF_CURRENT_VERSION));
            var nameHelper = SystemInfo.ApplicationName;
            var versionFromAssembly = new Version(SystemInfo.ApplicationVersion);
            if (versionFromAssembly > versionFromConfig)
            {
                var message =
                    @"Assalamu Aleikum,

Thank you for downloading Quran Phone. Please note that this is a BETA release and is still work in progress. 
New in Version 0.4.2:
* Bug fixes

If you find any issues with the app or would like to provide suggestions, please use Contact Us option available via the menu. 

Jazzakum Allahu Kheiran,
Quran Phone Team";
                await QuranApp.NativeProvider.ShowInfoMessageBox(message, "Welcome");
                SettingsUtils.Set(Constants.PREF_CURRENT_VERSION, versionFromAssembly.ToString());
            }
        }

        private void NavigateLink(object sender, TappedRoutedEventArgs e)
        {
            // If selected item is null (no selection) do nothing
            var list = sender as ListView;
            if (e.OriginalSource == null || !(e.OriginalSource is FrameworkElement))
            {
                return;
            }

            var selectedItem = ((FrameworkElement)e.OriginalSource).DataContext as ItemViewModel;

            try
            {
                SettingsUtils.Set<int>(Constants.PREF_LAST_PAGE, selectedItem.PageNumber);

                // Navigate to the new page
                if (selectedItem.SelectedAyah == null)
                {
                    Frame.Navigate(typeof(DetailsView));
                }
                else
                {
                    Frame.Navigate(typeof(DetailsView),
                        new NavigationData
                        {
                            Surah = selectedItem.SelectedAyah.Surah,
                            Ayah = selectedItem.SelectedAyah.Ayah
                        });
                }
            }
            catch
            {
                // Navigation exception - ignore
            }

            // Reset selected item to null (no selection)
            list.SelectedItem = null;
        }

        private void HamburgerButtonClick(object sender, RoutedEventArgs e)
        {
            MainSplitView.IsPaneOpen = !MainSplitView.IsPaneOpen;
        }

        private void NavLinkItemClick(object sender, ItemClickEventArgs e)
        {
            MainSplitView.IsPaneOpen = false;
            var item = e.ClickedItem as NavigationLink;
            if (item != null)
            {
                item.Action();
            }
        }

        // Build a localized ApplicationBar
        private void BuildLocalizedApplicationBar()
        {
            NavigationLinks.Add(new NavigationLink
            {
                Label = Quran.Core.Properties.Resources.search,
                Symbol = Symbol.Find,
                Action = () => { MainPivot.SelectedItem = SearchPivotItem; }
            });
        }

        private void SearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            SearchViewModel.Load(QuranSearchBox.Text);
        }

        private void GoToSettings(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsView), "general");
        }

        #region Context menu
        private bool _isEnablingMultiselect = false;
        private void PivotItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainPivot.SelectedItem == BookmarksPivotItem)
            {
                BottomAppBar.Visibility = Visibility.Visible;
            }
            else
            {
                BottomAppBar.Visibility = Visibility.Collapsed;
            }
        }

        private void OnBookmarkListEdgeTapped(ListView sender, ListViewEdgeTappedEventArgs e)
        {
            _isEnablingMultiselect = true;
            // When user releases the pointer after pessing on the left edge of the item,
            // the ListView will switch to Multiple Selection 
            BookmarksListView.SelectionMode = ListViewSelectionMode.Multiple;
            // Also, we want the Left Edge Tap funcionality will be no longer enable. 
            BookmarksListView.IsItemLeftEdgeTapEnabled = false;
            // It's desirable that the Appbar shows the actions available for multiselect
            SetCommandsVisibility(BookmarksListView);
            _isEnablingMultiselect = false;
        }

        private void OnBookmarkListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isEnablingMultiselect)
            {
                // When there are no selected items, the list returns to None selection mode.
                if (BookmarksListView.SelectedItems.Count == 0)
                {
                    BookmarksListView.SelectionMode = ListViewSelectionMode.None;
                    BookmarksListView.IsItemLeftEdgeTapEnabled = true;
                    SetCommandsVisibility(BookmarksListView);
                }
            }
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            // We want to exit from the multiselect mode when pressing back button
            if (BookmarksListView.SelectionMode == ListViewSelectionMode.Multiple)
            {
                BookmarksListView.SelectedItems.Clear();
                BookmarksListView.SelectionMode = ListViewSelectionMode.None;
                e.Handled = true;
            }
        }
        private void SetCommandsVisibility(ListView listView)
        {
            if (listView.SelectionMode == ListViewSelectionMode.Multiple || listView.SelectedItems.Count > 1)
            {
                SelectAppBarBtn.Visibility = Visibility.Collapsed;
                CancelSelectionAppBarBtn.Visibility = Visibility.Visible;
                RemoveItemAppBarBtn.Visibility = Visibility.Visible;
            }
            else
            {
                SelectAppBarBtn.Visibility = Visibility.Visible;
                CancelSelectionAppBarBtn.Visibility = Visibility.Collapsed;
                RemoveItemAppBarBtn.Visibility = Visibility.Collapsed;
            }
        }

        private void SelectBookmarkItems(object sender, RoutedEventArgs e)
        {
            _isEnablingMultiselect = true;
            BookmarksListView.SelectionMode = ListViewSelectionMode.Multiple;
            BookmarksListView.IsItemLeftEdgeTapEnabled = false;
            SetCommandsVisibility(BookmarksListView);
            _isEnablingMultiselect = false;
        }

        private void NavigateBookmarkLink(object sender, TappedRoutedEventArgs e)
        {
            if (BookmarksListView.SelectionMode == ListViewSelectionMode.None &&
                BookmarksListView.SelectedItems.Count == 0)
            {
                NavigateLink(sender, e);
                e.Handled = true;
            }
        }

        private void CancelBookmarkSelection(object sender, RoutedEventArgs e)
        {
            if (!_isEnablingMultiselect)
            {
                // If the list is multiple selection mode but there is no items selected, 
                // then the list should return to the initial selection mode.
                if (BookmarksListView.SelectedItems.Count == 0)
                {
                    BookmarksListView.SelectionMode = ListViewSelectionMode.None;
                    BookmarksListView.IsItemLeftEdgeTapEnabled = true;
                    SetCommandsVisibility(BookmarksListView);
                }
                else
                {
                    BookmarksListView.SelectedItems.Clear();
                }
            }
        }

        private void RemoveBookmarkItem(object sender, RoutedEventArgs e)
        {
            if (BookmarksListView.SelectedIndex != -1)
            {
                // When an item is removed from the underlying collection, the Listview is updated, 
                // hence the this.SelectedItems is updated as well. 
                // It's needed to copy the selected items collection to iterate over other collection that 
                // is not updated.
                List<ItemViewModel> selectedItems = new List<ItemViewModel>();
                foreach (ItemViewModel item in BookmarksListView.SelectedItems)
                {
                    selectedItems.Add(item);
                }
                foreach (ItemViewModel item in selectedItems)
                {
                    ViewModel.DeleteBookmark(item);
                }
                BookmarksViewSource.Source = ViewModel.GetGrouppedBookmarks();
            }
        }
        #endregion
    }
}