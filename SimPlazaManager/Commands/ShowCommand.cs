using SimPlazaManager.Extensions;
using SimPlazaManager.Models;
using SimPlazaManager.Networking;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Web;

namespace SimPlazaManager.Commands;

public class ShowCommand : Command<ShowCommand.Arguments>
{
    public class Arguments : CommandSettings
    {
        [CommandArgument(0, "<QUERY/LINK>")]
        [Description("The queries or links used to search for packages")]
        public string Query { get; set; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arguments args)
    {
        Article? article;

        if (args.Query.StartsWith("https"))
        {
            try
            {
                article = ArticlesNetwork.ArticleByLink(args.Query);
            }
            catch (System.AggregateException)
            {
                AnsiConsole.MarkupLine("[bold red]An invalid link was provided.[/]");
                return 1;
            }
            catch (System.Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]{ex.Message}[/]");
                return 1;
            }
        }
        else
        {
            var articles = ArticlesNetwork.ArticlesByQuery(args.Query);
            if (articles is null)
            {
                AnsiConsole.MarkupLine($"[bold red]No results[/] for '{args.Query}'. Try again with another query !");
                return 2;
            }

            if (articles.Count > 1)
            {
                string article_title = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[green]Please select the package you want to show.[/]")
                        .PageSize(articles.Count + 1)
                        .MoreChoicesText("[grey](Move up and down to reveal more fruits)[/]")
                        .AddChoices(articles.Select(a => $"{a.Editor} - {a.Name} - {a.Version}").ToArray())
                        );
                article = articles.FirstOrDefault(a => $"{a.Editor} - {a.Name} - {a.Version}" == article_title);
            }
            else
                article = articles.First();
        }

        if (article is null)
        {
            AnsiConsole.MarkupLine("[bold red]Please specify a valid link or query.[/]");
            return 3;
        }

        Table table = new()
        {
            Title = new TableTitle($"[bold red]Details for: [/]{article.Editor} - {article.Name}")
        };
        table.AddColumns("Param", "Value");
        table.AddRow("[green]Editor: [/]", $"[white]{article.Editor}[/]");
        table.AddRow("[gold1]Name: [/]", $"[grey70]{article.Name}[/]");
        table.AddRow("[maroon]Version: [/]", $"[white]{article.Version}[/]");
        table.AddRow("[purple]Release Date: [/]", $"[grey70]{article.Date.ToShortDateString()} ({article.Date.TimeAgo()})[/]");
        table.AddRow("[teal]Image: [/]", $"[white link={article.ImageUrl}]See in browser[/]");
        table.AddRow("[aqua]Links: [/]", $"[grey70][link={article.Link}]browser[/] | [link=https://www.google.com/maps/search/{HttpUtility.UrlEncode(article.Name)}]Google Maps[/][/]");
        table.AddRow("Description: ", $"[white]{article.Details.Value.Description}[/]");
        table.AddRow("[orange3]Info: [/]", $"[grey70]{article.Details.Value.Info}[/]");
        table.HideHeaders().Expand();

        AnsiConsole.Write(table);
        return 0;
    }
}
