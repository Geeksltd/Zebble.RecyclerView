namespace Zebble.Plugin
{
    using System.Threading.Tasks;

    public abstract class RecyclerViewItem : Canvas
    {
        public readonly Stack Content = new Stack {Direction = RepeatDirection.Horizontal, Id = "Content"};

        public override async Task OnInitializing()
        {
            await base.OnInitializing();

            await Add(Content);
        }
    }
}