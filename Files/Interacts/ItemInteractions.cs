using Files.Dialogs;
using Files.Filesystem;
using Files.Views.Pages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.System;
using Windows.System.UserProfile;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.WindowManagement;
using Windows.UI.WindowManagement.Preview;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Animation;

namespace Files.Interacts
{
    public abstract class InteractionOperationsBase
    {
        public static Dictionary<UIContext, AppWindow> AppWindows { get; set; }
    = new Dictionary<UIContext, AppWindow>();
        readonly InstanceTabsView instanceTabsView;
        public InteractionOperationsBase()
        {
            instanceTabsView = (Window.Current.Content as Frame).Content as InstanceTabsView;
        }
        public async void SetItemAsDesktopBackground(List<ListedItem> items)
        {
            // Get the path of the selected file
            StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(items[0].ItemPath);

            // Get the app's local folder to use as the destination folder.
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            // Copy the file to the destination folder.
            // Replace the existing file if the file already exists.
            StorageFile file = await sourceFile.CopyAsync(localFolder, "Background.png", NameCollisionOption.ReplaceExisting);

            // Set the desktop background
            UserProfilePersonalizationSettings profileSettings = UserProfilePersonalizationSettings.Current;
            await profileSettings.TrySetWallpaperImageAsync(file);
        }

        public async void OpenItems(List<ListedItem> itemsToOpen, bool displayApplicationPicker = false)
        {
            try
            {
                string selectedItemPath = null;
                int selectedItemCount;
                Type sourcePageType = App.CurrentInstance.CurrentPageType;
                selectedItemCount = itemsToOpen.Count;
                if (selectedItemCount == 1)
                {
                    selectedItemPath = itemsToOpen[0].ItemPath;
                }

                // Access MRU List
                var mostRecentlyUsed = Windows.Storage.AccessCache.StorageApplicationPermissions.MostRecentlyUsedList;

                if (selectedItemCount == 1)
                {
                    var clickedOnItem = itemsToOpen[0];
                    if (clickedOnItem.PrimaryItemAttribute == StorageItemTypes.Folder)
                    {
                        // Add location to MRU List
                        mostRecentlyUsed.Add(await StorageFolder.GetFolderFromPathAsync(selectedItemPath));
                        App.CurrentInstance.ContentFrame.Navigate(sourcePageType, selectedItemPath, new SuppressNavigationTransitionInfo());
                    }
                    else
                    {
                        // Add location to MRU List
                        mostRecentlyUsed.Add(await StorageFile.GetFileFromPathAsync(clickedOnItem.ItemPath));
                        if (displayApplicationPicker)
                        {
                            StorageFile file = await StorageFile.GetFileFromPathAsync(clickedOnItem.ItemPath);
                            var options = new LauncherOptions
                            {
                                DisplayApplicationPicker = true
                            };
                            await Launcher.LaunchFileAsync(file, options);
                        }
                        else
                        {
                            await InvokeWin32Component(clickedOnItem.ItemPath);
                        }
                    }
                }
                else if (selectedItemCount > 1)
                {
                    foreach (ListedItem clickedOnItem in itemsToOpen)
                    {
                        if (clickedOnItem.PrimaryItemAttribute == StorageItemTypes.Folder)
                        {
                            instanceTabsView.AddNewTab(typeof(ModernShellPage), clickedOnItem.ItemPath);
                        }
                        else
                        {
                            // Add location to MRU List
                            mostRecentlyUsed.Add(await StorageFile.GetFileFromPathAsync(clickedOnItem.ItemPath));
                            if (displayApplicationPicker)
                            {
                                StorageFile file = await StorageFile.GetFileFromPathAsync(clickedOnItem.ItemPath);
                                var options = new LauncherOptions
                                {
                                    DisplayApplicationPicker = true
                                };
                                await Launcher.LaunchFileAsync(file, options);
                            }
                            else
                            {
                                await InvokeWin32Component(clickedOnItem.ItemPath);
                            }
                        }
                    }
                }
            }
            catch (FileNotFoundException)
            {
                MessageDialog dialog = new MessageDialog("The file you are attempting to access may have been moved or deleted.", "File Not Found");
                await dialog.ShowAsync();
            }
        }

        public async void OpenItemsInNewWindow(List<ListedItem> items)
        {
            foreach (ListedItem listedItem in items)
            {
                var selectedItemPath = listedItem.ItemPath;
                var folderUri = new Uri("files-uwp:" + "?folder=" + @selectedItemPath);
                await Launcher.LaunchUriAsync(folderUri);
            }
        }

        public void OpenDirectoryItemsInNewTab(List<ListedItem> items)
        {
            foreach (ListedItem listedItem in items)
            {
                if (listedItem.PrimaryItemAttribute == StorageItemTypes.Folder)
                    instanceTabsView.AddNewTab(typeof(ModernShellPage), listedItem.ItemPath);
            }
        }

        public async void OpenDirectoryInTerminal(string workingDirectory)
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            var terminalId = 1;

            if (localSettings.Values["terminal_id"] != null) terminalId = (int)localSettings.Values["terminal_id"];

            var terminal = App.AppSettings.Terminals.Single(p => p.Id == terminalId);

            localSettings.Values["Application"] = terminal.Path;
            localSettings.Values["Arguments"] = String.Format(terminal.arguments, workingDirectory);

            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        public async void PinItems(List<ListedItem> itemsToPin)
        {
            StorageFolder cacheFolder = ApplicationData.Current.LocalCacheFolder;
            var ListFile = await cacheFolder.CreateFileAsync("PinnedItems.txt", CreationCollisionOption.OpenIfExists);
            
            await FileIO.AppendLinesAsync(ListFile, itemsToPin.Select(x => x.ItemPath));

            foreach (string itemPath in await FileIO.ReadLinesAsync(ListFile))
            {
                try
                {
                    StorageFolder fol = await StorageFolder.GetFolderFromPathAsync(itemPath);
                    var name = fol.DisplayName;
                    var content = name;
                    var icon = "\uE8B7";

                    bool isDuplicate = false;
                    foreach (INavigationControlItem sbi in App.sideBarItems)
                    {
                        if (sbi is LocationItem)
                        {
                            if (!string.IsNullOrWhiteSpace(sbi.Path) && !(sbi as LocationItem).IsDefaultLocation)
                            {
                                if (sbi.Path.ToString() == itemPath)
                                {
                                    isDuplicate = true;
                                }
                            }
                        }
                    }

                    if (!isDuplicate)
                    {
                        int insertIndex = App.sideBarItems.IndexOf(App.sideBarItems.Last(x => x.ItemType == NavigationControlItemType.Location)) + 1;
                        App.sideBarItems.Insert(insertIndex, new LocationItem { Path = itemPath, Glyph = icon, IsDefaultLocation = false, Text = content });
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.WriteLine(ex.Message);
                }
                catch (FileNotFoundException ex)
                {
                    Debug.WriteLine("Pinned item was deleted and will be removed from the file lines list soon: " + ex.Message);
                    App.AppSettings.LinesToRemoveFromFile.Add(itemPath);
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    Debug.WriteLine("Pinned item's drive was ejected and will be removed from the file lines list soon: " + ex.Message);
                    App.AppSettings.LinesToRemoveFromFile.Add(itemPath);
                }
            }
            App.AppSettings.RemoveStaleSidebarItems();
        }

        public static async Task InvokeWin32Component(string ApplicationPath)
        {
            Debug.WriteLine("Launching EXE in FullTrustProcess");
            ApplicationData.Current.LocalSettings.Values["Application"] = ApplicationPath;
            ApplicationData.Current.LocalSettings.Values["Arguments"] = null;
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        public void CopyWorkingDirectory(string workingDir)
        {
            Clipboard.Clear();
            DataPackage data = new DataPackage();
            data.SetText(workingDir);
            Clipboard.SetContent(data);
            Clipboard.Flush();
        }

        public abstract void ItemViewControl_RightTapped();

        public void InvokeShareUI(List<ListedItem> items)
        {
            DataTransferManager manager = DataTransferManager.GetForCurrentView();
            manager.DataRequested += new TypedEventHandler<DataTransferManager, DataRequestedEventArgs>(async (x, y) => 
            {
                DataRequestDeferral dataRequestDeferral = y.Request.GetDeferral();
                List<IStorageItem> items = new List<IStorageItem>();
                foreach (ListedItem li in items)
                {
                    if (li.PrimaryItemAttribute == StorageItemTypes.Folder)
                    {
                        var folderAsItem = await StorageFolder.GetFolderFromPathAsync(li.ItemPath);
                        items.Add(folderAsItem);
                    }
                    else
                    {
                        var fileAsItem = await StorageFile.GetFileFromPathAsync(li.ItemPath);
                        items.Add(fileAsItem);
                    }
                }

                DataRequest dataRequest = y.Request;
                dataRequest.Data.SetStorageItems(items);
                dataRequest.Data.Properties.Title = "Data Shared From Files";
                dataRequest.Data.Properties.Description = "The items you selected will be shared";
                dataRequestDeferral.Complete();
            });
            DataTransferManager.ShowShareUI();
        }

        public async void DisplayItemProperties(ListedItem item)
        {
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                AppWindow appWindow = await AppWindow.TryCreateAsync();
                Frame frame = new Frame();
                frame.Navigate(typeof(Properties), null, new SuppressNavigationTransitionInfo());
                WindowManagementPreview.SetPreferredMinSize(appWindow, new Size(400, 475));
                appWindow.RequestSize(new Size(400, 475));
                appWindow.Title = "Properties"; // TODO: Localize this

                ElementCompositionPreview.SetAppWindowContent(appWindow, frame);
                AppWindows.Add(frame.UIContext, appWindow);

                appWindow.Closed += delegate
                {
                    AppWindows.Remove(frame.UIContext);
                    frame.Content = null;
                    appWindow = null;
                };

                await appWindow.TryShowAsync();
            }
            else
            {
                App.PropertiesDialogDisplay.propertiesFrame.Tag = App.PropertiesDialogDisplay;
                App.PropertiesDialogDisplay.propertiesFrame.Navigate(typeof(Properties), item, new SuppressNavigationTransitionInfo());
                await App.PropertiesDialogDisplay.ShowAsync(ContentDialogPlacement.Popup);
            }
        }

        public enum MyResult
        {
            Delete,
            Cancel,
            Nothing
        }

        private void ConfirmDeleteItems(List<ListedItem> deletedItems, ref ObservableCollection<ListedItem> itemsCollection)
        {
            try
            {
                if (storItem.PrimaryItemAttribute == StorageItemTypes.File)
                {
                    var item = await StorageFile.GetFileFromPathAsync(storItem.ItemPath);

                    if (App.InteractionViewModel.PermanentlyDelete)
                    {
                        await item.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                    else
                    {
                        await item.DeleteAsync(StorageDeleteOption.Default);
                    }
                }
                else
                {
                    var item = await StorageFolder.GetFolderFromPathAsync(storItem.ItemPath);

                    if (App.InteractionViewModel.PermanentlyDelete)
                    {
                        await item.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                    else
                    {
                        await item.DeleteAsync(StorageDeleteOption.Default);
                    }
                }
            }
            catch (FileLoadException)
            {
                // try again
                if (storItem.PrimaryItemAttribute == StorageItemTypes.File)
                {
                    var item = await StorageFile.GetFileFromPathAsync(storItem.ItemPath);

                    if (App.InteractionViewModel.PermanentlyDelete)
                    {
                        await item.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                    else
                    {
                        await item.DeleteAsync(StorageDeleteOption.Default);
                    }
                }
                else
                {
                    var item = await StorageFolder.GetFolderFromPathAsync(storItem.ItemPath);

                    if (App.InteractionViewModel.PermanentlyDelete)
                    {
                        await item.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                    else
                    {
                        await item.DeleteAsync(StorageDeleteOption.Default);
                    }
                }
            }
        }

        public async void DeleteItems(List<ListedItem> items)
        {
            if (App.AppSettings.ShowConfirmDeleteDialog == true) //check if the setting to show a confirmation dialog is on
            {
                var dialog = new ConfirmDeleteDialog();
                await dialog.ShowAsync();

                if (dialog.Result != MyResult.Delete) //delete selected  item(s) if the result is yes
                {
                    return; //return if the result isn't delete
                }
            }

            try
            {
                List<ListedItem> selectedItems = new List<ListedItem>();
                foreach (ListedItem selectedItem in items)
                {
                    selectedItems.Add(selectedItem);
                }

                int itemsDeleted = 0;
                if (selectedItems.Count > 3)
                {
                    (App.CurrentInstance as ModernShellPage).UpdateProgressFlyout(InteractionOperationType.DeleteItems, itemsDeleted, selectedItems.Count);
                }

                foreach (ListedItem storItem in selectedItems)
                {
                    if (selectedItems.Count > 3) { (App.CurrentInstance as ModernShellPage).UpdateProgressFlyout(InteractionOperationType.DeleteItems, ++itemsDeleted, selectedItems.Count); }

                    

                    CurrentInstance.ViewModel.RemoveFileOrFolder(storItem);
                }
                App.CurrentInstance.NavigationToolbar.CanGoForward = false;

            }
            catch (UnauthorizedAccessException)
            {
                MessageDialog AccessDeniedDialog = new MessageDialog("Access Denied", "Unable to delete this item");
                await AccessDeniedDialog.ShowAsync();
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine("Attention: Tried to delete an item that could be found");
            }

            App.InteractionViewModel.PermanentlyDelete = false; //reset PermanentlyDelete flag
        }
    }
}


