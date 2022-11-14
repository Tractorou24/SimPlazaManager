using SimPlazaManager.Extensions;
using SimPlazaManager.Models;
using SimPlazaManager.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace SimPlazaManager;

public static class Settings
{
    public static void SetConfig(string community_folder, string mods_folder)
    {
        _settings.SelectSingleNode("/Settings/CommunityFolder")!.InnerText = community_folder;
        _settings.SelectSingleNode("/Settings/ModsFolder")!.InnerText = mods_folder;
        _settings.Save(_settings_path);
    }

    public static void ResetConfig()
    {
        _settings.SelectSingleNode("/Settings/CommunityFolder")!.InnerText = string.Empty;
        _settings.SelectSingleNode("/Settings/ModsFolder")!.InnerText = string.Empty;
    }

    public static string CommunityFolder()
    {
        string path = _settings.SelectSingleNode("/Settings/CommunityFolder")!.InnerText;
        if (!Directory.Exists(path))
            throw new InvalidOperationException("Community folder is not set or is an invalid folder");
        return path;
    }

    public static string ModsFolder()
    {
        string path = _settings.SelectSingleNode("/Settings/ModsFolder")!.InnerText;
        if (!Directory.Exists(path))
            throw new InvalidOperationException("Mods folder is not set or is an invalid folder");
        return path;
    }

    public static void AddOrUpdatePackage(Guid package_id, string package_xml)
    {
        XmlNode? packages_root = _packages.SelectSingleNode("/Packages");
        XmlNodeList? packages = _packages.SelectNodes("/Packages/Package");
        XmlNode? package = null;

        if (packages is not null)
            for (int i = 0; i < packages.Count; i++)
            {
                XmlNode? node = packages.Item(i)!.SelectSingleNode("Id");
                if (node!.InnerText == package_id.ToString())
                {
                    package = packages.Item(i);
                    break;
                }
            }

        if (package is not null && package_xml == string.Empty)
        {
            packages_root!.RemoveChild(package);
            _packages.Save(_packages_path);
            return;
        }

        if (package is null)
        {
            package = _packages.CreateElement("Package");
            packages_root!.AppendChild(package);
        }

        XmlDocument doc = new();
        doc.LoadXml(package_xml);
        package.InnerXml = doc.SelectSingleNode("/Package")!.InnerXml;
        _packages.Save(_packages_path);
    }

    public static Package? GetPackageByArticle(Article article)
    {
        XmlNodeList? packages = _packages.SelectNodes("/Packages/Package");
        if (packages is null)
            return null;

        XmlNode? article_cmp = new XmlDocument().FromArticle(article).SelectSingleNode("Article");
        for (int i = 0; i < packages.Count; i++)
        {
            var item = packages.Item(i);
            if (item!.SelectSingleNode("WebArticle")!.InnerXml != article_cmp!.InnerXml)
                continue;

            using XmlReader reader = new XmlNodeReader(item);
            object? maybe_package = new System.Xml.Serialization.XmlSerializer(typeof(Package)).Deserialize(reader);
            if (maybe_package is null)
                continue;

            Package package = (Package)maybe_package;
            package.WebArticle.Details = new Lazy<Article.ArticleDetails>(() => ArticlesNetwork.ArticleDetails(package.WebArticle.Link), System.Threading.LazyThreadSafetyMode.PublicationOnly);
            return package;
        }
        return null;
    }

    public static List<Package>? GetAllPackages()
    {
        XmlNodeList? packages = _packages.SelectNodes("/Packages/Package");
        if (packages is null)
            return null;

        List<Package> list = new();
        for (int i = 0; i < packages.Count; i++)
        {
            var item = packages.Item(i);
            if (item is null)
                continue;

            using XmlReader reader = new XmlNodeReader(item);
            object? maybe_package = new System.Xml.Serialization.XmlSerializer(typeof(Package)).Deserialize(reader);
            if (maybe_package is null)
                continue;

            Package package = (Package)maybe_package;
            package.WebArticle.Details = new Lazy<Article.ArticleDetails>(() => ArticlesNetwork.ArticleDetails(package.WebArticle.Link), System.Threading.LazyThreadSafetyMode.PublicationOnly);
            list.Add(package);
        }
        return list.Count == 0 ? null : list;
    }

    public static List<Package>? PackagesByLink(string link)
    {
        var all_packages = GetAllPackages();
        if (all_packages is null)
            return null;

        return all_packages.Where(x => x.WebArticle.Link == link).ToList();
    }

    public static List<Package>? PackagesByQuery(string query)
    {
        var all_packages = GetAllPackages();
        if (all_packages is null)
            return null;

        List<Package> results = new();
        foreach (var pkg in all_packages)
        {
            string[] query_args = query.Split(" ");
            string search_data = string.Join(" ", pkg.WebArticle.Editor, pkg.WebArticle.Name, pkg.WebArticle.Version, pkg.WebArticle.Link, pkg.WebArticle.Date);

            foreach (string query_part in query_args)
                if (search_data.Contains(query_part, StringComparison.CurrentCultureIgnoreCase))
                    results.Add(pkg);
        }
        return results.DistinctBy(x => x.WebArticle.ToString()).ToList();
    }

    static readonly string _settings_path = "settings.xml";
    static readonly string _packages_path = "packages.xml";
    static readonly XmlDocument _settings = new XmlDocument().LoadXmlFromFile(_settings_path);
    static readonly XmlDocument _packages = new XmlDocument().LoadXmlFromFile(_packages_path);
}
