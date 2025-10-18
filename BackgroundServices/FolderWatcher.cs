using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kazama.Models;
using Microsoft.Extensions.Logging;

namespace Kazama.BackgroundServices
{
    internal class FolderWatcher : IDisposable
    {
        private readonly FolderSyncConfig config;
        private readonly ILogger _logger;
        private readonly FileSystemWatcher _watcher;
        private readonly HashSet<string> _recentMoves = new();

        public FolderWatcher(FolderSyncConfig config, ILogger logger)
        {
            this.config = config;
            _logger = logger;

            DirectoryCopy(config.Source, config.Destination, config.IncludeSubdirectories, config.OverwriteExisting);

            _watcher = new FileSystemWatcher(config.Source)
            {
                IncludeSubdirectories = config.IncludeSubdirectories,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            _watcher.Created += (_, e) =>
            {
                _ = Task.Run(async () => await CopyItemAsync(e.FullPath));
            };
            _watcher.Changed += (_, e) =>
            {
                _ = Task.Run(async () => await CopyItemAsync(e.FullPath));
            };
            _watcher.Renamed += (_, e) => {
                _ = Task.Run(async () => await HandleRenameAsync(e));
            };
            if (config.MirrorDeletions)
                _watcher.Deleted += (_, e) => {
                    _ = Task.Run(async () => await DeleteItemAsync(e.FullPath));
                };

            _watcher.EnableRaisingEvents = true;
        }

        private void HandleRename(RenamedEventArgs e)
        {
            try
            {
                string oldRel = Path.GetRelativePath(config.Source, e.OldFullPath);
                string newRel = Path.GetRelativePath(config.Source, e.FullPath);

                string oldDest = Path.Combine(config.Destination, oldRel);
                string newDest = Path.Combine(config.Destination, newRel);

                // Prevent duplicate triggers
                if (_recentMoves.Contains(oldDest)) return;
                _recentMoves.Add(oldDest);
                Task.Delay(2000).ContinueWith(_ => _recentMoves.Remove(oldDest));

                if (Directory.Exists(oldDest))
                {
                    Directory.Move(oldDest, newDest);
                    _logger.LogInformation("[DIR RENAME] {OldPath} -> {NewPath}", oldDest, newDest);
                }
                else if (File.Exists(oldDest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newDest)!);
                    File.Move(oldDest, newDest, config.OverwriteExisting);
                    _logger.LogInformation("[FILE RENAME] {OldPath} -> {NewPath}", oldDest, newDest);
                }
                else
                {
                    // fallback: copy new item if old destination doesn't exist
                    CopyItem(e.FullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error renaming {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
            }
        }

        private void CopyItem(string changedPath)
        {
            try
            {
                if (Directory.Exists(changedPath))
                {
                    string destPath = Path.Combine(config.Destination, Path.GetRelativePath(config.Source, changedPath));
                    Directory.CreateDirectory(destPath);
                    _logger.LogInformation("[DIR] Created: {Path}", destPath);
                }
                else if (File.Exists(changedPath))
                {
                    string rel = Path.GetRelativePath(config.Source, changedPath);
                    string destPath = Path.Combine(config.Destination, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                    if (!config.OverwriteExisting && File.Exists(destPath))
                        return;

                    // Retry logic for locked files
                    for (int retry = 0; retry < 3; retry++)
                    {
                        try
                        {
                            File.Copy(changedPath, destPath, config.OverwriteExisting);
                            _logger.LogInformation("[FILE] Copied: {Path}", destPath);
                            break;
                        }
                        catch (IOException) when (retry < 2)
                        {
                            Thread.Sleep(500);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error copying {Path}", changedPath);
            }
        }

        private void DeleteItem(string changedPath)
        {
            try
            {
                string rel = Path.GetRelativePath(config.Source, changedPath);
                string destPath = Path.Combine(config.Destination, rel);

                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                    _logger.LogInformation("[DELETE FILE] {Path}", destPath);
                }
                else if (Directory.Exists(destPath))
                {
                    Directory.Delete(destPath, true);
                    _logger.LogInformation("[DELETE DIR] {Path}", destPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deleting {Path}", changedPath);
            }
        }

        private static void DirectoryCopy(string sourceDir, string destDir, bool copySubDirs, bool overwrite)
        {
            DirectoryInfo dir = new(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

            Directory.CreateDirectory(destDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string tempPath = Path.Combine(destDir, file.Name);
                file.CopyTo(tempPath, overwrite);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dir.GetDirectories())
                {
                    string tempPath = Path.Combine(destDir, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs, overwrite);
                }
            }
        }

        private async Task HandleRenameAsync(RenamedEventArgs e)
        {
            try
            {
                string oldRel = Path.GetRelativePath(config.Source, e.OldFullPath);
                string newRel = Path.GetRelativePath(config.Source, e.FullPath);

                string oldDest = Path.Combine(config.Destination, oldRel);
                string newDest = Path.Combine(config.Destination, newRel);

                // Prevent duplicate triggers
                if (_recentMoves.Contains(oldDest)) return;
                _recentMoves.Add(oldDest);
                _ = Task.Delay(2000).ContinueWith(_ => _recentMoves.Remove(oldDest));

                if (Directory.Exists(oldDest))
                {
                    Directory.Move(oldDest, newDest);
                    _logger.LogInformation("[DIR RENAME] {OldPath} -> {NewPath}", oldDest, newDest);
                }
                else if (File.Exists(oldDest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newDest)!);
                    File.Move(oldDest, newDest, config.OverwriteExisting);
                    _logger.LogInformation("[FILE RENAME] {OldPath} -> {NewPath}", oldDest, newDest);
                }
                else
                {
                    // fallback: copy new item if old destination doesn't exist
                    await CopyItemAsync(e.FullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error renaming {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
            }
        }

        private async Task CopyItemAsync(string changedPath)
        {
            try
            {
                if (Directory.Exists(changedPath))
                {
                    string destPath = Path.Combine(config.Destination, Path.GetRelativePath(config.Source, changedPath));
                    Directory.CreateDirectory(destPath);
                    _logger.LogInformation("[DIR] Created: {Path}", destPath);
                }
                else if (File.Exists(changedPath))
                {
                    string rel = Path.GetRelativePath(config.Source, changedPath);
                    string destPath = Path.Combine(config.Destination, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                    if (!config.OverwriteExisting && File.Exists(destPath))
                        return;

                    // Retry logic for locked files
                    for (int retry = 0; retry < 3; retry++)
                    {
                        try
                        {
                            using var sourceStream = File.Open(changedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            using var destStream = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            await sourceStream.CopyToAsync(destStream);

                            _logger.LogInformation("[FILE] Copied: {Path}", destPath);
                            break;
                        }
                        catch (IOException) when (retry < 2)
                        {
                            await Task.Delay(500);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error copying {Path}", changedPath);
            }
        }

        private async Task DeleteItemAsync(string changedPath)
        {
            try
            {
                string rel = Path.GetRelativePath(config.Source, changedPath);
                string destPath = Path.Combine(config.Destination, rel);

                if (File.Exists(destPath))
                {
                    await Task.Run(() => File.Delete(destPath));
                    _logger.LogInformation("[DELETE FILE] {Path}", destPath);
                }
                else if (Directory.Exists(destPath))
                {
                    await Task.Run(() => Directory.Delete(destPath, true));
                    _logger.LogInformation("[DELETE DIR] {Path}", destPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deleting {Path}", changedPath);
            }
        }

        private static async Task DirectoryCopyAsync(string sourceDir, string destDir, bool copySubDirs, bool overwrite)
        {
            DirectoryInfo dir = new(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

            Directory.CreateDirectory(destDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string tempPath = Path.Combine(destDir, file.Name);
                using var sourceStream = file.OpenRead();
                using var destStream = File.Open(tempPath, FileMode.Create, FileAccess.Write);
                await sourceStream.CopyToAsync(destStream);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dir.GetDirectories())
                {
                    string tempPath = Path.Combine(destDir, subdir.Name);
                    await DirectoryCopyAsync(subdir.FullName, tempPath, copySubDirs, overwrite);
                }
            }
        }


        public void Dispose()
        {
            _watcher.Dispose();
        }
    }
}
