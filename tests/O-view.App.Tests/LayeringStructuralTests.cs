using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OView.App.Tests;

/// <summary>
/// Encodes ADR-0007 D1's admission rule as a durable check, not just a doc statement:
/// O-view.App may reference O-view.Core, but never a skin; O-view.Core may reference
/// neither O-view.App nor a skin. Reads the ProjectReference items straight out of each
/// project's .csproj rather than reflecting on built assemblies, because an unused
/// reference to an interfaces-only project like O-view.App's current state can be elided
/// from the compiled assembly's reference list even though the ProjectReference itself
/// (and the admission rule it represents) still stands.
/// </summary>
public class LayeringStructuralTests
{
    private static readonly string SrcDirectory = FindSrcDirectory();

    private static string FindSrcDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && directory.GetDirectories("src").Length == 0)
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new DirectoryNotFoundException("Could not locate the repository's 'src' directory above " + AppContext.BaseDirectory)
            : Path.Combine(directory.FullName, "src");
    }

    private static string[] ProjectReferences(string projectFolder, string projectFileName)
    {
        var path = Path.Combine(SrcDirectory, projectFolder, projectFileName);
        var document = XDocument.Load(path);
        return document.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")!.Value))
            .ToArray();
    }

    [Fact]
    public void Core_does_not_reference_App()
    {
        Assert.DoesNotContain("O-view.App", ProjectReferences("O-view.Core", "O-view.Core.csproj"));
    }

    [Fact]
    public void Core_does_not_reference_Tray()
    {
        Assert.DoesNotContain("O-view.Tray", ProjectReferences("O-view.Core", "O-view.Core.csproj"));
    }

    [Fact]
    public void Core_does_not_reference_Linux()
    {
        Assert.DoesNotContain("O-view.Linux", ProjectReferences("O-view.Core", "O-view.Core.csproj"));
    }

    [Fact]
    public void App_does_not_reference_Tray()
    {
        Assert.DoesNotContain("O-view.Tray", ProjectReferences("O-view.App", "O-view.App.csproj"));
    }

    [Fact]
    public void App_does_not_reference_Linux()
    {
        Assert.DoesNotContain("O-view.Linux", ProjectReferences("O-view.App", "O-view.App.csproj"));
    }

    [Fact]
    public void App_references_Core()
    {
        Assert.Contains("O-view.Core", ProjectReferences("O-view.App", "O-view.App.csproj"));
    }
}
