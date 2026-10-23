// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// S10 revision-2 review R3/R4: the committed <c>Run-S10-Results.ps1</c>, run phase, driven with process doubles in place
    /// of benchmark-tas and the harness, temporary git checkouts in place of SAM and SAM_Tas, and a Tas process name that
    /// matches nothing, so no Tas process is started, stopped or opened. The doubles log every invocation; the tests check
    /// which processes were started, the watchdog, the status file and the wrapper exit. Each workspace is built by the
    /// committed <c>Build-S10-Receipt.ps1</c> with an MSBuild double, so the preflight verifies a real receipt (R3 of the
    /// correction review); R5 runs the scripts' hashing where module autoload cannot supply Get-FileHash.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class S10RunnerScriptTests
    {
        private const string NoTasProcess = "S10RunnerDoubleNoSuchProcess";

        private const string Double = @"$a = @($args)
$mode = if ($a[0] -eq '--model') { 'simulate' } else { $a[0] }
Add-Content -Encoding utf8 $env:S10_DOUBLE_LOG ($mode + ' ' + ($a -join ' '))
$key = ($mode -replace '-', '_').ToUpperInvariant()
$sleep = [Environment]::GetEnvironmentVariable('S10_DOUBLE_SLEEP_' + $key)
if ($sleep) { Start-Sleep -Seconds ([int]$sleep) }
$code = [Environment]::GetEnvironmentVariable('S10_DOUBLE_EXIT_' + $key)
if ($mode -eq 'simulate' -and -not $code -and -not $env:S10_DOUBLE_NO_FILES) {
    $tbd = $a[[array]::IndexOf($a, '--tbd') + 1]
    $out = $a[[array]::IndexOf($a, '--out') + 1]
    foreach ($file in @($tbd, [IO.Path]::ChangeExtension($tbd, '.tsd'), [IO.Path]::ChangeExtension($tbd, '.json'), $out)) { Set-Content -Encoding ascii $file 'double' }
}
if ($code) { exit ([int]$code) }
exit 0
";

        // The MSBuild double Build-S10-Receipt.ps1 drives: a SAM project writes SAM\build\<project>.dll, the SAM_Tas solution
        // writes the harness and benchmark-tas libraries, rewrites their .cmd "apphosts" and copies SAM.Analytical.dll beside
        // them. Each output carries the stamp the script passed (/p:SAMSourceRevision), unless S10_BUILD_DOUBLE_UNSTAMPED.
        // Windows PowerShell splits '-p:Name=value' arguments, so the double reads its raw command line.
        private const string BuildDouble = @"$line = [Environment]::CommandLine
Add-Content -Encoding utf8 $env:S10_BUILD_DOUBLE_LOG $line
if ($env:S10_BUILD_DOUBLE_EXIT) { exit ([int]$env:S10_BUILD_DOUBLE_EXIT) }
$project = @($args)[0]
$revision = if ($line -match '-p:SAMSourceRevision=([0-9a-f]{40})') { $Matches[1] } else { 'none' }
$stamp = if ($env:S10_BUILD_DOUBLE_UNSTAMPED) { '1.0.0.0' } else { '1.0.0.0+' + $revision }
if ($project -like '*.csproj') {
    $name = [IO.Path]::GetFileNameWithoutExtension($project)
    $build = Join-Path (Split-Path (Split-Path (Split-Path $project))) 'build'
    New-Item -ItemType Directory -Force $build | Out-Null
    Set-Content -Encoding ascii (Join-Path $build ($name + '.dll')) ('double ' + $name + ' ' + $stamp)
    exit 0
}
$out = $env:S10_BUILD_DOUBLE_OUT
foreach ($name in 's10-harness.dll', 's10-benchmark.dll', 'SAM.Analytical.Tas.dll') { Set-Content -Encoding ascii (Join-Path $out $name) ('double ' + $name + ' ' + $stamp) }
foreach ($name in 's10-harness.cmd', 's10-benchmark.cmd') { $path = Join-Path $out $name; [IO.File]::WriteAllText($path, [IO.File]::ReadAllText($path)) }
Copy-Item (Join-Path (Split-Path (Split-Path $project)) 'SAM\build\SAM.Analytical.dll') $out
exit 0
";

        // A Microsoft.PowerShell.Utility that shadows the Windows PowerShell 5.1 one and cannot load: module autoload then
        // cannot provide Get-FileHash, the failure every runner test hit on the hosted runner (review R5).
        private const string ShadowUtility = @"@{
GUID = '1DA87E53-152B-403E-98DC-74D7B4D63D59'
ModuleVersion = '7.0.0.0'
CompatiblePSEditions = @('Core')
PowerShellVersion = '3.0'
NestedModules = 'Microsoft.PowerShell.Commands.Utility.dll'
CmdletsToExport = @('Get-FileHash')
FunctionsToExport = @()
}
";

        private sealed class Workspace
        {
            public string Directory, Doubles, Sam, Runner, Root, Log, BuildLog, Weather, WeatherSha, RunnerBuilt, SamBuilt;
            public string Harness => Path.Combine(Doubles, "s10-harness.cmd");
            public string Benchmark => Path.Combine(Doubles, "s10-benchmark.cmd");
            public string Receipt => Path.Combine(Doubles, "s10-build-receipt.json");
        }

        private readonly List<string> cleanup = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (string directory in cleanup) DeleteTree(directory);
            cleanup.Clear();
        }

        /// <summary>Deletes a test folder, including git's read-only object files; a locked file is left for the next run.</summary>
        private static void DeleteTree(string directory)
        {
            if (!Directory.Exists(directory)) return;
            try
            {
                foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(directory, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static string Script(string name = "Run-S10-Results.ps1")
        {
            for (DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "SAM_Tas", "SAM.Analytical.Tas.DirectT3D.Validation", name);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException(name + " not found above " + TestContext.CurrentContext.TestDirectory);
        }

        private static string BuildScript() => Script("Build-S10-Receipt.ps1");

        private static string Sha(string path)
        {
            using (FileStream stream = File.OpenRead(path)) using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string Git(string directory, params string[] arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (string argument in new[] { "-c", "user.name=S10 test", "-c", "user.email=s10@example.invalid", "-c", "core.autocrlf=false", "-c", "commit.gpgsign=false" }.Concat(arguments)) info.ArgumentList.Add(argument);
            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.That(process.ExitCode, Is.EqualTo(0), "git " + string.Join(" ", arguments) + ": " + error);
                return output.Trim();
            }
        }

        /// <summary>A clean checkout with one tracked source file and an ignored build/bin folder.</summary>
        private static string Checkout(string directory, string ignored)
        {
            Directory.CreateDirectory(directory);
            Git(directory, "init", "-q");
            File.WriteAllText(Path.Combine(directory, ".gitignore"), ignored + "/\n");
            File.WriteAllText(Path.Combine(directory, "Source.cs"), "// S10 runner test source\n");
            File.SetLastWriteTimeUtc(Path.Combine(directory, "Source.cs"), DateTime.UtcNow.AddMinutes(-10));
            Git(directory, "add", "-A");
            Git(directory, "commit", "-q", "-m", "S10 runner test");
            return Git(directory, "rev-parse", "HEAD");
        }

        private Workspace Create(bool build = true)
        {
            Workspace w = new Workspace { Directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "s10-runner-" + Guid.NewGuid().ToString("N").Substring(0, 8)) };
            cleanup.Add(w.Directory);
            w.Doubles = Path.Combine(w.Directory, "doubles");
            w.Sam = Path.Combine(w.Directory, "SAM");
            w.Runner = Path.Combine(w.Directory, "SAM_Tas");
            w.Log = Path.Combine(w.Directory, "invocations.log");
            w.BuildLog = Path.Combine(w.Directory, "build.log");
            Directory.CreateDirectory(w.Doubles);
            w.SamBuilt = Checkout(w.Sam, "build");
            w.RunnerBuilt = Checkout(w.Runner, "bin");

            File.WriteAllText(Path.Combine(w.Doubles, "double.ps1"), Double);
            File.WriteAllText(Path.Combine(w.Doubles, "msbuild-double.ps1"), BuildDouble);
            File.WriteAllText(Path.Combine(w.Doubles, "msbuild-double.cmd"), "@powershell -NoProfile -ExecutionPolicy Bypass -File \"%~dp0msbuild-double.ps1\" %*\r\n@exit /b %ERRORLEVEL%\r\n");
            foreach (string name in new[] { "s10-harness", "s10-benchmark" })
            {
                File.WriteAllText(Path.Combine(w.Doubles, name + ".cmd"), "@powershell -NoProfile -ExecutionPolicy Bypass -File \"%~dp0double.ps1\" %*\r\n@exit /b %ERRORLEVEL%\r\n");
            }

            w.Weather = Path.Combine(w.Directory, "weather.epw");
            File.WriteAllText(w.Weather, "LOCATION,S10 runner test double\n");
            w.WeatherSha = Sha(w.Weather);

            // A short, fresh output root (Tas refuses long paths; the script enforces at most 40 characters).
            string temp = Path.GetTempPath();
            string suffix = "s" + Guid.NewGuid().ToString("N").Substring(0, 4);
            w.Root = temp.Length + suffix.Length <= 40 ? Path.Combine(temp, suffix) : Path.Combine(Path.GetPathRoot(temp), "s10t" + suffix);
            cleanup.Add(w.Root);

            if (build)
            {
                // The legitimate fresh build: the committed build script, with the MSBuild double in place of VS MSBuild.
                Built built = Build(w);
                Assert.That(built.ExitCode, Is.EqualTo(0), built.Output);
                Assert.That(File.Exists(w.Receipt), Is.True, built.Output);
            }

            return w;
        }

        private sealed class Built
        {
            public int ExitCode;
            public string Output;
            public List<string> Invocations;
        }

        private static (int ExitCode, string Output) PowerShell(IEnumerable<string> arguments, Dictionary<string, string> environment)
        {
            ProcessStartInfo info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (string argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File" }.Concat(arguments)) info.ArgumentList.Add(argument);
            foreach (KeyValuePair<string, string> pair in environment) info.Environment[pair.Key] = pair.Value;
            using (Process process = Process.Start(info))
            {
                System.Threading.Tasks.Task<string> error = process.StandardError.ReadToEndAsync();
                string output = process.StandardOutput.ReadToEnd();
                Assert.That(process.WaitForExit(300000), Is.True, "the script did not finish");
                return (process.ExitCode, output + error.Result);
            }
        }

        private static Built Build(Workspace w, Dictionary<string, string> environment = null)
        {
            Dictionary<string, string> variables = new Dictionary<string, string> { ["S10_BUILD_DOUBLE_LOG"] = w.BuildLog, ["S10_BUILD_DOUBLE_OUT"] = w.Doubles };
            foreach (KeyValuePair<string, string> pair in environment ?? new Dictionary<string, string>()) variables[pair.Key] = pair.Value;
            if (File.Exists(w.BuildLog)) File.Delete(w.BuildLog);
            (int exitCode, string output) = PowerShell(new[]
            {
                BuildScript(), "-MSBuild", Path.Combine(w.Doubles, "msbuild-double.cmd"), "-Exe", w.Harness, "-BenchmarkExe", w.Benchmark, "-SamRepository", w.Sam, "-RunnerRepository", w.Runner,
            }, variables);
            return new Built { ExitCode = exitCode, Output = output, Invocations = File.Exists(w.BuildLog) ? File.ReadAllLines(w.BuildLog).Where(x => x.Trim().Length > 0).ToList() : new List<string>() };
        }

        /// <summary>Commits one file to a test checkout, dated before the build so only the receipt binding can object.</summary>
        private static string Commit(string checkout, string file, string text)
        {
            string path = Path.Combine(checkout, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-10));
            Git(checkout, "add", "-A");
            Git(checkout, "commit", "-q", "-m", "S10 runner test: " + file);
            return Git(checkout, "rev-parse", "HEAD");
        }

        private sealed class Run
        {
            public int ExitCode;
            public string Output;
            public List<string> Invocations;
            public JsonElement Status;
            public JsonElement[] Watchdog;
            public bool HasStatus;
        }

        private Run Execute(Workspace w, Dictionary<string, string> environment = null, string weatherSha = null, int timeoutSeconds = 120, string harness = null)
        {
            Dictionary<string, string> variables = new Dictionary<string, string> { ["S10_DOUBLE_LOG"] = w.Log };
            foreach (KeyValuePair<string, string> pair in environment ?? new Dictionary<string, string>()) variables[pair.Key] = pair.Value;
            (int exitCode, string output) = PowerShell(new[]
            {
                Script(), "-Phase", "run", "-Root", w.Root, "-WeatherEpw", w.Weather, "-WeatherEpwSha256", weatherSha ?? w.WeatherSha,
                "-Exe", harness ?? w.Harness, "-BenchmarkExe", w.Benchmark, "-SamRepository", w.Sam, "-RunnerRepository", w.Runner, "-TasProcessNames", NoTasProcess, "-TimeoutSeconds", timeoutSeconds.ToString(),
            }, variables);
            Run run = new Run { ExitCode = exitCode, Output = output, Invocations = File.Exists(w.Log) ? File.ReadAllLines(w.Log).Where(x => x.Trim().Length > 0).ToList() : new List<string>() };
            string status = Path.Combine(w.Root, "status-run.json");
            run.HasStatus = File.Exists(status);
            if (run.HasStatus) run.Status = JsonDocument.Parse(File.ReadAllText(status)).RootElement;
            string watchdog = Path.Combine(w.Root, "watchdog-run.json");
            run.Watchdog = File.Exists(watchdog) ? Entries(JsonDocument.Parse(File.ReadAllText(watchdog)).RootElement) : new JsonElement[0];
            return run;
        }

        /// <summary>A refused preflight: wrapper exit 3, nothing launched, the reason recorded.</summary>
        private static void AssertLaunchesNothing(Run run, string reason)
        {
            Assert.That(run.ExitCode, Is.EqualTo(3), Describe(run));
            Assert.That(run.Invocations, Is.Empty, Describe(run));
            Assert.That(run.Watchdog, Is.Empty);
            Assert.That(Text(run.Status, "failedStep"), Is.EqualTo("preflight"));
            Assert.That(Text(run.Status, "reason"), Does.Contain("source provenance not proven").And.Contain(reason));
        }

        private static JsonElement[] Entries(JsonElement element) => element.ValueKind == JsonValueKind.Array ? element.EnumerateArray().ToArray() : new[] { element };

        private static string Text(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static string Describe(Run run) => "exit " + run.ExitCode + "\n" + run.Output + "\ninvocations:\n" + string.Join("\n", run.Invocations);

        [SetUp]
        public void WindowsOnly()
        {
            Assume.That(OperatingSystem.IsWindows(), "Windows PowerShell 5.1 script");
        }

        [Test]
        public void CompleteRun_PassesTheProvenCommits_AndAMeasuredBlockedIsNotAnInfrastructureFailure()
        {
            Workspace w = Create();
            Run run = Execute(w, new Dictionary<string, string> { ["S10_DOUBLE_EXIT_RESULTS_COMPARE"] = "11" });

            Assert.That(run.ExitCode, Is.EqualTo(0), Describe(run));
            Assert.That(Text(run.Status, "infrastructure"), Is.EqualTo("completed"));
            Assert.That(run.Status.GetProperty("simulationsStarted").GetInt32(), Is.EqualTo(2));
            Assert.That(run.Status.GetProperty("outcomes").EnumerateArray().Select(x => Text(x, "compare")), Is.EqualTo(new[] { "BLOCKED", "BLOCKED" }));

            string samHead = Git(w.Sam, "rev-parse", "HEAD"), runnerHead = Git(w.Runner, "rev-parse", "HEAD");
            List<string> simulations = run.Invocations.Where(x => x.StartsWith("simulate ", StringComparison.Ordinal)).ToList();
            Assert.That(simulations, Has.Count.EqualTo(2));
            Assert.That(simulations, Has.All.Contains("--sam-commit " + samHead + " --runner-commit " + runnerHead));
            Assert.That(run.Invocations.Select(x => x.Split(' ')[0]), Is.EqualTo(new[] { "simulate", "results-read", "results-read-design", "results-subject", "results-compare", "results-negatives", "simulate", "results-read", "results-read-design", "results-subject", "results-compare", "results-negatives" }));
            Assert.That(run.Watchdog.Select(x => Text(x, "state")), Has.All.EqualTo("completed"));

            JsonElement provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(w.Root, "provenance-run.json"))).RootElement;
            Assert.That(provenance.GetProperty("proven").GetBoolean(), Is.True);
            Assert.That(Text(provenance, "samCommit"), Is.EqualTo(samHead));
            Assert.That(Text(provenance, "runnerCommit"), Is.EqualTo(runnerHead));
        }

        [Test]
        public void SimulationExit4_StartsNoCaptureAndNoSecondFixture()
        {
            // The acc1 incident: benchmark-tas exit 4 was followed by the reads and then the second simulation.
            Workspace w = Create();
            Run run = Execute(w, new Dictionary<string, string> { ["S10_DOUBLE_EXIT_SIMULATE"] = "4" });

            Assert.That(run.ExitCode, Is.EqualTo(3), Describe(run));
            Assert.That(run.Invocations, Has.Count.EqualTo(1), Describe(run));
            Assert.That(run.Invocations[0], Does.StartWith("simulate --model ").And.Contain("s08-fix-box.json"));
            Assert.That(Text(run.Status, "infrastructure"), Is.EqualTo("failed"));
            Assert.That(Text(run.Status, "failedStep"), Is.EqualTo("simulate-s08-fix-box"));
            Assert.That(Text(run.Status, "reason"), Does.Contain("exited 4"));
            Assert.That(run.Status.GetProperty("simulationsStarted").GetInt32(), Is.EqualTo(1));
            Assert.That(run.Watchdog, Has.Length.EqualTo(1));
            Assert.That(Text(run.Watchdog[0], "state"), Is.EqualTo("completed"));
            Assert.That(run.Watchdog[0].GetProperty("exitCode").GetInt32(), Is.EqualTo(4));
        }

        [Test]
        public void TimedOutSimulation_IsTerminated_AndStopsTheRun()
        {
            Workspace w = Create();
            Run run = Execute(w, new Dictionary<string, string> { ["S10_DOUBLE_SLEEP_SIMULATE"] = "60" }, timeoutSeconds: 3);

            Assert.That(run.ExitCode, Is.EqualTo(3), Describe(run));
            Assert.That(run.Invocations, Has.Count.EqualTo(1));
            Assert.That(Text(run.Watchdog.Single(), "state"), Is.EqualTo("terminated"));
            Assert.That(Text(run.Status, "reason"), Does.Contain("timed out"));
        }

        [Test]
        public void SimulationExit0WithoutItsFiles_StopsBeforeTheReads()
        {
            Workspace w = Create();
            Run run = Execute(w, new Dictionary<string, string> { ["S10_DOUBLE_NO_FILES"] = "1" });

            Assert.That(run.ExitCode, Is.EqualTo(3), Describe(run));
            Assert.That(run.Invocations, Has.Count.EqualTo(1));
            Assert.That(Text(run.Status, "reason"), Does.Contain("produced no tbd, tsd, samJson, b1a"));
        }

        [TestCase("RESULTS_READ", 2)]
        [TestCase("RESULTS_READ_DESIGN", 3)]
        [TestCase("RESULTS_SUBJECT", 4)]
        public void FailedCapture_StopsBeforeTheDependentStepsAndTheSecondFixture(string step, int invocations)
        {
            Workspace w = Create();
            Run run = Execute(w, new Dictionary<string, string> { ["S10_DOUBLE_EXIT_" + step] = "70" });

            Assert.That(run.ExitCode, Is.EqualTo(3), Describe(run));
            Assert.That(run.Invocations, Has.Count.EqualTo(invocations), Describe(run));
            Assert.That(run.Invocations.Count(x => x.StartsWith("simulate ", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(Text(run.Status, "reason"), Does.Contain("exited 70"));
        }

        [Test]
        public void ComparisonInfrastructureExit_Stops_ButAFailVerdictDoesNot()
        {
            Workspace infrastructure = Create();
            Run stopped = Execute(infrastructure, new Dictionary<string, string> { ["S10_DOUBLE_EXIT_RESULTS_COMPARE"] = "70" });
            Assert.That(stopped.ExitCode, Is.EqualTo(3), Describe(stopped));
            Assert.That(stopped.Invocations.Count(x => x.StartsWith("simulate ", StringComparison.Ordinal)), Is.EqualTo(1));

            Workspace measured = Create();
            Run completed = Execute(measured, new Dictionary<string, string> { ["S10_DOUBLE_EXIT_RESULTS_COMPARE"] = "10", ["S10_DOUBLE_EXIT_RESULTS_NEGATIVES"] = "10" });
            Assert.That(completed.ExitCode, Is.EqualTo(0), Describe(completed));
            Assert.That(completed.Status.GetProperty("outcomes").EnumerateArray().Select(x => Text(x, "compare")), Is.EqualTo(new[] { "FAIL", "FAIL" }));
            Assert.That(completed.Status.GetProperty("outcomes").EnumerateArray().Select(x => x.GetProperty("negativesAllAsIntended").GetBoolean()), Has.All.False);
        }

        [Test]
        public void WrongWeatherSha_LaunchesNothing()
        {
            Workspace w = Create();
            Run run = Execute(w, weatherSha: new string('0', 64));

            Assert.That(run.ExitCode, Is.EqualTo(3), Describe(run));
            Assert.That(run.Invocations, Is.Empty);
            Assert.That(run.Watchdog, Is.Empty);
            Assert.That(Text(run.Status, "failedStep"), Is.EqualTo("preflight"));
            Assert.That(Text(run.Status, "reason"), Does.Contain("expected " + new string('0', 64)));
        }

        [Test]
        public void DirtyOrUntrackedCheckout_LaunchesNothing()
        {
            Workspace w = Create();
            File.WriteAllText(Path.Combine(w.Runner, "Untracked.cs"), "// not committed\n");
            Run run = Execute(w);

            Assert.That(run.ExitCode, Is.EqualTo(3), Describe(run));
            Assert.That(run.Invocations, Is.Empty);
            Assert.That(Text(run.Status, "reason"), Does.Contain("source provenance not proven").And.Contain("uncommitted or untracked change(s)"));
        }

        [Test]
        public void SourceNewerThanTheBuild_LaunchesNothing()
        {
            Workspace w = Create();
            File.SetLastWriteTimeUtc(Path.Combine(w.Sam, "Source.cs"), DateTime.UtcNow.AddMinutes(10));
            Run run = Execute(w);

            Assert.That(run.ExitCode, Is.EqualTo(3), Describe(run));
            Assert.That(run.Invocations, Is.Empty);
            Assert.That(Text(run.Status, "reason"), Does.Contain("is older than the source").And.Contain("rebuild"));
        }

        [Test]
        public void SamLibraryNotTheSamBuildOutput_LaunchesNothing()
        {
            Workspace w = Create();
            File.WriteAllText(Path.Combine(w.Doubles, "SAM.Analytical.dll"), "another build");
            Run run = Execute(w);

            Assert.That(run.ExitCode, Is.EqualTo(3), Describe(run));
            Assert.That(run.Invocations, Is.Empty);
            Assert.That(Text(run.Status, "reason"), Does.Contain("differs from the SAM build output"));
        }

        [Test]
        public void NotACheckout_LaunchesNothing()
        {
            Workspace w = Create();
            Directory.Move(Path.Combine(w.Sam, ".git"), Path.Combine(w.Directory, "sam-git-moved"));
            Run run = Execute(w);

            Assert.That(run.ExitCode, Is.EqualTo(3), Describe(run));
            Assert.That(run.Invocations, Is.Empty);
            Assert.That(Text(run.Status, "reason"), Does.Contain("is not a git checkout"));
        }

        // ---- Review R3 of the revision-2 correction: the binaries are bound to their build by the receipt, not by time. ----

        [Test]
        public void ForeignNewerArtifactUnderTheHarnessName_LaunchesNothing()
        {
            // The review's probe: another build's assembly, newer than every source, under the harness DLL's name. The
            // reviewed guard (clean checkout + timestamps) accepted it and declared it built from HEAD.
            Workspace w = Create();
            string harnessDll = Path.ChangeExtension(w.Harness, ".dll");
            File.WriteAllText(harnessDll, "double s10-harness.dll from another build");
            File.SetLastWriteTimeUtc(harnessDll, DateTime.UtcNow.AddMinutes(1));
            Run run = Execute(w);

            AssertLaunchesNothing(run, "s10-harness.dll");
            Assert.That(Text(run.Status, "reason"), Does.Contain("does not match the build receipt"));
            JsonElement provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(w.Root, "provenance-run.json"))).RootElement;
            Assert.That(provenance.GetProperty("proven").GetBoolean(), Is.False);
        }

        [Test]
        public void ForeignRebuiltLibraryBesideTheBenchmark_LaunchesNothing()
        {
            Workspace w = Create();
            File.WriteAllText(Path.Combine(w.Doubles, "SAM.Analytical.Tas.dll"), "double SAM.Analytical.Tas.dll 1.0.0.0+" + w.RunnerBuilt + " rebuilt later");
            AssertLaunchesNothing(Execute(w), "SAM.Analytical.Tas.dll");
        }

        [Test]
        public void UnlistedLibraryBesideTheBenchmark_LaunchesNothing()
        {
            Workspace w = Create();
            File.WriteAllText(Path.Combine(w.Doubles, "Extra.dll"), "not built by the receipt's build");
            AssertLaunchesNothing(Execute(w), "Extra.dll is not in the build receipt");
        }

        [Test]
        public void MissingReceipt_LaunchesNothing()
        {
            Workspace w = Create();
            File.Delete(w.Receipt);
            AssertLaunchesNothing(Execute(w), "no build receipt at");
        }

        [Test]
        public void MalformedOrForeignSchemaReceipt_LaunchesNothing()
        {
            Workspace malformed = Create();
            File.WriteAllText(malformed.Receipt, "{ not json");
            AssertLaunchesNothing(Execute(malformed), "is not JSON");

            Workspace schema = Create();
            File.WriteAllText(schema.Receipt, File.ReadAllText(schema.Receipt).Replace("s10-build-receipt/1", "s10-build-receipt/0"));
            AssertLaunchesNothing(Execute(schema), "is not s10-build-receipt/1");
        }

        [Test]
        public void ReceiptHashMismatch_LaunchesNothing()
        {
            // The files are untouched; the receipt's recorded hash of the harness DLL is not theirs.
            Workspace w = Create();
            string harnessSha = Sha(Path.ChangeExtension(w.Harness, ".dll"));
            File.WriteAllText(w.Receipt, File.ReadAllText(w.Receipt).Replace(harnessSha, new string('0', 64)));
            AssertLaunchesNothing(Execute(w), "does not match the build receipt (" + new string('0', 64) + ")");
        }

        [Test]
        public void ReceiptOfAnotherCommit_LaunchesNothing()
        {
            Workspace w = Create();
            File.WriteAllText(w.Receipt, File.ReadAllText(w.Receipt).Replace(w.SamBuilt, new string('a', 40)));
            AssertLaunchesNothing(Execute(w), "nor an ancestor of it: rebuild");
        }

        [Test]
        public void StaleReceipt_BuildInputCommittedAfterTheBuild_LaunchesNothing()
        {
            // Dated before the build, so the timestamp check passes: only the receipt binding can refuse it.
            Workspace w = Create();
            Commit(w.Runner, "Later.cs", "// committed after the build\n");
            AssertLaunchesNothing(Execute(w), "changed 1 build input(s) since the receipt's build " + w.RunnerBuilt + ", e.g. Later.cs: rebuild");
        }

        [Test]
        public void StaleSamReceipt_SamInputCommittedAfterTheBuild_LaunchesNothing()
        {
            Workspace w = Create();
            Commit(w.Sam, Path.Combine("SAM", "SAM.Analytical", "Later.csproj"), "<Project />\n");
            AssertLaunchesNothing(Execute(w), "since the receipt's build " + w.SamBuilt);
        }

        [Test]
        public void RecordOnlyCommitAfterTheBuild_RunsWithTheBuiltCommit()
        {
            // A PR record commit after the tested build: HEAD moves, the build inputs do not; the built commit is passed.
            Workspace w = Create();
            string head = Commit(w.Runner, Path.Combine("Documentation", "S10-Record.md"), "# record-only\n");
            Run run = Execute(w, new Dictionary<string, string> { ["S10_DOUBLE_EXIT_RESULTS_COMPARE"] = "11" });

            Assert.That(run.ExitCode, Is.EqualTo(0), Describe(run));
            Assert.That(head, Is.Not.EqualTo(w.RunnerBuilt));
            Assert.That(run.Invocations.Where(x => x.StartsWith("simulate ", StringComparison.Ordinal)), Has.All.Contains("--sam-commit " + w.SamBuilt + " --runner-commit " + w.RunnerBuilt));
            JsonElement provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(w.Root, "provenance-run.json"))).RootElement;
            Assert.That(Text(provenance, "runnerCommit"), Is.EqualTo(w.RunnerBuilt));
            Assert.That(Text(provenance, "runnerHead"), Is.EqualTo(head));
            Assert.That(provenance.GetProperty("runner").GetProperty("recordOnly").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "Documentation/S10-Record.md" }));
        }

        [Test]
        public void CustomHarnessPathNotTheBuiltOne_LaunchesNothing()
        {
            // Byte-identical copies elsewhere are still not the receipt's harness: the configured path is checked too.
            Workspace w = Create();
            string elsewhere = Path.Combine(w.Directory, "elsewhere");
            Directory.CreateDirectory(elsewhere);
            foreach (string name in new[] { "s10-harness.cmd", "s10-harness.dll", "double.ps1" }) File.Copy(Path.Combine(w.Doubles, name), Path.Combine(elsewhere, name));
            AssertLaunchesNothing(Execute(w, harness: Path.Combine(elsewhere, "s10-harness.cmd")), "is not the receipt's exe");
        }

        [Test]
        public void Build_DirtyCheckout_BuildsNothing_AndRemovesTheOldReceipt()
        {
            Workspace w = Create();
            File.WriteAllText(Path.Combine(w.Sam, "Untracked.cs"), "// not committed\n");
            Built built = Build(w);

            Assert.That(built.ExitCode, Is.EqualTo(3), built.Output);
            Assert.That(built.Output, Does.Contain("uncommitted or untracked change(s)"));
            Assert.That(built.Invocations, Is.Empty);
            Assert.That(File.Exists(w.Receipt), Is.False);
            AssertLaunchesNothing(Execute(w), "no build receipt at");
        }

        [Test]
        public void Build_FailedBuild_WritesNoReceipt()
        {
            Workspace w = Create(build: false);
            Built built = Build(w, new Dictionary<string, string> { ["S10_BUILD_DOUBLE_EXIT"] = "1" });

            Assert.That(built.ExitCode, Is.EqualTo(3), built.Output);
            Assert.That(built.Output, Does.Contain("MSBuild exited 1"));
            Assert.That(built.Invocations, Has.Count.EqualTo(1));
            Assert.That(File.Exists(w.Receipt), Is.False);
        }

        [Test]
        public void Build_OutputWithoutTheCommitStamp_WritesNoReceipt()
        {
            Workspace w = Create(build: false);
            Built built = Build(w, new Dictionary<string, string> { ["S10_BUILD_DOUBLE_UNSTAMPED"] = "1" });

            Assert.That(built.ExitCode, Is.EqualTo(3), built.Output);
            Assert.That(built.Output, Does.Contain("does not carry the stamp +"));
            Assert.That(File.Exists(w.Receipt), Is.False);
        }

        [Test]
        public void Build_StampsEachCheckoutsCommit_AndRecordsEveryLoadedFile()
        {
            Workspace w = Create(build: false);
            Built built = Build(w);
            Assert.That(built.ExitCode, Is.EqualTo(0), built.Output);

            // Eight SAM core projects, then the SAM_Tas solution, each stamped with its own checkout's HEAD.
            Assert.That(built.Invocations, Has.Count.EqualTo(9));
            Assert.That(built.Invocations.Take(8), Has.All.Contains("-p:SAMSourceRevision=" + w.SamBuilt));
            Assert.That(built.Invocations[8], Does.Contain("SAM_Tas.sln").And.Contain("-p:SAMSourceRevision=" + w.RunnerBuilt));

            JsonElement receipt = JsonDocument.Parse(File.ReadAllText(w.Receipt)).RootElement;
            Assert.That(receipt.GetProperty("sam").GetProperty("commit").GetString(), Is.EqualTo(w.SamBuilt));
            Assert.That(receipt.GetProperty("runner").GetProperty("commit").GetString(), Is.EqualTo(w.RunnerBuilt));
            Dictionary<string, string> files = receipt.GetProperty("files").EnumerateArray().ToDictionary(x => Path.GetFileName(x.GetProperty("path").GetString()), x => x.GetProperty("sha256").GetString());
            Assert.That(files.Keys, Is.EquivalentTo(new[] { "s10-harness.cmd", "s10-harness.dll", "s10-benchmark.cmd", "s10-benchmark.dll", "SAM.Analytical.Tas.dll", "SAM.Analytical.dll" }));
            foreach (string name in files.Keys) Assert.That(files[name], Is.EqualTo(Sha(Path.Combine(w.Doubles, name))), name);
            Assert.That(receipt.GetProperty("samLibraries").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "SAM.Analytical.dll").GetProperty("sha256").GetString(), Is.EqualTo(Sha(Path.Combine(w.Sam, "build", "SAM.Analytical.dll"))));
        }

        // ---- Review R5: the scripts' hashing must not depend on Windows PowerShell module autoload. ----

        [TestCase("Run-S10-Results.ps1")]
        [TestCase("Build-S10-Receipt.ps1")]
        public void Sha_NeedsNoModuleAutoload(string script)
        {
            // Run the script's own Sha function, taken from its syntax tree, in a Windows PowerShell child whose
            // PSModulePath puts an unloadable Microsoft.PowerShell.Utility first. Get-FileHash is not found there.
            Workspace w = Create(build: false);
            string modules = Path.Combine(w.Directory, "modules");
            Directory.CreateDirectory(Path.Combine(modules, "Microsoft.PowerShell.Utility"));
            File.WriteAllText(Path.Combine(modules, "Microsoft.PowerShell.Utility", "Microsoft.PowerShell.Utility.psd1"), ShadowUtility);
            string probe = Path.Combine(w.Directory, "sha-probe.ps1");
            File.WriteAllText(probe, @"param([string]$Script, [string]$File)
$ErrorActionPreference = 'Stop'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($Script, [ref]$tokens, [ref]$errors)
$definition = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Sha' }, $true))[0]
. ([scriptblock]::Create($definition.Extent.Text))
'sha=' + (Sha $File)
'missing=' + ($null -eq (Sha ($File + '.missing')))
");
            string system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "Modules");
            (int exitCode, string output) = PowerShell(new[] { probe, "-Script", Script(script), "-File", w.Weather }, new Dictionary<string, string> { ["PSModulePath"] = modules + ";" + system });

            Assert.That(exitCode, Is.EqualTo(0), output);
            Assert.That(output, Does.Contain("sha=" + w.WeatherSha).And.Contain("missing=True"), output);
        }
    }
}
