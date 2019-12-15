using ByteSizeLib;
using Files.Enums;
using Files.Interacts;
using Files.Navigation;
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace Files.Filesystem
{
    public class ItemViewModel : INotifyPropertyChanged
    {
        public EmptyFolderTextState EmptyTextState { get; set; } = new EmptyFolderTextState();
        public LoadingIndicator LoadIndicator { get; set; } = new LoadingIndicator();
        public ReadOnlyObservableCollection<ListedItem> FilesAndFolders { get; }
        public CollectionViewSource viewSource;
        private ObservableCollection<ListedItem> _filesAndFolders;
        private StorageFolderQueryResult _folderQueryResult;
        public StorageFileQueryResult _fileQueryResult;
        private CancellationTokenSource _cancellationTokenSource;
        private StorageFolder _rootFolder;
        private QueryOptions _options;
        private volatile bool _filesRefreshing;
        private const int _step = 250;
        public event PropertyChangedEventHandler PropertyChanged;



        public ItemViewModel()
        {
            _filesAndFolders = new ObservableCollection<ListedItem>();
            FilesAndFolders = new ReadOnlyObservableCollection<ListedItem>(_filesAndFolders);
            App.occupiedInstance.HomeItems.PropertyChanged += HomeItems_PropertyChanged;
            App.occupiedInstance.ShareItems.PropertyChanged += ShareItems_PropertyChanged;
            App.occupiedInstance.LayoutItems.PropertyChanged += LayoutItems_PropertyChanged;
            App.occupiedInstance.AlwaysPresentCommands.PropertyChanged += AlwaysPresentCommands_PropertyChanged;
            _cancellationTokenSource = new CancellationTokenSource();

            App.occupiedInstance.WorkingDirectory.PropertyChanged += Universal_PropertyChanged;
        }

        /*
         * Ensure that the path bar gets updated for user interaction
         * whenever the path changes. We will get the individual directories from
         * the updated, most-current path and add them to the UI.
         */

        private void Universal_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Clear the path UI
            App.occupiedInstance.PathBoxItems.Clear();
            // Style tabStyleFixed = App.selectedTabInstance.accessiblePathTabView.Resources["PathSectionTabStyle"] as Style;
            FontWeight weight = new FontWeight()
            {
                Weight = FontWeights.SemiBold.Weight
            };
            List<string> pathComponents = new List<string>();
            if (e.PropertyName == "path")
            {
                // If path is a library, simplify it

                // If path is found to not be a library
                pathComponents =  App.occupiedInstance.WorkingDirectory.Path.Split("\\", StringSplitOptions.RemoveEmptyEntries).ToList();
                int index = 0;
                foreach(string s in pathComponents)
                {
                    string componentLabel = null;
                    string tag = "";
                    if (s.Contains(":"))
                    {
                        if (s == @"C:" || s == @"c:")
                        {
                            componentLabel = @"Local Disk (C:\)";
                        }
                        else
                        {
                            componentLabel = @"Drive (" + s + @"\)";
                        }
                        tag = s + @"\";

                        PathBoxItem item = new PathBoxItem()
                        {
                            Title = componentLabel,
                            Path = tag,
                        };
                        App.occupiedInstance.PathBoxItems.Add(item);
                    }
                    else
                    {

                        componentLabel = s;
                        foreach (string part in pathComponents.GetRange(0, index + 1))
                        {
                            tag = tag + part + @"\";
                        }
                        
                        tag = "\\\\" + tag;

                        PathBoxItem item = new PathBoxItem()
                        {
                            Title = componentLabel,
                            Path = tag,
                        };
                        App.occupiedInstance.PathBoxItems.Add(item);

                    }
                    index++;
                }
            }
        }

        private void AlwaysPresentCommands_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(App.occupiedInstance.AlwaysPresentCommands.isEnabled == true)
            {
                App.occupiedInstance.AlwaysPresentCommands.isEnabled = true;
            }
            else
            {
                App.occupiedInstance.AlwaysPresentCommands.isEnabled = false;
            }
        }

        private void LayoutItems_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (App.occupiedInstance.LayoutItems.isEnabled == true)
            {
                App.occupiedInstance.LayoutItems.isEnabled = true;
            }
            else
            {
                App.occupiedInstance.LayoutItems.isEnabled = false;
            }
        }

        private void ShareItems_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (App.occupiedInstance.ShareItems.isEnabled == true)
            {
                App.occupiedInstance.ShareItems.isEnabled = true;
            }
            else
            {
                App.occupiedInstance.ShareItems.isEnabled = false;
            }
        }

        private void HomeItems_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (App.occupiedInstance.HomeItems.isEnabled == true)
            {
                App.occupiedInstance.HomeItems.isEnabled = true;
            }
            else
            {
                App.occupiedInstance.HomeItems.isEnabled = false;
            }

        }

        public void AddFileOrFolder(ListedItem item)
        {
            _filesAndFolders.Add(item);
            EmptyTextState.isVisible = Visibility.Collapsed;
        }

        public void RemoveFileOrFolder(ListedItem item)
        {
            _filesAndFolders.Remove(item);
            if (_filesAndFolders.Count == 0)
            {
                EmptyTextState.isVisible = Visibility.Visible;
            }
        }

        public void CancelLoadAndClearFiles()
        {
            if (isLoadingItems == false) { return; }

            _cancellationTokenSource.Cancel();
            _filesAndFolders.Clear();
            //_folderQueryResult.ContentsChanged -= FolderContentsChanged;
            if (_fileQueryResult != null)
            {
                _fileQueryResult.ContentsChanged -= FileContentsChanged;
            }
            App.occupiedInstance.BackButtonState = true;
            App.occupiedInstance.ForwardButtonState = true;
            App.occupiedInstance.UpButtonState = true;

        }

        public void OrderFiles()
        {
            if (_filesAndFolders.Count == 0)
                return;

            object orderByNameFunc(ListedItem item) => item.FileName;
            Func<ListedItem, object> orderFunc = orderByNameFunc;
            switch (App.occupiedInstance.DirectorySortOption)
            {
                case SortOption.Name:
                    orderFunc = orderByNameFunc;
                    break;
                case SortOption.DateModified:
                    orderFunc = item => item.FileDateReal;
                    break;
                case SortOption.FileType:
                    orderFunc = item => item.FileType;
                    break;
                case SortOption.Size:
                    orderFunc = item => item.FileSizeBytes;
                    break;
            }

            // In ascending order, show folders first, then files.
            // So, we use != "Folder" to make the value for "Folder" = 0, and for the rest, 1.
            Func<ListedItem, bool> folderThenFile = listedItem => listedItem.FileType != "Folder";
            IOrderedEnumerable<ListedItem> ordered;
            List<ListedItem> orderedList;

            if (App.occupiedInstance.DirectorySortDirection == SortDirection.Ascending)
                ordered = _filesAndFolders.OrderBy(folderThenFile).ThenBy(orderFunc);
            else
            {
                if (App.occupiedInstance.DirectorySortOption == SortOption.FileType)
                    ordered = _filesAndFolders.OrderBy(folderThenFile).ThenByDescending(orderFunc);
                else
                    ordered = _filesAndFolders.OrderByDescending(folderThenFile).ThenByDescending(orderFunc);
            }

            // Further order by name if applicable
            if (App.occupiedInstance.DirectorySortOption != SortOption.Name)
            {
                if (App.occupiedInstance.DirectorySortDirection == SortDirection.Ascending)
                    ordered = ordered.ThenBy(orderByNameFunc);
                else
                    ordered = ordered.ThenByDescending(orderByNameFunc);
            }
            orderedList = ordered.ToList();
            _filesAndFolders.Clear();
            foreach (ListedItem i in orderedList)
                _filesAndFolders.Add(i);
        }

        bool isLoadingItems = false;
        public async void AddItemsToCollectionAsync(string path)
        {
            App.occupiedInstance.RefreshButtonState = false;

            Frame rootFrame = Window.Current.Content as Frame;
            var instanceTabsView = rootFrame.Content as InstanceTabsView;
            instanceTabsView.SetSelectedTabInfo(new DirectoryInfo(path).Name, path);
            CancelLoadAndClearFiles();

            isLoadingItems = true;
            EmptyTextState.isVisible = Visibility.Collapsed;
            App.occupiedInstance.WorkingDirectory.Path = path;
            _filesAndFolders.Clear();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            LoadIndicator.isVisible = Visibility.Visible;

            switch (App.occupiedInstance.WorkingDirectory.Path)
            {
                case "Desktop":
                    App.occupiedInstance.WorkingDirectory.Path = App.DesktopPath;
                    break;
                case "Downloads":
                    App.occupiedInstance.WorkingDirectory.Path = App.DownloadsPath;
                    break;
                case "Documents":
                    App.occupiedInstance.WorkingDirectory.Path = App.DocumentsPath;
                    break;
                case "Pictures":
                    App.occupiedInstance.WorkingDirectory.Path = App.PicturesPath;
                    break;
                case "Music":
                    App.occupiedInstance.WorkingDirectory.Path = App.MusicPath;
                    break;
                case "Videos":
                    App.occupiedInstance.WorkingDirectory.Path = App.VideosPath;
                    break;
                case "OneDrive":
                    App.occupiedInstance.WorkingDirectory.Path = App.OneDrivePath;
                    break;
            }

            try
            {
                _rootFolder = await StorageFolder.GetFolderFromPathAsync(App.occupiedInstance.WorkingDirectory.Path);

                App.occupiedInstance.BackButtonState = App.occupiedInstance.CanNavigateBack;
                App.occupiedInstance.ForwardButtonState = App.occupiedInstance.CanNavigateForward;

                switch (await _rootFolder.GetIndexedStateAsync())
                {
                    case (IndexedState.FullyIndexed):
                        _options = new QueryOptions();
                        _options.FolderDepth = FolderDepth.Shallow;

                        if (App.occupiedInstance.LayoutType == typeof(GenericFileBrowser))
                        {
                            _options.SetThumbnailPrefetch(ThumbnailMode.ListView, 20, ThumbnailOptions.ResizeThumbnail);
                            _options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.DateModified", "System.ContentType", "System.Size", "System.FileExtension" });
                        }
                        else if (App.occupiedInstance.LayoutType == typeof(PhotoAlbum))
                        {
                            _options.SetThumbnailPrefetch(ThumbnailMode.ListView, 80, ThumbnailOptions.ResizeThumbnail);
                            _options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.FileExtension" });
                        }
                        _options.IndexerOption = IndexerOption.OnlyUseIndexerAndOptimizeForIndexedProperties;
                        break;
                    default:
                        _options = new QueryOptions();
                        _options.FolderDepth = FolderDepth.Shallow;

                        if (App.occupiedInstance.LayoutType == typeof(GenericFileBrowser))
                        {
                            _options.SetThumbnailPrefetch(ThumbnailMode.ListView, 20, ThumbnailOptions.ResizeThumbnail);
                            _options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.DateModified", "System.ContentType", "System.ItemPathDisplay", "System.Size", "System.FileExtension" });
                        }
                        else if (App.occupiedInstance.LayoutType == typeof(PhotoAlbum))
                        {
                            _options.SetThumbnailPrefetch(ThumbnailMode.ListView, 80, ThumbnailOptions.ResizeThumbnail);
                            _options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.FileExtension" });
                        }

                        _options.IndexerOption = IndexerOption.UseIndexerWhenAvailable;
                        break;
                }

                uint index = 0;
                _folderQueryResult = _rootFolder.CreateFolderQueryWithOptions(_options);
                //_folderQueryResult.ContentsChanged += FolderContentsChanged;
                var numFolders = await _folderQueryResult.GetItemCountAsync();
                IReadOnlyList<StorageFolder> storageFolders = await _folderQueryResult.GetFoldersAsync(index, _step);
                while (storageFolders.Count > 0)
                {
                    foreach (StorageFolder folder in storageFolders)
                    {
                        if (_cancellationTokenSource.IsCancellationRequested)
                        {
                            _cancellationTokenSource = new CancellationTokenSource();
                            isLoadingItems = false;
                            return;
                        }
                        await AddFolder(folder);
                    }
                    index += _step;
                    storageFolders = await _folderQueryResult.GetFoldersAsync(index, _step);
                }

                index = 0;
                _fileQueryResult = _rootFolder.CreateFileQueryWithOptions(_options);
                _fileQueryResult.ContentsChanged += FileContentsChanged;
                var numFiles = await _fileQueryResult.GetItemCountAsync();
                IReadOnlyList<StorageFile> storageFiles = await _fileQueryResult.GetFilesAsync(index, _step);
                while (storageFiles.Count > 0)
                {
                    foreach (StorageFile file in storageFiles)
                    {
                        if (_cancellationTokenSource.IsCancellationRequested)
                        {
                            _cancellationTokenSource = new CancellationTokenSource();
                            isLoadingItems = false;
                            return;
                        }
                        await AddFile(file);
                    }
                    index += _step;
                    storageFiles = await _fileQueryResult.GetFilesAsync(index, _step);
                }

                if(FilesAndFolders.Count == 0)
                {
                    if (_cancellationTokenSource.IsCancellationRequested)
                    {
                        _cancellationTokenSource = new CancellationTokenSource();
                        isLoadingItems = false;
                        return;
                    }
                    EmptyTextState.isVisible = Visibility.Visible;
                }
                

                OrderFiles();
                stopwatch.Stop();
                Debug.WriteLine("Loading of items in " + App.occupiedInstance.WorkingDirectory.Path + " completed in " + stopwatch.ElapsedMilliseconds + " milliseconds.\n");
                App.occupiedInstance.RefreshButtonState = true;
            }
            catch (UnauthorizedAccessException)
            {
                await App.consentDialog.ShowAsync();
            }
            catch (COMException e)
            {
                Frame rootContentFrame = Window.Current.Content as Frame;
                MessageDialog driveGone = new MessageDialog(e.Message, "Did you unplug this drive?");
                await driveGone.ShowAsync();
                rootContentFrame.Navigate(typeof(InstanceTabsView), null, new SuppressNavigationTransitionInfo());
                isLoadingItems = false;
                return;
            }
            catch (FileNotFoundException)
            {
                Frame rootContentFrame = Window.Current.Content as Frame;
                MessageDialog folderGone = new MessageDialog("The folder you've navigated to was removed.", "Did you delete this folder?");
                await folderGone.ShowAsync();
                rootContentFrame.Navigate(typeof(InstanceTabsView), null, new SuppressNavigationTransitionInfo());
                isLoadingItems = false;
                return;
            }

            if (_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                isLoadingItems = false;
                return;
            }

            LoadIndicator.isVisible = Visibility.Collapsed;
            
            isLoadingItems = false;
        }

        private async Task AddFolder(StorageFolder folder)
        {
            var basicProperties = await folder.GetBasicPropertiesAsync();

            if ((App.occupiedInstance.LayoutType == typeof(GenericFileBrowser)) || (App.occupiedInstance.LayoutType == typeof(PhotoAlbum)))
            {
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    isLoadingItems = false;
                    return;
                }
                _filesAndFolders.Add(new ListedItem(folder.FolderRelativeId)
                {
                    FileName = folder.Name,
                    FileDateReal = basicProperties.DateModified,
                    FileType = "Folder",    //TODO: Take a look at folder.DisplayType
                    FolderImg = Visibility.Visible,
                    FileImg = null,
                    FileIconVis = Visibility.Collapsed,
                    FilePath = folder.Path,
                    EmptyImgVis = Visibility.Collapsed,
                    FileSize = null,
                    FileSizeBytes = 0
                });
                
                EmptyTextState.isVisible = Visibility.Collapsed;
            }
        }

        private async Task AddFile(StorageFile file)
        {
            var basicProperties = await file.GetBasicPropertiesAsync();

            var itemName = file.DisplayName;
            var itemDate = basicProperties.DateModified;
            var itemPath = file.Path;
            var itemSize = ByteSize.FromBytes(basicProperties.Size).ToString();
            var itemSizeBytes = basicProperties.Size;
            var itemType = file.DisplayType;
            var itemFolderImgVis = Visibility.Collapsed;
            var itemFileExtension = file.FileType;

            BitmapImage icon = new BitmapImage();
            Visibility itemThumbnailImgVis;
            Visibility itemEmptyImgVis;

            if (!(App.occupiedInstance.LayoutType == typeof(PhotoAlbum)))
            {
                try
                {
                    var itemThumbnailImg = await file.GetThumbnailAsync(ThumbnailMode.ListView, 20, ThumbnailOptions.ResizeThumbnail);
                    if (itemThumbnailImg != null)
                    {
                        itemEmptyImgVis = Visibility.Collapsed;
                        itemThumbnailImgVis = Visibility.Visible;
                        icon.DecodePixelWidth = 20;
                        icon.DecodePixelHeight = 20;
                        await icon.SetSourceAsync(itemThumbnailImg);
                    }
                    else
                    {
                        itemEmptyImgVis = Visibility.Visible;
                        itemThumbnailImgVis = Visibility.Collapsed;
                    }
                }
                catch
                {
                    itemEmptyImgVis = Visibility.Visible;
                    itemThumbnailImgVis = Visibility.Collapsed;
                    // Catch here to avoid crash
                    // TODO maybe some logging could be added in the future...
                }
            }
            else
            {
                try
                {
                    var itemThumbnailImg = await file.GetThumbnailAsync(ThumbnailMode.ListView, 80, ThumbnailOptions.ResizeThumbnail);
                    if (itemThumbnailImg != null)
                    {
                        itemEmptyImgVis = Visibility.Collapsed;
                        itemThumbnailImgVis = Visibility.Visible;
                        icon.DecodePixelWidth = 80;
                        icon.DecodePixelHeight = 80;
                        await icon.SetSourceAsync(itemThumbnailImg);
                    }
                    else
                    {
                        itemEmptyImgVis = Visibility.Visible;
                        itemThumbnailImgVis = Visibility.Collapsed;
                    }
                }
                catch
                {
                    itemEmptyImgVis = Visibility.Visible;
                    itemThumbnailImgVis = Visibility.Collapsed;

                }
            }
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                isLoadingItems = false;
                return;
            }
            _filesAndFolders.Add(new ListedItem(file.FolderRelativeId)
            {
                DotFileExtension = itemFileExtension,
                EmptyImgVis = itemEmptyImgVis,
                FileImg = icon,
                FileIconVis = itemThumbnailImgVis,
                FolderImg = itemFolderImgVis,
                FileName = itemName,
                FileDateReal = itemDate,
                FileType = itemType,
                FilePath = itemPath,
                FileSize = itemSize,
                FileSizeBytes = itemSizeBytes
            });

            EmptyTextState.isVisible = Visibility.Collapsed;
        }

        public async void FileContentsChanged(IStorageQueryResultBase sender, object args)
        {
            if (_filesRefreshing)
            {
                Debug.WriteLine("Filesystem change event fired but refresh is already running");
                return;
            }
            else
            {
                Debug.WriteLine("Filesystem change event fired. Refreshing...");
            }

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                LoadIndicator.isVisible = Visibility.Visible;
            });
            _filesRefreshing = true;

            //query options have to be reapplied otherwise old results are returned
            _fileQueryResult.ApplyNewQueryOptions(_options);
            _folderQueryResult.ApplyNewQueryOptions(_options);

            var fileCount = await _fileQueryResult.GetItemCountAsync();
            var folderCount = await _folderQueryResult.GetItemCountAsync();
            var files = await _fileQueryResult.GetFilesAsync();
            var folders = await _folderQueryResult.GetFoldersAsync();

            // modifying a file also results in a new unique FolderRelativeId so no need to check for DateModified explicitly

            var addedFiles = files.Select(f => f.FolderRelativeId).Except(_filesAndFolders.Select(f => f.FolderRelativeId));
            var addedFolders = folders.Select(f => f.FolderRelativeId).Except(_filesAndFolders.Select(f => f.FolderRelativeId));
            var removedFilesAndFolders = _filesAndFolders
                .Select(f => f.FolderRelativeId)
                .Except(files.Select(f => f.FolderRelativeId))
                .Except(folders.Select(f => f.FolderRelativeId))
                .ToArray();

            foreach (var file in addedFiles)
            {
                var toAdd = files.First(f => f.FolderRelativeId == file);
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                async () =>
                {
                    await AddFile(toAdd);
                });
            }
            foreach (var folder in addedFolders)
            {
                var toAdd = folders.First(f => f.FolderRelativeId == folder);
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                async () =>
                {
                    await AddFolder(toAdd);
                });
            }
            foreach (var item in removedFilesAndFolders)
            {
                var toRemove = _filesAndFolders.First(f => f.FolderRelativeId == item);
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    RemoveFileOrFolder(toRemove);
                });
            }

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                LoadIndicator.isVisible = Visibility.Collapsed;
            });

            _filesRefreshing = false;
            Debug.WriteLine("Filesystem refresh complete");
        }

    }
}
