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

        if (args.Length < 2 || !string.Equals(args[0], "validate", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Usage: engloopkit validate <root|config|commands|reachability|learnings|installation|agent-entry|agent-surfaces> [--root <path>] [--stage <id>] | engloopkit readiness emit --root <path> --evidence <stage-08-evidence> --verdict pass | engloopkit overlay <install|register|verify|pack|unpack|remove|status>");
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
            "installation" => ValidationCommands.ValidateInstallation(args),
            "agent-entry" => ValidationCommands.ValidateAgentEntry(args),
            "agent-surfaces" => ValidationCommands.ValidateAgentSurfaces(args),
            _ => 1
        };
    }
}
