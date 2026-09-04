namespace EngLoopKit.Tool;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length >= 1 && string.Equals(args[0], "overlay", StringComparison.Ordinal))
        {
            return OverlayCommands.Execute(args[1..]);
        }

        if (args.Length >= 1 && string.Equals(args[0], "readiness", StringComparison.Ordinal))
        {
            return ValidationCommands.ExecuteReadiness(args[1..]);
        }

        if (args.Length >= 1 && string.Equals(args[0], "repair-gate", StringComparison.Ordinal))
        {
            return ValidationCommands.ExecuteRepairGate(args[1..]);
        }

        if (args.Length >= 1 && string.Equals(args[0], "operations-hook", StringComparison.Ordinal))
        {
            return OperationsHookCommands.Execute(args[1..]);
        }

        if (args.Length >= 1 && string.Equals(args[0], "postmortem-route", StringComparison.Ordinal))
        {
            return OperationsHookCommands.ExecutePostmortemRoute(args[1..]);
        }

        if (args.Length >= 1 && string.Equals(args[0], "code-review-response-hook", StringComparison.Ordinal))
        {
            return CodeReviewResponseCommands.ExecuteHook(args[1..]);
        }

        if (args.Length >= 1 && string.Equals(args[0], "code-review-response", StringComparison.Ordinal))
        {
            return CodeReviewResponseCommands.Execute(args[1..]);
        }

        if (args.Length >= 1 && string.Equals(args[0], "refactor-profile", StringComparison.Ordinal))
        {
            return RefactorProfileCommands.Execute(args[1..]);
        }

        if (args.Length < 2 || !string.Equals(args[0], "validate", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Usage: engloopkit validate <root|config|commands|reachability|learnings|incident-context|postmortem-learning|repair-learning|installation|agent-entry|agent-entry-hook|agent-surfaces> [options] | engloopkit code-review-response-hook <initialize|guard|post-tool|stop> <address|reply-resolve> | engloopkit code-review-response apply --gate <path> --approval <path> | engloopkit postmortem-route bind --collection <path> --token <token> --incidents <INxxx,...> --postmortem <path> --confirmation-receipt <path> | engloopkit refactor-profile <bind|clear> | engloopkit repair-gate execute [options] | engloopkit readiness emit [options] | engloopkit overlay <install|register|verify|pack|unpack|remove|status>");
            return 1;
        }

        var command = args[1];
        return command switch
        {
            "root" => ValidationCommands.ValidateRoot(args),
            "config" => ValidationCommands.ValidateConfig(args),
            "commands" => ValidationCommands.ValidateCommands(args),
            "reachability" => ValidationCommands.ValidateReachability(args),
            "learnings" => ValidationCommands.ValidateLearnings(args),
            "incident-context" => ValidationCommands.ValidateIncidentContext(args),
            "postmortem-learning" => ValidationCommands.ValidatePostmortemLearning(args),
            "repair-learning" => ValidationCommands.ValidateRepairLearning(args),
            "installation" => ValidationCommands.ValidateInstallation(args),
            "agent-entry" => ValidationCommands.ValidateAgentEntry(args),
            "agent-entry-hook" => ValidationCommands.ValidateAgentEntryHook(args),
            "agent-surfaces" => ValidationCommands.ValidateAgentSurfaces(args),
            _ => 1
        };
    }
}
