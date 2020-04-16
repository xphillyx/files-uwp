using Files.Filesystem;
using Files.Interacts;
using Files.Views.Pages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;


namespace Files
{
    /// <summary>
    /// The base class which every layout page must derive from
    /// </summary>
    public abstract partial class BaseLayout : Page, INotifyPropertyChanged
    {
        public Interaction AssociatedOperations { get; internal set; }
        public ItemViewModel ViewModel { get; internal set; }
        public bool IsQuickLookEnabled { get; set; } = false;
        public bool isRenamingItem = false;
        private bool isItemSelected = false;
        public bool IsItemSelected
        {
            get
            {
                return isItemSelected;
            }
            internal set
            {
                if (value != isItemSelected)
                {
                    isItemSelected = value;
                    NotifyPropertyChanged("IsItemSelected");
                }
            }
        }
        private List<ListedItem> _SelectedItems;
        public List<ListedItem> SelectedItems
        {
            get
            {
                if (_SelectedItems == null)
                {
                    return new List<ListedItem>();
                }
                else
                {
                    return _SelectedItems;
                }
            }
            internal set
            {
                if (value != _SelectedItems)
                {
                    _SelectedItems = value;
                    if (value == null)
                    {
                        IsItemSelected = false;
                    }
                    else
                    {
                        IsItemSelected = true;
                    }
                    SetSelectedItemsOnUi(value);
                    NotifyPropertyChanged("SelectedItems");
                }
            }
        }
        private ListedItem _SelectedItem;
        public ListedItem SelectedItem
        {
            get
            {
                return _SelectedItem;
            }
            internal set
            {
                if (value != _SelectedItem)
                {
                    _SelectedItem = value;
                    if (value == null)
                    {
                        IsItemSelected = false;
                    }
                    else
                    {
                        IsItemSelected = true;
                    }
                    SetSelectedItemOnUi(value);
                    NotifyPropertyChanged("SelectedItem");
                }
            }
        }
        readonly InstanceTabsView instanceTabsView;

        public BaseLayout()
        {
            this.Loaded += Page_Loaded;
            instanceTabsView = (Window.Current.Content as Frame).Content as InstanceTabsView;
            // QuickLook Integration
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            var isQuickLookIntegrationEnabled = localSettings.Values["quicklook_enabled"];

            if (isQuickLookIntegrationEnabled != null && isQuickLookIntegrationEnabled.Equals(true))
            {
                IsQuickLookEnabled = true;
            }
        }

        protected abstract void SetSelectedItemOnUi(ListedItem selectedItem);

        protected abstract void SetSelectedItemsOnUi(List<ListedItem> selectedItems);

        protected abstract ListedItem GetItemFromElement(object element);

        private void AppSettings_LayoutModeChangeRequested(object sender, EventArgs e)
        {
            if (App.CurrentInstance.CurrentPageType != typeof(YourHome))
            {
                ViewModel.CancelLoadAndClearFiles();
                ViewModel.IsLoadingItems = true;
                ViewModel.IsLoadingItems = false;
                if (App.AppSettings.LayoutMode == 0)
                {
                    App.CurrentInstance.ContentFrame.Navigate(typeof(GenericFileBrowser), ViewModel.WorkingDirectory, null);
                }
                else
                {
                    App.CurrentInstance.ContentFrame.Navigate(typeof(PhotoAlbum), ViewModel.WorkingDirectory, null);
                }
            }

        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override async void OnNavigatedTo(NavigationEventArgs eventArgs)
        {
            base.OnNavigatedTo(eventArgs);
            // Add item jumping handler
            App.AppSettings.LayoutModeChangeRequested += AppSettings_LayoutModeChangeRequested;
            Window.Current.CoreWindow.CharacterReceived += Page_CharacterReceived;
            var parameters = (string)eventArgs.Parameter;
            if (App.AppSettings.FormFactor == Enums.FormFactorMode.Regular)
            {
                Frame rootFrame = Window.Current.Content as Frame;
                InstanceTabsView instanceTabsView = rootFrame.Content as InstanceTabsView;
                instanceTabsView.TabStrip_SelectionChanged(null, null);
            }
            App.CurrentInstance.NavigationToolbar.CanRefresh = true;
            IsItemSelected = false;
            ViewModel.EmptyTextState.IsVisible = Visibility.Collapsed;
            ViewModel.WorkingDirectory = parameters;

            if (ViewModel.WorkingDirectory == Path.GetPathRoot(ViewModel.WorkingDirectory))
            {
                App.CurrentInstance.NavigationToolbar.CanNavigateToParent = false;
            }
            else
            {
                App.CurrentInstance.NavigationToolbar.CanNavigateToParent = true;
            }

            await ViewModel.RefreshItems();

            App.Clipboard_ContentChanged(null, null);
            App.CurrentInstance.NavigationToolbar.PathControlDisplayText = parameters;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            // Remove item jumping handler
            Window.Current.CoreWindow.CharacterReceived -= Page_CharacterReceived;
            if (ViewModel._fileQueryResult != null)
            {
                ViewModel._fileQueryResult.ContentsChanged -= ViewModel.FileContentsChanged;
            }
            App.AppSettings.LayoutModeChangeRequested -= AppSettings_LayoutModeChangeRequested;
        }

        private void UnloadMenuFlyoutItemByName(string nameToUnload)
        {
            Windows.UI.Xaml.Markup.XamlMarkupHelper.UnloadObject(this.FindName(nameToUnload) as DependencyObject);
        }

        public void RightClickContextMenu_Opening(object sender, object e)
        {
            var selectedFileSystemItems = SelectedItems;

            // Find selected items that are not folders
            if (selectedFileSystemItems.Cast<ListedItem>().Any(x => x.PrimaryItemAttribute != StorageItemTypes.Folder))
            {
                UnloadMenuFlyoutItemByName("SidebarPinItem");
                UnloadMenuFlyoutItemByName("OpenInNewTab");
                UnloadMenuFlyoutItemByName("OpenInNewWindowItem");

                if (selectedFileSystemItems.Count == 1)
                {
                    var selectedDataItem = selectedFileSystemItems[0] as ListedItem;

                    if (selectedDataItem.FileExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        UnloadMenuFlyoutItemByName("OpenItem");
                        this.FindName("UnzipItem");
                    }
                    else if (!selectedDataItem.FileExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        this.FindName("OpenItem");
                        UnloadMenuFlyoutItemByName("UnzipItem");
                    }
                }
                else if (selectedFileSystemItems.Count > 1)
                {
                    UnloadMenuFlyoutItemByName("OpenItem");
                    UnloadMenuFlyoutItemByName("UnzipItem");
                }
            }
            else     // All are Folders
            {
                UnloadMenuFlyoutItemByName("OpenItem");
                if (selectedFileSystemItems.Count <= 5 && selectedFileSystemItems.Count > 0)
                {
                    this.FindName("SidebarPinItem");
                    this.FindName("OpenInNewTab");
                    this.FindName("OpenInNewWindowItem");
                    UnloadMenuFlyoutItemByName("UnzipItem");
                }
                else if (selectedFileSystemItems.Count > 5)
                {
                    this.FindName("SidebarPinItem");
                    UnloadMenuFlyoutItemByName("OpenInNewTab");
                    UnloadMenuFlyoutItemByName("OpenInNewWindowItem");
                    UnloadMenuFlyoutItemByName("UnzipItem");
                }

            }

            //check if the selected file is an image
            App.InteractionViewModel.CheckForImage();
        }


        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.CurrentInstance == null)
            {
                App.CurrentInstance = ItemViewModel.GetCurrentSelectedTabInstance<ModernShellPage>();
            }
            AssociatedOperations = new Interaction();
            ViewModel = new ItemViewModel();
            this.Loaded -= Page_Loaded;
        }

        private async void BaseLayout_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var alt = Window.Current.CoreWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            var tabInstance = App.CurrentInstance != null;

            switch (c: ctrl, s: shift, a: alt, t: tabInstance, k: e.Key)
            {
                case (true, true, false, true, VirtualKey.N): //ctrl + shift + n, new item
                    //     await App.AddItemDialogDisplay.ShowAsync();
                    break;
                case (false, true, false, true, VirtualKey.Delete): //shift + delete, PermanentDelete
                    if (!App.CurrentInstance.NavigationToolbar.IsEditModeEnabled)
                    {
                        App.InteractionViewModel.PermanentlyDelete = true;
                        DeleteItems();
                    }
                    break;
                case (true, false, false, true, VirtualKey.C): //ctrl + c, copy
                    if (!App.CurrentInstance.NavigationToolbar.IsEditModeEnabled)
                        CopyItem();
                    break;
                case (true, false, false, true, VirtualKey.V): //ctrl + v, paste
                    if (!App.CurrentInstance.NavigationToolbar.IsEditModeEnabled)
                        await PasteItems();
                    break;
                case (true, false, false, true, VirtualKey.X): //ctrl + x, cut
                    if (!App.CurrentInstance.NavigationToolbar.IsEditModeEnabled)
                        CutItem();
                    break;
                case (true, false, false, true, VirtualKey.A): //ctrl + a, select all
                    if (!App.CurrentInstance.NavigationToolbar.IsEditModeEnabled)
                        (App.CurrentInstance.ContentFrame.Content as IChildLayout).SelectAllItems();
                    break;
                case (true, false, false, true, VirtualKey.N): //ctrl + n, new window
                    instanceTabsView.LaunchNewWindow();
                    break;
                case (true, false, false, true, VirtualKey.W): //ctrl + w, close tab
                    instanceTabsView.CloseTab();
                    break;
                case (true, false, false, true, VirtualKey.F4): //ctrl + F4, close tab
                    instanceTabsView.CloseTab();
                    break;
                case (false, false, false, true, VirtualKey.Delete): //delete, delete item
                    if (IsItemSelected && !isRenamingItem)
                        DeleteItems();
                    break;
                case (false, false, false, true, VirtualKey.Space): //space, quick look
                    if (!App.CurrentInstance.NavigationToolbar.IsEditModeEnabled)
                    {
                        if (IsQuickLookEnabled)
                        {
                            ToggleQuickLook();
                        }
                    }
                    break;
            };


            if (App.CurrentInstance.CurrentPageType == typeof(PhotoAlbum))
            {
                switch (e.Key)
                {
                    case VirtualKey.F2: //F2, rename
                        if (SelectedItems.Count > 0)
                        {
                            (App.CurrentInstance.ContentFrame.Content as IChildLayout).BeginRename();
                        }
                        break;
                }
            }
        }

        protected virtual void Page_CharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            var focusedElement = FocusManager.GetFocusedElement(XamlRoot) as FrameworkElement;
            if (focusedElement is TextBox)
                return;

            char letterPressed = Convert.ToChar(args.KeyCode);
            PushJumpChar(letterPressed);
        }

        protected async void List_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> draggedItems = await e.DataView.GetStorageItemsAsync();
                // As long as one file doesn't already belong to this folder
                if (draggedItems.Any(draggedItem => !Directory.GetParent(draggedItem.Path).FullName.Equals(ViewModel.WorkingDirectory, StringComparison.OrdinalIgnoreCase)))
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                    e.Handled = true;
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                }
            }
        }

        protected async void List_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                await PasteItems(e.DataView);
                e.Handled = true;
            }
        }

        protected async void Item_DragStarting(object sender, DragStartingEventArgs e)
        {
            List<IStorageItem> selectedStorageItems = new List<IStorageItem>();
            foreach (ListedItem item in SelectedItems)
            {
                if (item.PrimaryItemAttribute == StorageItemTypes.File)
                    selectedStorageItems.Add(await StorageFile.GetFileFromPathAsync(item.ItemPath));
                else if (item.PrimaryItemAttribute == StorageItemTypes.Folder)
                    selectedStorageItems.Add(await StorageFolder.GetFolderFromPathAsync(item.ItemPath));
            }

            e.Data.SetStorageItems(selectedStorageItems);
            e.DragUI.SetContentFromDataPackage();
        }

        protected async void Item_DragOver(object sender, DragEventArgs e)
        {
            ListedItem item = GetItemFromElement(sender);
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.Handled = true;
                IReadOnlyList<IStorageItem> draggedItems = await e.DataView.GetStorageItemsAsync();
                // Items from the same parent folder as this folder are dragged into this folder, so we move the items instead of copy
                if (draggedItems.Any(draggedItem => Directory.GetParent(draggedItem.Path).FullName == Directory.GetParent(item.ItemPath).FullName))
                {
                    e.AcceptedOperation = DataPackageOperation.Move;
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                }
            }
        }

        protected async void Item_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            ListedItem rowItem = GetItemFromElement(sender);
            await PasteItems(e.DataView);
        }

        protected void InitializeDrag(UIElement element)
        {
            ListedItem item = GetItemFromElement(element);
            if (item != null)
            {
                element.AllowDrop = false;
                element.DragStarting -= Item_DragStarting;
                element.DragStarting += Item_DragStarting;
                element.DragOver -= Item_DragOver;
                element.Drop -= Item_Drop;
                if (item.PrimaryItemAttribute == StorageItemTypes.Folder)
                {
                    element.AllowDrop = true;
                    element.DragOver += Item_DragOver;
                    element.Drop += Item_Drop;
                }
            }
        }
    }
}
