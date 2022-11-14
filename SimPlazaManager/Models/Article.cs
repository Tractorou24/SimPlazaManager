using System;

namespace SimPlazaManager.Models;

public class Article
{
    public class ArticleDetails
    {
        public string Description { get; init; } = string.Empty;
        public string Info { get; init; } = string.Empty;
        public string DownloadLink { get; init; } = string.Empty;
    }

    public uint Id { get; init; } = 0;
    public string Link { get; init; } = string.Empty;
    public string Editor { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public SerializableVersion Version { get; init; } = SerializableVersion.Parse("0.0.0");
    public DateTime Date { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public Lazy<ArticleDetails> Details { get; set; } = new();

    public override string ToString()
    {
        return string.Format(_model, Editor, Name, Version);
    }

    private static readonly string _model = "{0} - {1} - v{2}";
}