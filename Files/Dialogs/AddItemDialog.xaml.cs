using Files.Filesystem;
using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace Files.Dialogs
{
    public sealed partial class AddItemDialog : ContentDialog
    {
        public AddItemResultType Result { get; set; } = AddItemResultType.Nothing;
        public ListView addItemsChoices;
        public AddItemDialog()
        {
            this.InitializeComponent();
            addItemsChoices = AddItemsListView;
            AddItemsToList();
        }

        public List<AddListItem> AddItemsList = new List<AddListItem>();

        public void AddItemsToList()
        {
            AddItemsList.Clear();
            AddItemsList.Add(new AddListItem { Header = "Folder", SubHeader = "Creates an empty folder", Icon = "\xE838", IsItemEnabled = true });
            AddItemsList.Add(new AddListItem { Header = "Text Document", SubHeader = "Creates a simple text file", Icon = "\xE8A5", IsItemEnabled = true });
            AddItemsList.Add(new AddListItem { Header = "Bitmap Image", SubHeader = "Creates an empty bitmap image file", Icon = "\xEB9F", IsItemEnabled = true });

        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            switch((e.ClickedItem as AddListItem).Header)
            {
                case "Folder":
                    Result = AddItemResultType.Folder;
                    break;
                case "Text Document":
                    Result = AddItemResultType.TextDocument;
                    break;
                case "Bitmap Image":
                    Result = AddItemResultType.BitmapImage;
                    break;
            }
            App.AddItemDialogDisplay.Hide();

        }
    }

    public enum AddItemResultType
    {
        Folder = 0,
        TextDocument = 1,
        BitmapImage = 2,
        CompressedArchive = 3,
        Nothing = 4
    }

    public class AddListItem
    {
        public string Header { get; set; }
        public string SubHeader { get; set; }
        public string Icon { get; set; }
        public bool IsItemEnabled { get; set; }
    }
}
