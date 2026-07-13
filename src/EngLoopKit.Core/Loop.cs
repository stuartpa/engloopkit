namespace EngLoopKit.Core;

/// <summary>
/// Stateful real-SUT boundary driven by SEK-generated conformance tests. Every public
/// method maps to one ordered v2 command stage. A model-forbidden call reaches the same
/// evidence-gated evaluator as a legal call and throws its stable rejection code.
/// </summary>
public sealed class Loop
{
    private EngineeringLoopState _state = EngineeringLoopState.Initial;

    public EngineeringLoopState State => _state;

    public void Northstar() => Apply(Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));

    public void Scaffold() => Apply(Stage.Scaffold, new TransitionEvidence(RunwayProven: true));

    public void Architect() => Apply(Stage.Architect, new TransitionEvidence(ArchitectureCurrent: true));

    public void Refactor() => Apply(Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));

    public void Model() => Apply(Stage.Model, new TransitionEvidence(ModelAdequate: true));

    public void Explore() => Apply(Stage.Explore, new TransitionEvidence(GenerationFresh: true));

    public void Validate() => Apply(Stage.Validate, new TransitionEvidence(FunctionalPass: true));

    public void UnitTest() => Apply(Stage.UnitTest, new TransitionEvidence(
        ReachabilityDispositionComplete: true,
        DirectEvidenceCurrent: true,
        ReadinessGatePass: true));

    public void Incident(bool actualDemand) => Apply(Stage.Incident, new TransitionEvidence(
        IncidentDemand: actualDemand,
        IncidentStabilized: actualDemand));

    public void Postmortem(bool selectedStabilizedSet) => Apply(Stage.Postmortem, new TransitionEvidence(
        SelectedIncidentSet: selectedStabilizedSet,
        RepairItemDemand: selectedStabilizedSet));

    public void Repair(bool repairItemDemand) => Apply(Stage.Repair, new TransitionEvidence(RepairItemDemand: repairItemDemand));

    public void RefactorScan(bool capacity, bool directionChange, bool architectureImpact) => Apply(Stage.RefactorScan, new TransitionEvidence(
        StewardshipCapacity: capacity,
        DirectionChange: directionChange,
        ArchitectureImpact: architectureImpact));

    public void LearningsPyramid(bool capacity) => Apply(Stage.LearningsPyramid, new TransitionEvidence(
        StewardshipCapacity: capacity,
        LearningRefreshCurrent: capacity));

    private void Apply(Stage stage, TransitionEvidence supplied)
    {
        // State-derived evidence is copied in explicitly. This facade is only the
        // generated-test boundary; production tooling supplies real evidence instead.
        var evidence = supplied with
        {
            ArchitectureCurrent = supplied.ArchitectureCurrent || _state.DeliveryCursor is DeliveryCursor.Architecture or DeliveryCursor.Refactor or DeliveryCursor.Model or DeliveryCursor.Explored or DeliveryCursor.Validated or DeliveryCursor.Disposition or DeliveryCursor.Ready,
            ImplementationCurrent = supplied.ImplementationCurrent || _state.DeliveryCursor is DeliveryCursor.Architecture or DeliveryCursor.Ready || _state.HasOpenRepairObligation(),
            ModelAdequate = supplied.ModelAdequate || _state.DeliveryCursor is DeliveryCursor.Refactor or DeliveryCursor.Validated,
            GenerationFresh = supplied.GenerationFresh || string.Equals(_state.ModelRevision, _state.ProductRevision, StringComparison.Ordinal),
            FunctionalPass = supplied.FunctionalPass || string.Equals(_state.ExplorationRevision, _state.ProductRevision, StringComparison.Ordinal),
            ReachabilityDispositionComplete = supplied.ReachabilityDispositionComplete || _state.ReachabilityDispositionComplete,
            DirectEvidenceCurrent = supplied.DirectEvidenceCurrent || _state.ReachabilityDispositionComplete,
            SelectedIncidentSet = supplied.SelectedIncidentSet || _state.SelectedIncidentSet,
            RepairItemDemand = supplied.RepairItemDemand || _state.RepairItemDemand,
            LearningRefreshCurrent = supplied.LearningRefreshCurrent || _state.LearningRefreshPending,
        };

        var result = EngineeringLoop.Evaluate(_state, stage, evidence);
        if (!result.Accepted)
        {
            throw new InvalidOperationException(result.Reason);
        }

        _state = result.State;
    }
}
