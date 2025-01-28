using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;
using SimPlazaManager.Extensions;

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
        var allBoxes = article.SelectNodes("//div[@class='su-box su-box-style-default']");
        var filteredBoxes = allBoxes.Where(b => b.SelectSingleNode("div[@class='su-box-title']").InnerText == "Torrent").ToArray();
        if (filteredBoxes.Length is 0 or > 1)
            throw new Exception($"Cannot find single torrent box (count: {filteredBoxes.Length} in {article_link}");

        string? webLink = filteredBoxes[0].SelectSingleNode("div[@class='su-box-content su-u-clearfix su-u-trim']//a").Attributes["href"].Value;
        if (webLink is null)
            return "";

        webLink = webLink.Replace("&#038;", "&");
        string content = Networking.HttpGet(webLink);
        var doc = new HtmlDocument().NodeFromRawString(content);
        var button = doc.SelectSingleNode("//button");
        return button is null ? "" : button.Attributes["value"].Value;
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

    public string Link(HtmlNode article, string articleLink)
    {
        var box = article.SelectSingleNode("//div[@class='scad-dl-torrent']");
        string? webLink = box.SelectSingleNode("a").Attributes["href"].Value;
        if (webLink is null)
            return "";

        webLink = webLink.Replace("&#038;", "&");
        string content = Networking.HttpGet(webLink);
        var doc = new HtmlDocument().NodeFromRawString(content);
        var button = doc.SelectSingleNode("//button");
        return button is null ? "" : button.Attributes["value"].Value;
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