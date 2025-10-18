# Kazama

Kazama is a lightweight game save data sync application that runs as a Windows Service (Linux planned) and backs up your game save data as the files are changed.

## Getting Started

> When the service runs for the first time, or when it picks up changes to the `foldersync.toml` file it will copy all existing data from the source folders into the destination ones.

1. Download the Release from the releases pages.
2. Unzip the folder.
3. Under `Scripts/Windows`, you will find the `*.bat` files for installing and uninstalling the application as a windows service. Run the `InstallKazamaWindowsService.bat` script (as Administrator) to install it into your Windows Services. It will run on start up, and it will recover if it crashes.
4. Now next to the `Kazama.exe` file, you are going to want to create/edit the file `foldersync.toml`. See the next section for how to edit this file.
5. The service will watch the `foldersync.toml` for changes and automatically update itself and begin watching.
6. You can view the logs for the application under the `logs/` folder.


### Editing the `foldersync.toml` file

Here is what a single folder sync entry looks like:

```toml
[[FolderSyncs]]
Source = "C:\\Users\\{YOUR_USER_NAME}\\AppData\\Local\\TEKKEN 8"
Destination = "D:\\{YOUR_SAVE_LOCATION}\\Tekken 8"
IncludeSubdirectories = true
MirrorDeletions = true
OverwriteExisting = true
```

You include as many of those entries as you like. Each one of the `[[FolderSyncs]]` entry will be watched. For example:

```toml
[[FolderSyncs]]
Source = "C:\\Users\\AppData\\Local\\TEKKEN 8"
Destination = "D:\\Test\\Tekken 8"
IncludeSubdirectories = true
MirrorDeletions = true
OverwriteExisting = true

[[FolderSyncs]]
Source = "C:\\Users\\AppData\\Local\\Stormgate"
Destination = "D:\\Test\\Stormgate"
IncludeSubdirectories = true
MirrorDeletions = true
OverwriteExisting = true
```

That is how you would watch multiple folders, and mark them for sync/backup. You can include as many as you'd like.


## Roadmap

- Ideally I would like to have a database of known game save locations (Steam, GOG, Linux ISOs etc.) so that users will only have to select a root destination folder and/or maybe their names.
- There are a couple of small changes to be made for ensuring it works on linux.

## Limitations

- The application will simply copy the files after they have been written. It doesn't do anything particularly smart.