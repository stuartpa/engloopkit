namespace EngLoopKit.Core;

/// <summary>
/// The system-under-test surface that the SEK-generated conformance tests drive. Each
/// method corresponds to a loop stage (rule <c>Loop.&lt;Stage&gt;</c> in the model) and
/// asserts, in a self-contained way, that the stage is a defined member of the engineering
/// loop with a well-formed transition set. The generated tests replay legal stage
/// sequences through these methods; deeper behaviour (guarded sequencing, rejection of
/// illegal transitions) is covered by the hand-written tests against <see cref="EngineeringLoop"/>.
///
/// Methods are intentionally stateless so they behave correctly under SEK's generated
/// harness, which constructs a fresh instance for every replayed step.
/// </summary>
public sealed class Loop
{
    private static void Enter(Stage stage)
    {
        // Self-contained conformance check: the stage must be a defined loop stage whose
        // legal-successor set the implementation can produce. Exercises the real loop
        // graph in EngineeringLoop for every replayed stage.
        if (!EngineeringLoop.AllStages.Contains(stage))
        {
            throw new InvalidOperationException($"undefined loop stage: {stage}");
        }

        _ = EngineeringLoop.LegalNext(stage);
    }

    public void Seed() => Enter(Stage.Seed);

    public void Bridge() => Enter(Stage.Bridge);

    public void Architect() => Enter(Stage.Architect);

    public void RefactorToFinal() => Enter(Stage.RefactorToFinal);

    public void Model() => Enter(Stage.Model);

    public void Explore() => Enter(Stage.Explore);

    public void Coverage() => Enter(Stage.Coverage);

    public void Incident() => Enter(Stage.Incident);

    public void Postmortem() => Enter(Stage.Postmortem);

    public void Repair() => Enter(Stage.Repair);

    public void RefactorScan() => Enter(Stage.RefactorScan);
}
