namespace EngLoopKit.Core;

public enum DeliveryCursor
{
    NotStarted,
    Northstar,
    Scaffold,
    Architecture,
    Refactor,
    Model,
    Explored,
    Validated,
    Disposition,
    Ready,
}

public sealed record RepairObligation(
    string Id,
    bool SourceImplemented,
    bool ReleaseArtifactBuilt,
    bool TargetVerified,
    bool Closed)
{
    public bool CanClose(EngineeringLoopState state)
        => SourceImplemented
           && ReleaseArtifactBuilt
           && TargetVerified
           && state.HasCurrentReadinessPass();
}

public sealed record ReadinessEvidence(bool Passed, string ProductRevision, DateTimeOffset CapturedAtUtc);

public sealed record EngineeringLoopState(
    Stage? LastAcceptedStage,
    DeliveryCursor DeliveryCursor,
    string ProductRevision,
    string? ModelRevision,
    string? ExplorationRevision,
    string? ValidationRevision,
    ReadinessEvidence? Readiness,
    IReadOnlyList<RepairObligation> RepairObligations,
    bool ReachabilityDispositionComplete,
    bool LearningRefreshPending,
    bool IncidentDemandActive,
    bool SelectedIncidentSet,
    bool RepairItemDemand,
    bool StewardshipCapacity)
{
    /// <summary>Stage 20 has captured verified stabilization evidence for the active incident set.</summary>
    public bool IncidentStabilized { get; init; }

    /// <summary>Stage 30 selected work that changes the living Northstar.</summary>
    public bool DirectionChangePending { get; init; }

    /// <summary>Northstar consumed a selected direction-change scan and must now route through Refactor.</summary>
    public bool DirectionRefactorRequired { get; init; }

    /// <summary>Stage 30 selected work that must return through architecture before refactor.</summary>
    public bool ArchitectureImpactPending { get; init; }

    /// <summary>Stage that invoked independent Stage 31; used to resume legal context after learning refresh.</summary>
    public Stage? ReturnStage { get; init; }

    public static EngineeringLoopState Initial { get; } = new(
        LastAcceptedStage: null,
        DeliveryCursor.NotStarted,
        ProductRevision: "0",
        ModelRevision: null,
        ExplorationRevision: null,
        ValidationRevision: null,
        Readiness: null,
        RepairObligations: [],
        ReachabilityDispositionComplete: false,
        LearningRefreshPending: false,
        IncidentDemandActive: false,
        SelectedIncidentSet: false,
        RepairItemDemand: false,
        StewardshipCapacity: false);

    public bool HasCurrentReadinessPass()
        => Readiness is not null
           && Readiness.Passed
           && string.Equals(Readiness.ProductRevision, ProductRevision, StringComparison.Ordinal);

    public bool HasOpenRepairObligation()
        => RepairObligations.Any(o => !o.Closed);
}
