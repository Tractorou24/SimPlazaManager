using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Web;

namespace SimPlazaManager.Commands;

public class ListCommand : Command<ListCommand.Arguments>
{
    public class Arguments : CommandSettings
    {
        [CommandOption("-n|--name")]
        [Description("Filter results by name")]
        [DefaultValue(false)]
        public bool Name { get; set; }

        [CommandOption("-d|--date")]
        [Description("Filter results by date")]
        [DefaultValue(false)]
        public bool Date { get; set; }

        [CommandOption("-c|--count")]
        [Description("Show no more than specified number of results (between 1 and 999999)")]
        [DefaultValue(999999)]
        public int Count { get; set; }
    }

    public override ValidationResult Validate([NotNull] CommandContext context, [NotNull] Arguments args)
    {
        if (args.Name && args.Date)
            return ValidationResult.Error("Can't sort by name and by date at the same time.");
        return base.Validate(context, args);
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arguments args)
    {
        var packages = Settings.GetAllPackages();
        if (packages is null)
        {
            AnsiConsole.MarkupLine("[bold red]You don't currently have installed packages.[/]");
            return 1;
        }

        packages = packages.Take(args.Count).ToList();
        if (args.Name)
            packages = packages.OrderBy(x => x.WebArticle.Date).ToList();

        if (args.Date)
            packages = packages.OrderBy(x => x.WebArticle.Name).ToList();

        Table table = new()
        {
            Title = new TableTitle("[bold underline invert]INSTALLED PACKAGES[/]")
        };
        table.AddColumn(new TableColumn("[green]Editor[/]").Centered());
        table.AddColumn(new TableColumn("[gold1]Name[/]").Centered());
        table.AddColumn(new TableColumn("[maroon]Version[/]").Centered());
        table.AddColumn(new TableColumn("[deeppink4_1]Enabled[/]").Centered());
        table.AddColumn(new TableColumn("[aqua]Links[/]").Centered());

        table.Columns[3].NoWrap();
        foreach (var pkg in packages)
            table.AddRow(pkg.WebArticle.Editor, pkg.WebArticle.Name, pkg.WebArticle.Version.ToString(), pkg.IsEnabled ? "Yes" : "No", $"Open in [link={pkg.WebArticle.Link}]SimPlaza[/] | [link=https://www.google.com/maps/search/{HttpUtility.UrlEncode(pkg.WebArticle.Name)}]Google Maps[/]");
        table.Expand();

        AnsiConsole.Write(table);
        return 0;
    }
}
