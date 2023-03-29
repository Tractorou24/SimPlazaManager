using SimPlazaManager.Extensions;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.IO;

namespace SimPlazaManager.Commands;

public class CleanCommand : Command
{
    public override int Execute(CommandContext context)
    {
        new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "torrents").Empty();
        new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "package_downloads").Empty();
        new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "images").Empty();
        AnsiConsole.MarkupLine("[green]Temporary folders cleaned successfully.[/]");
        return 0;
    }
}
