using HtmlAgilityPack;
using SimPlazaManager.Extensions;
using SimPlazaManager.Models;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SimPlazaManager.Networking;

public class ArticlesNetwork
{
    public static ulong MaxPageByQuery(string query) => Task.Run(async () => await MaxPageByQueryAsync(query)).Result;

    public static async Task<ulong> MaxPageByQueryAsync(string query)
    {
        ulong max = 1;

        foreach (ISource source in _sources)
        {
            string html_data = await Networking.HttpGetAsync(source.Address + $"/?s={HttpUtility.UrlEncode(query)}");
            HtmlNode document_node = new HtmlDocument().NodeFromRawString(html_data);

            HtmlNode numbers = document_node.SelectSingleNode("//ul[@class='page-numbers']");
            if (numbers is null)
                return max;

            foreach (HtmlNode nb_node in numbers.ChildNodes)
                if (ulong.TryParse(nb_node.InnerText, out ulong tmp))
                    max = Math.Max(max, tmp);
        }

        return max;
    }

    public static IList<Article>? ArticlesByQuery(string query, int page = 1, ProgressTask? progress_task = null)
    {
        IList<Article> articles = new List<Article>();

        foreach (ISource source in _sources)
        {
            string str = source.Address + $"/page/{page}/?s={HttpUtility.UrlEncode(query)}";
            string html_data = Networking.HttpGet(str, progress_task);
            var main_node = new HtmlDocument().NodeFromRawString(html_data);

            var nodes = main_node.SelectNodes("//article");
            if (nodes is null)
                continue;

            ParseArticles(nodes, ref articles);
        }

        return articles;
    }

    public static IList<Article> ArticlesByPage(int page) => ArticlesByPageRange(page, page + 1);

    public static IList<Article> ArticlesByPageRange(int page_start, int page_end)
    {
        IList<Article> articles = new List<Article>();
        Parallel.For(page_start, page_end, (current_page) =>
        {
            foreach (ISource source in _sources)
            {
                string html_data = Networking.HttpGet(source.Address + $"/page/{current_page}/");
                var article_nodes = new HtmlDocument().NodeFromRawString(html_data).SelectNodes("//article");

                ParseArticles(article_nodes, ref articles);
            }
        });
        return articles;
    }

    public static Article? ArticleByLink(string link, ProgressTask? progress_task = null) => Task.Run(async () => await ArticleByLinkAsync(link, progress_task)).Result;

    public static async Task<Article?> ArticleByLinkAsync(string link, ProgressTask? progress_task = null)
    {
        var response = await _client.GetAsync(link);
        Uri? request_uri = response.RequestMessage!.RequestUri;
        if (request_uri is null || _sources.Any(s => s.Address.Contains(request_uri.ToString())))
            return null;

        string html_data = await Networking.HttpGetAsync(link, progress_task);
        if (html_data.Length == 0)
            return null;

        HtmlNode article_node = new HtmlDocument().NodeFromRawString(html_data).SelectSingleNode("//article");
        return ParseArticle(article_node, link);
    }

    public static Article? ArticleByOutdatedLink(string old_link, ProgressTask? progress_task = null) =>
        Task.Run(async () => await ArticleByOutdatedLinkAsync(old_link, progress_task)).Result;

    public static async Task<Article?> ArticleByOutdatedLinkAsync(string old_link, ProgressTask? progress_task = null)
    {
        var response = await _client.GetAsync(old_link);
        Uri? request_uri = response.RequestMessage!.RequestUri;
        if (request_uri is null || _sources.Any(s => s.Address.Contains(request_uri.ToString())))
            return null;

        return await ArticleByLinkAsync(request_uri.ToString());
    }

    public static Article.ArticleDetails ArticleDetails(string article_link, ProgressTask? progress_task = null) =>
        Task.Run(async () => await ArticleDetailsAsync(article_link, progress_task)).Result;

    public static async Task<Article.ArticleDetails> ArticleDetailsAsync(string article_link, ProgressTask? progress_task = null)
    {
        HtmlNode article_node1 = new HtmlDocument().NodeFromRawString(await Networking.HttpGetAsync(article_link, progress_task));
        HtmlNode article_node = article_node1.SelectSingleNode(".//article");
        ISource source = SourceFromLink(article_link);

        string description = source.Description(article_node);
        string info = source.Info(article_node);

        if (description.Length == 0)
            description = "No description provided.";
        if (info.Length == 0)
            info = "No info provided.";

        string torrent_link = source.Link(article_node, article_link);

        return new Article.ArticleDetails
        {
            Description = HttpUtility.HtmlDecode(description).Trim('\n').Replace("\n\n", "\n").Replace('•', '-'),
            Info = HttpUtility.HtmlDecode(info).Trim('\n').Replace("\n\n", "\n"),
            DownloadLink = torrent_link
        };
    }

    public static string TorrentLocalPath(Article article, ProgressTask? progress_task = null) => Task.Run(async () => await TorrentLocalPathAsync(article, progress_task)).Result;

    public static async Task<string> TorrentLocalPathAsync(Article article, ProgressTask? progress_task = null)
    {
        string download_url = article.Details.Value.DownloadLink;
        string torrent_path = AppDomain.CurrentDomain.BaseDirectory + $"torrents/{article.Editor}-{article.Name.Replace('/', ' ').Replace('\\', ' ')}-{article.Version}.";

        if (!File.Exists(torrent_path))
        {
            if (download_url.StartsWith("magnet"))
            {
                await File.WriteAllTextAsync(torrent_path + "magnet", download_url);
            }
            else
            {
                byte[] file_bytes = await Networking.HttpGetBytesAsync(download_url, progress_task);
                if (file_bytes.Length == 0)
                    return string.Empty;

                await File.WriteAllBytesAsync(torrent_path + "torrent", file_bytes);
            }
        }
        else if (progress_task is not null) progress_task.Value = double.MaxValue;

        return torrent_path;
    }


    public static Tuple<int, Article?> CheckUpdate(Article old_article)
    {
        var article = ArticleByOutdatedLink(old_article.Link);
        if (article is null)
            return new Tuple<int, Article?>(2, null);

        if (article.Editor == old_article.Editor && article.Name == old_article.Name)
            if (article.Version == old_article.Version)
                return new Tuple<int, Article?>(1, null);
            else
                return new Tuple<int, Article?>(0, article);
        return new Tuple<int, Article?>(2, null);
    }

    private static void ParseArticles(HtmlNodeCollection article_nodes, ref IList<Article> articles)
    {
        foreach (HtmlNode article in article_nodes)
        {
            HtmlNode article_node = article.SelectSingleNode(".//div[@class='nv-post-thumbnail-wrap img-wrap']//a");

            try
            {
                string title = HttpUtility.HtmlDecode(article_node.Attributes["title"].Value);
                string editor = title[..title.IndexOf(" – ")];
                string link = article_node.Attributes["href"].Value;
                string version = new Regex("v(\\d+\\.)?(\\d+\\.)*(\\*|\\d+)( |$)").Match(title).Value;
                var compatibility = SourceFromLink(link).Compatibility(article);
                if (!compatibility.Contains(Settings.SimVersion()))
                    continue;

                articles.Add(new Article()
                {
                    Id = uint.Parse(article.Id[(article.Id.IndexOf("-") + 1)..]),
                    Link = link,
                    Editor = editor,
                    Version = SerializableVersion.Parse(version[1..]),
                    Name = HttpUtility.HtmlDecode(title.Substring(editor.Length + 3, title[(editor.Length + 3)..].LastIndexOf("v") - 1)),
                    Date = DateTime.Parse(article.SelectSingleNode(".//time[@class='entry-date published']").Attributes["datetime"].Value),
                    ImageUrl = article.SelectSingleNode(".//img").Attributes["src"].Value,
                    Details = new Lazy<Article.ArticleDetails>(() => ArticleDetails(link), System.Threading.LazyThreadSafetyMode.PublicationOnly)
                });
            }
            catch (Exception)
            {
            }
        }
    }

    private static Article? ParseArticle(HtmlNode article_node, string link)
    {
        try
        {
            string title = HttpUtility.HtmlDecode(article_node.SelectSingleNode(".//h1[@class='title entry-title']").InnerText);
            string editor = title[..title.IndexOf(" – ")];
            string version = new Regex("v(\\d+\\.)?(\\d+\\.)*(\\*|\\d+)( |$)").Match(title).Value;

            return new Article()
            {
                Id = uint.Parse(article_node.Id[(article_node.Id.IndexOf("-") + 1)..]),
                Link = link,
                Editor = editor,
                Version = SerializableVersion.Parse(version[1..]),
                Name = HttpUtility.HtmlDecode(title.Substring(editor.Length + 3, title[(editor.Length + 3)..].LastIndexOf("v") - 1)),
                Date = DateTime.Parse(article_node.SelectSingleNode(".//time[@class='entry-date published']").Attributes["datetime"].Value),
                ImageUrl = article_node.SelectSingleNode(".//img").Attributes["src"].Value,
                Details = new Lazy<Article.ArticleDetails>(() => ArticleDetails(link), System.Threading.LazyThreadSafetyMode.PublicationOnly)
            };
        }
        catch (Exception)
        {
        }

        return null;
    }

    private static ISource SourceFromLink(string link)
    {
        ISource result = null;
        uint counter = 0;
        foreach (ISource source in _sources)
        {
            if (link.Contains(source.Address))
            {
                result = source;
                counter++;
            }
        }

        if (counter != 1)
            throw new Exception($"None or multiple sources (count={counter}) for link {link} was found.");
        return result;
    }

    private static readonly ISource[] _sources = [new SimPlaza(), new SceneryAddons()];
    private static readonly HttpClient _client = new();
}