using System.IO;
using System.Text.Json;

namespace YahooMonthPrint.App.Services;

public interface ISettingsStore
{
    Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class SerializedSettingsStore(ISettingsStore inner) : ISettingsStore, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await inner.LoadAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await inner.SaveAsync(settings, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await inner.ClearAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();
}

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string path;

    public JsonSettingsStore(string? applicationDataPath = null)
    {
        var directory = applicationDataPath ?? AppStoragePaths.Root;
        path = Path.Combine(directory, "settings.json");
    }

    public async Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return new ApplicationSettings();
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<ApplicationSettings>(
                stream,
                SerializerOptions,
                cancellationToken);
            return settings is { Version: ApplicationSettings.CurrentVersion }
                ? settings
                : new ApplicationSettings();
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            return new ApplicationSettings();
        }
    }

    public async Task SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await AtomicJsonFile.WriteAsync(path, settings, SerializerOptions, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(path);
        return Task.CompletedTask;
    }

    private static bool IsUnreadable(Exception exception) => exception is
        JsonException
        or IOException
        or UnauthorizedAccessException;
}

internal static class AtomicJsonFile
{
    public static async Task WriteAsync<T>(
        string path,
        T value,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The settings path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, value, options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, path, true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}

public static class AppStoragePaths
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YahooMonthPrint");
}
