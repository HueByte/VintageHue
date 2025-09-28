using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HueHordes.Models;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Asynchronous configuration management with file watching and hot-reload capabilities
/// </summary>
public class AsyncConfigurationManager : IDisposable
{
    private readonly ICoreServerAPI sapi;
    private readonly string configFilePath;
    private readonly SemaphoreSlim configSemaphore = new(1, 1);

    private ServerConfig? currentConfig;
    private FileSystemWatcher? configWatcher;
    private DateTime lastConfigChange = DateTime.MinValue;

    // Events for configuration changes
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    private const string CONFIG_FILE = "Horde.server.json";

    public AsyncConfigurationManager(ICoreServerAPI serverApi)
    {
        sapi = serverApi;
        configFilePath = Path.Combine(sapi.GetOrCreateDataPath("ModConfig"), CONFIG_FILE);
    }

    /// <summary>
    /// Asynchronously initialize configuration management
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await configSemaphore.WaitAsync(cancellationToken);

        try
        {
            // Load initial configuration
            currentConfig = await LoadConfigurationAsync(cancellationToken);

            // Set up file system watcher for hot-reload
            SetupConfigurationWatcher();

            sapi.Logger.Debug("[AsyncConfigurationManager] Configuration management initialized");
        }
        finally
        {
            configSemaphore.Release();
        }
    }

    /// <summary>
    /// Asynchronously load configuration from file
    /// </summary>
    public async Task<ServerConfig> LoadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            await configSemaphore.WaitAsync(cancellationToken);

            try
            {
                ServerConfig config;

                if (File.Exists(configFilePath))
                {
                    try
                    {
                        var configData = await File.ReadAllTextAsync(configFilePath, cancellationToken);
                        config = sapi.LoadModConfig<ServerConfig>(CONFIG_FILE) ?? new ServerConfig();

                        sapi.Logger.Debug($"[AsyncConfigurationManager] Configuration loaded from {configFilePath}");
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning($"[AsyncConfigurationManager] Failed to load config: {ex.Message}");
                        config = new ServerConfig();
                    }
                }
                else
                {
                    // Create default configuration
                    config = new ServerConfig();
                    await SaveConfigurationAsync(config, cancellationToken);
                }

                currentConfig = config;
                return config;
            }
            finally
            {
                configSemaphore.Release();
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously save configuration to file
    /// </summary>
    public async Task SaveConfigurationAsync(ServerConfig config, CancellationToken cancellationToken = default)
    {
        await configSemaphore.WaitAsync(cancellationToken);

        try
        {
            await Task.Run(() =>
            {
                // Temporarily disable watcher to prevent recursive calls
                var wasWatcherEnabled = configWatcher?.EnableRaisingEvents ?? false;
                if (configWatcher != null)
                    configWatcher.EnableRaisingEvents = false;

                try
                {
                    sapi.StoreModConfig(config, CONFIG_FILE);
                    currentConfig = config;
                    lastConfigChange = DateTime.UtcNow;

                    sapi.Logger.Debug($"[AsyncConfigurationManager] Configuration saved to {configFilePath}");
                }
                finally
                {
                    // Re-enable watcher
                    if (configWatcher != null && wasWatcherEnabled)
                    {
                        // Add small delay to avoid immediate trigger
                        Task.Delay(500, cancellationToken).ContinueWith(_ =>
                        {
                            if (configWatcher != null)
                                configWatcher.EnableRaisingEvents = true;
                        }, cancellationToken);
                    }
                }
            }, cancellationToken);
        }
        finally
        {
            configSemaphore.Release();
        }
    }

    /// <summary>
    /// Get current configuration (thread-safe)
    /// </summary>
    public ServerConfig GetCurrentConfiguration()
    {
        return currentConfig ?? new ServerConfig();
    }

    /// <summary>
    /// Asynchronously update specific configuration values
    /// </summary>
    public async Task UpdateConfigurationAsync(Action<ServerConfig> updateAction, CancellationToken cancellationToken = default)
    {
        await configSemaphore.WaitAsync(cancellationToken);

        try
        {
            var config = currentConfig ?? new ServerConfig();
            var oldConfig = CloneConfiguration(config);

            // Apply update
            updateAction(config);

            // Save updated configuration
            await SaveConfigurationAsync(config, cancellationToken);

            // Raise configuration changed event
            var args = new ConfigurationChangedEventArgs(oldConfig, config);
            ConfigurationChanged?.Invoke(this, args);

            sapi.Logger.Debug("[AsyncConfigurationManager] Configuration updated");
        }
        finally
        {
            configSemaphore.Release();
        }
    }

    /// <summary>
    /// Set up file system watcher for hot-reload functionality
    /// </summary>
    private void SetupConfigurationWatcher()
    {
        try
        {
            var configDirectory = Path.GetDirectoryName(configFilePath);
            if (string.IsNullOrEmpty(configDirectory) || !Directory.Exists(configDirectory))
                return;

            configWatcher = new FileSystemWatcher(configDirectory, Path.GetFileName(configFilePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            configWatcher.Changed += OnConfigurationFileChanged;

            sapi.Logger.Debug("[AsyncConfigurationManager] File watcher set up for hot-reload");
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning($"[AsyncConfigurationManager] Failed to set up file watcher: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle configuration file changes for hot-reload
    /// </summary>
    private async void OnConfigurationFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Debounce file change events (avoid multiple rapid calls)
            var now = DateTime.UtcNow;
            if ((now - lastConfigChange).TotalSeconds < 2)
                return;

            lastConfigChange = now;

            // Small delay to ensure file write is complete
            await Task.Delay(500);

            var oldConfig = currentConfig ?? new ServerConfig();
            var newConfig = await LoadConfigurationAsync();

            // Check if configuration actually changed
            if (!AreConfigurationsEqual(oldConfig, newConfig))
            {
                var args = new ConfigurationChangedEventArgs(oldConfig, newConfig);
                ConfigurationChanged?.Invoke(this, args);

                sapi.Logger.Notification("[AsyncConfigurationManager] Configuration hot-reloaded from file");
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning($"[AsyncConfigurationManager] Error during hot-reload: {ex.Message}");
        }
    }

    /// <summary>
    /// Clone configuration for comparison
    /// </summary>
    private ServerConfig CloneConfiguration(ServerConfig config)
    {
        return new ServerConfig
        {
            DaysBetweenHordes = config.DaysBetweenHordes,
            Count = config.Count,
            SpawnRadiusMin = config.SpawnRadiusMin,
            SpawnRadiusMax = config.SpawnRadiusMax,
            EntityCodes = (string[])config.EntityCodes.Clone(),
            NudgeTowardInitialPos = config.NudgeTowardInitialPos,
            NudgeSeconds = config.NudgeSeconds,
            NudgeSpeed = config.NudgeSpeed
        };
    }

    /// <summary>
    /// Compare two configurations for equality
    /// </summary>
    private bool AreConfigurationsEqual(ServerConfig config1, ServerConfig config2)
    {
        return config1.DaysBetweenHordes == config2.DaysBetweenHordes &&
               config1.Count == config2.Count &&
               Math.Abs(config1.SpawnRadiusMin - config2.SpawnRadiusMin) < 0.001f &&
               Math.Abs(config1.SpawnRadiusMax - config2.SpawnRadiusMax) < 0.001f &&
               config1.NudgeTowardInitialPos == config2.NudgeTowardInitialPos &&
               Math.Abs(config1.NudgeSeconds - config2.NudgeSeconds) < 0.001f &&
               Math.Abs(config1.NudgeSpeed - config2.NudgeSpeed) < 0.001f &&
               ArraysEqual(config1.EntityCodes, config2.EntityCodes);
    }

    /// <summary>
    /// Compare two string arrays for equality
    /// </summary>
    private bool ArraysEqual(string[] array1, string[] array2)
    {
        if (array1.Length != array2.Length)
            return false;

        for (int i = 0; i < array1.Length; i++)
        {
            if (array1[i] != array2[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Validate configuration values
    /// </summary>
    public async Task<ValidationResult> ValidateConfigurationAsync(ServerConfig config, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var result = new ValidationResult { IsValid = true };

            // Validate days between hordes
            if (config.DaysBetweenHordes < 1)
            {
                result.IsValid = false;
                result.Errors.Add("DaysBetweenHordes must be at least 1");
            }

            // Validate count
            if (config.Count < 1 || config.Count > 100)
            {
                result.IsValid = false;
                result.Errors.Add("Count must be between 1 and 100");
            }

            // Validate spawn radius
            if (config.SpawnRadiusMin <= 0 || config.SpawnRadiusMax <= 0)
            {
                result.IsValid = false;
                result.Errors.Add("Spawn radius values must be positive");
            }

            if (config.SpawnRadiusMin >= config.SpawnRadiusMax)
            {
                result.IsValid = false;
                result.Errors.Add("SpawnRadiusMin must be less than SpawnRadiusMax");
            }

            // Validate entity codes
            if (config.EntityCodes.Length == 0)
            {
                result.IsValid = false;
                result.Errors.Add("At least one entity code must be specified");
            }

            // Validate nudge settings
            if (config.NudgeSeconds < 0)
            {
                result.IsValid = false;
                result.Errors.Add("NudgeSeconds must be non-negative");
            }

            if (config.NudgeSpeed < 0)
            {
                result.IsValid = false;
                result.Errors.Add("NudgeSpeed must be non-negative");
            }

            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Get configuration statistics
    /// </summary>
    public async Task<string> GetConfigurationStatsAsync()
    {
        return await Task.Run(() =>
        {
            var config = GetCurrentConfiguration();
            var stats = new System.Text.StringBuilder();

            stats.AppendLine("=== Configuration Stats ===");
            stats.AppendLine($"Config File: {configFilePath}");
            stats.AppendLine($"File Exists: {File.Exists(configFilePath)}");
            stats.AppendLine($"Hot-Reload: {(configWatcher?.EnableRaisingEvents == true ? "Enabled" : "Disabled")}");
            stats.AppendLine($"Last Changed: {lastConfigChange:yyyy-MM-dd HH:mm:ss}");
            stats.AppendLine();

            stats.AppendLine("=== Current Configuration ===");
            stats.AppendLine($"Days Between Hordes: {config.DaysBetweenHordes}");
            stats.AppendLine($"Entity Count: {config.Count}");
            stats.AppendLine($"Spawn Radius: {config.SpawnRadiusMin}-{config.SpawnRadiusMax}");
            stats.AppendLine($"Entity Types: {string.Join(", ", config.EntityCodes)}");
            stats.AppendLine($"Nudge Enabled: {config.NudgeTowardInitialPos}");
            stats.AppendLine($"Nudge Duration: {config.NudgeSeconds}s");
            stats.AppendLine($"Nudge Speed: {config.NudgeSpeed}");

            return stats.ToString();
        });
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        configWatcher?.Dispose();
        configSemaphore.Dispose();
    }
}

/// <summary>
/// Event arguments for configuration changes
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    public ServerConfig OldConfiguration { get; }
    public ServerConfig NewConfiguration { get; }

    public ConfigurationChangedEventArgs(ServerConfig oldConfig, ServerConfig newConfig)
    {
        OldConfiguration = oldConfig;
        NewConfiguration = newConfig;
    }
}

/// <summary>
/// Configuration validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}