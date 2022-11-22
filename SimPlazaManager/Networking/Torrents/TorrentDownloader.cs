using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimPlazaManager.Networking.Torrents;

public class TorrentDownloader
{
    public void Download()
    {
        Task task = DownloadTask(_cancellation.Token);
        task.Wait();
    }

    public void Cancel()
    {
        _cancellation.Cancel();
    }

    private async Task DownloadTask(CancellationToken token)
    {
        MonoTorrent.Client.EngineSettingsBuilder settings_builder = new()
        {
            AllowPortForwarding = true,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true
        };

        MonoTorrent.Client.ClientEngine engine = new(settings_builder.ToSettings());

        try
        {
            StandardDownloader downloader = new(engine);
            downloader.DisplayRequested += DisplayRequested;
            await downloader.DownloadAsync(token);
        }
        catch (OperationCanceledException) { }

        foreach (var manager in engine.Torrents)
        {
            Task stopping_task = manager.StopAsync();
            while (manager.State != MonoTorrent.Client.TorrentState.Stopped)
                await Task.WhenAll(stopping_task, Task.Delay(250));
            await stopping_task;
        }
    }

    public event EventHandler<DisplayTorrentData>? DisplayRequested;
    protected virtual void SendDownloadInfos(DisplayTorrentData args)
    {
        DisplayRequested?.Invoke(this, args);
    }

    private readonly CancellationTokenSource _cancellation = new();
}
