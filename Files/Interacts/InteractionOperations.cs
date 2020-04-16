using Files.Dialogs;
using Files.Filesystem;
using Files.Interacts;
using Files.Views.Pages;
using GalaSoft.MvvmLight.Command;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.UserProfile;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.WindowManagement;
using Windows.UI.WindowManagement.Preview;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Animation;

namespace Files
{
    public abstract partial class BaseLayout
    {
        public static Dictionary<UIContext, AppWindow> AppWindows { get; set; } 
            = new Dictionary<UIContext, AppWindow>();

        public async void SetItemAsDesktopBackground()
        {
            // Get the path of the selected file
            StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(SelectedItems[0].ItemPath);

            // Get the app's local folder to use as the destination folder.
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            // Copy the file to the destination folder.
            // Replace the existing file if the file already exists.
            StorageFile file = await sourceFile.CopyAsync(localFolder, "Background.png", NameCollisionOption.ReplaceExisting);

            // Set the desktop background
            UserProfilePersonalizationSettings profileSettings = UserProfilePersonalizationSettings.Current;
            await profileSettings.TrySetWallpaperImageAsync(file);
        }

        public async void OpenItems(bool displayApplicationPicker = false)
        {
            try
            {
                string selectedItemPath = null;
                int selectedItemCount;
                Type sourcePageType = App.CurrentInstance.CurrentPageType;
                selectedItemCount = SelectedItems.Count;
                if (selectedItemCount == 1)
                {
                    selectedItemPath = SelectedItems[0].ItemPath;
                }

                // Access MRU List
                var mostRecentlyUsed = Windows.Storage.AccessCache.StorageApplicationPermissions.MostRecentlyUsedList;

                if (selectedItemCount == 1)
                {
                    var clickedOnItem = SelectedItems[0];
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
                    foreach (ListedItem clickedOnItem in SelectedItems)
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

        public async void OpenItemsInNewWindow()
        {
            foreach (ListedItem listedItem in SelectedItems)
            {
                var selectedItemPath = listedItem.ItemPath;
                var folderUri = new Uri("files-uwp:" + "?folder=" + @selectedItemPath);
                await Launcher.LaunchUriAsync(folderUri);
            }
        }

        public void OpenDirectoryItemsInNewTab()
        {
            foreach (ListedItem listedItem in SelectedItems)
            {
                if (listedItem.PrimaryItemAttribute == StorageItemTypes.Folder)
                    instanceTabsView.AddNewTab(typeof(ModernShellPage), listedItem.ItemPath);
            }
        }

        public async void OpenDirectoryInTerminal()
        {
            string workingDirectory = ViewModel.WorkingDirectory;
            var localSettings = ApplicationData.Current.LocalSettings;

            var terminalId = 1;

            if (localSettings.Values["terminal_id"] != null) terminalId = (int)localSettings.Values["terminal_id"];

            var terminal = App.AppSettings.Terminals.Single(p => p.Id == terminalId);

            localSettings.Values["Application"] = terminal.Path;
            localSettings.Values["Arguments"] = String.Format(terminal.arguments, workingDirectory);

            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        public async void PinItems()
        {
            StorageFolder cacheFolder = ApplicationData.Current.LocalCacheFolder;
            var ListFile = await cacheFolder.CreateFileAsync("PinnedItems.txt", CreationCollisionOption.OpenIfExists);

            await FileIO.AppendLinesAsync(ListFile, SelectedItems.Select(x => x.ItemPath));

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

        public void CopyWorkingDirectory()
        {
            Clipboard.Clear();
            DataPackage data = new DataPackage();
            data.SetText(ViewModel.WorkingDirectory);
            Clipboard.SetContent(data);
            Clipboard.Flush();
        }


        public void InvokeShareUI()
        {
            DataTransferManager manager = DataTransferManager.GetForCurrentView();
            manager.DataRequested += new TypedEventHandler<DataTransferManager, DataRequestedEventArgs>(async (x, y) =>
            {
                DataRequestDeferral dataRequestDeferral = y.Request.GetDeferral();
                List<IStorageItem> items = new List<IStorageItem>();
                foreach (ListedItem li in SelectedItems)
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

        public async void DisplayItemProperties()
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
                App.PropertiesDialogDisplay.propertiesFrame.Navigate(typeof(Properties), SelectedItem, new SuppressNavigationTransitionInfo());
                await App.PropertiesDialogDisplay.ShowAsync(ContentDialogPlacement.Popup);
            }
        }

        public enum MyResult
        {
            Delete,
            Cancel,
            Nothing
        }

        public async void DeleteItems()
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
                foreach (ListedItem selectedItem in SelectedItems)
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


                    ViewModel.RemoveFileOrFolder(storItem);
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

        public async void DisplayCurrentFolderProperties()
        {
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                AppWindow appWindow = await AppWindow.TryCreateAsync();
                Frame frame = new Frame();
                frame.Navigate(typeof(Properties), null, new SuppressNavigationTransitionInfo());
                WindowManagementPreview.SetPreferredMinSize(appWindow, new Size(400, 475));
                appWindow.RequestSize(new Size(400, 475));
                appWindow.Title = "Properties";

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
                App.PropertiesDialogDisplay.propertiesFrame.Navigate(typeof(Properties), ViewModel.CurrentFolder, new SuppressNavigationTransitionInfo());
                await App.PropertiesDialogDisplay.ShowAsync(ContentDialogPlacement.Popup);
            }
        }

        public async Task<bool> RenameItem(ListedItem item, string oldName, string newName)
        {
            if (oldName == newName)
                return true;

            if (!string.IsNullOrWhiteSpace(newName))
            {
                try
                {
                    if (item.PrimaryItemAttribute == StorageItemTypes.Folder)
                    {
                        var folder = await StorageFolder.GetFolderFromPathAsync(item.ItemPath);
                        await folder.RenameAsync(newName, NameCollisionOption.FailIfExists);
                    }
                    else
                    {
                        var file = await StorageFile.GetFileFromPathAsync(item.ItemPath);
                        await file.RenameAsync(newName, NameCollisionOption.FailIfExists);
                    }
                }
                catch (Exception)
                {
                    var dialog = new ContentDialog()
                    {
                        Title = "Item already exists",
                        Content = "An item with this name already exists inside this folder.",
                        PrimaryButtonText = "Generate new name",
                        SecondaryButtonText = "Replace existing item"
                    };

                    ContentDialogResult result = await dialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        if (item.PrimaryItemAttribute == StorageItemTypes.Folder)
                        {
                            var folder = await StorageFolder.GetFolderFromPathAsync(item.ItemPath);

                            await folder.RenameAsync(newName, NameCollisionOption.GenerateUniqueName);
                        }
                        else
                        {
                            var file = await StorageFile.GetFileFromPathAsync(item.ItemPath);

                            await file.RenameAsync(newName, NameCollisionOption.GenerateUniqueName);
                        }
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        if (item.PrimaryItemAttribute == StorageItemTypes.Folder)
                        {
                            var folder = await StorageFolder.GetFolderFromPathAsync(item.ItemPath);

                            await folder.RenameAsync(newName, NameCollisionOption.ReplaceExisting);
                        }
                        else
                        {
                            var file = await StorageFile.GetFileFromPathAsync(item.ItemPath);

                            await file.RenameAsync(newName, NameCollisionOption.ReplaceExisting);
                        }
                    }
                }
            }

            App.CurrentInstance.NavigationToolbar.CanGoForward = false;
            return true;
        }

        public async void CutItem()
        {
            DataPackage dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Move
            };
            List<IStorageItem> items = new List<IStorageItem>();

            if (SelectedItems.Count != 0)
            {
                // First, reset DataGrid Rows that may be in "cut" command mode
                (App.CurrentInstance.ContentFrame.Content as IChildLayout).ResetDimmedItems();

                foreach (ListedItem listedItem in SelectedItems)
                {
                    // Dim opacities accordingly
                    (App.CurrentInstance.ContentFrame.Content as IChildLayout).DimItemForCut(listedItem);

                    if (listedItem.PrimaryItemAttribute == StorageItemTypes.File)
                    {
                        var item = await StorageFile.GetFileFromPathAsync(listedItem.ItemPath);
                        items.Add(item);
                    }
                    else
                    {
                        var item = await StorageFolder.GetFolderFromPathAsync(listedItem.ItemPath);
                        items.Add(item);
                    }
                }
            }
            dataPackage.SetStorageItems(items);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }

        enum ImpossibleActionResponseTypes
        {
            Skip,
            Abort
        }

        public IReadOnlyList<IStorageItem> itemsToPaste;
        public int itemsPasted;
        public async Task PasteItems(DataPackageView packageView = null)
        {
            if (packageView == null)
            {
                packageView = Clipboard.GetContent();
            }
            string destinationPath = ViewModel.WorkingDirectory;
            var acceptedOperation = packageView.RequestedOperation;
            itemsToPaste = await packageView.GetStorageItemsAsync();
            HashSet<IStorageItem> pastedItems = new HashSet<IStorageItem>();
            itemsPasted = 0;
            if (itemsToPaste.Count > 3)
            {
                (App.CurrentInstance as ModernShellPage).UpdateProgressFlyout(InteractionOperationType.PasteItems, itemsPasted, itemsToPaste.Count);
            }

            foreach (IStorageItem item in itemsToPaste)
            {
                if (item.IsOfType(StorageItemTypes.Folder))
                {
                    if (destinationPath.Contains(item.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        ImpossibleActionResponseTypes responseType = ImpossibleActionResponseTypes.Abort;
                        ContentDialog dialog = new ContentDialog()
                        {
                            Title = ResourceController.GetTranslation("ErrorDialogThisActionCannotBeDone"),
                            Content = ResourceController.GetTranslation("ErrorDialogTheDestinationFolder") + " (" + destinationPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Last() + ") " + ResourceController.GetTranslation("ErrorDialogIsASubfolder") + " (" + item.Name + ")",
                            PrimaryButtonText = ResourceController.GetTranslation("ErrorDialogSkip"),
                            CloseButtonText = ResourceController.GetTranslation("ErrorDialogCancel"),
                            PrimaryButtonCommand = new RelayCommand(() => { responseType = ImpossibleActionResponseTypes.Skip; }),
                            CloseButtonCommand = new RelayCommand(() => { responseType = ImpossibleActionResponseTypes.Abort; })
                        };
                        await dialog.ShowAsync();
                        if (responseType == ImpossibleActionResponseTypes.Skip)
                        {
                            continue;
                        }
                        else if (responseType == ImpossibleActionResponseTypes.Abort)
                        {
                            return;
                        }
                    }
                    else
                    {
                        StorageFolder pastedFolder = await CloneDirectoryAsync(item.Path, destinationPath, item.Name, false);
                        pastedItems.Add(pastedFolder);
                        if (destinationPath == ViewModel.WorkingDirectory)
                            ViewModel.AddFolder(pastedFolder.Path);
                    }
                }
                else if (item.IsOfType(StorageItemTypes.File))
                {
                    if (itemsToPaste.Count > 3)
                    {
                        (App.CurrentInstance as ModernShellPage).UpdateProgressFlyout(InteractionOperationType.PasteItems, ++itemsPasted, itemsToPaste.Count);
                    }
                    StorageFile clipboardFile = await StorageFile.GetFileFromPathAsync(item.Path);
                    StorageFile pastedFile = await clipboardFile.CopyAsync(await StorageFolder.GetFolderFromPathAsync(destinationPath), item.Name, NameCollisionOption.GenerateUniqueName);
                    pastedItems.Add(pastedFile);
                    if (destinationPath == ViewModel.WorkingDirectory)
                        ViewModel.AddFile(pastedFile.Path);
                }
            }

            if (acceptedOperation == DataPackageOperation.Move)
            {
                foreach (IStorageItem item in itemsToPaste)
                {
                    if (item.IsOfType(StorageItemTypes.File))
                    {
                        StorageFile file = await StorageFile.GetFileFromPathAsync(item.Path);
                        await file.DeleteAsync();
                    }
                    else if (item.IsOfType(StorageItemTypes.Folder))
                    {
                        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(item.Path);
                        await folder.DeleteAsync();
                    }
                    ListedItem listedItem = ViewModel.FilesAndFolders.FirstOrDefault(listedItem => listedItem.ItemPath.Equals(item.Path, StringComparison.OrdinalIgnoreCase));
                    if (listedItem != null)
                        ViewModel.RemoveFileOrFolder(listedItem);
                }
            }
            if (destinationPath == ViewModel.WorkingDirectory)
            {
                List<string> pastedItemPaths = pastedItems.Select(item => item.Path).ToList();
                List<ListedItem> copiedItems = ViewModel.FilesAndFolders.Where(listedItem => pastedItemPaths.Contains(listedItem.ItemPath)).ToList();
                SelectedItems = copiedItems;
            }
            packageView.ReportOperationCompleted(acceptedOperation);
        }

        public async Task<StorageFolder> CloneDirectoryAsync(string SourcePath, string DestinationPath, string sourceRootName, bool suppressProgressFlyout)
        {
            StorageFolder SourceFolder = await StorageFolder.GetFolderFromPathAsync(SourcePath);
            StorageFolder DestinationFolder = await StorageFolder.GetFolderFromPathAsync(DestinationPath);
            var createdRoot = await DestinationFolder.CreateFolderAsync(sourceRootName, CreationCollisionOption.GenerateUniqueName);
            DestinationFolder = await StorageFolder.GetFolderFromPathAsync(createdRoot.Path);

            foreach (StorageFile fileInSourceDir in await SourceFolder.GetFilesAsync())
            {
                if (itemsToPaste != null)
                {
                    if (itemsToPaste.Count > 3 && !suppressProgressFlyout)
                    {
                        (App.CurrentInstance as ModernShellPage).UpdateProgressFlyout(InteractionOperationType.PasteItems, ++itemsPasted, itemsToPaste.Count + (await SourceFolder.GetItemsAsync()).Count);
                    }
                }

                await fileInSourceDir.CopyAsync(DestinationFolder, fileInSourceDir.Name, NameCollisionOption.GenerateUniqueName);
            }
            foreach (StorageFolder folderinSourceDir in await SourceFolder.GetFoldersAsync())
            {
                if (itemsToPaste != null)
                {
                    if (itemsToPaste.Count > 3 && !suppressProgressFlyout)
                    {
                        (App.CurrentInstance as ModernShellPage).UpdateProgressFlyout(InteractionOperationType.PasteItems, ++itemsPasted, itemsToPaste.Count + (await SourceFolder.GetItemsAsync()).Count);
                    }
                }

                await CloneDirectoryAsync(folderinSourceDir.Path, DestinationFolder.Path, folderinSourceDir.Name, false);
            }
            return createdRoot;
        }

        public async void CreateFile(AddItemResultType fileType)
        {
            string currentPath = ViewModel.WorkingDirectory;

            StorageFolder folderToCreateItem = await StorageFolder.GetFolderFromPathAsync(currentPath);
            RenameDialog renameDialog = new RenameDialog();

            await renameDialog.ShowAsync();
            var userInput = renameDialog.storedRenameInput;

            if (fileType == AddItemResultType.Folder)
            {
                StorageFolder folder;
                if (!string.IsNullOrWhiteSpace(userInput))
                {
                    folder = await folderToCreateItem.CreateFolderAsync(userInput, CreationCollisionOption.GenerateUniqueName);
                }
                else
                {
                    folder = await folderToCreateItem.CreateFolderAsync(ResourceController.GetTranslation("NewFolder"), CreationCollisionOption.GenerateUniqueName);
                }
                ViewModel.AddFileOrFolder(new ListedItem(folder.FolderRelativeId) { PrimaryItemAttribute = StorageItemTypes.Folder, ItemName = folder.DisplayName, ItemDateModifiedReal = DateTimeOffset.Now, LoadUnknownTypeGlyph = false, LoadFolderGlyph = true, LoadFileIcon = false, ItemType = "Folder", FileImage = null, ItemPath = folder.Path });
            }
            else if (fileType == AddItemResultType.TextDocument)
            {
                StorageFile item;
                if (!string.IsNullOrWhiteSpace(userInput))
                {
                    item = await folderToCreateItem.CreateFileAsync(userInput + ".txt", CreationCollisionOption.GenerateUniqueName);
                }
                else
                {
                    item = await folderToCreateItem.CreateFileAsync(ResourceController.GetTranslation("NewTextDocument") + ".txt", CreationCollisionOption.GenerateUniqueName);
                }
                ViewModel.AddFileOrFolder(new ListedItem(item.FolderRelativeId) { PrimaryItemAttribute = StorageItemTypes.File, ItemName = item.DisplayName, ItemDateModifiedReal = DateTimeOffset.Now, LoadUnknownTypeGlyph = true, LoadFolderGlyph = false, LoadFileIcon = false, ItemType = item.DisplayType, FileImage = null, ItemPath = item.Path, FileExtension = item.FileType });
            }
            else if (fileType == AddItemResultType.BitmapImage)
            {
                StorageFile item;
                if (!string.IsNullOrWhiteSpace(userInput))
                {
                    item = await folderToCreateItem.CreateFileAsync(userInput + ".bmp", CreationCollisionOption.GenerateUniqueName);
                }
                else
                {
                    item = await folderToCreateItem.CreateFileAsync(ResourceController.GetTranslation("NewBitmapImage") + ".bmp", CreationCollisionOption.GenerateUniqueName);
                }
                ViewModel.AddFileOrFolder(new ListedItem(item.FolderRelativeId) { PrimaryItemAttribute = StorageItemTypes.File, ItemName = item.DisplayName, ItemDateModifiedReal = DateTimeOffset.Now, LoadUnknownTypeGlyph = true, LoadFolderGlyph = false, LoadFileIcon = false, ItemType = item.DisplayType, FileImage = null, ItemPath = item.Path, FileExtension = item.FileType });
            }
        }

        public async void ShowAddItemDialog()
        {
            var dialog = App.AddItemDialogDisplay;
            await dialog.ShowAsync();
            if (dialog.Result != AddItemResultType.Nothing)
            {
                CreateFile(dialog.Result);
            }
        }

        public async void CopyItem()
        {
            DataPackage dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            List<IStorageItem> items = new List<IStorageItem>();

            if (SelectedItems.Count != 0)
            {
                foreach (ListedItem StorItem in SelectedItems)
                {
                    if (StorItem.PrimaryItemAttribute == StorageItemTypes.File)
                    {
                        var item = await StorageFile.GetFileFromPathAsync(StorItem.ItemPath);
                        items.Add(item);
                    }
                    else
                    {
                        var item = await StorageFolder.GetFolderFromPathAsync(StorItem.ItemPath);
                        items.Add(item);
                    }
                }
            }

            if (items?.Count > 0)
            {
                dataPackage.SetStorageItems(items);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
            }
        }

        public async void ExtractItems()
        {
            StorageFile selectedItem = await StorageFile.GetFileFromPathAsync(SelectedItem.ItemPath);

            ExtractFilesDialog extractFilesDialog = new ExtractFilesDialog(ViewModel.WorkingDirectory);
            await extractFilesDialog.ShowAsync();
            if (((bool)ApplicationData.Current.LocalSettings.Values["Extract_Destination_Cancelled"]) == false)
            {
                var bufferItem = await selectedItem.CopyAsync(ApplicationData.Current.TemporaryFolder, selectedItem.DisplayName, NameCollisionOption.ReplaceExisting);
                string destinationPath = ApplicationData.Current.LocalSettings.Values["Extract_Destination_Path"].ToString();
                //ZipFile.ExtractToDirectory(selectedItem.Path, destinationPath, );
                var destFolder_InBuffer = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(selectedItem.DisplayName + "_Extracted", CreationCollisionOption.ReplaceExisting);
                using FileStream fs = new FileStream(bufferItem.Path, FileMode.Open);
                ZipArchive zipArchive = new ZipArchive(fs);
                int totalCount = zipArchive.Entries.Count;
                int index = 0;

                ViewModel.LoadIndicator.IsVisible = Visibility.Visible;

                foreach (ZipArchiveEntry archiveEntry in zipArchive.Entries)
                {
                    if (archiveEntry.FullName.Contains('/'))
                    {
                        var nestedDirectories = archiveEntry.FullName.Split('/').ToList();
                        nestedDirectories.Remove(nestedDirectories.Last());
                        var relativeOutputPathToEntry = Path.Combine(nestedDirectories.ToArray());
                        Directory.CreateDirectory(Path.Combine(destFolder_InBuffer.Path, relativeOutputPathToEntry));
                    }

                    if (!string.IsNullOrWhiteSpace(archiveEntry.Name))
                        archiveEntry.ExtractToFile(Path.Combine(destFolder_InBuffer.Path, archiveEntry.FullName));

                    index++;
                    if (index == totalCount)
                    {
                        ViewModel.LoadIndicator.IsVisible = Visibility.Collapsed;
                    }
                }
                await CloneDirectoryAsync(destFolder_InBuffer.Path, destinationPath, destFolder_InBuffer.Name, true)
                    .ContinueWith(async (x) =>
                    {
                        await destFolder_InBuffer.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            Frame rootFrame = Window.Current.Content as Frame;
                            var instanceTabsView = rootFrame.Content as InstanceTabsView;
                            instanceTabsView.AddNewTab(typeof(ModernShellPage), destinationPath + "\\" + selectedItem.DisplayName + "_Extracted");
                        });
                    });
            }
            else if (((bool)ApplicationData.Current.LocalSettings.Values["Extract_Destination_Cancelled"]) == true)
            {
                return;
            }
        }

        public async void ToggleQuickLook()
        {
            try
            { 
                if (SelectedItems.Count == 1)
                {
                    Debug.WriteLine("Toggle QuickLook");
                    ApplicationData.Current.LocalSettings.Values["path"] = SelectedItems[0].ItemPath;
                    ApplicationData.Current.LocalSettings.Values["Arguments"] = "ToggleQuickLook";
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                }
            }
            catch (FileNotFoundException)
            {
                MessageDialog dialog = new MessageDialog("The file you are attempting to preview may have been moved or deleted.", "File Not Found");
                await dialog.ShowAsync();
            }
        }

        public void PushJumpChar(char letter)
        {
            ViewModel.JumpString += letter.ToString().ToLower();
        }

        public async Task<string> GetHashForFile(ListedItem fileItem, string nameOfAlg)
        {
            HashAlgorithmProvider algorithmProvider = HashAlgorithmProvider.OpenAlgorithm(nameOfAlg);
            CryptographicHash objHash = algorithmProvider.CreateHash();
            var itemFromPath = await StorageFile.GetFileFromPathAsync(fileItem.ItemPath);
            var fileBytes = await StorageFileHelper.ReadBytesAsync(itemFromPath);

            IBuffer buffer = CryptographicBuffer.CreateFromByteArray(fileBytes);
            objHash.Append(buffer);
            IBuffer bufferHash = objHash.GetValueAndReset();

            return CryptographicBuffer.EncodeToHexString(bufferHash);
        }
    }
}
