using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>
/// Direct finite reason-table tests for the ordered v2 evaluator. Generated SEK tests
/// prove representative behavior; these tests deepen every stable rejection category and
/// prove that a rejected attempt never mutates durable workflow state.
/// </summary>
public sealed class EngineeringLoopTests
{
    [Fact]
    public void AllStages_hasTheExactThirteenV2Stages()
    {
        Assert.Equal(13, EngineeringLoop.AllStages.Count);
        Assert.Equal(
        [
            Stage.Northstar, Stage.Scaffold, Stage.Architect, Stage.Refactor,
            Stage.Model, Stage.Explore, Stage.Validate, Stage.UnitTest,
            Stage.Incident, Stage.Postmortem, Stage.Repair, Stage.RefactorScan,
            Stage.LearningsPyramid,
        ],
        EngineeringLoop.AllStages);
    }

    [Fact]
    public void NormalDelivery_reachesCurrentReadinessPass()
    {
        var state = EngineeringLoopState.Initial;
        state = Accept(state, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));
        state = Accept(state, Stage.Scaffold, new TransitionEvidence(RunwayProven: true));
        state = Accept(state, Stage.Architect, new TransitionEvidence(ArchitectureCurrent: true));
        state = Accept(state, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));
        state = Accept(state, Stage.Model, new TransitionEvidence(ModelAdequate: true));
        state = Accept(state, Stage.Explore, new TransitionEvidence(GenerationFresh: true));
        state = Accept(state, Stage.Validate, new TransitionEvidence(FunctionalPass: true));
        state = Accept(state, Stage.UnitTest, new TransitionEvidence(ReachabilityDispositionComplete: true));
        state = Accept(state, Stage.UnitTest, new TransitionEvidence(DirectEvidenceCurrent: true, ReadinessGatePass: true));

        Assert.Equal(DeliveryCursor.Ready, state.DeliveryCursor);
        Assert.True(state.HasCurrentReadinessPass());
    }

    [Theory]
    [InlineData(TransitionReasons.MissingProcessRoot)]
    [InlineData(TransitionReasons.AmbiguousProcessRoot)]
    public void LayoutFailures_rejectWithoutStateMutation(string reason)
    {
        var state = EngineeringLoopState.Initial;
        var result = EngineeringLoop.Evaluate(state, Stage.Northstar, new TransitionEvidence(
            RootLayoutValid: false,
            RootFailureReason: reason,
            NorthstarComplete: true));

        RejectsWithoutMutation(state, result, reason);
    }

    [Fact]
    public void UnknownCommand_rejectsWithoutStateMutation()
    {
        var state = EngineeringLoopState.Initial;
        var result = EngineeringLoop.EvaluateUnknown(state, new TransitionEvidence());
        RejectsWithoutMutation(state, result, TransitionReasons.InvalidCommand);
    }

    [Fact]
    public void DuplicateNorthstar_rejectsWithoutStateMutation()
    {
        var state = Accept(EngineeringLoopState.Initial, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));
        var result = EngineeringLoop.Evaluate(state, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));
        RejectsWithoutMutation(state, result, TransitionReasons.DuplicateStart);
    }

    [Fact]
    public void ScaffoldWithoutRunway_rejects()
    {
        var state = Accept(EngineeringLoopState.Initial, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));
        var result = EngineeringLoop.Evaluate(state, Stage.Scaffold, new TransitionEvidence());
        RejectsWithoutMutation(state, result, TransitionReasons.MissingProvenRunway);
    }

    [Fact]
    public void DeliveryFeedbackPaths_acceptModelAndExploreFromValidation()
    {
        var validated = StateAtValidated();
        var remodelling = Accept(validated, Stage.Model, new TransitionEvidence(ModelAdequate: true));
        var reexplored = Accept(remodelling, Stage.Explore, new TransitionEvidence(GenerationFresh: true));
        var revalidated = Accept(reexplored, Stage.Validate, new TransitionEvidence(FunctionalPass: true));
        Assert.Equal(Stage.Validate, revalidated.LastAcceptedStage);

        var generationGap = Accept(validated, Stage.Explore, new TransitionEvidence(GenerationFresh: true));
        Assert.Equal(Stage.Explore, generationGap.LastAcceptedStage);
    }

    [Fact]
    public void DeliveryEvidenceGuards_rejectEachMissingFact()
    {
        var initial = EngineeringLoopState.Initial;
        RejectsWithoutMutation(initial, EngineeringLoop.Evaluate(initial, Stage.Northstar, new TransitionEvidence()), TransitionReasons.MissingNorthstar);

        var northstar = Accept(initial, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));
        RejectsWithoutMutation(northstar, EngineeringLoop.Evaluate(northstar, Stage.Architect, new TransitionEvidence(ArchitectureCurrent: true)), TransitionReasons.InvalidOrder);

        var scaffold = Accept(northstar, Stage.Scaffold, new TransitionEvidence(RunwayProven: true));
        RejectsWithoutMutation(scaffold, EngineeringLoop.Evaluate(scaffold, Stage.Architect, new TransitionEvidence()), TransitionReasons.MissingArchitecture);

        var architecture = Accept(scaffold, Stage.Architect, new TransitionEvidence(ArchitectureCurrent: true));
        RejectsWithoutMutation(architecture, EngineeringLoop.Evaluate(architecture, Stage.Refactor, new TransitionEvidence()), TransitionReasons.MissingImplementation);

        var refactor = Accept(architecture, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));
        RejectsWithoutMutation(refactor, EngineeringLoop.Evaluate(refactor, Stage.Model, new TransitionEvidence()), TransitionReasons.MissingModelOrExploration);

        var model = Accept(refactor, Stage.Model, new TransitionEvidence(ModelAdequate: true));
        RejectsWithoutMutation(model, EngineeringLoop.Evaluate(model, Stage.Explore, new TransitionEvidence()), TransitionReasons.MissingModelOrExploration);

        var explore = Accept(model, Stage.Explore, new TransitionEvidence(GenerationFresh: true));
        RejectsWithoutMutation(explore, EngineeringLoop.Evaluate(explore, Stage.Validate, new TransitionEvidence()), TransitionReasons.MissingFunctionalValidation);
    }

    [Fact]
    public void RefactorBeforeArchitecture_rejects()
    {
        var state = Accept(EngineeringLoopState.Initial, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));
        var result = EngineeringLoop.Evaluate(state, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));
        RejectsWithoutMutation(state, result, TransitionReasons.MissingArchitecture);
    }

    [Fact]
    public void ValidateBeforeModelAndExploration_rejects()
    {
        var state = StateAtRefactor();
        var result = EngineeringLoop.Evaluate(state, Stage.Validate, new TransitionEvidence(FunctionalPass: true));
        RejectsWithoutMutation(state, result, TransitionReasons.MissingModelOrExploration);
    }

    [Fact]
    public void OperationsBeforeReadiness_rejects()
    {
        var state = StateAtValidated();
        var result = EngineeringLoop.Evaluate(state, Stage.Incident, new TransitionEvidence(IncidentDemand: true));
        RejectsWithoutMutation(state, result, TransitionReasons.MissingCurrentReadiness);
    }

    [Fact]
    public void StaleReadiness_rejects()
    {
        var state = ReadyState() with
        {
            ProductRevision = "2",
            Readiness = new ReadinessEvidence(true, "1", DateTimeOffset.UtcNow),
        };
        var result = EngineeringLoop.Evaluate(state, Stage.Incident, new TransitionEvidence(IncidentDemand: true));
        RejectsWithoutMutation(state, result, TransitionReasons.StaleReadiness);
    }

    [Fact]
    public void Incident_allowsRepeatedDemandButNotAfterDeliveryInvalidation()
    {
        var ready = ReadyState();
        var first = Accept(ready, Stage.Incident, new TransitionEvidence(IncidentDemand: true, IncidentStabilized: false));
        var repeated = Accept(first, Stage.Incident, new TransitionEvidence(IncidentDemand: true, IncidentStabilized: true));
        Assert.True(repeated.IncidentStabilized);

        var invalidated = Accept(ready, Stage.RefactorScan, new TransitionEvidence(StewardshipCapacity: true, DirectionChange: true));
        var northstar = Accept(invalidated, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));
        RejectsWithoutMutation(northstar, EngineeringLoop.Evaluate(northstar, Stage.Incident, new TransitionEvidence(IncidentDemand: true)), TransitionReasons.MissingCurrentReadiness);
    }

    [Fact]
    public void PostmortemCannotBypassRepairRouting()
    {
        var state = ReadyState() with
        {
            LastAcceptedStage = Stage.Postmortem,
            SelectedIncidentSet = true,
            RepairItemDemand = true,
        };
        var result = EngineeringLoop.Evaluate(state, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));
        RejectsWithoutMutation(state, result, TransitionReasons.MissingRepairRouting);
    }

    [Fact]
    public void RepairCannotBypassDeliveryAndReadiness()
    {
        var state = ReadyState() with
        {
            LastAcceptedStage = Stage.Repair,
            RepairObligations = [new RepairObligation("RPI-001", false, false, false, false)],
        };
        var result = EngineeringLoop.Evaluate(state, Stage.UnitTest, new TransitionEvidence(DirectEvidenceCurrent: true, ReadinessGatePass: true));
        RejectsWithoutMutation(state, result, TransitionReasons.RepairGateBypass);
    }

    [Fact]
    public void RefactorScanCannotBypassDeliveryAndReadiness()
    {
        var state = ReadyState() with { LastAcceptedStage = Stage.RefactorScan };
        var result = EngineeringLoop.Evaluate(state, Stage.UnitTest, new TransitionEvidence(DirectEvidenceCurrent: true, ReadinessGatePass: true));
        RejectsWithoutMutation(state, result, TransitionReasons.RefactorGateBypass);
    }

    [Fact]
    public void OperationsDemandGuards_rejectAbsentDemand()
    {
        var ready = ReadyState();
        RejectsWithoutMutation(ready,
            EngineeringLoop.Evaluate(ready, Stage.Incident, new TransitionEvidence(IncidentDemand: false)),
            TransitionReasons.NoIncidentDemand);

        var postmortem = ready with { LastAcceptedStage = Stage.Incident, IncidentDemandActive = true, IncidentStabilized = true };
        RejectsWithoutMutation(postmortem,
            EngineeringLoop.Evaluate(postmortem, Stage.Postmortem, new TransitionEvidence(SelectedIncidentSet: false)),
            TransitionReasons.NoPostmortemSelection);

        var repair = ready with { LastAcceptedStage = Stage.Postmortem, SelectedIncidentSet = true };
        RejectsWithoutMutation(repair,
            EngineeringLoop.Evaluate(repair, Stage.Repair, new TransitionEvidence(RepairItemDemand: false)),
            TransitionReasons.NoRepairDemand);
    }

    [Fact]
    public void StewardshipDemandGuards_rejectAbsentCapacityOrLearningDemand()
    {
        var ready = ReadyState();
        RejectsWithoutMutation(ready,
            EngineeringLoop.Evaluate(ready, Stage.RefactorScan, new TransitionEvidence(StewardshipCapacity: false)),
            TransitionReasons.NoStewardshipCapacity);

        RejectsWithoutMutation(ready,
            EngineeringLoop.Evaluate(ready, Stage.LearningsPyramid, new TransitionEvidence(StewardshipCapacity: false)),
            TransitionReasons.NoStewardshipCapacity);

        RejectsWithoutMutation(ready,
            EngineeringLoop.Evaluate(ready, Stage.LearningsPyramid, new TransitionEvidence(StewardshipCapacity: true, LearningRefreshCurrent: true)),
            TransitionReasons.NoLearningRefreshDemand);
    }

    [Fact]
    public void StageEight_requiresDispositionBeforeDirectTests()
    {
        var state = StateAtValidated();
        RejectsWithoutMutation(state,
            EngineeringLoop.Evaluate(state, Stage.UnitTest, new TransitionEvidence(ReachabilityDispositionComplete: false)),
            TransitionReasons.UnclassifiedReachability);

        RejectsWithoutMutation(state,
            EngineeringLoop.Evaluate(state, Stage.UnitTest, new TransitionEvidence(ReachabilityDispositionComplete: true, ReachabilityAmbiguous: true)),
            TransitionReasons.AmbiguousReachability);

        var refactor = StateAtRefactor();
        RejectsWithoutMutation(refactor,
            EngineeringLoop.Evaluate(refactor, Stage.UnitTest, new TransitionEvidence()),
            TransitionReasons.UnitTestsTooEarly);

        var disposition = Accept(state, Stage.UnitTest, new TransitionEvidence(ReachabilityDispositionComplete: true));
        RejectsWithoutMutation(disposition,
            EngineeringLoop.Evaluate(disposition, Stage.UnitTest, new TransitionEvidence(DirectEvidenceCurrent: false, ReadinessGatePass: true)),
            TransitionReasons.UnclassifiedReachability);

        var staleValidation = state with { ValidationRevision = "older" };
        RejectsWithoutMutation(staleValidation,
            EngineeringLoop.Evaluate(staleValidation, Stage.UnitTest, new TransitionEvidence(ReachabilityDispositionComplete: true)),
            TransitionReasons.MissingFunctionalValidation);
    }

    [Fact]
    public void StageThirtyDirectionBranch_routesThroughNorthstarThenArchitect()
    {
        var ready = ReadyState();
        var scanned = Accept(ready, Stage.RefactorScan, new TransitionEvidence(
            StewardshipCapacity: true,
            DirectionChange: true,
            ArchitectureImpact: true));

        RejectsWithoutMutation(scanned,
            EngineeringLoop.Evaluate(scanned, Stage.Architect, new TransitionEvidence(ArchitectureCurrent: true)),
            TransitionReasons.InvalidOrder);

        var northstar = Accept(scanned, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));
        var architecture = Accept(northstar, Stage.Architect, new TransitionEvidence(ArchitectureCurrent: true));
        var refactored = Accept(architecture, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));
        Assert.Equal(Stage.Refactor, refactored.LastAcceptedStage);
    }

    [Fact]
    public void StageThirtyNoWork_returnsToSteadyContext()
    {
        var ready = ReadyState();
        var scanned = Accept(ready, Stage.RefactorScan, new TransitionEvidence(StewardshipCapacity: true));
        var incident = Accept(scanned, Stage.Incident, new TransitionEvidence(IncidentDemand: true, IncidentStabilized: true));
        Assert.Equal(Stage.Incident, incident.LastAcceptedStage);
    }

    [Fact]
    public void ArchitectureImpactWithoutDirection_routesFromScanDirectlyToArchitect()
    {
        var ready = ReadyState();
        var scan = Accept(ready, Stage.RefactorScan, new TransitionEvidence(
            StewardshipCapacity: true,
            DirectionChange: false,
            ArchitectureImpact: true));
        var architecture = Accept(scan, Stage.Architect, new TransitionEvidence(ArchitectureCurrent: true));
        var implementation = Accept(architecture, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));
        Assert.Equal(Stage.Refactor, implementation.LastAcceptedStage);
    }

    [Fact]
    public void RepairReturnAndFeedbackValidationRoutes_areAccepted()
    {
        var repaired = ReadyState() with
        {
            LastAcceptedStage = Stage.Repair,
            RepairObligations = [new RepairObligation("RPI-001", false, false, false, false)],
            DeliveryCursor = DeliveryCursor.Architecture,
        };
        var implementation = Accept(repaired, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));
        var model = Accept(implementation, Stage.Model, new TransitionEvidence(ModelAdequate: true));
        var exploration = Accept(model, Stage.Explore, new TransitionEvidence(GenerationFresh: true));
        var validation = Accept(exploration, Stage.Validate, new TransitionEvidence(FunctionalPass: true));
        var disposition = Accept(validation, Stage.UnitTest, new TransitionEvidence(ReachabilityDispositionComplete: true));
        var revalidation = Accept(disposition, Stage.Validate, new TransitionEvidence(FunctionalPass: true));
        Assert.Equal(Stage.Validate, revalidation.LastAcceptedStage);
    }

    [Fact]
    public void UnitTestCanRecordFailReadinessWithoutAuthorizingOperations()
    {
        var disposition = Accept(StateAtValidated(), Stage.UnitTest, new TransitionEvidence(ReachabilityDispositionComplete: true));
        var failed = Accept(disposition, Stage.UnitTest, new TransitionEvidence(DirectEvidenceCurrent: true, ReadinessGatePass: false));
        Assert.Equal(DeliveryCursor.Disposition, failed.DeliveryCursor);
        Assert.False(failed.HasCurrentReadinessPass());
        RejectsWithoutMutation(failed,
            EngineeringLoop.Evaluate(failed, Stage.Incident, new TransitionEvidence(IncidentDemand: true)),
            TransitionReasons.StaleReadiness);
    }

    [Fact]
    public void PostmortemAndRepair_rejectEachIndependentMissingFact()
    {
        var incident = ReadyState() with { LastAcceptedStage = Stage.Incident };
        RejectsWithoutMutation(incident,
            EngineeringLoop.Evaluate(incident, Stage.Postmortem, new TransitionEvidence(SelectedIncidentSet: true)),
            TransitionReasons.NoPostmortemSelection);

        incident = incident with { IncidentDemandActive = true, IncidentStabilized = false };
        RejectsWithoutMutation(incident,
            EngineeringLoop.Evaluate(incident, Stage.Postmortem, new TransitionEvidence(SelectedIncidentSet: true)),
            TransitionReasons.NoPostmortemSelection);

        incident = incident with { IncidentStabilized = true };
        var postmortem = Accept(incident, Stage.Postmortem, new TransitionEvidence(SelectedIncidentSet: true, RepairItemDemand: false));
        RejectsWithoutMutation(postmortem,
            EngineeringLoop.Evaluate(postmortem, Stage.Repair, new TransitionEvidence(RepairItemDemand: false)),
            TransitionReasons.NoRepairDemand);
    }

    [Fact]
    public void StageThirtyOne_requiresCurrentLearningEvidenceAndPreservesSteadyContext()
    {
        var ready = ReadyState() with { LearningRefreshPending = true };
        RejectsWithoutMutation(ready,
            EngineeringLoop.Evaluate(ready, Stage.LearningsPyramid, new TransitionEvidence(StewardshipCapacity: true, LearningRefreshCurrent: false)),
            TransitionReasons.NoLearningRefreshDemand);

        var refreshed = Accept(ready, Stage.LearningsPyramid, new TransitionEvidence(StewardshipCapacity: true, LearningRefreshCurrent: true));
        var incident = Accept(refreshed, Stage.Incident, new TransitionEvidence(IncidentDemand: true, IncidentStabilized: true));
        Assert.Equal(Stage.Incident, incident.LastAcceptedStage);
    }

    [Fact]
    public void StageThirtyOne_returnsToItsInvokingContext()
    {
        var postmortem = ReadyState() with
        {
            LastAcceptedStage = Stage.Postmortem,
            SelectedIncidentSet = true,
            RepairItemDemand = true,
            LearningRefreshPending = true,
        };
        var refreshed = Accept(postmortem, Stage.LearningsPyramid, new TransitionEvidence(StewardshipCapacity: true, LearningRefreshCurrent: true));
        Assert.Equal(Stage.Postmortem, refreshed.ReturnStage);
        var repair = Accept(refreshed, Stage.Repair, new TransitionEvidence(RepairItemDemand: true));
        Assert.Equal(Stage.Repair, repair.LastAcceptedStage);
    }

    [Fact]
    public void RevisionFallbackAndUnknownRootFailure_areFailClosed()
    {
        var architecture = Accept(
            Accept(
                Accept(EngineeringLoopState.Initial, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true)),
                Stage.Scaffold, new TransitionEvidence(RunwayProven: true)),
            Stage.Architect, new TransitionEvidence(ArchitectureCurrent: true));
        var nonNumeric = architecture with { ProductRevision = "non-numeric-revision" };
        var refactored = Accept(nonNumeric, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));
        Assert.NotEqual("non-numeric-revision", refactored.ProductRevision);

        var root = EngineeringLoop.Evaluate(EngineeringLoopState.Initial, Stage.Northstar, new TransitionEvidence(
            RootLayoutValid: false,
            RootFailureReason: "unrecognized-root-error"));
        RejectsWithoutMutation(EngineeringLoopState.Initial, root, TransitionReasons.MissingProcessRoot);
    }

    [Fact]
    public void DeliveryAndOperationsAlternateGuards_coverCombinedBranchOutcomes()
    {
        var directionPending = EngineeringLoopState.Initial with
        {
            LastAcceptedStage = Stage.Northstar,
            DeliveryCursor = DeliveryCursor.Northstar,
            DirectionChangePending = true,
        };
        var acceptedDirection = Accept(directionPending, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));
        Assert.True(acceptedDirection.DirectionRefactorRequired);

        var scan = ReadyState() with
        {
            LastAcceptedStage = Stage.RefactorScan,
            ArchitectureImpactPending = true,
            DirectionChangePending = false,
        };
        RejectsWithoutMutation(scan,
            EngineeringLoop.Evaluate(scan, Stage.Architect, new TransitionEvidence(ArchitectureCurrent: false)),
            TransitionReasons.MissingArchitecture);

        var openRepair = ReadyState() with
        {
            LastAcceptedStage = Stage.Repair,
            RepairObligations = [new RepairObligation("RPI-001", false, false, false, false)],
        };
        var refactored = Accept(openRepair, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));
        Assert.Equal(Stage.Refactor, refactored.LastAcceptedStage);

        var staleModel = StateAtRefactor() with { ModelRevision = "old" };
        RejectsWithoutMutation(staleModel,
            EngineeringLoop.Evaluate(staleModel, Stage.Explore, new TransitionEvidence(GenerationFresh: true)),
            TransitionReasons.MissingModelOrExploration);

        var ready = ReadyState();
        var postmortemMissingDemand = ready with { LastAcceptedStage = Stage.Incident, IncidentDemandActive = false, IncidentStabilized = true };
        RejectsWithoutMutation(postmortemMissingDemand,
            EngineeringLoop.Evaluate(postmortemMissingDemand, Stage.Postmortem, new TransitionEvidence(SelectedIncidentSet: true)),
            TransitionReasons.NoPostmortemSelection);

        var scanWithoutCurrentReadiness = ready with { LastAcceptedStage = Stage.UnitTest, Readiness = new ReadinessEvidence(false, ready.ProductRevision, DateTimeOffset.UtcNow) };
        RejectsWithoutMutation(scanWithoutCurrentReadiness,
            EngineeringLoop.Evaluate(scanWithoutCurrentReadiness, Stage.RefactorScan, new TransitionEvidence(StewardshipCapacity: true)),
            TransitionReasons.RefactorGateBypass);
    }

    [Fact]
    public void TransitionBranchMatrix_coversAlternateDeliveryAndStewardshipOutcomes()
    {
        var started = Accept(EngineeringLoopState.Initial, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));
        RejectsWithoutMutation(started,
            EngineeringLoop.Evaluate(started, Stage.Scaffold, new TransitionEvidence(RunwayProven: false)),
            TransitionReasons.MissingProvenRunway);

        var scaffold = Accept(started, Stage.Scaffold, new TransitionEvidence(RunwayProven: true));
        RejectsWithoutMutation(scaffold,
            EngineeringLoop.Evaluate(scaffold, Stage.Architect, new TransitionEvidence(ArchitectureCurrent: false)),
            TransitionReasons.MissingArchitecture);

        var architecture = Accept(scaffold, Stage.Architect, new TransitionEvidence(ArchitectureCurrent: true));
        RejectsWithoutMutation(architecture,
            EngineeringLoop.Evaluate(architecture, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: false)),
            TransitionReasons.MissingImplementation);

        var refactor = Accept(architecture, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));
        RejectsWithoutMutation(refactor,
            EngineeringLoop.Evaluate(refactor, Stage.Model, new TransitionEvidence(ModelAdequate: false)),
            TransitionReasons.MissingModelOrExploration);

        var model = Accept(refactor, Stage.Model, new TransitionEvidence(ModelAdequate: true));
        RejectsWithoutMutation(model,
            EngineeringLoop.Evaluate(model, Stage.Explore, new TransitionEvidence(GenerationFresh: false)),
            TransitionReasons.MissingModelOrExploration);

        var ready = ReadyState();
        var scan = Accept(ready, Stage.RefactorScan, new TransitionEvidence(StewardshipCapacity: true));
        Assert.Equal(Stage.RefactorScan, scan.LastAcceptedStage);

        var rootFailure = EngineeringLoop.EvaluateUnknown(ready, new TransitionEvidence(RootLayoutValid: false, RootFailureReason: TransitionReasons.AmbiguousProcessRoot));
        RejectsWithoutMutation(ready, rootFailure, TransitionReasons.AmbiguousProcessRoot);
    }

    private static EngineeringLoopState StateAtRefactor()
    {
        var state = EngineeringLoopState.Initial;
        state = Accept(state, Stage.Northstar, new TransitionEvidence(NorthstarComplete: true));
        state = Accept(state, Stage.Scaffold, new TransitionEvidence(RunwayProven: true));
        state = Accept(state, Stage.Architect, new TransitionEvidence(ArchitectureCurrent: true));
        return Accept(state, Stage.Refactor, new TransitionEvidence(ImplementationCurrent: true));
    }

    private static EngineeringLoopState StateAtValidated()
    {
        var state = StateAtRefactor();
        state = Accept(state, Stage.Model, new TransitionEvidence(ModelAdequate: true));
        state = Accept(state, Stage.Explore, new TransitionEvidence(GenerationFresh: true));
        return Accept(state, Stage.Validate, new TransitionEvidence(FunctionalPass: true));
    }

    private static EngineeringLoopState ReadyState()
    {
        var state = StateAtValidated();
        state = Accept(state, Stage.UnitTest, new TransitionEvidence(ReachabilityDispositionComplete: true));
        return Accept(state, Stage.UnitTest, new TransitionEvidence(DirectEvidenceCurrent: true, ReadinessGatePass: true));
    }

    private static EngineeringLoopState Accept(EngineeringLoopState state, Stage stage, TransitionEvidence evidence)
    {
        var result = EngineeringLoop.Evaluate(state, stage, evidence);
        Assert.True(result.Accepted, $"Expected {stage} to be accepted but got {result.Reason}");
        return result.State;
    }

    private static void RejectsWithoutMutation(EngineeringLoopState before, TransitionResult result, string reason)
    {
        Assert.False(result.Accepted);
        Assert.Equal(reason, result.Reason);
        Assert.Same(before, result.State);
    }
}
