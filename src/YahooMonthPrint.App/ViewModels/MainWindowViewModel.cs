using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using YahooMonthPrint.App.Services;
using YahooMonthPrint.Core;

namespace YahooMonthPrint.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly ICalendarOccurrenceSource source;
    private readonly TimeSpan filterDebounce;
    private readonly Dictionary<string, CalendarSource> calendarLookup;
    private readonly Dictionary<OccurrenceKey, CalendarOccurrence> hiddenOccurrenceLookup = [];
    private CancellationTokenSource? loadCancellation;
    private CancellationTokenSource? filterCancellation;
    private MonthOccurrenceSet occurrenceSet = new([]);
    private MonthViewState state;
    private DateOnly displayedMonth;
    private string filterText = string.Empty;
    private bool isLoading;
    private string statusText = "Offline sample schedule";
    private int loadVersion;
    private Task pendingFilterUpdate = Task.CompletedTask;
    private bool isBatchUpdatingTitleFilters;
    private bool disposed;

    public MainWindowViewModel(
        ICalendarOccurrenceSource source,
        DateOnly? initialMonth = null,
        TimeSpan? filterDebounce = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.filterDebounce = filterDebounce ?? TimeSpan.FromMilliseconds(180);
        displayedMonth = FirstOfMonth(initialMonth ?? DateOnly.FromDateTime(DateTime.Today));
        calendarLookup = source.Calendars.ToDictionary(calendar => calendar.Id, StringComparer.Ordinal);
        state = new MonthViewState(displayedMonth)
        {
            EnabledCalendars = source.Calendars
                .Where(calendar => calendar.IsEnabled)
                .Select(calendar => calendar.Id)
                .ToHashSet(StringComparer.Ordinal),
        };

        DetailLevelOptions =
        [
            new(DetailLevel.TitlesOnly, "Titles Only"),
            new(DetailLevel.Compact, "Compact"),
            new(DetailLevel.Detailed, "Detailed"),
        ];
        DescriptionLineOptions = [1, 2, 3, 4];

        PreviousMonthCommand = new AsyncRelayCommand(() => NavigateAsync(-1), ReportUnexpectedError);
        NextMonthCommand = new AsyncRelayCommand(() => NavigateAsync(1), ReportUnexpectedError);
        TodayCommand = new AsyncRelayCommand(
            () => NavigateToAsync(DateOnly.FromDateTime(DateTime.Today)),
            ReportUnexpectedError);
        RefreshCommand = new AsyncRelayCommand(
            RefreshAsync,
            ReportUnexpectedError,
            () => !IsLoading);
        ShowAllTitlesCommand = new RelayCommand(() => SetAllTitles(true));
        HideAllTitlesCommand = new RelayCommand(() => SetAllTitles(false));
        RestoreAllCommand = new RelayCommand(RestoreAll, () => state.HiddenOccurrences.Count > 0);

        BuildCalendarFilters();
        RebuildDays();
    }

    public ObservableCollection<FilterOptionViewModel> CalendarFilters { get; } = [];

    public ObservableCollection<FilterOptionViewModel> TitleFilters { get; } = [];

    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

    public ObservableCollection<HiddenOccurrenceViewModel> HiddenOccurrences { get; } = [];

    public IReadOnlyList<string> WeekdayNames { get; } =
        CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;

    public IReadOnlyList<DetailLevelOption> DetailLevelOptions { get; }

    public IReadOnlyList<int> DescriptionLineOptions { get; }

    public ICommand PreviousMonthCommand { get; }

    public ICommand NextMonthCommand { get; }

    public ICommand TodayCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand ShowAllTitlesCommand { get; }

    public ICommand HideAllTitlesCommand { get; }

    public ICommand RestoreAllCommand { get; }

    public string DisplayedMonthLabel => displayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

    public string FilterText
    {
        get => filterText;
        set
        {
            if (SetProperty(ref filterText, value ?? string.Empty))
            {
                pendingFilterUpdate = DebounceFilterAsync();
            }
        }
    }

    public bool IsShowOnlyMatching
    {
        get => state.QuickFilterMode == QuickFilterMode.ShowOnlyMatching;
        set
        {
            if (value && state.QuickFilterMode != QuickFilterMode.ShowOnlyMatching)
            {
                state = state with { QuickFilterMode = QuickFilterMode.ShowOnlyMatching };
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsHideMatching));
                ApplyVisibility();
            }
        }
    }

    public bool IsHideMatching
    {
        get => state.QuickFilterMode == QuickFilterMode.HideMatching;
        set
        {
            if (value && state.QuickFilterMode != QuickFilterMode.HideMatching)
            {
                state = state with { QuickFilterMode = QuickFilterMode.HideMatching };
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsShowOnlyMatching));
                ApplyVisibility();
            }
        }
    }

    public DetailLevel SelectedDetailLevel
    {
        get => state.DetailLevel;
        set
        {
            if (state.DetailLevel != value)
            {
                state = state with { DetailLevel = value };
                OnPropertyChanged();
                ApplyVisibility();
            }
        }
    }

    public int MaximumDescriptionLines
    {
        get => state.MaximumDescriptionLines;
        set
        {
            if (state.MaximumDescriptionLines != value)
            {
                state = state with { MaximumDescriptionLines = value };
                OnPropertyChanged();
                ApplyVisibility();
            }
        }
    }

    public bool ShowLocations
    {
        get => state.ShowLocations;
        set
        {
            if (state.ShowLocations != value)
            {
                state = state with { ShowLocations = value };
                OnPropertyChanged();
                ApplyVisibility();
            }
        }
    }

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (SetProperty(ref isLoading, value))
            {
                ((AsyncRelayCommand)RefreshCommand).NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public int VisibleOccurrenceCount { get; private set; }

    public IReadOnlyList<CalendarOccurrence> VisibleOccurrences { get; private set; } = [];

    public int RawOccurrenceCount => occurrenceSet.RawOccurrences.Count;

    public string VisibleCountLabel => $"{VisibleOccurrenceCount} events displayed";

    public string HiddenCountLabel => $"Hidden items ({HiddenOccurrences.Count})";

    public Task PendingFilterUpdate => pendingFilterUpdate;

    public async Task InitializeAsync() => await RefreshAsync();

    public async Task NavigateAsync(int months) =>
        await NavigateToAsync(displayedMonth.AddMonths(months));

    public async Task RefreshAsync()
    {
        ThrowIfDisposed();
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();
        var cancellationToken = loadCancellation.Token;
        var version = ++loadVersion;

        IsLoading = true;
        StatusText = "Loading sample schedule…";
        try
        {
            var range = MonthGrid.Create(displayedMonth.Year, displayedMonth.Month);
            var occurrences = await source.LoadAsync(range, cancellationToken);
            if (version != loadVersion)
            {
                return;
            }

            occurrenceSet = new MonthOccurrenceSet(occurrences);
            BuildTitleFilters();
            ApplyVisibility();
            StatusText = $"Sample schedule loaded at {DateTime.Now:t}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (CalendarLoadException exception)
        {
            StatusText = $"Sample schedule unavailable: {exception.Message}";
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            ReportUnexpectedError(exception);
        }
        finally
        {
            if (version == loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        filterCancellation?.Cancel();
        filterCancellation?.Dispose();
    }

    private async Task NavigateToAsync(DateOnly month)
    {
        displayedMonth = FirstOfMonth(month);
        state = state with { DisplayedMonth = displayedMonth };
        OnPropertyChanged(nameof(DisplayedMonthLabel));
        RebuildDays();
        await RefreshAsync();
    }

    private void BuildCalendarFilters()
    {
        CalendarFilters.Clear();
        foreach (var calendar in source.Calendars)
        {
            CalendarFilters.Add(new FilterOptionViewModel(
                calendar.Id,
                calendar.DisplayName,
                state.EnabledCalendars.Contains(calendar.Id),
                enabled => SetCalendarEnabled(calendar.Id, enabled)));
        }
    }

    private void BuildTitleFilters()
    {
        TitleFilters.Clear();
        foreach (var title in occurrenceSet.TitlesInDisplayedMonth(displayedMonth))
        {
            TitleFilters.Add(new FilterOptionViewModel(
                title,
                title,
                !state.DisabledTitles.Contains(title),
                enabled => SetTitleEnabled(title, enabled)));
        }
    }

    private void SetCalendarEnabled(string calendarId, bool enabled)
    {
        var enabledCalendars = state.EnabledCalendars.ToHashSet(StringComparer.Ordinal);
        if (enabled)
        {
            enabledCalendars.Add(calendarId);
        }
        else
        {
            enabledCalendars.Remove(calendarId);
        }

        state = state with { EnabledCalendars = enabledCalendars };
        ApplyVisibility();
    }

    private void SetTitleEnabled(string title, bool enabled)
    {
        if (isBatchUpdatingTitleFilters)
        {
            return;
        }

        var disabledTitles = state.DisabledTitles.ToHashSet(StringComparer.Ordinal);
        if (enabled)
        {
            disabledTitles.Remove(title);
        }
        else
        {
            disabledTitles.Add(title);
        }

        state = state with { DisabledTitles = disabledTitles };
        ApplyVisibility();
    }

    private void SetAllTitles(bool enabled)
    {
        var disabledTitles = state.DisabledTitles.ToHashSet(StringComparer.Ordinal);
        if (enabled)
        {
            disabledTitles.ExceptWith(TitleFilters.Select(item => item.Id));
        }
        else
        {
            disabledTitles.UnionWith(TitleFilters.Select(item => item.Id));
        }

        state = state with { DisabledTitles = disabledTitles };
        isBatchUpdatingTitleFilters = true;
        try
        {
            foreach (var item in TitleFilters)
            {
                item.IsEnabled = enabled;
            }
        }
        finally
        {
            isBatchUpdatingTitleFilters = false;
        }

        ApplyVisibility();
    }

    private async Task DebounceFilterAsync()
    {
        filterCancellation?.Cancel();
        filterCancellation?.Dispose();
        filterCancellation = new CancellationTokenSource();
        var cancellationToken = filterCancellation.Token;

        try
        {
            await Task.Delay(filterDebounce, cancellationToken);
            state = state with { QuickFilterText = FilterText };
            ApplyVisibility();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void HideOccurrence(CalendarOccurrence occurrence)
    {
        hiddenOccurrenceLookup[occurrence.Key] = occurrence;
        state = state.Hide(occurrence.Key);
        ApplyVisibility();
    }

    private void RestoreOccurrence(CalendarOccurrence occurrence)
    {
        hiddenOccurrenceLookup.Remove(occurrence.Key);
        state = state.Restore(occurrence.Key);
        ApplyVisibility();
    }

    private void RestoreAll()
    {
        hiddenOccurrenceLookup.Clear();
        state = state.RestoreAll();
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        var visible = occurrenceSet.Visible(state);
        VisibleOccurrences = visible;
        VisibleOccurrenceCount = visible.Count;
        RebuildDays(visible);
        RebuildHiddenOccurrences();
        OnPropertyChanged(nameof(VisibleOccurrences));
        OnPropertyChanged(nameof(VisibleOccurrenceCount));
        OnPropertyChanged(nameof(VisibleCountLabel));
    }

    private void RebuildDays(IReadOnlyList<CalendarOccurrence>? visible = null)
    {
        visible ??= occurrenceSet.Visible(state);
        var range = MonthGrid.Create(displayedMonth.Year, displayedMonth.Month);
        var today = DateOnly.FromDateTime(DateTime.Today);
        Days.Clear();

        foreach (var date in range.Dates)
        {
            var dayOccurrences = visible
                .Where(occurrence => OccurrenceDateRange.OccursOnDate(occurrence, date))
                .Select(CreateOccurrenceViewModel)
                .ToArray();
            Days.Add(new CalendarDayViewModel(
                date,
                date.Month == displayedMonth.Month && date.Year == displayedMonth.Year,
                date == today,
                dayOccurrences));
        }
    }

    private OccurrenceViewModel CreateOccurrenceViewModel(CalendarOccurrence occurrence)
    {
        var content = EventDisplayProjection.Create(occurrence, state);
        var calendar = calendarLookup[occurrence.CalendarId];
        return new OccurrenceViewModel(
            occurrence,
            calendar.DisplayName,
            calendar.Color ?? "#666666",
            content.TimeText,
            content.Title,
            string.Join(Environment.NewLine, content.DescriptionLines),
            content.Location,
            new RelayCommand(() => HideOccurrence(occurrence)));
    }

    private void RebuildHiddenOccurrences()
    {
        HiddenOccurrences.Clear();
        foreach (var occurrence in hiddenOccurrenceLookup.Values.Order(OccurrenceComparer.Instance))
        {
            var label = occurrence.IsAllDay
                ? $"{occurrence.Title} — {occurrence.Start:MMM d}"
                : $"{occurrence.Title} — {occurrence.Start:MMM d, h:mm tt}";
            HiddenOccurrences.Add(new HiddenOccurrenceViewModel(
                occurrence,
                label,
                new RelayCommand(() => RestoreOccurrence(occurrence))));
        }

        OnPropertyChanged(nameof(HiddenCountLabel));
        ((RelayCommand)RestoreAllCommand).NotifyCanExecuteChanged();
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private void ReportUnexpectedError(Exception exception)
    {
        _ = exception;
        StatusText = "The sample schedule could not be loaded because of an unexpected error. Try again.";
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and not AccessViolationException;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
