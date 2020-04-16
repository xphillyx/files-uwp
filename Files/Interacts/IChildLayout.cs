
using Files.Filesystem;
using System.Collections.Generic;

namespace Files.Interacts
{
    public interface IChildLayout
    {
        public abstract void BeginRename();
        public abstract void DimItemForCut(ListedItem li);
        public abstract void ResetDimmedItems();
        public abstract void SelectAllItems();
        public abstract void ClearAllItems();
    }
}


