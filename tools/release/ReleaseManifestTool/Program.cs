using OView.Tools.ReleaseManifest;

return args switch
{
    ["name", var key] => RunName(key),
    ["generate-manifest", var stageDir] => ReleaseManifest.GenerateManifest(stageDir, Console.Out),
    ["verify", var stageDir] => ReleaseManifest.Verify(stageDir, Console.Out),
    _ => Usage(),
};

static int RunName(string key)
{
    var value = ReleaseManifest.Name(key);
    if (value is null)
    {
        Console.Error.WriteLine($"unknown name key: {key}");
        return 1;
    }
    Console.WriteLine(value);
    return 0;
}

static int Usage()
{
    Console.Error.WriteLine("""
        usage:
          ReleaseManifestTool name <windows-installer|windows-portable|checksums>
          ReleaseManifestTool generate-manifest <stage-dir>
          ReleaseManifestTool verify <stage-dir>
        """);
    return 1;
}
