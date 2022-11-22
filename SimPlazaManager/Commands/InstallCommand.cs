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
using System.Threading.Tasks;

namespace SimPlazaManager.Commands;

public class InstallCommand : Command<InstallCommand.Arguments>
{
    public class Arguments : CommandSettings
    {
        [CommandArgument(0, "<QUERY/LINK>")]
        [Description("The queries or links used to search for packages")]
        public string Query { get; set; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arguments args)
    {
        try
        {
            Article? article = null;
            IList<Article>? possibilities = null;
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
                var get_article = ctx.AddTask("[white]Getting SimPlaza Article(s)[/]", false);

                get_article.StartTask();
                if (args.Query.StartsWith("https://simplaza.org"))
                    article = ArticlesNetwork.ArticleByLink(args.Query, get_article);
                else
                    possibilities = ArticlesNetwork.ArticlesByQuery(args.Query, progress_task: get_article);
                get_article.StopTask();
            });

            if (possibilities is not null)
            {
                string selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                                    .Title("Select the package you want to install")
                                    .PageSize(10)
                                    .MoreChoicesText("[grey](Move up and down to reveal more packages)[/]")
                                    .AddChoices(possibilities.Select(x => x.ToString())));
                article = possibilities.FirstOrDefault(x => x.ToString() == selected);
                if (article is null)
                {
                    AnsiConsole.MarkupLine("[bold red]Unknown error:[/] Please try again.");
                    return 1;
                }
                AnsiConsole.MarkupLine($"Package [yellow]{article}[/] selected");
            }

            if (article is null)
                throw new InvalidOperationException("Unable to find any package for your query/link.");

            Package? maybe_package_installed = Settings.GetPackageByArticle(article);
            if (maybe_package_installed is not null)
            {
                if (maybe_package_installed.IsEnabled)
                    throw new InvalidOperationException("[yellow]Package is already installed and enabled.[/]");
                if (AnsiConsole.Confirm($"Package is already installed [bold red]but not enabled[/].\nEnable package {maybe_package_installed.WebArticle}"))
                {
                    maybe_package_installed.Enable();
                    AnsiConsole.MarkupLine("[green]Package enabled successfully ![/]");
                    return 0;
                }
            }

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
                var get_article_link = ctx.AddTask("[white]Getting Torrent Link[/]", false);
                var download_torrent_file = ctx.AddTask("[white]Downloading Torrent[/]", false);
                var download_package = ctx.AddTask("[white]Downloading Package[/]", false);
                var unpack_file = ctx.AddTask("[white]Unpacking Files[/]", false);
                var install_file = ctx.AddTask("[white]Installing Package[/]", false);

                // Get torrent link
                get_article_link.StartTask();
                if (!article.Details.IsValueCreated)
                    article.Details = new Lazy<Article.ArticleDetails>(() => ArticlesNetwork.ArticleDetails(article.Link, get_article_link), System.Threading.LazyThreadSafetyMode.PublicationOnly);
                _ = article.Details.Value.DownloadLink;
                get_article_link.StopTask();

                // Download torrent file
                download_torrent_file.StartTask();
                _ = ArticlesNetwork.TorrentLocalPath(article, download_torrent_file);
                download_torrent_file.StopTask();

                // Download package from torrent
                download_package.StartTask();
                Networking.Torrents.TorrentDownloader downloader = new();
                downloader.DisplayRequested += RenderDownloadData;
                _ = Task.Run(() => downloader.Download());

                while (_data.Engine is null) { } // Wait until first engine is sent

                lock (_data)
                    download_package.MaxValue = _data.Engine.Torrents.Sum(x => x.Torrent.Size);

                while (!_data.Engine.Torrents.All(x => x.Complete))
                {
                    if (!_new_value)
                        continue;
                    lock (_data)
                        download_package.Value(_data.Engine.Torrents.Sum(x => x.Monitor.DataBytesDownloaded + x.Monitor.ProtocolBytesDownloaded));
                    _new_value = false;
                }
                download_package.Value = double.MaxValue;
                download_package.StopTask();
                new DirectoryInfo("torrents").Empty();

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

                            string directory_path = $"package_downloads/{archive.Entries.First().Key[..archive.Entries.First().Key.IndexOf("\\")]}";
                            if (Directory.Exists(directory_path))
                                Directory.Delete(directory_path, true);

                            foreach (var entry in archive.Entries)
                            {
                                if (entry.IsDirectory)
                                    continue;

                                string destination_path = $"package_downloads\\{entry.Key}";
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
                Package pkg = new(article);
                foreach (string extracted_directory in Directory.EnumerateDirectories("package_downloads"))
                {
                    pkg.Install(extracted_directory);
                    Directory.Delete(extracted_directory, true);
                }
                pkg.Enable();
                install_file.Value = double.MaxValue;
                install_file.StopTask();
            });
            AnsiConsole.MarkupLine("[green]Package installed successfully[/]");
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"[bold red]{e.Message}[/]");
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
