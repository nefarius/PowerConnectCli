using System;
using System.Linq;

using JetBrains.Annotations;

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///     - JetBrains ReSharper        https://nuke.build/resharper
    ///     - JetBrains Rider            https://nuke.build/rider
    ///     - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///     - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;

    static AbsolutePath PublishRoot => RootDirectory / "publish";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetClean(s => s
                .SetProject(Solution)
                .SetConfiguration(Configuration));
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetNoRestore(true));
        });

    [UsedImplicitly]
    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTasks.DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetNoBuild(true));
        });

    [UsedImplicitly]
    Target PublishLinux => _ => _
        .Description("Publish a self-contained single-file linux-x64 pc28xx binary")
        .Executes(() => PublishCli("linux-x64"));

    [UsedImplicitly]
    Target PublishWindows => _ => _
        .Description("Publish a self-contained single-file win-x64 pc28xx binary")
        .Executes(() => PublishCli("win-x64"));

    [UsedImplicitly]
    Target Pack => _ => _
        .Description("Pack Nefarius.PowerConnect.Ews and Nefarius.PowerConnect.CLI")
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTasks.DotNetPack(s => s
                .SetProject(Solution)
                .SetConfiguration(Configuration.Release)
                .SetOutput(RootDirectory / "artifacts"));
        });

    void PublishCli(string runtime)
    {
        Project project = Solution.GetAllProjects("pc28xx").Single();
        AbsolutePath output = PublishRoot / runtime;

        DotNetTasks.DotNetPublish(s => s
            .SetProject(project)
            .SetConfiguration(Configuration.Release)
            .SetRuntime(runtime)
            .SetOutput(output)
            .AddProperty("PublishSingleFile", "true")
            .AddProperty("SelfContained", "true")
            .AddProperty("IncludeNativeLibrariesForSelfExtract", "true")
            .AddProperty("DebugType", "embedded"));
    }
}
