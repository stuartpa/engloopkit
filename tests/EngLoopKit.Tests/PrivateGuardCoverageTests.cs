using System.Reflection;
using System.Text.Json;
using EngLoopKit.Core;
using EngLoopKit.Tool;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class PrivateGuardCoverageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "elk-private-guards-" + Guid.NewGuid().ToString("N"));

    public PrivateGuardCoverageTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".engloop", "out"));
        File.WriteAllText(Path.Combine(_root, "file.txt"), "content");
        Directory.CreateDirectory(Path.Combine(_root, "directory"));
    }

    [Fact]
    public void OperationsPolicy_privateParsers_coverSuccessAndFailureShapes()
    {
        var failures = new List<string>();
        Assert.Empty(Invoke<IReadOnlyList<string>>(typeof(OperationsLearningPolicy), "ParseRuleList", "NONE", true, "bad", failures));
        Assert.Equal(["RULE:a", "RULE:b"], Invoke<IReadOnlyList<string>>(typeof(OperationsLearningPolicy), "ParseRuleList", "`RULE:b`, `RULE:a`", false, "bad", failures));
        Assert.Equal(["PM001/LEARN001"], Invoke<IReadOnlyList<string>>(typeof(OperationsLearningPolicy), "ParseSourceList", "PM001/LEARN001", false, "bad-source", failures));
        _ = Invoke<IReadOnlyList<string>>(typeof(OperationsLearningPolicy), "ParseRuleList", "RULE:a,RULE:a", false, "duplicate", failures);
        _ = Invoke<IReadOnlyList<string>>(typeof(OperationsLearningPolicy), "ParseSourceList", "bad", false, "invalid", failures);
        Assert.Contains("duplicate", failures);
        Assert.Contains("invalid", failures);

        failures.Clear();
        Assert.Equal(["dotnet", "--version"], Invoke<IReadOnlyList<string>>(typeof(OperationsLearningPolicy), "ParseArgumentVector", "[\"dotnet\",\"--version\"]", "gate", failures));
        foreach (var bad in new[] { "", "<placeholder>", "{}", "[]", "[1]", "[\"\"]", "not-json" })
            _ = Invoke<IReadOnlyList<string>>(typeof(OperationsLearningPolicy), "ParseArgumentVector", bad, "gate", failures);
        Assert.NotEmpty(failures);

        using var valid = JsonDocument.Parse("{\"ids\":[\"b\",\"a\"]}");
        using var missing = JsonDocument.Parse("{}");
        using var invalid = JsonDocument.Parse("{\"ids\":[1,\"\",\"a\",\"a\"]}");
        failures.Clear();
        Assert.Equal(["b", "a"], Invoke<IReadOnlyList<string>>(typeof(OperationsLearningPolicy), "ReadJsonStringArrayStrict", valid.RootElement, "ids", failures));
        _ = Invoke<IReadOnlyList<string>>(typeof(OperationsLearningPolicy), "ReadJsonStringArrayStrict", missing.RootElement, "ids", failures);
        _ = Invoke<IReadOnlyList<string>>(typeof(OperationsLearningPolicy), "ReadJsonStringArrayStrict", invalid.RootElement, "ids", failures);
        Assert.Contains(failures, value => value.Contains("missing", StringComparison.Ordinal));
        Assert.Contains(failures, value => value.Contains("invalid", StringComparison.Ordinal));
        Assert.Contains(failures, value => value.Contains("duplicate", StringComparison.Ordinal));
    }

    [Fact]
    public void OperationsPolicy_privateTextAndPathGuards_coverAllShapes()
    {
        Assert.Equal("a/b", Invoke<string>(typeof(OperationsLearningPolicy), "NormalizeRelative", " `a\\b` "));
        Assert.Equal("a\nb\nc", Invoke<string>(typeof(OperationsLearningPolicy), "NormalizeText", "a\r\nb\rc"));
        Assert.Equal(["a", "b"], Invoke<string[]>(typeof(OperationsLearningPolicy), "SplitTableRow", "| `a` | b |"));
        Assert.Equal("root", Invoke<string>(typeof(OperationsLearningPolicy), "ToKebab", " Root! "));
        Assert.True(Invoke<bool>(typeof(OperationsLearningPolicy), "IsSubstantive", "six useful words make this evidence substantive"));
        foreach (var weak in new[] { "", "TODO later", "TBD later", "<placeholder>", "one two" })
            Assert.False(Invoke<bool>(typeof(OperationsLearningPolicy), "IsSubstantive", weak));

        var document = "# X\n\n## A\nbody\n## B\nnext\n";
        Assert.Contains("body", Invoke<string>(typeof(OperationsLearningPolicy), "ExtractSection", document, "## A", new[] { "## " }));
        Assert.Empty(Invoke<string>(typeof(OperationsLearningPolicy), "ExtractSection", document, "## Missing", new[] { "## " }));
        Assert.Empty(Invoke<string>(typeof(OperationsLearningPolicy), "ExtractSection", document + "## A\nduplicate\n", "## A", new[] { "## " }));

        var failures = new List<string>();
        Invoke<object?>(typeof(OperationsLearningPolicy), "RequireUniqueHeading", document, "## A", failures);
        Invoke<object?>(typeof(OperationsLearningPolicy), "RequireUniqueHeading", document, "## Missing", failures);
        Invoke<object?>(typeof(OperationsLearningPolicy), "RequireCheckedEvidence", "- [x] Check: six useful words prove the observed result\n", "Check", failures);
        Invoke<object?>(typeof(OperationsLearningPolicy), "RequireCheckedEvidence", "- [ ] Check: missing\n", "Check", failures);
        Assert.NotEmpty(failures);

        Assert.Equal("value", Invoke<string>(typeof(OperationsLearningPolicy), "ReadField", "- **Name:** `value`\n", "Name"));
        Assert.Empty(Invoke<string>(typeof(OperationsLearningPolicy), "ReadField", "none", "Name"));
        Assert.Throws<InvalidOperationException>(() => Invoke<object>(typeof(OperationsLearningPolicy), "ResolveGovernedPath", _root, Path.Combine(_root, "file.txt"), "", true));
        Assert.Throws<InvalidOperationException>(() => Invoke<object>(typeof(OperationsLearningPolicy), "ResolveGovernedPath", _root, "../escape", "", false));
        Assert.Throws<InvalidOperationException>(() => Invoke<object>(typeof(OperationsLearningPolicy), "ResolveGovernedPath", _root, "file.txt", ".engloop/", true));
        Assert.Throws<FileNotFoundException>(() => Invoke<object>(typeof(OperationsLearningPolicy), "ResolveGovernedPath", _root, "missing.txt", "", true));
        _ = Invoke<object>(typeof(OperationsLearningPolicy), "ResolveGovernedPath", _root, "file.txt", "", true);
        _ = Invoke<object>(typeof(OperationsLearningPolicy), "ResolveGovernedPath", _root, ".engloop/out/new.txt", ".engloop/out/", false);
    }

    [Fact]
    public void OperationsPolicy_sekGateClassifier_coversSupportedForms()
    {
        Assert.True(Invoke<bool>(typeof(OperationsLearningPolicy), "IsSekGate", (IReadOnlyList<string>)["sek", "test"]));
        Assert.True(Invoke<bool>(typeof(OperationsLearningPolicy), "IsSekGate", (IReadOnlyList<string>)["dotnet", "tool", "run", "sek", "--", "test"]));
        Assert.True(Invoke<bool>(typeof(OperationsLearningPolicy), "IsSekGate", (IReadOnlyList<string>)["pwsh", "verify-sek.ps1"]));
        Assert.False(Invoke<bool>(typeof(OperationsLearningPolicy), "IsSekGate", (IReadOnlyList<string>)Array.Empty<string>()));
        Assert.False(Invoke<bool>(typeof(OperationsLearningPolicy), "IsSekGate", (IReadOnlyList<string>)["dotnet", "test"]));
    }

    [Fact]
    public void OperationsPolicy_privateTablesGitAndStatus_coverDefensiveBranches()
    {
        Assert.NotEmpty(OperationsLearningPolicy.ComputeCardsDigest(Path.Combine(_root, "missing-cards")));
        Assert.Null(OperationsLearningPolicy.GitHead(_root));
        Assert.Throws<InvalidOperationException>(() => OperationsLearningPolicy.ComputeGitStatusDigest(_root, []));
        Assert.Throws<InvalidOperationException>(() => OperationsLearningPolicy.ComputeReadinessWorktreeDigest(_root));
        Assert.Throws<InvalidOperationException>(() => OperationsLearningPolicy.ComputeGitIndexDigest(_root));

        var malformed = Invoke<(string Status, string Path, string ContentIdentity)>(typeof(OperationsLearningPolicy), "SnapshotStatusLine", _root, "x");
        Assert.Equal("malformed", malformed.ContentIdentity);
        var directory = Invoke<(string Status, string Path, string ContentIdentity)>(typeof(OperationsLearningPolicy), "SnapshotStatusLine", _root, "?? directory");
        Assert.Equal("directory", directory.ContentIdentity);
        var missing = Invoke<(string Status, string Path, string ContentIdentity)>(typeof(OperationsLearningPolicy), "SnapshotStatusLine", _root, "?? missing.txt");
        Assert.Equal("missing", missing.ContentIdentity);
        var renamed = Invoke<(string Status, string Path, string ContentIdentity)>(typeof(OperationsLearningPolicy), "SnapshotStatusLine", _root, "R  old.txt -> file.txt");
        Assert.Equal("file.txt", renamed.Path);

        var failures = new List<string>();
        Invoke<object?>(typeof(OperationsLearningPolicy), "ValidateRootAndConfig", _root, failures);
        Assert.NotEmpty(failures);

        failures.Clear();
        var incidentTable = "## Selected stabilized incidents\n| Incident ID | Path | SHA-256 |\n|---|---|---|\n| IN001 | a | h |\n| malformed |\n| IN001 | b | h |\n";
        var rows = Invoke<object>(typeof(OperationsLearningPolicy), "ParseSelectedIncidentTable", incidentTable, failures);
        Assert.NotNull(rows);
        Assert.Contains(failures, value => value.Contains("shape", StringComparison.Ordinal));
        Assert.Contains(failures, value => value.Contains("duplicate", StringComparison.Ordinal));

        failures.Clear();
        var dispositions = "### Rule dispositions\n| Rule ID | Card ID | Source IDs | Disposition | Incident evidence | Pyramid action |\n|---|---|---|---|---|---|\n| RULE:none | none | NONE | MISSING | enough evidence words for this row | enough action words for this row |\n| malformed |\n";
        _ = Invoke<object>(typeof(OperationsLearningPolicy), "ParseRuleDispositions", dispositions, failures);
        Assert.Contains(failures, value => value.Contains("row-shape", StringComparison.Ordinal));

        failures.Clear();
        var repairs = "| RPI | Description |\n|---|---|\n| BAD | invalid |\n| RPI001 | first |\n| RPI001 | duplicate |\n";
        _ = Invoke<IReadOnlyList<string>>(typeof(OperationsLearningPolicy), "ParseRepairItemIds", repairs, failures);
        Assert.Contains(failures, value => value.Contains("row-invalid", StringComparison.Ordinal));
        Assert.Contains(failures, value => value.Contains("duplicate", StringComparison.Ordinal));

        using var json = JsonDocument.Parse("{\"string\":\"value\",\"null\":null,\"number\":1}");
        Assert.Equal("value", Invoke<string>(typeof(OperationsLearningPolicy), "ReadJsonString", json.RootElement, "string"));
        Assert.Empty(Invoke<string>(typeof(OperationsLearningPolicy), "ReadJsonString", json.RootElement, "missing"));
        Assert.Empty(Invoke<string>(typeof(OperationsLearningPolicy), "ReadJsonString", json.RootElement, "null"));
        Assert.Empty(Invoke<string>(typeof(OperationsLearningPolicy), "ReadJsonString", json.RootElement, "number"));

        failures.Clear();
        Assert.Null(Invoke<string?>(typeof(OperationsLearningPolicy), "ResolveFixedRegularFile", _root, "directory", failures, "dir"));
        Assert.Null(Invoke<string?>(typeof(OperationsLearningPolicy), "ResolveFixedRegularFile", _root, "missing", failures, "missing"));
        Assert.NotEmpty(failures);
        Invoke<object?>(typeof(OperationsLearningPolicy), "ValidateExistingDirectoryPath", _root, Path.GetFullPath(Path.Combine(_root, "directory")), "generated", failures);
        Invoke<object?>(typeof(OperationsLearningPolicy), "ValidateExistingDirectoryPath", _root, "../outside", "generated", failures);
        Assert.Contains(failures, value => value.Contains("generated-invalid", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => Invoke<object>(typeof(OperationsLearningPolicy), "ResolveGovernedPath", _root, " ", "", false));
    }

    [Fact]
    public void Tool_privateOptionPathAndHookArgumentGuards_coverAllShapes()
    {
        Assert.Equal("x", Invoke<string>(typeof(ValidationCommands), "GetOption", new[] { "--x", "x" }, "--x", "."));
        Assert.Equal(".", Invoke<string>(typeof(ValidationCommands), "GetOption", Array.Empty<string>(), "--x", "."));
        Assert.Equal("x", Invoke<string>(typeof(ValidationCommands), "RequireOption", new[] { "--x", "x" }, "--x"));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(ValidationCommands), "RequireOption", Array.Empty<string>(), "--x"));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(ValidationCommands), "RequireOption", new[] { "--x", "--y" }, "--x"));

        Assert.Equal("value", Invoke<string>(typeof(OperationsHookCommands), "Argument", "--x value", "--x"));
        Assert.Equal("two words", Invoke<string>(typeof(OperationsHookCommands), "Argument", "--x \"two words\"", "--x"));
        Assert.Equal("two words", Invoke<string>(typeof(OperationsHookCommands), "Argument", "--x 'two words'", "--x"));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(OperationsHookCommands), "Argument", "none", "--x"));
        Assert.Equal(".engloop/x.md", Invoke<string>(typeof(OperationsHookCommands), "GovernedPath", ".engloop\\x.md", ".engloop/"));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(OperationsHookCommands), "GovernedPath", "C:/x", ".engloop/"));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(OperationsHookCommands), "GovernedPath", "../x", ".engloop/"));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(OperationsHookCommands), "GovernedPath", "other/x", ".engloop/"));

        Assert.Equal(["IN001", "IN002"], Invoke<string[]>(typeof(OperationsHookCommands), "IdList", "IN002,IN001", "^IN\\d{3}$", "incident"));
        Assert.Throws<InvalidOperationException>(() => Invoke<string[]>(typeof(OperationsHookCommands), "IdList", "", "^IN\\d{3}$", "incident"));
        Assert.Throws<InvalidOperationException>(() => Invoke<string[]>(typeof(OperationsHookCommands), "IdList", "IN001,IN001", "^IN\\d{3}$", "incident"));
        Assert.Throws<InvalidOperationException>(() => Invoke<string[]>(typeof(OperationsHookCommands), "IdList", "BAD", "^IN\\d{3}$", "incident"));
        Assert.Equal("short", Invoke<string>(typeof(OperationsHookCommands), "Bound", "short"));
        Assert.EndsWith("...[truncated]", Invoke<string>(typeof(OperationsHookCommands), "Bound", new string('x', 5000)));

        Assert.Equal("operations-hook-json-invalid", Invoke<string>(typeof(OperationsHookCommands), "IncidentDiagnosticCode", new JsonException("invalid")));
        Assert.Equal("operations-hook-storage-unavailable", Invoke<string>(typeof(OperationsHookCommands), "IncidentDiagnosticCode", new IOException("unavailable")));
        Assert.Equal("operations-hook-storage-unavailable", Invoke<string>(typeof(OperationsHookCommands), "IncidentDiagnosticCode", new UnauthorizedAccessException("denied")));
        Assert.Equal("operations-hook-known", Invoke<string>(typeof(OperationsHookCommands), "IncidentDiagnosticCode", new InvalidOperationException("operations-hook-known")));
        Assert.Equal("operations-hook-invalid-state", Invoke<string>(typeof(OperationsHookCommands), "IncidentDiagnosticCode", new InvalidOperationException("uncoded")));
        Assert.Equal("operations-hook-unexpected-failure", Invoke<string>(typeof(OperationsHookCommands), "IncidentDiagnosticCode", new Exception("unexpected")));
        Assert.Equal("--incident", Invoke<string?>(typeof(OperationsHookCommands), "MissingOption", "operations-hook-option-missing:--incident"));
        Assert.Equal("--incident", Invoke<string?>(typeof(OperationsHookCommands), "MissingOption", "operations-hook-prompt-missing"));
        Assert.Null(Invoke<string?>(typeof(OperationsHookCommands), "MissingOption", "operations-hook-json-invalid"));
    }

    [Fact]
    public void Tool_privateValidationAndHookHelpers_coverDefensiveBranches()
    {
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>(typeof(ValidationCommands), "EnsureValidation", false, "expected"));
        Invoke<object?>(typeof(ValidationCommands), "EnsureValidation", true, "unused");

        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(ValidationCommands), "ResolveOperationsOutput", _root, "", ".engloop/out/", false));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(ValidationCommands), "ResolveOperationsOutput", _root, Path.GetFullPath(Path.Combine(_root, "file.txt")), ".engloop/out/", false));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(ValidationCommands), "ResolveOperationsOutput", _root, "../escape", ".engloop/out/", false));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(ValidationCommands), "ResolveOperationsOutput", _root, "file.txt", ".engloop/out/", false));
        Assert.Throws<FileNotFoundException>(() => Invoke<string>(typeof(ValidationCommands), "ResolveOperationsOutput", _root, ".engloop/out/missing", ".engloop/out/", true));
        Assert.Contains(".engloop", Invoke<string>(typeof(ValidationCommands), "ResolveOperationsOutput", _root, ".engloop/out/new", ".engloop/out/", false));

        Assert.Throws<InvalidOperationException>(() => Invoke<object?>(typeof(ValidationCommands), "ValidateRootAndConfigForOperations", _root));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(ValidationCommands), "NormalizeReadinessEvidencePath", _root, ""));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(ValidationCommands), "NormalizeReadinessEvidencePath", _root, Path.GetFullPath(Path.Combine(_root, "file.txt"))));
        Assert.Throws<InvalidOperationException>(() => Invoke<string>(typeof(ValidationCommands), "NormalizeReadinessEvidencePath", _root, "file.txt"));
        Assert.Equal(".engloop/coverage/e.json", Invoke<string>(typeof(ValidationCommands), "NormalizeReadinessEvidencePath", _root, ".engloop/coverage/e.json"));

        using var strings = JsonDocument.Parse("{\"second\":\"value\",\"null\":null,\"number\":1}");
        Assert.Equal("value", Invoke<string>(typeof(OperationsHookCommands), "ReadString", strings.RootElement, new[] { "first", "second" }));
        Assert.Empty(Invoke<string>(typeof(OperationsHookCommands), "ReadString", strings.RootElement, new[] { "null" }));
        Assert.Empty(Invoke<string>(typeof(OperationsHookCommands), "ReadString", strings.RootElement, new[] { "number", "missing" }));

        var captured = Invoke<(int ExitCode, string Diagnostic)>(typeof(OperationsHookCommands), "CaptureValidation", (Func<int>)(() => { Console.Write("out"); Console.Error.Write("err"); return 7; }));
        Assert.Equal(7, captured.ExitCode);
        Assert.Contains("out", captured.Diagnostic);
        Assert.Contains("err", captured.Diagnostic);
        Assert.Empty(Invoke<(int ExitCode, string Diagnostic)>(typeof(OperationsHookCommands), "CaptureValidation", (Func<int>)(() => 0)).Diagnostic);
        Assert.Null(Invoke<object?>(typeof(OperationsHookCommands), "ParseArguments", "", "incident", true));
        Assert.Throws<InvalidOperationException>(() => Invoke<object?>(typeof(OperationsHookCommands), "ParseArguments", "", "incident", false));

        Assert.Equal(1, Program.Main(["repair-gate"]));
        Assert.Equal(1, Program.Main(["repair-gate", "unknown"]));
        Assert.Equal(1, Program.Main(["readiness"]));
        Assert.Equal(0, Program.Main(["operations-hook"]));
        var originalIn = Console.In;
        var originalOut = Console.Out;
        using var input = new StringReader(JsonSerializer.Serialize(new { cwd = _root, session_id = "session" }));
        using var hookOutput = new StringWriter();
        try
        {
            Console.SetIn(input);
            Console.SetOut(hookOutput);
            foreach (var phase in new[] { "start", "initialize", "stop", "unknown" })
                Invoke<object?>(typeof(OperationsHookCommands), "WriteIncidentContextDeferred", phase, "operations-hook-unexpected-failure", "unexpected");
            Assert.Equal(0, Program.Main(["operations-hook", "start", "incident"]));
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
        var responses = hookOutput.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(5, responses.Length);
        var messages = responses.Select(response =>
        {
            using var json = JsonDocument.Parse(response);
            Assert.True(json.RootElement.GetProperty("continue").GetBoolean());
            return json.RootElement.GetProperty("systemMessage").GetString() ?? string.Empty;
        }).ToArray();
        foreach (var phase in new[] { "start", "initialize", "stop", "unknown" })
            Assert.Contains(messages, message => message.Contains($"\"phase\":\"{phase}\"", StringComparison.Ordinal));
    }

    private static T Invoke<T>(Type type, string name, params object?[] args)
    {
        var method = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(method => method.Name == name && method.GetParameters().Length == args.Length);
        try { return (T)method.Invoke(null, args)!; }
        catch (TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
