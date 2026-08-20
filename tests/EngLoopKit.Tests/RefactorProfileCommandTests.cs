using System.Diagnostics;
using System.Text.Json;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

[CollectionDefinition("RefactorProfileConsole", DisableParallelization = true)]
public sealed class RefactorProfileConsoleCollection;

[Collection("RefactorProfileConsole")]
public sealed class RefactorProfileCommandTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "elk-refactor-profile-" + Guid.NewGuid().ToString("N"));

    public RefactorProfileCommandTests() => Directory.CreateDirectory(_work);

    [Fact]
    public void OmittedProfile_defaultsToPoint_andIgnoresUntrustedModelMetadata()
    {
        var repo = CreateRepository();
        var result = RunRaw(repo, ["bind"], JsonSerializer.Serialize(new
        {
            cwd = repo,
            session_id = "point-default",
            prompt = "--scope src/parser",
            model = "GPT-5.6 SOL",
            modelFamily = "frontier",
            thinking = "max",
            reasoningEffort = "high",
            tokenBudget = 1_000_000,
        }));

        Assert.True(Continues(result), result.Output);
        Assert.Contains("profile=POINT", result.Output, StringComparison.Ordinal);
        Assert.Contains("source=DEFAULT-POINT", result.Output, StringComparison.Ordinal);
        Assert.Contains("scope=src/parser", result.Output, StringComparison.Ordinal);
        Assert.Contains("modelMetadata=UNAVAILABLE-NOT-INFERRED", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bounded", "BOUNDED")]
    [InlineData("deep", "DEEP")]
    public void BroaderProfiles_requireAndPreserveExplicitSelection(string profile, string expected)
    {
        var repo = CreateRepository();
        var result = Run(repo, "bind", $"--scope src/storage --profile {profile}", "explicit-" + profile);

        Assert.True(Continues(result), result.Output);
        Assert.Contains("profile=" + expected, result.Output, StringComparison.Ordinal);
        Assert.Contains("source=EXPLICIT", result.Output, StringComparison.Ordinal);

        var followup = Run(repo, "bind", "continue the selected evidence review", "explicit-" + profile);
        Assert.True(Continues(followup), followup.Output);
        Assert.Contains("profile=" + expected, followup.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--scope \"src/parser components\" --profile point", "scope=src/parser components")]
    [InlineData("--scope 'src/storage seam' --profile bounded", "scope=src/storage seam")]
    public void QuotedScopes_areParsedWithoutChangingProfileSemantics(string prompt, string expectedScope)
    {
        var repo = CreateRepository();
        var result = Run(repo, "bind", prompt, "quoted-" + Guid.NewGuid().ToString("N"));
        Assert.True(Continues(result), result.Output);
        Assert.Contains(expectedScope, result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--profile deep")]
    [InlineData("--scope src/x --profile automatic")]
    [InlineData("--scope <repository> --profile deep")]
    [InlineData("--scope src/x --scope src/y")]
    [InlineData("--scope src/x --profile point --profile deep")]
    [InlineData("--scope repository")]
    [InlineData("--scope . --profile bounded")]
    public void MissingOrInvalidArguments_failClosed(string prompt)
    {
        var repo = CreateRepository();
        var result = Run(repo, "bind", prompt, "invalid-" + Guid.NewGuid().ToString("N"));

        Assert.False(Continues(result));
        Assert.Contains("failed closed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScopeLongerThan256Characters_failsClosed()
    {
        var repo = CreateRepository();
        var result = Run(repo, "bind", "--scope " + new string('x', 257), "overlong-scope");
        Assert.False(Continues(result));
        Assert.Contains("scope-length-invalid", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundScopeOrProfile_cannotChangeMidSession()
    {
        var repo = CreateRepository();
        const string session = "bound-session";
        Assert.True(Continues(Run(repo, "bind", "--scope src/parser --profile point", session)));

        var profileChange = Run(repo, "bind", "--scope src/parser --profile deep", session);
        Assert.False(Continues(profileChange));
        Assert.Contains("scope-or-profile-changed", profileChange.Output, StringComparison.Ordinal);

        var scopeChange = Run(repo, "bind", "--scope src/storage --profile point", session);
        Assert.False(Continues(scopeChange));
        Assert.Contains("scope-or-profile-changed", scopeChange.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryScope_isAllowedOnlyForExplicitDeep()
    {
        var repo = CreateRepository();
        var result = Run(repo, "bind", "--scope repository --profile deep", "deep-repository");
        Assert.True(Continues(result), result.Output);
        Assert.Contains("profile=DEEP", result.Output, StringComparison.Ordinal);
        Assert.Contains("scope=repository", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void HeadChange_blocksFollowup_andClearRemovesGate()
    {
        var repo = CreateRepository();
        const string session = "head-session";
        Assert.True(Continues(Run(repo, "bind", "--scope src/parser", session)));
        Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "refactor-profile-gates"), "*.json"));

        File.WriteAllText(Path.Combine(repo, "changed.txt"), "new head");
        Git(repo, "add", "changed.txt");
        Git(repo, "commit", "-m", "change head");
        var changed = Run(repo, "bind", "continue", session);
        Assert.False(Continues(changed));
        Assert.Contains("head-changed", changed.Output, StringComparison.Ordinal);

        var clear = Run(repo, "clear", string.Empty, session);
        Assert.True(Continues(clear));
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "refactor-profile-gates"), "*.json"));
    }

    [Fact]
    public void BlankFollowup_reusesBoundProfile_andClearIsIdempotent()
    {
        var repo = CreateRepository();
        const string session = "blank-followup";
        Assert.True(Continues(Run(repo, "bind", "--scope src/parser --profile bounded", session)));
        var followup = Run(repo, "bind", string.Empty, session);
        Assert.True(Continues(followup), followup.Output);
        Assert.Contains("profile=BOUNDED", followup.Output, StringComparison.Ordinal);
        Assert.True(Continues(Run(repo, "clear", string.Empty, session)));
        Assert.True(Continues(Run(repo, "clear", string.Empty, session)));
    }

    [Fact]
    public void NullGateAndUnbornHead_failClosed()
    {
        var repo = CreateRepository();
        const string session = "null-gate";
        Assert.True(Continues(Run(repo, "bind", "--scope src/parser", session)));
        var gate = Assert.Single(Directory.GetFiles(Path.Combine(repo, ".engloop", "out", "refactor-profile-gates"), "*.json"));
        File.WriteAllText(gate, "null");
        Assert.False(Continues(Run(repo, "bind", "continue", session)));

        var unborn = CreateRepository();
        Git(unborn, "checkout", "--orphan", "unborn");
        Git(unborn, "rm", "-rf", ".");
        File.WriteAllText(Path.Combine(unborn, ".gitignore"), ".engloop/out/\n");
        Assert.False(Continues(Run(unborn, "bind", "--scope src/parser", "unborn-session")));
    }

    [Fact]
    public void CamelCaseSessionId_isAcceptedWhileNonStringAliasesAreIgnored()
    {
        var repo = CreateRepository();
        var payload = JsonSerializer.Serialize(new
        {
            cwd = repo,
            session_id = 123,
            sessionId = "camel-session",
            prompt = "--scope src/parser",
        });
        var result = RunRaw(repo, ["bind"], payload);
        Assert.True(Continues(result), result.Output);
        Assert.Contains("profile=POINT", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void DispatchAndRootIdentity_failClosedWithJsonProtocol()
    {
        var repo = CreateRepository();
        Assert.False(Continues(RunRaw(repo, [], HookJson(repo, "session", "--scope src"))));
        Assert.False(Continues(RunRaw(repo, ["unknown"], HookJson(repo, "session", "--scope src"))));
        Assert.False(Continues(RunRaw(repo, ["bind"], "not-json")));

        var child = Path.Combine(repo, "child");
        Directory.CreateDirectory(child);
        Assert.False(Continues(RunRaw(repo, ["bind"], HookJson(child, "session", "--scope src"))));
        Assert.False(Continues(RunRaw(repo, ["bind"], HookJson(repo, string.Empty, "--scope src"))));
    }

    [Fact]
    public void Program_dispatchesRefactorProfileWithJsonProtocol()
    {
        var repo = CreateRepository();
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var reader = new StringReader(HookJson(repo, "program-session", "--scope src/parser --profile bounded"));
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetIn(reader);
            Console.SetOut(output);
            Console.SetError(error);
            Assert.Equal(0, Program.Main(["refactor-profile", "bind"]));
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        Assert.True(Continues((0, output.ToString().Trim(), error.ToString().Trim())));
        Assert.Contains("profile=BOUNDED", output.ToString(), StringComparison.Ordinal);
    }

    private string CreateRepository()
    {
        var repo = Path.Combine(_work, "repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, ".gitignore"), ".engloop/out/\n");
        File.WriteAllText(Path.Combine(repo, "README.md"), "fixture\n");
        Git(repo, "init");
        Git(repo, "config", "user.email", "refactor-profile@example.invalid");
        Git(repo, "config", "user.name", "Refactor Profile Test");
        Git(repo, "add", ".");
        Git(repo, "commit", "-m", "fixture");
        return repo;
    }

    private static (int ExitCode, string Output, string Error) Run(
        string repo,
        string action,
        string prompt,
        string session,
        object? extra = null)
    {
        var input = extra is null
            ? HookJson(repo, session, prompt)
            : JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["cwd"] = repo,
                ["session_id"] = session,
                ["prompt"] = prompt,
                ["untrusted"] = extra,
            });
        return RunRaw(repo, [action], input);
    }

    private static string HookJson(string repo, string session, string prompt)
        => JsonSerializer.Serialize(new { cwd = repo, session_id = session, prompt });

    private static (int ExitCode, string Output, string Error) RunRaw(string repo, string[] args, string input)
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var reader = new StringReader(input);
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetIn(reader);
            Console.SetOut(output);
            Console.SetError(error);
            var exitCode = RefactorProfileCommands.Execute(args);
            return (exitCode, output.ToString().Trim(), error.ToString().Trim());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static bool Continues((int ExitCode, string Output, string Error) result)
    {
        Assert.Equal(0, result.ExitCode);
        using var json = JsonDocument.Parse(result.Output);
        return json.RootElement.GetProperty("continue").GetBoolean();
    }

    private static void Git(string repo, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output + error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            try { Directory.Delete(_work, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
