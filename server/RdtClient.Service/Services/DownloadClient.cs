﻿using RdtClient.Data.Models.Data;
using RdtClient.Service.Helpers;
using RdtClient.Service.Services.Downloaders;

namespace RdtClient.Service.Services;

public class DownloadClient(Download download, Torrent torrent, String destinationPath)
{
    private readonly String _destinationPath = destinationPath;

    private readonly Download _download = download;
    private readonly Torrent _torrent = torrent;

    public IDownloader? Downloader;

    public Data.Enums.DownloadClient Type { get; private set; }

    public Boolean Finished { get; private set; }

    public String? Error { get; private set; }

    public Int64 Speed { get; private set; }
    public Int64 BytesTotal { get; private set; }
    public Int64 BytesDone { get; private set; }

    public async Task<String?> Start()
    {
        BytesDone = 0;
        BytesTotal = 0;
        Speed = 0;

        try
        {
            if (_download.Link == null)
            {
                throw new Exception($"Invalid download link");
            }

            var filePath = DownloadHelper.GetDownloadPath(_destinationPath, _torrent, _download);
            var downloadPath = DownloadHelper.GetDownloadPath(_torrent, _download);

            if (filePath == null || downloadPath == null)
            {
                throw new Exception("Invalid download path");
            }

            await FileHelper.Delete(filePath);

            Type = Settings.Get.DownloadClient.Client;

            Downloader = Settings.Get.DownloadClient.Client switch
            {
                Data.Enums.DownloadClient.Internal => new InternalDownloader(_download.Link, filePath),
                Data.Enums.DownloadClient.Bezzad => new BezzadDownloader(_download.Link, filePath),
                Data.Enums.DownloadClient.Aria2c => new Aria2cDownloader(_download.RemoteId, _download.Link, filePath, downloadPath),
                Data.Enums.DownloadClient.Symlink => new SymlinkDownloader(_download.Link, filePath),
                _ => throw new Exception($"Unknown download client {Settings.Get.DownloadClient}")
            };

            Downloader.DownloadComplete += (_, args) =>
            {
                Finished = true;
                Error ??= args.Error;
            };

            Downloader.DownloadProgress += (_, args) =>
            {
                Speed = args.Speed;
                BytesDone = args.BytesDone;
                BytesTotal = args.BytesTotal;
            };

            var result = await Downloader.Download();
                
            await Task.Delay(1000);

            return result;
        }
        catch (Exception ex)
        {
            if (Downloader != null)
            {
                await Downloader.Cancel();
            }

            Error = $"An unexpected error occurred preparing download {_download.Link} for torrent {_torrent.RdName}: {ex.Message}";
            Finished = true;

            return null;
        }
    }

    public async Task Cancel()
    {
        Finished = true;
        Error = null;

        if (Downloader == null)
        {
            return;
        }
        await Downloader.Cancel();
    }

    public async Task Pause()
    {
        if (Downloader == null)
        {
            return;
        }
        await Downloader.Pause();
    }

    public async Task Resume()
    {
        if (Downloader == null)
        {
            return;
        }
        await Downloader.Resume();
    }
}