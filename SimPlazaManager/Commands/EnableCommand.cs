using SimPlazaManager.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace SimPlazaManager.Commands;

public class EnableCommand : Command<EnableCommand.Arguments>
{
    public class Arguments : CommandSettings
    {
        [CommandArgument(0, "[QUERY/LINK]")]
        [Description("The queries or links used to find packages to upgrade")]
        public string Query { get; set; } = string.Empty;

        [CommandOption("-a|--all")]
        [Description("Upgrade all installed packages to newest version")]
        [DefaultValue(false)]
        public bool All { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arguments args)
    {
        List<Package>? packages;
        if (args.All)
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

        foreach (Package package in packages)
        {
            if (package.IsEnabled)
                continue;

            package.Enable();
            AnsiConsole.MarkupLine($"[bold green]Package[/] {package.WebArticle}[bold green] enabled.[/]");
        }
        return 0;
    }
}
