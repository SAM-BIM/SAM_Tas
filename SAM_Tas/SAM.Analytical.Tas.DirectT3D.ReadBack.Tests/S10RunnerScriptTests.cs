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
    /// which processes were started, the watchdog, the status file and the wrapper exit.
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

        private sealed class Workspace
        {
            public string Directory, Doubles, Sam, Runner, Root, Log, Weather, WeatherSha;
            public string Harness => Path.Combine(Doubles, "s10-harness.cmd");
            public string Benchmark => Path.Combine(Doubles, "s10-benchmark.cmd");
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

        private static string Script()
        {
            for (DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "SAM_Tas", "SAM.Analytical.Tas.DirectT3D.Validation", "Run-S10-Results.ps1");
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException("Run-S10-Results.ps1 not found above " + TestContext.CurrentContext.TestDirectory);
        }

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

        private Workspace Create()
        {
            Workspace w = new Workspace { Directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "s10-runner-" + Guid.NewGuid().ToString("N").Substring(0, 8)) };
            cleanup.Add(w.Directory);
            w.Doubles = Path.Combine(w.Directory, "doubles");
            w.Sam = Path.Combine(w.Directory, "SAM");
            w.Runner = Path.Combine(w.Directory, "SAM_Tas");
            w.Log = Path.Combine(w.Directory, "invocations.log");
            Directory.CreateDirectory(w.Doubles);
            Checkout(w.Sam, "build");
            Checkout(w.Runner, "bin");

            // Artifacts built after the sources; the SAM library beside the benchmark double is the SAM build output.
            Directory.CreateDirectory(Path.Combine(w.Sam, "build"));
            File.WriteAllText(Path.Combine(w.Sam, "build", "SAM.Analytical.dll"), "sam analytical double");
            File.Copy(Path.Combine(w.Sam, "build", "SAM.Analytical.dll"), Path.Combine(w.Doubles, "SAM.Analytical.dll"));
            File.WriteAllText(Path.Combine(w.Doubles, "double.ps1"), Double);
            foreach (string name in new[] { "s10-harness", "s10-benchmark" })
            {
                File.WriteAllText(Path.Combine(w.Doubles, name + ".cmd"), "@powershell -NoProfile -ExecutionPolicy Bypass -File \"%~dp0double.ps1\" %*\r\n@exit /b %ERRORLEVEL%\r\n");
                File.WriteAllText(Path.Combine(w.Doubles, name + ".dll"), name + " double");
            }

            File.WriteAllText(Path.Combine(w.Doubles, "SAM.Analytical.Tas.dll"), "sam tas double");
            w.Weather = Path.Combine(w.Directory, "weather.epw");
            File.WriteAllText(w.Weather, "LOCATION,S10 runner test double\n");
            w.WeatherSha = Sha(w.Weather);

            // A short, fresh output root (Tas refuses long paths; the script enforces at most 40 characters).
            string temp = Path.GetTempPath();
            string suffix = "s" + Guid.NewGuid().ToString("N").Substring(0, 4);
            w.Root = temp.Length + suffix.Length <= 40 ? Path.Combine(temp, suffix) : Path.Combine(Path.GetPathRoot(temp), "s10t" + suffix);
            cleanup.Add(w.Root);
            return w;
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

        private Run Execute(Workspace w, Dictionary<string, string> environment = null, string weatherSha = null, int timeoutSeconds = 120)
        {
            ProcessStartInfo info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (string argument in new[]
            {
                "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Script(), "-Phase", "run", "-Root", w.Root, "-WeatherEpw", w.Weather, "-WeatherEpwSha256", weatherSha ?? w.WeatherSha,
                "-Exe", w.Harness, "-BenchmarkExe", w.Benchmark, "-SamRepository", w.Sam, "-RunnerRepository", w.Runner, "-TasProcessNames", NoTasProcess, "-TimeoutSeconds", timeoutSeconds.ToString(),
            })
            {
                info.ArgumentList.Add(argument);
            }

            info.Environment["S10_DOUBLE_LOG"] = w.Log;
            foreach (KeyValuePair<string, string> pair in environment ?? new Dictionary<string, string>()) info.Environment[pair.Key] = pair.Value;
            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                Assert.That(process.WaitForExit(300000), Is.True, "the script did not finish");
                Run run = new Run { ExitCode = process.ExitCode, Output = output, Invocations = File.Exists(w.Log) ? File.ReadAllLines(w.Log).Where(x => x.Trim().Length > 0).ToList() : new List<string>() };
                string status = Path.Combine(w.Root, "status-run.json");
                run.HasStatus = File.Exists(status);
                if (run.HasStatus) run.Status = JsonDocument.Parse(File.ReadAllText(status)).RootElement;
                string watchdog = Path.Combine(w.Root, "watchdog-run.json");
                run.Watchdog = File.Exists(watchdog) ? Entries(JsonDocument.Parse(File.ReadAllText(watchdog)).RootElement) : new JsonElement[0];
                return run;
            }
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
    }
}
