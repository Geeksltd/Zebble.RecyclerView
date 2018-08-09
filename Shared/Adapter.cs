namespace Zebble
{
    using System.Collections.Generic;
    using System.Linq;

    public abstract class Adapter<TItem>
    {
        readonly List<TItem> DataSource;
        internal List<RecyclerViewItem> ViewItems = new List<RecyclerViewItem>();

        protected Adapter(IEnumerable<TItem> dataSource) => DataSource = dataSource.ToList();

        internal RecyclerViewItem CreateViewItem()
        {
            var recyclerViewItem = OnCreateViewItem();
            ViewItems.Add(recyclerViewItem);

            return recyclerViewItem;
        }

        public abstract RecyclerViewItem OnCreateViewItem();

        internal RecyclerViewItem BindViewItem(int dataIndex, int viewItemIndex, bool createViewItems)
        {
            if (createViewItems) CreateViewItem();

            OnBindViewItem(ViewItems[viewItemIndex], dataIndex);

            return ViewItems[viewItemIndex];
        }

        public abstract void OnBindViewItem(RecyclerViewItem recyclerViewItem, int position);

        internal int GetDataSourceCount() => DataSource.Count;
    }
}