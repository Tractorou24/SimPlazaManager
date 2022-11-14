using SimPlazaManager.Extensions;
using Spectre.Console;
using Spectre.Console.Cli;
using System.IO;

namespace SimPlazaManager.Commands;

public class CleanCommand : Command
{
    public override int Execute(CommandContext context)
    {
        new DirectoryInfo("torrents").Empty();
        new DirectoryInfo("package_downloads").Empty();
        new DirectoryInfo("images").Empty();
        AnsiConsole.MarkupLine("[green]Temporary folders cleaned successfully.[/]");
        return 0;
    }
}
