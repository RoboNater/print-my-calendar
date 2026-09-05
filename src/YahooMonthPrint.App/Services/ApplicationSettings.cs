using System.Text.Json;
using System.Text.Json.Serialization;
using YahooMonthPrint.Core;
using YahooMonthPrint.Printing;

namespace YahooMonthPrint.App.Services;

public sealed record ApplicationSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public string? YahooAccount { get; init; }

    public IReadOnlyList<SavedCalendar> Calendars { get; init; } = [];

    public DetailLevel DetailLevel { get; init; } = DetailLevel.Detailed;

    public int MaximumDescriptionLines { get; init; } = 3;

    public bool ShowLocations { get; init; } = true;

    public string PaperSize { get; init; } = "Printer default";

    public string Orientation { get; init; } = "Landscape";

    [JsonConverter(typeof(PrintOverflowPolicyJsonConverter))]
    public PrintOverflowPolicy OverflowPolicy { get; init; } =
        PrintOverflowPolicy.ReduceDetailAutomatically;
}

public sealed class PrintOverflowPolicyJsonConverter : JsonConverter<PrintOverflowPolicy>
{
    public override PrintOverflowPolicy Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        _ = typeToConvert;
        _ = options;
        if (reader.TokenType == JsonTokenType.Number
            && reader.TryGetInt32(out var numericValue)
            && Enum.IsDefined(typeof(PrintOverflowPolicy), numericValue))
        {
            return (PrintOverflowPolicy)numericValue;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return PrintOverflowPolicy.ReduceDetailAutomatically;
        }

        return reader.GetString() switch
        {
            nameof(PrintOverflowPolicy.ReduceDetailAutomatically)
                or "Reduce detail automatically" => PrintOverflowPolicy.ReduceDetailAutomatically,
            nameof(PrintOverflowPolicy.UseSmallerText)
                or "Use smaller text" => PrintOverflowPolicy.UseSmallerText,
            nameof(PrintOverflowPolicy.PrintDetailsPages)
                or "Print overflow details on page 2" => PrintOverflowPolicy.PrintDetailsPages,
            _ => PrintOverflowPolicy.ReduceDetailAutomatically,
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        PrintOverflowPolicy value,
        JsonSerializerOptions options)
    {
        _ = options;
        writer.WriteStringValue(value.ToString());
    }
}

public sealed record SavedCalendar(
    string Id,
    string DisplayName,
    string Uri,
    string? Color,
    bool IsSelected)
{
    public CalendarSource ToCalendarSource() => new(
        Id,
        DisplayName,
        System.Uri.TryCreate(Uri, UriKind.Absolute, out var calendarUri) ? calendarUri : null,
        Color,
        IsSelected);
}
