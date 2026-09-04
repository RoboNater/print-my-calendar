namespace YahooMonthPrint.App.ViewModels;

public sealed class FilterOptionViewModel : ObservableObject
{
    private readonly Action<bool> changed;
    private bool isEnabled;

    public FilterOptionViewModel(string id, string name, bool isEnabled, Action<bool> changed)
    {
        Id = id;
        Name = name;
        this.isEnabled = isEnabled;
        this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
    }

    public string Id { get; }

    public string Name { get; }

    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (SetProperty(ref isEnabled, value))
            {
                changed(value);
            }
        }
    }
}
