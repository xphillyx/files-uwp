using Files.Dialogs;
using Files.Filesystem;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.System.UserProfile;
using static Files.Dialogs.ConfirmDeleteDialog;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Files.Views.Pages;
using Windows.Foundation.Metadata;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml.Hosting;
using Windows.UI.WindowManagement.Preview;
using Windows.UI;
using Files.View_Models;
using System.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Microsoft.Toolkit.Uwp.Helpers;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using GalaSoft.MvvmLight.Command;

namespace Files.Interacts
{
    public class Interaction
    {
        readonly InstanceTabsView instanceTabsView;

        public void List_ItemClick(object sender, DoubleTappedRoutedEventArgs e)
        {
            OpenSelectedItems(false);
        }

        //public async void GrantAccessPermissionHandler(IUICommand command)
        //{
        //    await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-broadfilesystemaccess"));
        //}

        public DataGrid dataGrid;

        public void AllView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            dataGrid = (DataGrid)sender;
            var RowPressed = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (RowPressed != null)
            {
                var ObjectPressed = ((ReadOnlyObservableCollection<ListedItem>)dataGrid.ItemsSource)[RowPressed.GetIndex()];

                // Check if RightTapped row is currently selected
                if (!App.CurrentInstance.ContentPage.SelectedItems.Equals(null))
                {
                    if (App.CurrentInstance.ContentPage.SelectedItems.Contains(ObjectPressed))
                        return;
                }
                
                // The following code is only reachable when a user RightTapped an unselected row
                dataGrid.SelectedItems.Clear();
                dataGrid.SelectedItems.Add(ObjectPressed);
            }

        }

        public static T FindChild<T>(DependencyObject startNode) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(startNode);
            for (int i = 0; i < count; i++)
            {
                DependencyObject current = VisualTreeHelper.GetChild(startNode, i);
                if ((current.GetType()).Equals(typeof(T)) || (current.GetType().GetTypeInfo().IsSubclassOf(typeof(T))))
                {
                    T asType = (T)current;
                    return asType;
                }
                FindChild<T>(current);
            }
            return null;
        }

        public static void FindChildren<T>(List<T> results, DependencyObject startNode) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(startNode);
            for (int i = 0; i < count; i++)
            {
                DependencyObject current = VisualTreeHelper.GetChild(startNode, i);
                if ((current.GetType()).Equals(typeof(T)) || (current.GetType().GetTypeInfo().IsSubclassOf(typeof(T))))
                {
                    T asType = (T)current;
                    results.Add(asType);
                }
                FindChildren<T>(results, current);
            }
        }

        public static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            T parent = null;
            DependencyObject CurrentParent = VisualTreeHelper.GetParent(child);
            while (CurrentParent != null)
            {
                if (CurrentParent is T)
                {
                    parent = (T)CurrentParent;
                    break;
                }
                CurrentParent = VisualTreeHelper.GetParent(CurrentParent);

            }
            return parent;
        }





        

        public async void ShowFolderPropertiesButton_Click(object sender, RoutedEventArgs e)
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
                    Interaction.AppWindows.Remove(frame.UIContext);
                    frame.Content = null;
                    appWindow = null;
                };

                await appWindow.TryShowAsync();
            }
            else
            {

                App.PropertiesDialogDisplay.propertiesFrame.Tag = App.PropertiesDialogDisplay;
                App.PropertiesDialogDisplay.propertiesFrame.Navigate(typeof(Properties), App.CurrentInstance.ViewModel.CurrentFolder, new SuppressNavigationTransitionInfo());
                await App.PropertiesDialogDisplay.ShowAsync(ContentDialogPlacement.Popup);
            }
        }





        public void RenameItem_Click(object sender, RoutedEventArgs e)
        {
            if (App.CurrentInstance.CurrentPageType == typeof(GenericFileBrowser))
            {
                var fileBrowser = App.CurrentInstance.ContentPage as GenericFileBrowser;
                if (fileBrowser.AllView.SelectedItem != null)
                    fileBrowser.AllView.CurrentColumn = fileBrowser.AllView.Columns[1];
                fileBrowser.AllView.BeginEdit();
            }
            else if (App.CurrentInstance.CurrentPageType == typeof(PhotoAlbum))
            {
                var photoAlbum = App.CurrentInstance.ContentPage as PhotoAlbum;
                photoAlbum.StartRename();
            }
        }

        public async Task<bool> RenameFileItem(ListedItem item, string oldName, string newName)
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
                        Content = "An item with this name already exists in this folder.",
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
            
            CurrentInstance.NavigationToolbar.CanGoForward = false;
            return true;
        }

        public async void CutItem_Click(object sender, RoutedEventArgs e)
        {
            DataPackage dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Move
            };
            List<IStorageItem> items = new List<IStorageItem>();
            var CurrentInstance = App.CurrentInstance;
            if ((CurrentInstance.ContentPage as BaseLayout).SelectedItems.Count != 0)
            {
                // First, reset DataGrid Rows that may be in "cut" command mode
                foreach (ListedItem listedItem in CurrentInstance.ViewModel.FilesAndFolders)
                {
                    if (App.CurrentInstance.CurrentPageType == typeof(GenericFileBrowser))
                    {
                        FrameworkElement element = (CurrentInstance.ContentPage as GenericFileBrowser).AllView.Columns[0].GetCellContent(listedItem);
                        if (element != null)
                            element.Opacity = 1;
                    }
                    else if (App.CurrentInstance.CurrentPageType == typeof(PhotoAlbum))
                    {
                        List<Grid> itemContentGrids = new List<Grid>();
                        GridViewItem gridViewItem = (CurrentInstance.ContentPage as PhotoAlbum).FileList.ContainerFromItem(listedItem) as GridViewItem;
                        if (gridViewItem == null)
                            continue;
                        FindChildren<Grid>(itemContentGrids, gridViewItem);
                        var imageOfItem = itemContentGrids.Find(x => x.Tag?.ToString() == "ItemImage");
                        imageOfItem.Opacity = 1;
                    }
                }

                foreach (ListedItem listedItem in CurrentInstance.ContentPage.SelectedItems)
                {
                    // Dim opacities accordingly
                    if (App.CurrentInstance.CurrentPageType == typeof(GenericFileBrowser))
                    {
                        (CurrentInstance.ContentPage as GenericFileBrowser).AllView.Columns[0].GetCellContent(listedItem).Opacity = 0.4;
                    } else if (App.CurrentInstance.CurrentPageType == typeof(PhotoAlbum))
                    {
                        GridViewItem itemToDimForCut = (GridViewItem)(CurrentInstance.ContentPage as PhotoAlbum).FileList.ContainerFromItem(listedItem);
                        List<Grid> itemContentGrids = new List<Grid>();
                        FindChildren<Grid>(itemContentGrids, itemToDimForCut);
                        var imageOfItem = itemContentGrids.Find(x => x.Tag?.ToString() == "ItemImage");
                        imageOfItem.Opacity = 0.4;
                    }

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
        public string CopySourcePath;
        public IReadOnlyList<IStorageItem> itemsToPaste;
        public int itemsPasted;

        public async void CopyItem_ClickAsync(object sender, RoutedEventArgs e)
        {
            DataPackage dataPackage = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            List<IStorageItem> items = new List<IStorageItem>();
            if (App.CurrentInstance.CurrentPageType == typeof(GenericFileBrowser))
            {
                var CurrentInstance = App.CurrentInstance;
                CopySourcePath = CurrentInstance.ViewModel.WorkingDirectory;

                if ((CurrentInstance.ContentPage as BaseLayout).SelectedItems.Count != 0)
                {
                    foreach (ListedItem StorItem in (CurrentInstance.ContentPage as BaseLayout).SelectedItems)
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
            }
            else if (App.CurrentInstance.CurrentPageType == typeof(PhotoAlbum))
            {
                CopySourcePath = CurrentInstance.ViewModel.WorkingDirectory;

                if ((CurrentInstance.ContentPage as BaseLayout).SelectedItems.Count != 0)
                {
                    foreach (ListedItem StorItem in (CurrentInstance.ContentPage as BaseLayout).SelectedItems)
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
            }
            if (items?.Count > 0)
            {
                dataPackage.SetStorageItems(items);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
            }

        }

        enum ImpossibleActionResponseTypes
        {
            Skip,
            Abort
        }

        public async void PasteItem_ClickAsync(object sender, RoutedEventArgs e)
        {
            DataPackageView packageView = Clipboard.GetContent();
            string destinationPath = CurrentInstance.ViewModel.WorkingDirectory;

            await PasteItems(packageView, destinationPath, packageView.RequestedOperation);
        }

        public async Task PasteItems(DataPackageView packageView, string destinationPath, DataPackageOperation acceptedOperation)
        {
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
                        if (destinationPath == CurrentInstance.ViewModel.WorkingDirectory)
                            CurrentInstance.ViewModel.AddFolder(pastedFolder.Path);
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
                    if (destinationPath == CurrentInstance.ViewModel.WorkingDirectory)
                        CurrentInstance.ViewModel.AddFile(pastedFile.Path);
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
                    ListedItem listedItem = CurrentInstance.ViewModel.FilesAndFolders.FirstOrDefault(listedItem => listedItem.ItemPath.Equals(item.Path, StringComparison.OrdinalIgnoreCase));
                    if (listedItem != null)
                        CurrentInstance.ViewModel.RemoveFileOrFolder(listedItem);
                }
            }
            if (destinationPath == CurrentInstance.ViewModel.WorkingDirectory)
            {
                List<string> pastedItemPaths = pastedItems.Select(item => item.Path).ToList();
                List<ListedItem> copiedItems = CurrentInstance.ViewModel.FilesAndFolders.Where(listedItem => pastedItemPaths.Contains(listedItem.ItemPath)).ToList();
                CurrentInstance.ContentPage.SelectedItems = copiedItems;
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
                if(itemsToPaste != null)
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

        public void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            AddItemDialog.CreateFile(AddItemType.Folder);
        }

        public void NewTextDocument_Click(object sender, RoutedEventArgs e)
        {
            AddItemDialog.CreateFile(AddItemType.TextDocument);
        }

        public void NewBitmapImage_Click(object sender, RoutedEventArgs e)
        {
            AddItemDialog.CreateFile(AddItemType.BitmapImage);
        }

        public async void ExtractItems_Click(object sender, RoutedEventArgs e)
        {
            StorageFile selectedItem = null;
            if (CurrentInstance.ContentFrame.CurrentSourcePageType == typeof(GenericFileBrowser))
            {
                var page = (CurrentInstance.ContentPage as GenericFileBrowser);
                selectedItem = await StorageFile.GetFileFromPathAsync(CurrentInstance.ViewModel.FilesAndFolders[page.AllView.SelectedIndex].ItemPath);

            }
            else if (CurrentInstance.ContentFrame.CurrentSourcePageType == typeof(PhotoAlbum))
            {
                var page = (CurrentInstance.ContentPage as PhotoAlbum);
                selectedItem = await StorageFile.GetFileFromPathAsync(CurrentInstance.ViewModel.FilesAndFolders[page.FileList.SelectedIndex].ItemPath);
            }

            ExtractFilesDialog extractFilesDialog = new ExtractFilesDialog(CurrentInstance.ViewModel.WorkingDirectory);
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

                (App.CurrentInstance.ContentPage as BaseLayout).AssociatedViewModel.LoadIndicator.IsVisible = Visibility.Visible;

                foreach (ZipArchiveEntry archiveEntry in zipArchive.Entries)
                {
                    if (archiveEntry.FullName.Contains('/'))
                    {
                        var nestedDirectories = archiveEntry.FullName.Split('/').ToList();
                        nestedDirectories.Remove(nestedDirectories.Last());
                        var relativeOutputPathToEntry = Path.Combine(nestedDirectories.ToArray());
                        System.IO.Directory.CreateDirectory(Path.Combine(destFolder_InBuffer.Path, relativeOutputPathToEntry));
                    }

                    if (!string.IsNullOrWhiteSpace(archiveEntry.Name))
                        archiveEntry.ExtractToFile(Path.Combine(destFolder_InBuffer.Path, archiveEntry.FullName));

                    index++;
                    if (index == totalCount)
                    {
                        (App.CurrentInstance.ContentPage as BaseLayout).AssociatedViewModel.LoadIndicator.IsVisible = Visibility.Collapsed;
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

        public void SelectAllItems()
        {
            if (App.CurrentInstance.CurrentPageType == typeof(GenericFileBrowser))
            {
                var CurrentInstance = App.CurrentInstance;
                foreach (ListedItem li in (CurrentInstance.ContentPage as GenericFileBrowser).AllView.ItemsSource)
                {
                    if (!(CurrentInstance.ContentPage as GenericFileBrowser).SelectedItems.Contains(li))
                    {
                        (CurrentInstance.ContentPage as GenericFileBrowser).AllView.SelectedItems.Add(li);
                    }
                }
            }
            else if (App.CurrentInstance.CurrentPageType == typeof(PhotoAlbum))
            {
                (CurrentInstance.ContentPage as PhotoAlbum).FileList.SelectAll();
            }
        }

        public void ClearAllItems()
        {
            if (App.CurrentInstance.CurrentPageType == typeof(GenericFileBrowser))
            {
                var CurrentInstance = App.CurrentInstance;
                (CurrentInstance.ContentPage as GenericFileBrowser).AllView.SelectedItems.Clear();
            }
            else if (App.CurrentInstance.CurrentPageType == typeof(PhotoAlbum))
            {
                (CurrentInstance.ContentPage as PhotoAlbum).FileList.SelectedItems.Clear();
            }
        }

        public void ToggleQuickLook_Click(object sender, RoutedEventArgs e)
        {
            ToggleQuickLook();
        }

        public async void ToggleQuickLook()
        {
            try
            {
                string selectedItemPath = null;
                int selectedItemCount;
                Type sourcePageType = App.CurrentInstance.CurrentPageType;
                selectedItemCount = (CurrentInstance.ContentPage as BaseLayout).SelectedItems.Count;
                if (selectedItemCount == 1)
                {
                    selectedItemPath = (CurrentInstance.ContentPage as BaseLayout).SelectedItems[0].ItemPath;
                }

                if (selectedItemCount == 1)
                {
                    var clickedOnItem = (CurrentInstance.ContentPage as BaseLayout).SelectedItems[0];

                    Debug.WriteLine("Toggle QuickLook");
                    ApplicationData.Current.LocalSettings.Values["path"] = clickedOnItem.ItemPath;
                    ApplicationData.Current.LocalSettings.Values["Arguments"] = "ToggleQuickLook";
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                }
            }
            catch (FileNotFoundException)
            {
                MessageDialog dialog = new MessageDialog("The file you are attempting to preview may have been moved or deleted.", "File Not Found");
                var task = dialog.ShowAsync();
                task.AsTask().Wait();
                NavigationActions.Refresh_Click(null, null);
            }
        }

        public void PushJumpChar(char letter)
        {
            App.CurrentInstance.ViewModel.JumpString += letter.ToString().ToLower();
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
