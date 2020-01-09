using System.Collections.Generic;
using Windows.UI.Xaml.Media.Imaging;

namespace Locations
{
    public class LocationItem
    {
        public string ImageSource { get; set; }
        public string Icon { get; set; }
        public BitmapImage FileThumbnail { get; set; }
        public string Text { get; set; }
        public string Reason { get; set; }
        public bool isFolderIconLoaded { get; set; } = false;
        public bool isFileThumbnailLoaded { get; set; } = false;
        public bool isOneDriveIconLoaded { get; set; } = false;
        public bool isSidebarPinIconLoaded { get; set; } = false;


        public LocationItem(SuggestedItemGlyphType GlyphType)
        {
            switch (GlyphType)
            {
                case SuggestedItemGlyphType.Folder:
                    isFolderIconLoaded = true;
                    break;
                case SuggestedItemGlyphType.FileThumbnail:
                    isFileThumbnailLoaded = true;
                    break;
                case SuggestedItemGlyphType.OneDriveBackup:
                    isOneDriveIconLoaded = true;
                    break;
                case SuggestedItemGlyphType.SidebarPin:
                    isSidebarPinIconLoaded = true;
                    break;
            }            
        }
    }

    public class ItemLoader
    {
        public ItemLoader()
        {
        }
        public static List<LocationItem> itemsAdded = new List<LocationItem>();
        public static void DisplayItems()
        {
            itemsAdded.Add(new LocationItem(SuggestedItemGlyphType.Folder) { Reason = "You typically open this folder in the evening", Icon = "\xE896", Text = "XAML Samples"});
            itemsAdded.Add(new LocationItem(SuggestedItemGlyphType.OneDriveBackup) { Reason = "Backup frequent library to OneDrive?", Icon = "\xE896", Text = "Pictures" });
            itemsAdded.Add(new LocationItem(SuggestedItemGlyphType.SidebarPin) {Reason = "Need to find this document often? Pin it to the sidebar.", Icon = "\xE896", Text = "ResumeDraft.docx" });

        }
    }

    public enum SuggestedItemGlyphType
    {
        Folder = 0,
        FileThumbnail = 1,
        OneDriveBackup = 2,
        SidebarPin = 3
    }
}