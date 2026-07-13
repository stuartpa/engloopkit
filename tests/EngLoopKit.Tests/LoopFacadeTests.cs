using EngLoopKit.Core;
using Xunit;

namespace EngLoopKit.Tests;

/// <summary>
/// Direct coverage of the real stateful façade used by generated tests. These tests do not
/// replace generated negative evidence; they exercise the façade's public boundary for
/// whole-product coverage after Stage 08 disposition.
/// </summary>
public sealed class LoopFacadeTests
{
    [Fact]
    public void DeliveryAndOperationsFacade_walksACompleteRepairCycle()
    {
        var sut = new Loop();
        sut.Northstar();
        sut.Scaffold();
        sut.Architect();
        sut.Refactor();
        sut.Model();
        sut.Explore();
        sut.Validate();
        sut.UnitTest();
        sut.UnitTest();
        Assert.True(sut.State.HasCurrentReadinessPass());

        sut.Incident(true);
        sut.Incident(true);
        sut.Postmortem(true);
        sut.Repair(true);
        sut.Refactor();
        sut.Model();
        sut.Explore();
        sut.Validate();
        sut.UnitTest();
        sut.UnitTest();
        Assert.True(sut.State.HasCurrentReadinessPass());
    }

    [Fact]
    public void Facade_rejectsIllegalOrderAndAbsentDemand()
    {
        var sut = new Loop();
        Assert.Throws<InvalidOperationException>(() => sut.Explore());
        sut.Northstar();
        sut.Scaffold();
        sut.Architect();
        sut.Refactor();
        sut.Model();
        sut.Explore();
        sut.Validate();
        sut.UnitTest();
        sut.UnitTest();
        Assert.Throws<InvalidOperationException>(() => sut.Incident(false));
    }

    [Fact]
    public void Facade_exercisesAllRefactorScanBranchesAndLearningRefresh()
    {
        var sut = Ready();
        sut.RefactorScan(true, false, false);
        sut.Incident(true);
        sut.Postmortem(true);
        sut.LearningsPyramid(true);
        sut.Repair(true);
        sut.Refactor();
        sut.Model();
        sut.Explore();
        sut.Validate();
        sut.UnitTest();
        sut.UnitTest();

        sut.RefactorScan(true, true, false);
        sut.Northstar();
        sut.Refactor();
        sut.Model();
        sut.Explore();
        sut.Validate();
        sut.UnitTest();
        sut.UnitTest();

        sut.RefactorScan(true, false, true);
        sut.Architect();
        sut.Refactor();
        Assert.Equal(Stage.Refactor, sut.State.LastAcceptedStage);
    }

    [Fact]
    public void Facade_requiresCapacityForStewardship()
    {
        var sut = Ready();
        Assert.Throws<InvalidOperationException>(() => sut.RefactorScan(false, false, false));
        Assert.Throws<InvalidOperationException>(() => sut.LearningsPyramid(false));
    }

    private static Loop Ready()
    {
        var sut = new Loop();
        sut.Northstar();
        sut.Scaffold();
        sut.Architect();
        sut.Refactor();
        sut.Model();
        sut.Explore();
        sut.Validate();
        sut.UnitTest();
        sut.UnitTest();
        return sut;
    }
}
