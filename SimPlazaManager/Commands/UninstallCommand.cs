using SimPlazaManager.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SimPlazaManager.Commands;

public class UninstallCommand : Command<UninstallCommand.Arguments>
{
    public class Arguments : CommandSettings
    {
        [CommandArgument(0, "[QUERY/LINK]")]
        [Description("The queries or links used to search for packages")]
        public string Query { get; set; } = string.Empty;

        [CommandOption("--all")]
        [Description("Remove all installed packages")]
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
                AnsiConsole.MarkupLine("[bold red]No packages installed.[/]");
                return 4;
            }

            foreach (var pkg in packages)
            {
                try
                {
                    pkg.Uninstall();
                    AnsiConsole.MarkupLine($"[green]Package[/] {pkg.WebArticle} [green]successfully uninstalled.[/]");
                }
                catch (System.Exception e)
                {
                    AnsiConsole.MarkupLine($"[bold red]{e.Message}[/]");
                    return 3;
                }
            }
        }

        packages = args.Query.StartsWith("https") ? Settings.PackagesByLink(args.Query) : Settings.PackagesByQuery(args.Query);

        if (packages is null || packages.Count == 0)
        {
            AnsiConsole.MarkupLine($"[bold red]Unable to find package with link or query {args.Query}.[/]");
            return 1;
        }

        string res = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select the package you want to uninstall")
                            .PageSize(10)
                            .MoreChoicesText("[grey](Move up and down to see more packages)[/]")
                            .AddChoices(packages.Select(x => x.WebArticle.ToString())));
        var possible_package = packages.Find(x => x.WebArticle.ToString() == res);
        if (possible_package is null)
        {
            AnsiConsole.MarkupLine("[bold red]Unknown error:[/] Please try again.");
            return 2;
        }

        try
        {
            possible_package.Uninstall();
            AnsiConsole.MarkupLine($"[green]Package[/] {possible_package.WebArticle} [green]successfully uninstalled.[/]");
        }
        catch (System.Exception e)
        {
            AnsiConsole.MarkupLine($"[bold red]{e.Message}[/]");
            return 3;
        }
        return 0;
    }
}
