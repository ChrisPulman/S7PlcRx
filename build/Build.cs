using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.NerdbankGitVersioning;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Nuke.Common.Tools.PowerShell;
using CP.BuildTools;
using System;
using System.Linq;
using System.Diagnostics;

////[GitHubActions(
////    "BuildOnly",
////    GitHubActionsImage.WindowsLatest,
////    OnPushBranchesIgnore = new[] { "main" },
////    FetchDepth = 0,
////    InvokedTargets = new[] { nameof(Compile) })]
////[GitHubActions(
////    "BuildDeploy",
////    GitHubActionsImage.WindowsLatest,
////    OnPushBranches = new[] { "main" },
////    FetchDepth = 0,
////    ImportSecrets = new[] { nameof(NuGetApiKey) },
////    InvokedTargets = new[] { nameof(Compile), nameof(Deploy) })]
partial class Build : NukeBuild
{
    //// Support plugins are available for:
    ////   - JetBrains ReSharper        https://nuke.build/resharper
    ////   - JetBrains Rider            https://nuke.build/rider
    ////   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ////   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Compile);

    [GitRepository] readonly GitRepository Repository;
    [Solution(GenerateProjects = true)] readonly Solution Solution;
    [NerdbankGitVersioning] readonly NerdbankGitVersioning NerdbankVersioning;
    [Parameter][Secret] readonly string NuGetApiKey;
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    AbsolutePath PackagesDirectory => RootDirectory / "output";

    AbsolutePath TestResultsDirectory => RootDirectory / "src" / "TestResults";
    AbsolutePath CoverageReportFile => RootDirectory / "coverage.cobertura.xml";

    Target Print => _ => _
        .Executes(() => Log.Information("NerdbankVersioning = {Value}", NerdbankVersioning.NuGetPackageVersion));

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            if (IsLocalBuild)
            {
                return;
            }

            PackagesDirectory.CreateOrCleanDirectory();
            DotNetWorkloadUpdate();
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() => DotNetRestore(s => s.SetProjectFile(Solution)));

    Target Compile => _ => _
        .DependsOn(Restore, Print)
        .Executes(() => DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()));

    Target Coverage => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var testProject = Solution.GetProject("S7PlcRx.Tests") ?? throw new Exception("Test project 'S7PlcRx.Tests' not found in solution.");
            TestResultsDirectory.CreateOrCleanDirectory();
            CoverageReportFile.DeleteFile();

            var projectPath = (string)testProject.Path;
            var resultsDir = (string)TestResultsDirectory;

            Log.Information("Running net8.0 tests with XPlat Code Coverage (cobertura)...");

            var dotnetExe = Environment.GetEnvironmentVariable("DOTNET_EXE");
            if (string.IsNullOrWhiteSpace(dotnetExe))
            {
                var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                if (!string.IsNullOrWhiteSpace(dotnetRoot))
                {
                    dotnetExe = System.IO.Path.Combine(dotnetRoot, "dotnet.exe");
                }
            }

            if (string.IsNullOrWhiteSpace(dotnetExe))
            {
                dotnetExe = "dotnet";
            }

            // Use external process so we can pass `--collect`, which isn't modeled in this NUKE version.
            var args =
                "test \"" + projectPath + "\"" +
                " -c \"" + Configuration + "\"" +
                " -f net8.0" +
                " --no-build" +
                " --results-directory \"" + resultsDir + "\"" +
                " --collect \"XPlat Code Coverage\"";

            Log.Information("{DotNet} {Args}", dotnetExe, args);

            var psi = new ProcessStartInfo
            {
                FileName = dotnetExe,
                Arguments = args,
                WorkingDirectory = (string)RootDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using (var p = Process.Start(psi)!)
            {
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    Log.Information(stdout);
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Log.Warning(stderr);
                }

                if (p.ExitCode != 0)
                {
                    throw new Exception($"dotnet test failed with exit code {p.ExitCode}");
                }
            }

            var cobertura = TestResultsDirectory.GlobFiles("**/coverage.cobertura.xml")
                .OrderByDescending(x => x.GetLastWriteTimeUtc())
                .FirstOrDefault() ?? throw new Exception("Cobertura report was not produced. Ensure coverlet.collector is referenced.");
            CoverageReportFile.DeleteFile();
            cobertura.Copy(CoverageReportFile);
            Log.Information("Coverage report written to {File}", CoverageReportFile);
        });

    Target Pack => _ => _
    .After(Compile)
    .Produces(PackagesDirectory / "*.nupkg")
    .Executes(() =>
    {
        if (Repository.IsOnMainOrMasterBranch())
        {
            var packableProjects = Solution.GetPackableProjects();

            foreach (var project in packableProjects!)
            {
                Log.Information("Packing {Project}", project.Name);
            }

            DotNetPack(settings => settings
                .SetConfiguration(Configuration)
                .SetVersion(NerdbankVersioning.NuGetPackageVersion)
                .SetOutputDirectory(PackagesDirectory)
                .CombineWith(packableProjects, (packSettings, project) =>
                    packSettings.SetProject(project)));
        }
    });

    Target Deploy => _ => _
    .DependsOn(Pack)
    .Requires(() => NuGetApiKey)
    .Executes(() =>
    {
        if (Repository.IsOnMainOrMasterBranch())
        {
            DotNetNuGetPush(settings => settings
                        .SetSource(this.PublicNuGetSource())
                        .SetSkipDuplicate(true)
                        .SetApiKey(NuGetApiKey)
                        .CombineWith(PackagesDirectory.GlobFiles("*.nupkg"), (s, v) => s.SetTargetPath(v)),
                    degreeOfParallelism: 5, completeOnFailure: true);
        }
    });
}
