using SimPlazaManager.Models;
using System.IO;
using System.Xml;

namespace SimPlazaManager.Extensions;

public static class XmlDocumentExtensions
{
    public static XmlDocument LoadXmlFromFile(this XmlDocument document, string xml_path)
    {
        document.LoadXml(File.ReadAllText(xml_path));
        return document;
    }

    public static XmlDocument FromArticle(this XmlDocument document, Article article)
    {
        StringWriter w = new();
        new System.Xml.Serialization.XmlSerializer(typeof(Article)).Serialize(w, article);
        document.LoadXml(w.ToString());
        return document;
    }

    public static XmlNodeList GetItems(this XmlDocument document, string xml)
    {
        document.LoadXml(xml);
        return document.GetElementsByTagName("item");
    }
}
