using MonoTorrent.Client;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SimPlazaManager.Extensions;
using SimPlazaManager.Models;
using SimPlazaManager.Networking;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SimPlazaManager.Commands;

public class UpgradeCommand : Command<UpgradeCommand.Arguments>
{
    public class Arguments : CommandSettings
    {
        [CommandArgument(0, "[QUERY/LINK]")]
        [Description("The queries or links used to find packages to upgrade")]
        public string Query { get; set; } = string.Empty;

        [CommandOption("-l|--list")]
        [Description("Upgrade all installed packages to newest version")]
        [DefaultValue(false)]
        public bool List { get; set; }

        [CommandOption("-a|--all")]
        [Description("Upgrade all installed packages to newest version")]
        [DefaultValue(false)]
        public bool All { get; set; }
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] Arguments args)
    {
        if (!args.All && !args.List && args.Query.Length == 0)
            return ValidationResult.Error("You must specify a query if you're not upgrading all the packages.");
        return base.Validate(context, args);
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arguments args)
    {
        List<Article> articles_list = new();
        List<Package>? packages;
        if (args.All || args.List)
        {
            packages = Settings.GetAllPackages();
            if (packages is null)
            {
                AnsiConsole.MarkupLine("[bold red]No installed packages[/]");
                return 1;
            }
        }
        else
        {
            packages = Settings.PackagesByQuery(args.Query);
            if (packages is null)
            {
                AnsiConsole.MarkupLine($"[bold red]No packages found for query[/] {args.Query}[bold red].[/]");
                return 1;
            }
        }

        bool no_one_has_updates = true;
        foreach (var package in packages)
        {
            AnsiConsole.MarkupLine($"[white]Checking for updates on:[/] {package.WebArticle}");
            Tuple<int, Article?> update_status = ArticlesNetwork.CheckUpdate(package.WebArticle);
            bool has_update = false;
            switch (update_status.Item1)
            {
                case 0:
                    has_update = true;
                    break;
                case 1:
                    has_update = false;
                    break;
                case 2:
                    AnsiConsole.MarkupLine("[bold red]Unable to find package[/] in SimPlaza, it may be deleted. Continuing...");
                    continue;
            }

            if (!has_update)
                continue;

            no_one_has_updates = false;
            if (update_status.Item2 is null)
            {
                AnsiConsole.MarkupLine($"[bold red]Unknown error occurred upgrading[/] {package.WebArticle}[bold red]. Continuing...[/]");
                continue;
            }

            if (args.List)
            {
                articles_list.Add(update_status.Item2);
                continue;
            }

            Article article = update_status.Item2;
            AnsiConsole.MarkupLine($"[green]Upgrading[/] package {article.Editor} - {article.Name}");
            AnsiConsole.Progress().Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new TransferSpeedColumn(),
                new RemainingTimeColumn(),
                new ElapsedTimeColumn(),
                new SpinnerColumn(),
            }).Start(ctx =>
            {
                var download_torrent_file = ctx.AddTask("[white]Downloading Torrent[/]", false);
                var download_package = ctx.AddTask("[white]Downloading Package[/]", false);
                var unpack_file = ctx.AddTask("[white]Unpacking Files[/]", false);
                var install_file = ctx.AddTask("[white]Installing Package[/]", false);

                new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "torrents").Empty();
                foreach (string dir in Directory.EnumerateDirectories(AppDomain.CurrentDomain.BaseDirectory + "package_downloads"))
                    Directory.Delete(dir, true);

                if (!article.Details.IsValueCreated)
                    article.Details = new Lazy<Article.ArticleDetails>(() => ArticlesNetwork.ArticleDetails(article.Link), System.Threading.LazyThreadSafetyMode.PublicationOnly);
                _ = article.Details.Value.DownloadLink;

                // Download torrent file
                download_torrent_file.StartTask();
                _ = ArticlesNetwork.TorrentLocalPath(article, download_torrent_file);
                download_torrent_file.StopTask();

                // Download package from torrent
                download_package.StartTask();
                _data.Engine = null;
                Networking.Torrents.TorrentDownloader downloader = new();
                downloader.DisplayRequested += RenderDownloadData;
                Task.Run(() => downloader.Download());

                while (_data.Engine is null) { } // Wait until first engine is sent
                while (!_data.Engine.Torrents.All(x => x.Size > 0)) { } // Wait until all torrents have a size

                lock (_data)
                    download_package.MaxValue = _data.Engine.Torrents.Sum(x => x.Torrent.Size);
                while (!_data.Engine.Torrents.All(x => x.Complete))
                {
                    if (!_new_value)
                        continue;
                    lock (_data)
                        download_package.Value(_data.Engine.Torrents.Sum(x => x.Monitor.DataBytesDownloaded));
                    _new_value = false;
                }
                download_package.Value = double.MaxValue;
                download_package.StopTask();
                downloader.Cancel();
                new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "torrents").Empty();

                // Unpack downloaded RAR file from torrent
                unpack_file.StartTask();
                unpack_file.MaxValue = _data.Engine.Torrents.Sum(x => x.Files.Sum(y =>
                {
                    int maxAttempts = 10, attemptNb = 1;
                    while (attemptNb < maxAttempts)
                    {
                        try
                        {
                            using RarArchive rarArchive = RarArchive.Open(y.FullPath);
                            return rarArchive.TotalUncompressSize;
                        }
                        catch (IOException)
                        {
                            System.Threading.Thread.Sleep(100);
                            attemptNb++;
                        }
                    }
                    return double.MaxValue;
                }));
                lock (_data)
                {
                    foreach (TorrentManager torrent in _data.Engine.Torrents)
                        foreach (ITorrentFileInfo file in torrent.Files)
                        {
                            using RarArchive archive = RarArchive.Open(file.FullPath);

                            string directory_path = AppDomain.CurrentDomain.BaseDirectory + $"package_downloads/{archive.Entries.First().Key[..archive.Entries.First().Key.IndexOf("\\")]}";
                            if (Directory.Exists(directory_path))
                                Directory.Delete(directory_path, true);
                            foreach (var entry in archive.Entries)
                            {
                                if (entry.IsDirectory)
                                    continue;

                                string destination_path = AppDomain.CurrentDomain.BaseDirectory + $"package_downloads/{entry.Key}";
                                Directory.CreateDirectory(destination_path[..destination_path.LastIndexOf("\\")]);

                                using var destination = File.Create(destination_path);
                                entry.WriteTo(destination);
                                unpack_file.Increment(destination.Length);
                            }
                        }
                }
                unpack_file.Value = double.MaxValue;
                unpack_file.StopTask();

                // Install package and enable it
                install_file.StartTask();
                package.Upgrade(update_status.Item2);
                install_file.Value = double.MaxValue;
                install_file.StopTask();
            });
            AnsiConsole.MarkupLine($"[green]Successfully updated {article.Name} from {package.WebArticle.Version} to {article.Version}[/]");
        }

        if (no_one_has_updates)
            AnsiConsole.MarkupLine("[yellow]No updates for packages found.[/]");
        else if (args.List)
        {
            Table table = new()
            {
                Title = new TableTitle("[bold underline invert]INSTALLED PACKAGES[/]")
            };
            table.AddColumn(new TableColumn("[green]Editor[/]").Centered());
            table.AddColumn(new TableColumn("[gold1]Name[/]").Centered());
            table.AddColumn(new TableColumn("[maroon]Version[/]").Centered());
            table.AddColumn(new TableColumn("[aqua]Links[/]").Centered());

            table.Columns[3].NoWrap();
            foreach (var article in articles_list)
                table.AddRow(article.Editor, article.Name, article.Version.ToString(), $"Open in [link={article.Link}]browser[/] | [link=https://www.google.com/maps/search/{HttpUtility.UrlEncode(article.Name)}]Google Maps[/]");
            table.Expand();

            AnsiConsole.Clear();
            AnsiConsole.Write(table);
        }
        return 0;
    }

    private static Networking.Torrents.DisplayTorrentData _data = new();
    private static bool _new_value;
    private static void RenderDownloadData(object? sender, Networking.Torrents.DisplayTorrentData args)
    {
        lock (_data)
        {
            _data = args;
            _new_value = true;
        }
    }
}