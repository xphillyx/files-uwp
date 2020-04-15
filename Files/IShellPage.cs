using Files.Filesystem;
using Files.Interacts;
using Files.UserControls;
using System;
using Windows.UI.Xaml.Controls;

namespace Files
{
    public interface IShellPage
    {
        public Frame ContentFrame { get; }
        public object OperationsControl { get; }   // Reserved for future use
        public Type CurrentPageType { get; }
        public INavigationControlItem SidebarSelectedItem { get; set; }
        public INavigationToolbar NavigationToolbar { get; }
    }
}
