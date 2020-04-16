using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static Files.BaseLayout;

namespace Files.Dialogs
{
    public sealed partial class ConfirmDeleteDialog : ContentDialog
    {
        public MyResult Result { get; set; }

        public ConfirmDeleteDialog()
        {
            this.InitializeComponent();

            this.Result = MyResult.Nothing; //clear the result in case the value is set from last time
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            Result = MyResult.Delete;
            Hide();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = MyResult.Cancel;
            Hide();
        }
    }
}
