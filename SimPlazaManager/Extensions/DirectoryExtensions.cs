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

    public static void Copy(this DirectoryInfo dir, string destinationDir)
    {
        DirectoryInfo[] dirs = dir.GetDirectories();
        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        foreach (DirectoryInfo subDir in dirs)
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            new DirectoryInfo(subDir.FullName).Copy(newDestinationDir);
        }
    }
}
