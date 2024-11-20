using MonoTorrent;
using MonoTorrent.Client;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SimPlazaManager.Networking.Torrents;

internal class StandardDownloader
{
    ClientEngine Engine { get; }

    public StandardDownloader(ClientEngine engine)
    {
        Engine = engine;
    }

    public async Task DownloadAsync(CancellationToken token)
    {
        string download_path = Path.Combine(Environment.CurrentDirectory, AppDomain.CurrentDomain.BaseDirectory + "package_downloads");
        string torrents_path = Path.Combine(Environment.CurrentDirectory, AppDomain.CurrentDomain.BaseDirectory + "torrents");

        if (!Directory.Exists(torrents_path))
            Directory.CreateDirectory(torrents_path);

        // Add torrents to engine
        foreach (string file in Directory.GetFiles(torrents_path))
            if (file.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Torrent torrent = await Torrent.LoadAsync(file);
                    await Engine.AddAsync(torrent, download_path);
                }
                catch (Exception e)
                {
                    Console.Write("Couldn't download {0}: ", file);
                    Console.WriteLine(e.Message);
                }
            }
            else if (file.EndsWith(".magnet", StringComparison.OrdinalIgnoreCase))
            {
                string magnet_str = await File.ReadAllTextAsync(file, token);
                var magnet = MagnetLink.Parse(magnet_str);
                await Engine.AddAsync(magnet, download_path);
            }

        if (Engine.Torrents.Count == 0)
            return;

        // Download torrents
        foreach (TorrentManager manager in Engine.Torrents)
            await manager.StartAsync();

        while (Engine.IsRunning && !token.IsCancellationRequested)
        {
            DisplayTorrentData data = new()
            {
                Engine = Engine
            };

            SendDownloadInfos(data);
            await Task.Delay(100, token);
        }
    }

    public event EventHandler<DisplayTorrentData>? DisplayRequested;

    protected virtual void SendDownloadInfos(DisplayTorrentData args)
    {
        DisplayRequested?.Invoke(this, args);
    }
}