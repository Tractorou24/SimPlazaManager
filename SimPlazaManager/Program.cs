using SimPlazaManager;
using SimPlazaManager.Commands;
using SimPlazaManager.Extensions;
using Spectre.Console;
using Spectre.Console.Cli;
using System.IO;

static void InitializeFileStructure()
{
    Directory.CreateDirectory("torrents").Empty();
    Directory.CreateDirectory("images").Empty();
    Directory.CreateDirectory("package_downloads");
}

try
{
    InitializeFileStructure();

    try
    {
        if (args.Length > 0 && args[0] != "settings")
        {
            _ = Settings.CommunityFolder();
            _ = Settings.ModsFolder();
        }
    }
    catch (System.InvalidOperationException ex)
    {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] {ex.Message}.");
        return;
    }

    var app = new CommandApp();
    app.Configure(config =>
    {
        config.AddCommand<InstallCommand>("install")
            .WithDescription("Installs the given packages");

        config.AddCommand<UninstallCommand>("uninstall")
            .WithDescription("Uninstalls the given packages");

        config.AddCommand<UpgradeCommand>("upgrade")
            .WithDescription("Upgrade package(s) to latest version");

        config.AddCommand<ShowCommand>("show")
            .WithDescription("Find and show basic info of packages");

        config.AddCommand<SearchCommand>("search")
            .WithDescription("Search for a package");

        config.AddCommand<ListCommand>("list")
            .WithDescription("Display installed packages");

        config.AddCommand<CleanCommand>("clean")
            .WithDescription("Clean temporary folders (save disk space)");

        config.AddCommand<SettingsCommand>("settings")
            .WithDescription("Set mods and community folder path");
    });

    app.Run(args);
}
catch (System.Exception ex)
{
    AnsiConsole.WriteException(ex);
}