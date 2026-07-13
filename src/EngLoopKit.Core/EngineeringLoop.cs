namespace EngLoopKit.Core;

/// <summary>
/// The stages of the EngLoopKit engineering loop. This enum is the executable form of the
/// stage list in <c>docs/engineering-loop.md</c>.
/// </summary>
public enum Stage
{
    Northstar,
    Scaffold,
    Architect,
    Refactor,
    Model,
    Explore,
    Validate,
    UnitTest,
    Incident,
    Postmortem,
    Repair,
    RefactorScan,
    LearningsPyramid,
}

/// <summary>
/// The engineering loop as a state machine: the single source of truth for which stage
/// transitions are legal. The vertical here supplies only the domain knowledge — the
/// <see cref="Stage"/> values and the transition graph — and composes the generic
/// guarded-machine machinery from the <c>EngLoopKit.Components.StateMachine</c> component.
/// This is the executable form of the loop diagram in <c>docs/engineering-loop.md</c>.
/// </summary>
public static class TransitionReasons
{
    public const string Accepted = "accepted";
    public const string MissingProcessRoot = "missing-process-root";
    public const string AmbiguousProcessRoot = "ambiguous-process-root";
    public const string InvalidCommand = "invalid-command";
    public const string DuplicateStart = "duplicate-start";
    public const string MissingNorthstar = "missing-northstar";
    public const string MissingProvenRunway = "missing-proven-runway";
    public const string MissingArchitecture = "missing-architecture";
    public const string MissingImplementation = "missing-implementation";
    public const string MissingModelOrExploration = "missing-model-or-exploration";
    public const string MissingFunctionalValidation = "missing-functional-validation";
    public const string UnclassifiedReachability = "unclassified-reachability";
    public const string UnitTestsTooEarly = "unit-tests-too-early";
    public const string AmbiguousReachability = "ambiguous-reachability";
    public const string MissingCurrentReadiness = "missing-current-readiness";
    public const string StaleReadiness = "stale-readiness";
    public const string NoIncidentDemand = "no-incident-demand";
    public const string NoPostmortemSelection = "no-postmortem-selection";
    public const string NoRepairDemand = "no-repair-demand";
    public const string MissingRepairRouting = "missing-repair-routing";
    public const string RepairGateBypass = "repair-gate-bypass";
    public const string NoStewardshipCapacity = "no-stewardship-capacity";
    public const string NoLearningRefreshDemand = "no-learning-refresh-demand";
    public const string RefactorGateBypass = "refactor-gate-bypass";
    public const string InvalidOrder = "invalid-order";
}

/// <summary>
/// Authoritative evidence and demand for one requested stage. Missing evidence is never
/// inferred from a neighbouring stage or a successful test run.
/// </summary>
public sealed record TransitionEvidence(
    bool RootLayoutValid = true,
    string RootFailureReason = "",
    bool NorthstarComplete = false,
    bool RunwayProven = false,
    bool ArchitectureCurrent = false,
    bool ImplementationCurrent = false,
    bool ModelAdequate = false,
    bool GenerationFresh = false,
    bool FunctionalPass = false,
    bool ReachabilityDispositionComplete = false,
    bool ReachabilityAmbiguous = false,
    bool DirectEvidenceCurrent = false,
    bool ReadinessGatePass = false,
    bool IncidentDemand = false,
    bool IncidentStabilized = false,
    bool SelectedIncidentSet = false,
    bool RepairItemDemand = false,
    bool StewardshipCapacity = false,
    bool LearningRefreshCurrent = false,
    bool DirectionChange = false,
    bool ArchitectureImpact = false);

/// <summary>One pure transition attempt. A rejection retains the exact prior state.</summary>
public sealed record TransitionResult(
    bool Accepted,
    string Reason,
    EngineeringLoopState State,
    IReadOnlyList<string> MissingEvidence)
{
    public static TransitionResult Reject(EngineeringLoopState state, string reason, params string[] missing)
        => new(false, reason, state, missing);

    public static TransitionResult Accept(EngineeringLoopState state)
        => new(true, TransitionReasons.Accepted, state, []);
}

/// <summary>
/// Pure ordered-v2 transition evaluator. Command ordinals are UI identity only: every
/// accepted transition is authorized by current evidence/demand, and every rejected one
/// leaves state untouched.
/// </summary>
public static class EngineeringLoop
{
    public static IReadOnlyList<Stage> AllStages { get; } = Enum.GetValues<Stage>();

    public static TransitionResult Evaluate(EngineeringLoopState state, Stage requested, TransitionEvidence e)
    {
        var rootFailure = RootFailure(e);
        if (rootFailure is not null)
        {
            return TransitionResult.Reject(state, rootFailure, rootFailure);
        }

        return requested switch
        {
            Stage.Northstar => Northstar(state, e),
            Stage.Scaffold => Scaffold(state, e),
            Stage.Architect => Architect(state, e),
            Stage.Refactor => Refactor(state, e),
            Stage.Model => Model(state, e),
            Stage.Explore => Explore(state, e),
            Stage.Validate => Validate(state, e),
            Stage.UnitTest => UnitTest(state, e),
            Stage.Incident => Incident(state, e),
            Stage.Postmortem => Postmortem(state, e),
            Stage.Repair => Repair(state, e),
            Stage.RefactorScan => RefactorScan(state, e),
            Stage.LearningsPyramid => LearningsPyramid(state, e),
            _ => TransitionResult.Reject(state, TransitionReasons.InvalidCommand, TransitionReasons.InvalidCommand),
        };
    }

    public static TransitionResult EvaluateUnknown(EngineeringLoopState state, TransitionEvidence e)
    {
        var rootFailure = RootFailure(e);
        return rootFailure is null
            ? TransitionResult.Reject(state, TransitionReasons.InvalidCommand, TransitionReasons.InvalidCommand)
            : TransitionResult.Reject(state, rootFailure, rootFailure);
    }

    private static string? RootFailure(TransitionEvidence e)
    {
        if (e.RootLayoutValid) return null;
        return e.RootFailureReason == TransitionReasons.AmbiguousProcessRoot
            ? TransitionReasons.AmbiguousProcessRoot
            : TransitionReasons.MissingProcessRoot;
    }

    private static TransitionResult Northstar(EngineeringLoopState s, TransitionEvidence e)
    {
        if (!e.NorthstarComplete) return TransitionResult.Reject(s, TransitionReasons.MissingNorthstar, TransitionReasons.MissingNorthstar);
        var directionBranch = e.DirectionChange || s.DirectionChangePending;
        if (s.DeliveryCursor != DeliveryCursor.NotStarted && !directionBranch)
            return TransitionResult.Reject(s, TransitionReasons.DuplicateStart, TransitionReasons.DuplicateStart);
        return TransitionResult.Accept(s with
        {
            LastAcceptedStage = Stage.Northstar,
            DeliveryCursor = DeliveryCursor.Northstar,
            Readiness = null,
            IncidentDemandActive = false,
            IncidentStabilized = false,
            SelectedIncidentSet = false,
            RepairItemDemand = false,
            DirectionChangePending = false,
            DirectionRefactorRequired = directionBranch,
        });
    }

    private static TransitionResult Scaffold(EngineeringLoopState s, TransitionEvidence e)
    {
        if (s.DeliveryCursor != DeliveryCursor.Northstar) return TransitionResult.Reject(s, TransitionReasons.InvalidOrder, TransitionReasons.MissingNorthstar);
        if (!e.RunwayProven) return TransitionResult.Reject(s, TransitionReasons.MissingProvenRunway, TransitionReasons.MissingProvenRunway);
        return TransitionResult.Accept(s with
        {
            LastAcceptedStage = Stage.Scaffold,
            DeliveryCursor = DeliveryCursor.Scaffold,
            Readiness = null,
        });
    }

    private static TransitionResult Architect(EngineeringLoopState s, TransitionEvidence e)
    {
        var last = EffectiveLastStage(s);
        var allowed = last == Stage.Scaffold
                      || ((e.ArchitectureImpact || s.ArchitectureImpactPending)
                          && (last == Stage.Northstar || (last == Stage.RefactorScan && !s.DirectionChangePending)));
        if (!allowed) return TransitionResult.Reject(s, TransitionReasons.InvalidOrder, TransitionReasons.MissingProvenRunway);
        if (!e.ArchitectureCurrent) return TransitionResult.Reject(s, TransitionReasons.MissingArchitecture, TransitionReasons.MissingArchitecture);
        return TransitionResult.Accept(s with
        {
            LastAcceptedStage = Stage.Architect,
            DeliveryCursor = DeliveryCursor.Architecture,
            Readiness = null,
        });
    }

    private static TransitionResult Refactor(EngineeringLoopState s, TransitionEvidence e)
    {
        var last = EffectiveLastStage(s);
        if (last == Stage.Postmortem)
        {
            return TransitionResult.Reject(s, TransitionReasons.MissingRepairRouting, TransitionReasons.MissingRepairRouting);
        }
        var allowed = last is Stage.Architect or Stage.Repair
                      || (s.HasOpenRepairObligation() && last == Stage.Repair)
                      || ((e.ArchitectureImpact || s.ArchitectureImpactPending || s.DirectionRefactorRequired)
                          && last == Stage.Northstar);
        if (!allowed) return TransitionResult.Reject(s, TransitionReasons.MissingArchitecture, TransitionReasons.MissingArchitecture);
        if (!e.ImplementationCurrent) return TransitionResult.Reject(s, TransitionReasons.MissingImplementation, TransitionReasons.MissingImplementation);
        return TransitionResult.Accept(s with
        {
            LastAcceptedStage = Stage.Refactor,
            DeliveryCursor = DeliveryCursor.Refactor,
            ProductRevision = NextRevision(s.ProductRevision),
            ModelRevision = null,
            ExplorationRevision = null,
            ValidationRevision = null,
            Readiness = null,
            ReachabilityDispositionComplete = false,
            DirectionChangePending = false,
            DirectionRefactorRequired = false,
            ArchitectureImpactPending = false,
        });
    }

    private static TransitionResult Model(EngineeringLoopState s, TransitionEvidence e)
    {
        if (EffectiveLastStage(s) is not (Stage.Refactor or Stage.Validate)) return TransitionResult.Reject(s, TransitionReasons.MissingImplementation, TransitionReasons.MissingImplementation);
        if (!e.ModelAdequate) return TransitionResult.Reject(s, TransitionReasons.MissingModelOrExploration, TransitionReasons.MissingModelOrExploration);
        return TransitionResult.Accept(s with { LastAcceptedStage = Stage.Model, DeliveryCursor = DeliveryCursor.Model, ModelRevision = s.ProductRevision, ExplorationRevision = null, ValidationRevision = null, Readiness = null, ReachabilityDispositionComplete = false });
    }

    private static TransitionResult Explore(EngineeringLoopState s, TransitionEvidence e)
    {
        if (EffectiveLastStage(s) is not (Stage.Model or Stage.Validate)) return TransitionResult.Reject(s, TransitionReasons.MissingModelOrExploration, TransitionReasons.MissingModelOrExploration);
        if (!e.GenerationFresh || !string.Equals(s.ModelRevision, s.ProductRevision, StringComparison.Ordinal)) return TransitionResult.Reject(s, TransitionReasons.MissingModelOrExploration, TransitionReasons.MissingModelOrExploration);
        return TransitionResult.Accept(s with { LastAcceptedStage = Stage.Explore, DeliveryCursor = DeliveryCursor.Explored, ExplorationRevision = s.ProductRevision, ValidationRevision = null, Readiness = null, ReachabilityDispositionComplete = false });
    }

    private static TransitionResult Validate(EngineeringLoopState s, TransitionEvidence e)
    {
        var last = EffectiveLastStage(s);
        if (last != Stage.Explore
            && !(last == Stage.UnitTest && s.DeliveryCursor == DeliveryCursor.Disposition))
        {
            return TransitionResult.Reject(s, TransitionReasons.MissingModelOrExploration, TransitionReasons.MissingModelOrExploration);
        }
        if (!e.FunctionalPass || !string.Equals(s.ExplorationRevision, s.ProductRevision, StringComparison.Ordinal)) return TransitionResult.Reject(s, TransitionReasons.MissingFunctionalValidation, TransitionReasons.MissingFunctionalValidation);
        return TransitionResult.Accept(s with { LastAcceptedStage = Stage.Validate, DeliveryCursor = DeliveryCursor.Validated, ValidationRevision = s.ProductRevision, Readiness = null });
    }

    private static TransitionResult UnitTest(EngineeringLoopState s, TransitionEvidence e)
    {
        var last = EffectiveLastStage(s);
        if (last == Stage.Repair)
        {
            return TransitionResult.Reject(s, TransitionReasons.RepairGateBypass, TransitionReasons.RepairGateBypass);
        }
        if (last == Stage.RefactorScan)
        {
            return TransitionResult.Reject(s, TransitionReasons.RefactorGateBypass, TransitionReasons.RefactorGateBypass);
        }
        if (last == Stage.Validate && s.DeliveryCursor == DeliveryCursor.Validated)
        {
            if (!string.Equals(s.ValidationRevision, s.ProductRevision, StringComparison.Ordinal)) return TransitionResult.Reject(s, TransitionReasons.MissingFunctionalValidation, TransitionReasons.MissingFunctionalValidation);
            if (e.ReachabilityAmbiguous) return TransitionResult.Reject(s, TransitionReasons.AmbiguousReachability, TransitionReasons.AmbiguousReachability);
            if (!e.ReachabilityDispositionComplete) return TransitionResult.Reject(s, TransitionReasons.UnclassifiedReachability, TransitionReasons.UnclassifiedReachability);
            return TransitionResult.Accept(s with { LastAcceptedStage = Stage.UnitTest, DeliveryCursor = DeliveryCursor.Disposition, ReachabilityDispositionComplete = true });
        }

        if (last != Stage.UnitTest || s.DeliveryCursor != DeliveryCursor.Disposition) return TransitionResult.Reject(s, TransitionReasons.UnitTestsTooEarly, TransitionReasons.UnitTestsTooEarly);
        if (!e.DirectEvidenceCurrent) return TransitionResult.Reject(s, TransitionReasons.UnclassifiedReachability, TransitionReasons.UnclassifiedReachability);
        return TransitionResult.Accept(s with
        {
            LastAcceptedStage = Stage.UnitTest,
            DeliveryCursor = e.ReadinessGatePass ? DeliveryCursor.Ready : DeliveryCursor.Disposition,
            Readiness = new ReadinessEvidence(e.ReadinessGatePass, s.ProductRevision, DateTimeOffset.UtcNow),
        });
    }

    private static TransitionResult Incident(EngineeringLoopState s, TransitionEvidence e)
    {
        if (!e.IncidentDemand) return TransitionResult.Reject(s, TransitionReasons.NoIncidentDemand, TransitionReasons.NoIncidentDemand);
        var effectiveStage = EffectiveLastStage(s);
        var steady = (effectiveStage is Stage.UnitTest or Stage.RefactorScan)
                 && s.DeliveryCursor == DeliveryCursor.Ready
                 && !s.DirectionChangePending
                 && !s.ArchitectureImpactPending;
        var repeated = effectiveStage == Stage.Incident;
        if ((!steady && !repeated) || !s.HasCurrentReadinessPass()) return TransitionResult.Reject(s, s.Readiness is null ? TransitionReasons.MissingCurrentReadiness : TransitionReasons.StaleReadiness, TransitionReasons.MissingCurrentReadiness);
        return TransitionResult.Accept(s with { LastAcceptedStage = Stage.Incident, IncidentDemandActive = true, IncidentStabilized = e.IncidentStabilized });
    }

    private static TransitionResult Postmortem(EngineeringLoopState s, TransitionEvidence e)
    {
        if (EffectiveLastStage(s) != Stage.Incident || !s.IncidentDemandActive || !s.IncidentStabilized || !e.SelectedIncidentSet) return TransitionResult.Reject(s, TransitionReasons.NoPostmortemSelection, TransitionReasons.NoPostmortemSelection);
        return TransitionResult.Accept(s with { LastAcceptedStage = Stage.Postmortem, SelectedIncidentSet = true, LearningRefreshPending = true, RepairItemDemand = e.RepairItemDemand });
    }

    private static TransitionResult Repair(EngineeringLoopState s, TransitionEvidence e)
    {
        var effectiveStage = EffectiveLastStage(s);
        if (effectiveStage != Stage.Postmortem || !s.SelectedIncidentSet || !e.RepairItemDemand) return TransitionResult.Reject(s, TransitionReasons.NoRepairDemand, TransitionReasons.NoRepairDemand);
        var obligation = new RepairObligation($"RPI-{s.RepairObligations.Count + 1:D3}", false, false, false, false);
        return TransitionResult.Accept(s with { LastAcceptedStage = Stage.Repair, RepairObligations = s.RepairObligations.Append(obligation).ToArray(), DeliveryCursor = DeliveryCursor.Architecture });
    }

    private static TransitionResult RefactorScan(EngineeringLoopState s, TransitionEvidence e)
    {
        if (!e.StewardshipCapacity) return TransitionResult.Reject(s, TransitionReasons.NoStewardshipCapacity, TransitionReasons.NoStewardshipCapacity);
        var last = EffectiveLastStage(s);
        if (last is not (Stage.UnitTest or Stage.RefactorScan) || s.DeliveryCursor != DeliveryCursor.Ready || !s.HasCurrentReadinessPass()) return TransitionResult.Reject(s, TransitionReasons.RefactorGateBypass, TransitionReasons.RefactorGateBypass);
        return TransitionResult.Accept(s with
        {
            LastAcceptedStage = Stage.RefactorScan,
            StewardshipCapacity = true,
            DirectionChangePending = e.DirectionChange,
            ArchitectureImpactPending = e.ArchitectureImpact,
        });
    }

    private static TransitionResult LearningsPyramid(EngineeringLoopState s, TransitionEvidence e)
    {
        if (!e.StewardshipCapacity) return TransitionResult.Reject(s, TransitionReasons.NoStewardshipCapacity, TransitionReasons.NoStewardshipCapacity);
        if (!s.LearningRefreshPending || !e.LearningRefreshCurrent) return TransitionResult.Reject(s, TransitionReasons.NoLearningRefreshDemand, TransitionReasons.NoLearningRefreshDemand);
        return TransitionResult.Accept(s with
        {
            LastAcceptedStage = Stage.LearningsPyramid,
            ReturnStage = s.LastAcceptedStage,
            LearningRefreshPending = false,
        });
    }

    private static string NextRevision(string current)
        => long.TryParse(current, out var value)
            ? (value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : Guid.NewGuid().ToString("N");

    private static Stage? EffectiveLastStage(EngineeringLoopState state)
        => state.LastAcceptedStage == Stage.LearningsPyramid ? state.ReturnStage : state.LastAcceptedStage;
}
