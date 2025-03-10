using System.IO;

namespace PhotoExplorer.ReactiveUI.ViewModels;

public record ADirectory(string FullPath)
{
    public string Name => Path.GetFileName(FullPath);
}