using Files.Filesystem;
using Files.Interacts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace Files
{
    /// <summary>
    /// The base class which every layout page must derive from
    /// </summary>
    public class BaseLayout : Page
    {
        public BaseLayout()
        {

        }

        protected override void OnNavigatedTo(NavigationEventArgs eventArgs)
        {
            base.OnNavigatedTo(eventArgs);
            var parameters = (string)eventArgs.Parameter;
            if (App.FormFactor == Enums.FormFactorMode.Regular)
            {
                Frame rootFrame = Window.Current.Content as Frame;
                InstanceTabsView instanceTabsView = rootFrame.Content as InstanceTabsView;
                instanceTabsView.TabStrip_SelectionChanged(null, null);
            }
            App.occupiedInstance.RefreshButtonState = true;
            App.occupiedInstance.AlwaysPresentCommands.isEnabled = true;
            App.occupiedInstance.ViewModel.EmptyTextState.isVisible = Visibility.Collapsed;
            App.occupiedInstance.WorkingDirectory.Path = parameters;
            
            if (App.occupiedInstance.WorkingDirectory.Path == Path.GetPathRoot(App.occupiedInstance.WorkingDirectory.Path))
            {
                App.occupiedInstance.UpButtonState = false;
            }
            else
            {
                App.occupiedInstance.UpButtonState = true;
            }

            App.occupiedInstance.ViewModel.AddItemsToCollectionAsync(App.occupiedInstance.WorkingDirectory.Path);
            App.Clipboard_ContentChanged(null, null);

            if (parameters.Equals(App.DesktopPath))
            {
                App.occupiedInstance.PathText.Text = "Desktop";
            }
            else if (parameters.Equals(App.DocumentsPath))
            {
                App.occupiedInstance.PathText.Text = "Documents";
            }
            else if (parameters.Equals(App.DownloadsPath))
            {
                App.occupiedInstance.PathText.Text = "Downloads";
            }
            else if (parameters.Equals(App.PicturesPath))
            {
                App.occupiedInstance.PathText.Text = "Pictures";
            }
            else if (parameters.Equals(App.MusicPath))
            {
                App.occupiedInstance.PathText.Text = "Music";
            }
            else if (parameters.Equals(App.OneDrivePath))
            {
                App.occupiedInstance.PathText.Text = "OneDrive";
            }
            else if (parameters.Equals(App.VideosPath))
            {
                App.occupiedInstance.PathText.Text = "Videos";
            }
            else
            {
                if (parameters.Equals(@"C:\") || parameters.Equals(@"c:\"))
                {
                    App.occupiedInstance.PathText.Text = @"Local Disk (C:\)";
                }
                else
                {
                    App.occupiedInstance.PathText.Text = parameters;
                }
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (App.occupiedInstance.ViewModel._fileQueryResult != null)
            {
                App.occupiedInstance.ViewModel._fileQueryResult.ContentsChanged -= App.occupiedInstance.ViewModel.FileContentsChanged;
            }
        }

        private void UnloadMenuFlyoutItemByName(string nameToUnload)
        {
            Windows.UI.Xaml.Markup.XamlMarkupHelper.UnloadObject(this.FindName(nameToUnload) as DependencyObject);
        }

        public void RightClickContextMenu_Opening(object sender, object e)
        {

            // Find selected items that are not folders
            if (App.occupiedInstance.SelectedStorageItems.Any(x => x.FileType != "Folder"))
            {
                UnloadMenuFlyoutItemByName("SidebarPinItem");
                UnloadMenuFlyoutItemByName("OpenInNewTab");
                UnloadMenuFlyoutItemByName("OpenInNewWindowItem");

                if (App.occupiedInstance.SelectedStorageItems.Count == 1)
                {
                    var selectedDataItem = App.occupiedInstance.SelectedStorageItems[0] as ListedItem;

                    if (selectedDataItem.DotFileExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        UnloadMenuFlyoutItemByName("OpenItem");
                        UnloadMenuFlyoutItemByName("UnzipItem");
                    }
                    else if (!selectedDataItem.DotFileExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        this.FindName("OpenItem");
                        UnloadMenuFlyoutItemByName("UnzipItem");
                    }
                }
                else if (App.occupiedInstance.SelectedStorageItems.Count > 1)
                {
                    UnloadMenuFlyoutItemByName("OpenItem");
                    UnloadMenuFlyoutItemByName("UnzipItem");
                }
            }
            else     // All are Folders
            {
                UnloadMenuFlyoutItemByName("OpenItem");
                if (App.occupiedInstance.SelectedStorageItems.Count <= 5 && App.occupiedInstance.SelectedStorageItems.Count > 0)
                {
                    this.FindName("SidebarPinItem");
                    this.FindName("OpenInNewTab");
                    this.FindName("OpenInNewWindowItem");
                    UnloadMenuFlyoutItemByName("UnzipItem");
                }
                else if (App.occupiedInstance.SelectedStorageItems.Count > 5)
                {
                    this.FindName("SidebarPinItem");
                    UnloadMenuFlyoutItemByName("OpenInNewTab");
                    UnloadMenuFlyoutItemByName("OpenInNewWindowItem");
                    UnloadMenuFlyoutItemByName("UnzipItem");
                }

            }
        }
    }
}
