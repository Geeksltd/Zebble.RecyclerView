[logo]: https://raw.githubusercontent.com/Geeksltd/Zebble.RecyclerView/master/icon.png "Zebble.RecyclerView"


## Zebble.RecyclerView

![logo]

A high-performance Zebble plugin for providing a list like view for large datasets.


[![NuGet](https://img.shields.io/nuget/v/Zebble.RecyclerView.svg?label=NuGet)](https://www.nuget.org/packages/Zebble.RecyclerView/)

<br>


### Setup
* Available on NuGet: [https://www.nuget.org/packages/Zebble.RecyclerView/](https://www.nuget.org/packages/Zebble.RecyclerView/)
* Install in your platform client projects.
* Available for iOS, Android and UWP.
<br>


### Api Usage
Implement your `RecyclerViewItem`. Each `RecyclerViewItem` will be rendered as a row of your `RecyclerView`.
```csharp
public class MyViewItem : RecyclerViewItem
{
    public TextView Caption = new TextView();
    public ImageView Image = new ImageView();

    public override async Task OnInitializing()
    {
        await base.OnInitializing();

        await Content.Add(Caption);
        await Content.Add(Image);
    }
}
```

Next, implement your `Adapter`. `Adapter` will be in charge of hosting your data and binding the `RecyclerViewItem`s as needed.
```csharp
public class MyAdapter : Adapter<Data>
{
    readonly List<Data> items;

    public MyAdapter(List<Data> items) : base(items)
    {
        this.items = items;
    }

    public override RecyclerViewItem OnCreateViewItem()
    {
        return new MyViewItem();
    }

    public override void OnBindViewItem(RecyclerViewItem recyclerViewItem, int position)
    {
        var viewItem = (MyViewItem) recyclerViewItem;

        viewItem.Caption.Text = items[position].Title;
        viewItem.Image.Path = items[position].ImageFile;
    }
}
```
And lastly, add the `RecyclerView` to your page. Note that you need to specify the height of the `RecyclerViewItem`.
```csharp
var data = GetData();
var adapter = new MyAdapter(data);
var recyclerView = new RecyclerView<Data>(adapter, 170);

await Body.Add(recyclerView);
```