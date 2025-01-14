using System.Collections.Generic;
using HtmlAgilityPack;
using SimPlazaManager.Extensions;
using System.Xml;

namespace SimPlazaManager.Networking;

public interface ISource
{
    string Name { get; }
    string Address { get; }

    public string Info(HtmlNode article);
    public string Description(HtmlNode article);
    public string Link(HtmlNode article, string article_link);
    public List<uint> Compatibility(HtmlNode article);
}

public class SimPlaza : ISource
{
    public string Name => "SimPlaza";
    public string Address => "https://simplaza.org";

    public string Info(HtmlNode article)
    {
        var boxes_nodes = article.SelectNodes("//div[@class='su-box su-box-style-default']");
        string info = string.Empty;
        foreach (var box in boxes_nodes)
        {
            string inner_text = box.SelectSingleNode(".//div[@class='su-box-title']").InnerText;
            if (inner_text != "Info") continue;
            foreach (var text in box.ChildNodes)
                if (text.InnerText != "Info")
                    info += text.InnerText + "\n";
        }

        return info;
    }

    public string Description(HtmlNode article)
    {
        var possible_description_boxes = article.SelectNodes("//div[@class='su-spoiler su-spoiler-style-fancy su-spoiler-icon-plus-square-1 su-spoiler-closed']");
        string description = string.Empty;
        foreach (var box in possible_description_boxes)
        {
            var title_node = box.SelectSingleNode(".//div[@class='su-spoiler-title']");
            if (title_node is null || title_node.InnerText != "Description")
                continue;
            foreach (var text in box.SelectSingleNode(".//div[@class='su-spoiler-content su-u-clearfix su-u-trim']").ChildNodes)
                description += text.InnerText + "\n";
        }

        return description;
    }

    public string Link(HtmlNode article, string article_link)
    {
        string torrent_link = string.Empty;
        var items = new XmlDocument().GetItems(Networking.HttpGet("https://simplaza.org/torrent/rss-sp-only.xml"));
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item is null)
                continue;

            string searchable_link = article_link;
            if (searchable_link.EndsWith('/'))
                searchable_link = article_link[..^1];

            if (item.InnerXml.Contains(searchable_link))
            {
                HtmlNode doc = new HtmlDocument().NodeFromRawString(item.SelectSingleNode("description").InnerText);

                string link = doc.SelectSingleNode("//a").Attributes["href"].Value;
                if (link != searchable_link && link != article_link)
                    continue;

                torrent_link = item.SelectSingleNode("enclosure").Attributes["url"].InnerText;
                break;
            }
        }

        return torrent_link;
    }

    public List<uint> Compatibility(HtmlNode article)
    {
        return [2020];
    }
}

public class SceneryAddons : ISource
{
    public string Name => "SceneryAddons";
    public string Address => "https://sceneryaddons.org/";

    public string Info(HtmlNode article)
    {
        var info = article.SelectSingleNode("//div[@class='scad-info']");
        return info is null ? string.Empty : info.InnerText;
    }

    public string Description(HtmlNode article)
    {
        return article.SelectSingleNode("//div[@class='scad-description']").InnerText;
    }

    public string Link(HtmlNode article, string article_link)
    {
        string torrent_link = string.Empty;
        var items = new XmlDocument().GetItems(Networking.HttpGet("https://sceneryaddons.org/torrent/rss.xml"));
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item is null)
                continue;

            string searchable_link = article_link;
            if (searchable_link.EndsWith('/'))
                searchable_link = article_link[..^1];

            if (item.InnerXml.Contains(searchable_link))
            {
                HtmlNode doc = new HtmlDocument().NodeFromRawString(item.SelectSingleNode("description").InnerText);

                string link = doc.SelectSingleNode("//a").Attributes["href"].Value;
                if (link != searchable_link && link != article_link)
                    continue;

                torrent_link = item.SelectSingleNode("link").InnerText;
                break;
            }
        }

        return torrent_link;
    }

    public List<uint> Compatibility(HtmlNode article)
    {
        List<uint> compatibility = [];
        if (article.InnerText.Contains("MSFS 2020"))
            compatibility.Add(2020);
        if (article.InnerText.Contains("MSFS 2024"))
            compatibility.Add(2024);
        return compatibility;
    }
}