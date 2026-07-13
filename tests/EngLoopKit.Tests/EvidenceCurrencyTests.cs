using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

public sealed class EvidenceCurrencyTests
{
    [Fact]
    public void CurrentReadiness_requiresMatchingProductRevision()
    {
        var state = new EngineeringLoopState(
            LastAcceptedStage: Stage.UnitTest,
            DeliveryCursor.Ready,
            ProductRevision: "2",
            ModelRevision: "2",
            ExplorationRevision: "2",
            ValidationRevision: "2",
            Readiness: new ReadinessEvidence(true, "1", DateTimeOffset.UtcNow),
            RepairObligations: [],
            ReachabilityDispositionComplete: true,
            LearningRefreshPending: false,
            IncidentDemandActive: false,
            SelectedIncidentSet: false,
            RepairItemDemand: false,
            StewardshipCapacity: false);

        Assert.False(state.HasCurrentReadinessPass());
    }

    [Fact]
    public void CurrentReadiness_passesOnlyForMatchingPassVerdict()
    {
        var state = new EngineeringLoopState(
            LastAcceptedStage: Stage.UnitTest,
            DeliveryCursor.Ready,
            ProductRevision: "2",
            ModelRevision: "2",
            ExplorationRevision: "2",
            ValidationRevision: "2",
            Readiness: new ReadinessEvidence(true, "2", DateTimeOffset.UtcNow),
            RepairObligations: [],
            ReachabilityDispositionComplete: true,
            LearningRefreshPending: false,
            IncidentDemandActive: false,
            SelectedIncidentSet: false,
            RepairItemDemand: false,
            StewardshipCapacity: false);

        Assert.True(state.HasCurrentReadinessPass());
    }
}
