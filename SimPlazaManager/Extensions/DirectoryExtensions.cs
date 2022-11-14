using System.IO;

namespace SimPlazaManager.Extensions;

public static class DirectoryExtensions
{
    public static void Empty(this DirectoryInfo directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory.FullName))
            File.Delete(file);
        foreach (string subdirectory in Directory.EnumerateDirectories(directory.FullName))
            Directory.Delete(subdirectory, true);
    }
}
