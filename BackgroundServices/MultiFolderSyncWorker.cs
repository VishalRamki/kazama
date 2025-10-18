using System.Collections.Concurrent;
using Kazama;
using Kazama.BackgroundServices;
using Kazama.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tomlyn;
using Tomlyn.Model;

public class MultiFolderSyncWorker : BackgroundService
{
    private readonly ILogger<MultiFolderSyncWorker> _logger;
    private readonly string _tomlPath;
    private readonly ConcurrentDictionary<string, FolderWatcher> _activeWatchers = new();

    private FileSystemWatcher? _configWatcher;

    public MultiFolderSyncWorker(ILogger<MultiFolderSyncWorker> logger, FolderSyncRoot configRoot)
    {
        _logger = logger;
        _tomlPath = Path.Combine(AppContext.BaseDirectory, Constants.ConfigurationFile);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Main Sync Worker Thread");

        // Load initial config
        await LoadAndApplyConfig();

        // Watch TOML file for changes
        _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(_tomlPath)!)
        {
            Filter = Path.GetFileName(_tomlPath),
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _configWatcher.Changed += (_, e) =>
        {
            _logger.LogInformation("Configuration file changed. Reloading...");
            // Delay slightly to avoid partial write issues
            Task.Delay(500).ContinueWith(_ => LoadAndApplyConfig());
        };

        _configWatcher.EnableRaisingEvents = true;

        return;
    }
    private async Task LoadAndApplyConfig()
    {
        try
        {
            if (!File.Exists(_tomlPath))
            {
                _logger.LogWarning("Configuration file not found: {Path}", _tomlPath);
                return;
            }
            using var stream = File.OpenRead(Constants.ConfigurationFile);
            using var reader = new StreamReader(stream);
            string toml = await reader.ReadToEndAsync();
            var model = Toml.ToModel(toml);
            var configs = new List<FolderSyncConfig>();

            if (model.TryGetValue("FolderSyncs", out var syncsObj) && syncsObj is TomlTableArray array)
            {
                foreach (TomlTable table in array)
                {
                    configs.Add(new FolderSyncConfig
                    {
                        Source = table["Source"]?.ToString() ?? "",
                        Destination = table["Destination"]?.ToString() ?? "",
                        IncludeSubdirectories = table.TryGetValue("IncludeSubdirectories", out var sub) && Convert.ToBoolean(sub),
                        MirrorDeletions = table.TryGetValue("MirrorDeletions", out var mir) && Convert.ToBoolean(mir),
                        OverwriteExisting = table.TryGetValue("OverwriteExisting", out var over) && Convert.ToBoolean(over)
                    });
                }
            }
            _logger.LogInformation("Configuration File Loaded");
            ApplyConfig(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load TOML configuration");
        }
    }

    private void ApplyConfig(List<FolderSyncConfig> configs)
    {
        _logger.LogInformation("Applying Configuration...");
        // Stop watchers that are no longer in config
        foreach (var key in _activeWatchers.Keys)
        {
            if (!configs.Exists(c => c.Source.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                if (_activeWatchers.TryRemove(key, out var oldWatcher))
                {
                    oldWatcher.Dispose();
                    _logger.LogInformation("Stopped watching removed folder: {Source}", key);
                }
            }
        }

        // Start new watchers
        foreach (var cfg in configs)
        {
            if (!_activeWatchers.ContainsKey(cfg.Source))
            {
                var watcher = new FolderWatcher(cfg, _logger);
                _activeWatchers[cfg.Source] = watcher;
                _logger.LogInformation("Started watching new folder: {Source}", cfg.Source);
            }
        }

        _logger.LogInformation("Configuration Applied.");

    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var watcher in _activeWatchers.Values)
            watcher.Dispose();

        _configWatcher?.Dispose();
        _logger.LogInformation("Stopped all watchers.");
        return base.StopAsync(cancellationToken);
    }
}
