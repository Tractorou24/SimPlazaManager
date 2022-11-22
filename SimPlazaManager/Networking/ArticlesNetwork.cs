using HtmlAgilityPack;
using SimPlazaManager.Extensions;
using SimPlazaManager.Models;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SimPlazaManager.Networking;

public class ArticlesNetwork
{
    public static ulong MaxPage() => Task.Run(async () => await MaxPageAsync()).Result;
    public static async Task<ulong> MaxPageAsync()
    {
        string html_data = await HttpGetAsync(string.Format(_url, "page/1/"));
        HtmlNode document_node = new HtmlDocument().NodeFromRawString(html_data);

        ulong max = 1;
        HtmlNode numbers = document_node.SelectSingleNode("//ul[@class='page-numbers']");
        foreach (HtmlNode nb_node in numbers.ChildNodes)
            if (ulong.TryParse(nb_node.InnerText, out ulong tmp))
                max = Math.Max(max, tmp);
        return max;
    }

    public static ulong MaxPageByQuery(string query) => Task.Run(async () => await MaxPageByQueryAsync(query)).Result;
    public static async Task<ulong> MaxPageByQueryAsync(string query)
    {
        string html_data = await HttpGetAsync(string.Format(_url, $"?s={HttpUtility.UrlEncode(query)}"));
        HtmlNode document_node = new HtmlDocument().NodeFromRawString(html_data);

        ulong max = 1;
        HtmlNode numbers = document_node.SelectSingleNode("//ul[@class='page-numbers']");
        if (numbers is null)
            return max;

        foreach (HtmlNode nb_node in numbers.ChildNodes)
            if (ulong.TryParse(nb_node.InnerText, out ulong tmp))
                max = Math.Max(max, tmp);
        return max;
    }

    public static IList<Article>? ArticlesByQuery(string query, int page = 1, ProgressTask? progress_task = null)
    {
        IList<Article> articles = new List<Article>();

        string str = string.Format(_url, $"/page/{page}/?s={HttpUtility.UrlEncode(query)}");
        string html_data = HttpGet(str, progress_task);
        var main_node = new HtmlDocument().NodeFromRawString(html_data);

        var nodes = main_node.SelectNodes("//article");
        if (nodes is null)
            return null;

        ParseArticles(nodes, ref articles);
        return articles;
    }

    public static IList<Article> ArticlesByPage(int page) => ArticlesByPageRange(page, page + 1);
    public static IList<Article> ArticlesByPageRange(int page_start, int page_end)
    {
        IList<Article> articles = new List<Article>();
        Parallel.For(page_start, page_end, (current_page) =>
        {
            string html_data = HttpGet(string.Format(_url, $"page/{current_page}/"));
            var article_nodes = new HtmlDocument().NodeFromRawString(html_data).SelectNodes("//article");

            ParseArticles(article_nodes, ref articles);
        });
        return articles;
    }

    public static Article? ArticleByLink(string link, ProgressTask? progress_task = null) => Task.Run(async () => await ArticleByLinkAsync(link, progress_task)).Result;
    public static async Task<Article?> ArticleByLinkAsync(string link, ProgressTask? progress_task = null)
    {
        var response = await _client.GetAsync(link);
        Uri? request_uri = response.RequestMessage!.RequestUri;
        if (request_uri is null || request_uri.ToString() == "https://www.simplaza.org/")
            return null;

        string html_data = await HttpGetAsync(link, progress_task);
        if (html_data.Length == 0)
            return null;

        HtmlNode article_node = new HtmlDocument().NodeFromRawString(html_data).SelectSingleNode("//article");
        return ParseArticle(article_node, link);
    }

    public static Article? ArticleByOutdatedLink(string old_link, ProgressTask? progress_task = null) => Task.Run(async () => await ArticleByOutdatedLinkAsync(old_link, progress_task)).Result;
    public static async Task<Article?> ArticleByOutdatedLinkAsync(string old_link, ProgressTask? progress_task = null)
    {
        var response = await _client.GetAsync(old_link);
        Uri? request_uri = response.RequestMessage!.RequestUri;
        if (request_uri is null || request_uri.ToString() == "https://simplaza.org/")
            return null;

        return await ArticleByLinkAsync(request_uri.ToString());
    }

    public static Article.ArticleDetails ArticleDetails(string article_link, ProgressTask? progress_task = null) => Task.Run(async () => await ArticleDetailsAsync(article_link, progress_task)).Result;
    public static async Task<Article.ArticleDetails> ArticleDetailsAsync(string article_link, ProgressTask? progress_task = null)
    {
        HtmlNode article_node1 = new HtmlDocument().NodeFromRawString(await HttpGetAsync(article_link, progress_task));
        HtmlNode article_node = article_node1.SelectSingleNode(".//article");

        // Description
        HtmlNodeCollection possible_description_boxes = article_node.SelectNodes("//div[@class='su-spoiler su-spoiler-style-fancy su-spoiler-icon-plus-square-1 su-spoiler-closed']");
        string description = string.Empty;
        foreach (var box in possible_description_boxes)
        {
            var title_node = box.SelectSingleNode(".//div[@class='su-spoiler-title']");
            if (title_node is null || title_node.InnerText != "Description")
                continue;
            foreach (var text in box.SelectSingleNode(".//div[@class='su-spoiler-content su-u-clearfix su-u-trim']").ChildNodes)
                description += text.InnerText + "\n";
        }

        // Info
        var boxes_nodes = article_node.SelectNodes("//div[@class='su-box su-box-style-default']");
        string info = string.Empty;
        foreach (var box in boxes_nodes)
        {
            string inner_text = box.SelectSingleNode(".//div[@class='su-box-title']").InnerText;
            if (inner_text == "Info")
                foreach (var text in box.ChildNodes)
                    if (text.InnerText != "Info")
                        info += text.InnerText + "\n";
        }

        if (description.Length == 0)
            description = "No description provided.";
        if (info.Length == 0)
            info = "No info provided.";

        // Download Link
        HtmlNode iframe_node = article_node.SelectNodes(".//iframe").First(node => node.ParentNode.ParentNode.InnerText.Split("\n").First() == "Torrent");
        string iframe_html_data = await HttpGetAsync($"http://simplaza.org/{iframe_node.Attributes["src"].Value}");
        string torrent_link = new HtmlDocument().NodeFromRawString(iframe_html_data).SelectSingleNode("//h2//a").Attributes["href"].Value;

        return new Article.ArticleDetails
        {
            Description = HttpUtility.HtmlDecode(description).Trim('\n').Replace("\n\n", "\n").Replace('•', '-'),
            Info = HttpUtility.HtmlDecode(info).Trim('\n').Replace("\n\n", "\n"),
            DownloadLink = torrent_link
        };
    }

    public static string ImageLocalPath(string image_url, ProgressTask? progress_task) => Task.Run(async () => await ImageLocalPathAsync(image_url, progress_task)).Result;
    public static async Task<string> ImageLocalPathAsync(string image_url, ProgressTask? progress_task)
    {
        string image_path = $"images/{image_url.AsSpan(image_url.LastIndexOf("/") + 1)}";
        if (File.Exists(image_path))
            return image_path;

        byte[] image_content = await HttpGetBytesAsync(image_url, progress_task);
        await File.WriteAllBytesAsync(image_path, image_content);
        return image_path;
    }

    public static string TorrentLocalPath(Article article, ProgressTask? progress_task = null) => Task.Run(async () => await TorrentLocalPathAsync(article, progress_task)).Result;
    public static async Task<string> TorrentLocalPathAsync(Article article, ProgressTask? progress_task = null)
    {
        string download_url = article.Details.Value.DownloadLink;
        string torrent_path = $"torrents/{article.Editor}-{article.Name.Replace('/', ' ').Replace('\\', ' ')}-{article.Version}.torrent";
        if (!File.Exists(torrent_path))
        {
            byte[] file_bytes = await HttpGetBytesAsync(download_url, progress_task);
            if (file_bytes.Length == 0)
                return string.Empty;

            await File.WriteAllBytesAsync(torrent_path, file_bytes);
        }
        else
            if (progress_task is not null) progress_task.Value = double.MaxValue;
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
            HtmlNode article_node = article.SelectSingleNode(".//div[@class='nv-post-thumbnail-wrap']//a");

            try
            {
                string title = HttpUtility.HtmlDecode(article_node.Attributes["title"].Value);
                string editor = title[..title.IndexOf(" – ")];
                string link = article_node.Attributes["href"].Value;
                string version = new Regex("v(\\d+\\.)?(\\d+\\.)*(\\*|\\d+)( |$)").Match(title).Value;

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
            catch (Exception) { }
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
        catch (Exception) { }
        return null;
    }

    private static string HttpGet(string url, ProgressTask? progress_task = null) => Task.Run(async () => await HttpGetAsync(url, progress_task)).Result;
    private static async Task<string> HttpGetAsync(string url, ProgressTask? progress_task = null) => Encoding.UTF8.GetString(await HttpGetBytesAsync(url, progress_task));
    private static async Task<byte[]> HttpGetBytesAsync(string url, ProgressTask? progress_task = null)
    {
        if (progress_task is null)
            return await (await _client.GetAsync(url)).Content.ReadAsByteArrayAsync();

        using HttpResponseMessage response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        progress_task.MaxValue(response.Content.Headers.ContentLength ?? (await response.Content.ReadAsStringAsync()).Length);
        progress_task.StartTask();

        byte[] buffer = new byte[128];
        await using var content_stream = await response.Content.ReadAsStreamAsync();
        using var memory_stream = new MemoryStream((int)(response.Content.Headers.ContentLength ?? 0));

        while (true)
        {
            int read_bytes = await content_stream.ReadAsync(buffer);
            if (read_bytes == 0)
            {
                progress_task.Value = double.MaxValue;
                break;
            }
            await memory_stream.WriteAsync(buffer.AsMemory(0, read_bytes));
            progress_task.Increment(read_bytes);
        }
        progress_task.Value = double.MaxValue;
        return memory_stream.ToArray();
    }

    private static readonly string _url = "https://simplaza.org/{0}";
    private static readonly HttpClient _client = new();
}
