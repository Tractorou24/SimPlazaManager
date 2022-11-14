using HtmlAgilityPack;

namespace SimPlazaManager.Extensions;

public static class HtmlDocumentExtensions
{
    public static HtmlNode NodeFromRawString(this HtmlDocument document, string html_data)
    {
        document.LoadHtml(html_data);
        return document.DocumentNode;
    }
}
