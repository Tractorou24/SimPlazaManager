using SimPlazaManager.Models;
using SimPlazaManager.Networking;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Web;

namespace SimPlazaManager.Commands;

public class SearchCommand : Command<SearchCommand.Arguments>
{
    public class Arguments : CommandSettings
    {
        [CommandArgument(0, "[QUERY]")]
        [Description("The query used to search for packages")]
        public string Query { get; set; } = string.Empty;

        [CommandOption("-p|--page")]
        [Description("The page of the result")]
        [DefaultValue(1)]
        public int Page { get; set; }

        [CommandOption("-c|--count")]
        [Description("Show no more than specified number of results (between 1 and 21)")]
        [DefaultValue(0)]
        public int Count { get; set; }

        [CommandOption("-n|--name")]
        [Description("Filter results by name")]
        [DefaultValue(false)]
        public bool Name { get; set; }

        [CommandOption("-f|--force")]
        [Description("Disable simulator version filter")]
        public bool Force { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arguments args)
    {
        IList<Article> articles;
        bool print_next_page;

        if (args.Query.Length != 0)
        {
            var max_page = ArticlesNetwork.MaxPageByQuery(args.Query);
            var tmp_articles = ArticlesNetwork.ArticlesByQuery(args.Query, args.Page, !args.Force);
            if (tmp_articles is null || tmp_articles.Count == 0)
            {
                AnsiConsole.MarkupLine($"[bold red]No results[/] for '{args.Query}'. Try again with another query !");
                return 1;
            }

            print_next_page = max_page > 1 && args.Page <= 1;
            articles = tmp_articles;
        }
        else
        {
            articles = ArticlesNetwork.ArticlesByPage(args.Page, !args.Force);
            print_next_page = true;
        }

        if (args.Count != 0)
        {
            articles = articles.Take(args.Count).ToList();
            print_next_page = false;
        }

        if (args.Name)
            articles = articles.OrderBy(a => a.Name).ToList();

        Table table = new()
        {
            Title = new TableTitle("[bold underline invert]SEARCH RESULTS[/]")
        };
        table.AddColumn(new TableColumn("[green]Editor[/]").Centered());
        table.AddColumn(new TableColumn("[gold1]Name[/]").Centered());
        table.AddColumn(new TableColumn("[maroon]Version[/]").Centered());
        table.AddColumn(new TableColumn("[aqua]Links[/]").Centered());

        table.Columns[3].NoWrap();
        foreach (var article in articles)
            table.AddRow(article.Editor, article.Name, article.Version.ToString(),
                $"Open in [link={article.Link}]browser[/] | [link=https://www.google.com/maps/search/{HttpUtility.UrlEncode(article.Name)}]Google Maps[/]");
        table.Expand();

        AnsiConsole.Write(table);
        if (print_next_page)
            AnsiConsole.MarkupLine("[yellow]Your query may have more results than 21, please specify page number with -p (--page).[/]");
        return 0;
    }
}