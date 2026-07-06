namespace EngLoopKit.Core;

/// <summary>
/// The stages of the EngLoopKit engineering loop. This enum is the executable form of the
/// stage list in <c>docs/engineering-loop.md</c>.
/// </summary>
public enum Stage
{
    Seed,
    Bridge,
    Architect,
    RefactorToFinal,
    Model,
    Explore,
    Coverage,
    Incident,
    Postmortem,
    Repair,
    RefactorScan,
}

/// <summary>
/// The engineering loop as a state machine: the single source of truth for which stage
/// transitions are legal. This is the executable form of the loop diagram in
/// <c>docs/engineering-loop.md</c>; the SEK model in <c>model/</c> mirrors it, and the
/// SEK-generated tests replay legal sequences against it.
/// </summary>
public sealed class EngineeringLoop
{
    // The loop's directed transition graph. Each stage lists the stages that may legally
    // follow it. Mirrors the nested Delivery/Verification/Operations/Evolution loops.
    private static readonly IReadOnlyDictionary<Stage, Stage[]> Transitions =
        new Dictionary<Stage, Stage[]>
        {
            [Stage.Seed] = [Stage.Bridge],
            [Stage.Bridge] = [Stage.Architect],
            [Stage.Architect] = [Stage.RefactorToFinal],
            [Stage.RefactorToFinal] = [Stage.Model],
            [Stage.Model] = [Stage.Explore],
            [Stage.Explore] = [Stage.Coverage],
            // Verification loop (explore <-> coverage) until covered; then operate, or evolve.
            [Stage.Coverage] = [Stage.Explore, Stage.Incident, Stage.RefactorScan],
            // Operations loop: one or more incidents, then a post-mortem.
            [Stage.Incident] = [Stage.Incident, Stage.Postmortem],
            [Stage.Postmortem] = [Stage.Repair],
            // A repair re-enters the Delivery loop at the refactor-to-final stage (Stage 3).
            [Stage.Repair] = [Stage.RefactorToFinal],
            // Evolution loop: the monthly scan emits a SEED, starting a fresh Delivery loop.
            [Stage.RefactorScan] = [Stage.Seed],
        };

    /// <summary>The stage the loop is currently in (once started).</summary>
    public Stage Current { get; private set; }

    /// <summary>Whether <see cref="Begin"/> has been called.</summary>
    public bool Started { get; private set; }

    /// <summary>Every stage the loop defines.</summary>
    public static IReadOnlyList<Stage> AllStages { get; } = Enum.GetValues<Stage>();

    /// <summary>True if <paramref name="to"/> may legally follow <paramref name="from"/>.</summary>
    public static bool IsLegalTransition(Stage from, Stage to) =>
        Transitions.TryGetValue(from, out var next) && Array.IndexOf(next, to) >= 0;

    /// <summary>The stages that may legally follow <paramref name="from"/>.</summary>
    public static IReadOnlyList<Stage> LegalNext(Stage from) =>
        Transitions.TryGetValue(from, out var next) ? next : [];

    /// <summary>Start the loop. A loop always begins at <see cref="Stage.Seed"/>.</summary>
    public void Begin()
    {
        if (Started)
        {
            throw new InvalidOperationException("loop already started");
        }

        Current = Stage.Seed;
        Started = true;
    }

    /// <summary>
    /// Advance to <paramref name="to"/>. Throws if the loop has not started or the
    /// transition is not legal from <see cref="Current"/>.
    /// </summary>
    public void Advance(Stage to)
    {
        if (!Started)
        {
            throw new InvalidOperationException("call Begin() before advancing");
        }

        if (!IsLegalTransition(Current, to))
        {
            throw new InvalidOperationException($"illegal transition {Current} -> {to}");
        }

        Current = to;
    }
}
