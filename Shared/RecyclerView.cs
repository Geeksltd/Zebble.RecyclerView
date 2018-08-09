namespace Zebble
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class RecyclerView<TItem> : ScrollView
    {
        const int ReserveCount = 2;
        readonly Adapter<TItem> adapter;
        readonly float itemHeight;
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
        readonly Canvas viewItemsContainer = new Canvas();

        State currentState = new State();
        State nextState = new State();
        float previousScrollY;
        int viewItemsCount;
        int visibleItemsCount;

        public RecyclerView(Adapter<TItem> adapter, float itemHeight)
        {
            this.adapter = adapter;
            this.itemHeight = itemHeight;
        }

        public override async Task OnInitializing()
        {
            await base.OnInitializing();

            var steps = CalculateInitialStates(Height.CurrentValue);
            visibleItemsCount = steps.Count;
            viewItemsCount = visibleItemsCount + ReserveCount;

            foreach (var step in steps)
            {
                nextState = step;

                await Load(1, true, ScrollDirection.Forward);

                currentState = nextState.Clone();
            }

            for (var i = 0; i < ReserveCount; i++)
            {
                var recyclerViewItem = adapter.CreateViewItem();
                recyclerViewItem.Height(itemHeight);
                recyclerViewItem.Y((visibleItemsCount + i) * itemHeight);
                await viewItemsContainer.Add(recyclerViewItem);
            }

            currentState.ForwardReserveCount += ReserveCount;
            currentState.LastViewItemIndex += ReserveCount;
            viewItemsContainer.Height(viewItemsCount * itemHeight);

            nextState = currentState.Clone();

            await Add(viewItemsContainer);
            UserScrolledVertically.Handle(OnUserScrolledVertically);
        }

        async Task OnUserScrolledVertically()
        {
            await semaphore.WaitAsync();
            try
            {
                var scrollY = ScrollY;
                var direction = scrollY >= previousScrollY ? ScrollDirection.Forward : ScrollDirection.Backward;

                var loadCount = CalculateState(scrollY, direction);

                if (loadCount > 0)
                {
                    await Load(loadCount, false, direction);

                    currentState = nextState.Clone();
                }

                previousScrollY = scrollY;
            }
            finally
            {
                semaphore.Release();
            }
        }

        async Task Load(int loadCount, bool isInitialLoad, ScrollDirection scrollDirection)
        {
            for (var loadIndex = 0; loadIndex < loadCount; loadIndex++)
            {
                var isThereNext = await LoadItem(isInitialLoad, scrollDirection);

                if (!isThereNext) break;

                // We maybe need this in iOS
                //if (OS.Platform.IsIOS()) await Task.Delay(Animation.OneFrame);
            }
        }

        async Task<bool> LoadItem(bool isInitialLoad, ScrollDirection scrollDirection)
        {
            var dataIndex = scrollDirection == ScrollDirection.Forward
                ? nextState.LastVisibleDataIndex
                : nextState.FirstVisibleDataIndex;

            var viewItemIndex = scrollDirection == ScrollDirection.Forward
                ? nextState.LastVisibleViewItem
                : nextState.FirstVisibleViewItem;

            // Bind the new one
            var viewItem = adapter.BindViewItem(dataIndex, viewItemIndex, isInitialLoad);
            if (isInitialLoad)
            {
                viewItem.Height(itemHeight);
                viewItem.Y(nextState.LastVisibleDataIndex * itemHeight);
                await viewItemsContainer.Add(viewItem);
            }

            if (!isInitialLoad)
                if (nextState.MoveOneView)
                {
                    if (scrollDirection == ScrollDirection.Forward)
                    {
                        var firstViewItem = adapter.ViewItems[currentState.FirstViewItemIndex];
                        firstViewItem.Y(firstViewItem.ActualY + viewItemsCount * itemHeight);
                        viewItemsContainer.Height((nextState.LastVisibleDataIndex + 2) * itemHeight);
                    }
                    else
                    {
                        var lastViewItem = adapter.ViewItems[currentState.LastViewItemIndex];
                        lastViewItem.Y(lastViewItem.ActualY - viewItemsCount * itemHeight);
                        viewItemsContainer.Height((nextState.LastVisibleDataIndex + 2) * itemHeight);
                    }
                }

            return true;
        }

        List<State> CalculateInitialStates(float scrollY)
        {
            var remainder = scrollY % itemHeight > 0.001 ? 1 : 0;
            var loadItems = (int)(scrollY / itemHeight) + remainder;

            var result = new List<State>();
            for (var stepIndex = 0; stepIndex < loadItems; stepIndex++)
            {
                var info = new State
                {
                    FirstViewItemIndex = 0,
                    FirstVisibleDataIndex = 0,
                    FirstVisibleViewItem = 0,
                    LastVisibleDataIndex = stepIndex,
                    LastVisibleViewItem = stepIndex,
                    LastViewItemIndex = stepIndex,
                    ForwardReserveCount = 0,
                    BackwardReserveCount = 0
                };

                result.Add(info);
            }

            return result;
        }

        int CalculateState(float scrollY, ScrollDirection scrollDirection)
        {
            // In forward scrolling we need to consider the height of the RecyclerView too
            var baseHight = scrollDirection == ScrollDirection.Forward ? ActualHeight : 0;

            var partialyVisible = (scrollY + baseHight) % itemHeight > 0.001 ? 1 : 0;
            var completelyVisible = (int)((scrollY + baseHight) / itemHeight);
            var nextVisibleDataIndex = completelyVisible + partialyVisible - 1;

            if (nextVisibleDataIndex < 0 || nextVisibleDataIndex >= adapter.GetDataSourceCount()) return 0;

            if (scrollDirection == ScrollDirection.Forward && currentState.LastVisibleDataIndex < nextVisibleDataIndex)
            {
                var loadCount = nextVisibleDataIndex - currentState.LastVisibleDataIndex;

                nextState.FirstVisibleDataIndex = currentState.FirstVisibleDataIndex + loadCount;
                nextState.FirstVisibleViewItem = nextState.FirstVisibleDataIndex % viewItemsCount;
                nextState.LastVisibleDataIndex = nextVisibleDataIndex;
                nextState.LastVisibleViewItem = nextState.LastVisibleDataIndex % viewItemsCount;

                nextState.MoveOneView = false;
                if (nextState.ForwardReserveCount > 1)
                {
                    nextState.BackwardReserveCount++;
                    nextState.ForwardReserveCount--;
                }
                else if (nextState.ForwardReserveCount == 1)
                {
                    nextState.BackwardReserveCount++;
                    nextState.ForwardReserveCount--;

                    if (nextVisibleDataIndex < adapter.GetDataSourceCount() - 1) nextState.MoveOneView = true;
                }
                else if (nextVisibleDataIndex < adapter.GetDataSourceCount() - 1)
                {
                    nextState.MoveOneView = true;
                }

                if (nextState.MoveOneView)
                {
                    nextState.FirstViewItemIndex = IncrementModular(nextState.FirstViewItemIndex, viewItemsCount);
                    nextState.LastViewItemIndex = IncrementModular(nextState.LastViewItemIndex, viewItemsCount);
                }

                return loadCount;
            }

            if (scrollDirection == ScrollDirection.Backward && currentState.FirstVisibleDataIndex > nextVisibleDataIndex)
            {
                var loadCount = currentState.FirstVisibleDataIndex - nextVisibleDataIndex;

                nextState.FirstVisibleDataIndex = nextVisibleDataIndex;
                nextState.FirstVisibleViewItem = nextState.FirstVisibleDataIndex % viewItemsCount;
                nextState.LastVisibleDataIndex = currentState.LastVisibleDataIndex - loadCount;
                nextState.LastVisibleViewItem = nextState.LastVisibleDataIndex % viewItemsCount;

                nextState.MoveOneView = false;
                if (nextState.BackwardReserveCount > 1)
                {
                    nextState.BackwardReserveCount--;
                    nextState.ForwardReserveCount++;
                }
                else if (nextState.BackwardReserveCount == 1)
                {
                    nextState.BackwardReserveCount--;
                    nextState.ForwardReserveCount++;

                    if (nextVisibleDataIndex > 0) nextState.MoveOneView = true;
                }
                else if (nextVisibleDataIndex > 0)
                {
                    nextState.MoveOneView = true;
                }

                if (nextState.MoveOneView)
                {
                    nextState.FirstViewItemIndex = DecrementModular(nextState.FirstViewItemIndex, viewItemsCount);
                    nextState.LastViewItemIndex = DecrementModular(nextState.LastViewItemIndex, viewItemsCount);
                }

                return loadCount;
            }

            return 0;
        }

        int IncrementModular(int value, int max)
        {
            value++;
            value %= max;

            return value;
        }

        int DecrementModular(int value, int max)
        {
            value--;

            if (value < 0) value += max;

            value %= max;

            return value;
        }

        class State
        {
            public int FirstViewItemIndex { get; set; }

            public int FirstVisibleDataIndex { get; set; }

            public int FirstVisibleViewItem { get; set; }

            public int LastVisibleDataIndex { get; set; }

            public int LastVisibleViewItem { get; set; }

            public int LastViewItemIndex { get; set; }

            public int ForwardReserveCount { get; set; }

            public int BackwardReserveCount { get; set; }

            public bool MoveOneView { get; set; }

            public State Clone() => new State
            {
                FirstViewItemIndex = FirstViewItemIndex,
                FirstVisibleDataIndex = FirstVisibleDataIndex,
                FirstVisibleViewItem = FirstVisibleViewItem,
                LastVisibleDataIndex = LastVisibleDataIndex,
                LastVisibleViewItem = LastVisibleViewItem,
                LastViewItemIndex = LastViewItemIndex,
                ForwardReserveCount = ForwardReserveCount,
                BackwardReserveCount = BackwardReserveCount,
                MoveOneView = MoveOneView
            };
        }

        enum ScrollDirection
        {
            Forward,
            Backward
        }
    }
}