using SimPlazaManager.Extensions;
using System;
using System.Collections.Generic;
using System.IO;

namespace SimPlazaManager.Models;

public class Package
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value. Used only for XML parsing
    internal Package()
    {
        if (new System.Diagnostics.StackTrace().GetFrame(1)!.GetMethod()!.Name != "InvokeMethod")
            throw new InvalidOperationException("This constructor is only for XML parsing.");
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value. Used only for XML parsing

    public Package(Article webArticle)
    {
        Id = Guid.NewGuid();
        WebArticle = webArticle;
        InstalledPaths = new();
    }

    public void Install(string extracted_directory)
    {
        string mods_folder = Settings.ModsFolder();

        var installed_path = Path.Combine(mods_folder, extracted_directory[(extracted_directory.LastIndexOf("\\") + 1)..]);
        InstalledPaths.Add(installed_path);
        
        if (Directory.Exists(installed_path))
            Directory.Delete(installed_path, true);
        try
        {
            Directory.Move(extracted_directory.Replace("/", "\\"), installed_path);
        }
        catch (IOException)
        {
            new DirectoryInfo(extracted_directory.Replace("/", "\\")).Copy(installed_path);
        }
        Save();
    }

    public void Uninstall()
    {
        if (IsEnabled)
            Disable();

        Settings.AddOrUpdatePackage(Id, string.Empty);
        InstalledPaths.ForEach(dir => Directory.Delete(dir, true));
        InstalledPaths.Clear();
    }

    public void Upgrade(Article new_article)
    {
        WebArticle = new_article;
        bool was_enabled = IsEnabled;

        Uninstall();

        foreach (string extracted_directory in Directory.EnumerateDirectories("package_downloads"))
        {
            Install(extracted_directory);
            Directory.Delete(extracted_directory, true);
        }

        if (was_enabled)
            Enable();
    }

    public void Enable()
    {
        if (IsEnabled)
            return;

        foreach (var installed_path in InstalledPaths)
        {
            if (installed_path.Length == 0)
                throw new InvalidOperationException("Package is not installed.");

            string community_folder = Settings.CommunityFolder();
            Directory.CreateSymbolicLink($"{community_folder}\\{installed_path[(installed_path.LastIndexOf("\\") + 1)..]}", installed_path);
        }
        IsEnabled = true;
        Save();
    }

    public void Disable()
    {
        if (!IsEnabled)
            return;

        foreach (var installed_path in InstalledPaths)
        {
            if (installed_path.Length == 0)
                throw new InvalidOperationException("Package is not installed.");

            string community_folder = Settings.CommunityFolder();
            Directory.Delete($"{community_folder}/{installed_path[(installed_path.LastIndexOf("\\") + 1)..]}");
        }
        IsEnabled = false;
        Save();
    }

    private void Save()
    {
        StringWriter w = new();
        _serializer.Serialize(w, this);

        Settings.AddOrUpdatePackage(Id, w.ToString());
    }

    public Guid Id { get; set; } = Guid.Empty;
    public List<string> InstalledPaths { get; set; }
    public bool IsEnabled { get; set; } = false;
    public Article WebArticle { get; set; }

    private static readonly System.Xml.Serialization.XmlSerializer _serializer = new(typeof(Package));
}
