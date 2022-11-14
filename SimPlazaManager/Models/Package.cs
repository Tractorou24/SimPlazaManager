using System;
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
    }

    public void Install(string extracted_directory)
    {
        string mods_folder = Settings.ModsFolder();

        InstalledPath = Path.Combine(mods_folder, extracted_directory[(extracted_directory.LastIndexOf("\\") + 1)..]);
        if (Directory.Exists(InstalledPath))
            Directory.Delete(InstalledPath, true);
        Directory.Move(extracted_directory.Replace("/", "\\"), InstalledPath);
        Save();
    }

    public void Uninstall()
    {
        if (IsEnabled)
            Disable();

        Settings.AddOrUpdatePackage(Id, string.Empty);
        Directory.Delete(InstalledPath, true);
        InstalledPath = string.Empty;
    }

    public void Upgrade(Article new_article, string extracted_directory)
    {
        WebArticle = new_article;
        bool was_enabled = IsEnabled;

        Uninstall();
        Install(extracted_directory);
        if (was_enabled)
            Enable();
    }

    public void Enable()
    {
        if (IsEnabled)
            return;

        if (InstalledPath.Length == 0)
            throw new InvalidOperationException("Package is not installed.");

        string community_folder = Settings.CommunityFolder();
        Directory.CreateSymbolicLink($"{community_folder}/{InstalledPath[(InstalledPath.LastIndexOf("\\") + 1)..]}", InstalledPath);
        IsEnabled = true;
        Save();
    }

    public void Disable()
    {
        if (!IsEnabled)
            return;

        if (InstalledPath.Length == 0)
            throw new InvalidOperationException("Package is not installed.");

        string community_folder = Settings.CommunityFolder();
        Directory.Delete($"{community_folder}/{InstalledPath[(InstalledPath.LastIndexOf("\\") + 1)..]}");
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
    public string InstalledPath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = false;
    public Article WebArticle { get; set; }

    private static readonly System.Xml.Serialization.XmlSerializer _serializer = new(typeof(Package));
}
