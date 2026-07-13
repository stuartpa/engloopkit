using Sek.Modeling;

namespace EngLoopKit.Model;

/// <summary>
/// Independent behavioral model of ordered EngLoop v2. It deliberately does not import
/// or call the SUT's transition table. The model holds interacting delivery, readiness,
/// incident, repair, learning and stewardship facts, so generated negative tests are
/// derived from real guards rather than hand-authored assertions.
/// </summary>
public sealed class LoopModel : ModelProgram
{
    public enum Cursor
    {
        Initial,
        Northstar,
        Scaffold,
        Architecture,
        Refactor,
        Model,
        Explore,
        Validate,
        Disposition,
        Ready,
        ScanPending,
        Incident,
        Postmortem,
        Repair,
    }

    public Cursor Current { get; set; } = Cursor.Initial;
    public bool IncidentActive { get; set; }
    public bool IncidentStabilized { get; set; }
    public bool LearningPending { get; set; }
    public bool RepairPending { get; set; }
    public bool DirectionChangePending { get; set; }
    public bool DirectionRefactorRequired { get; set; }
    public bool ArchitectureImpactPending { get; set; }

    [Rule("Loop.Northstar")]
    public void Northstar()
    {
        Require(Current == Cursor.Initial || (DirectionChangePending && Current == Cursor.ScanPending), "Northstar starts a repository or follows a direction-changing scan");
        var directionBranch = DirectionChangePending;
        Current = Cursor.Northstar;
        DirectionChangePending = false;
        DirectionRefactorRequired = directionBranch;
    }

    [Rule("Loop.Scaffold")]
    public void Scaffold()
    {
        Require(Current == Cursor.Northstar, "Scaffold follows Northstar for a new product");
        Current = Cursor.Scaffold;
    }

    [Rule("Loop.Architect")]
    public void Architect()
    {
        Require(Current == Cursor.Scaffold || (ArchitectureImpactPending && (Current == Cursor.Northstar || (Current == Cursor.ScanPending && !DirectionChangePending))), "Architect follows scaffold or an architecture-impacting branch");
        Current = Cursor.Architecture;
    }

    [Rule("Loop.Refactor")]
    public void Refactor()
    {
        Require(Current == Cursor.Architecture || Current == Cursor.Repair || (Current == Cursor.Northstar && (ArchitectureImpactPending || DirectionRefactorRequired)), "Refactor follows a governed delivery or repair branch");
        Current = Cursor.Refactor;
        ArchitectureImpactPending = false;
        DirectionRefactorRequired = false;
    }

    [Rule("Loop.Model")]
    public void Model()
    {
        Require(Current == Cursor.Refactor || Current == Cursor.Validate, "Model follows implementation or a model-gap branch");
        Current = Cursor.Model;
    }

    [Rule("Loop.Explore")]
    public void Explore()
    {
        Require(Current == Cursor.Model || Current == Cursor.Validate, "Explore follows Model or a generation-gap branch");
        Current = Cursor.Explore;
    }

    [Rule("Loop.Validate")]
    public void Validate()
    {
        Require(Current == Cursor.Explore || Current == Cursor.Disposition, "Validate follows exploration or residue deletion");
        Current = Cursor.Validate;
    }

    [Rule("Loop.UnitTest")]
    public void UnitTest()
    {
        Require(Current == Cursor.Validate || Current == Cursor.Disposition, "UnitTest requires current functional validation");
        Current = Current == Cursor.Validate ? Cursor.Disposition : Cursor.Ready;
    }

    [Rule("Loop.Incident")]
    public void Incident(bool actualDemand)
    {
        Require(Current == Cursor.Ready || Current == Cursor.Incident, "Incident follows readiness or another Incident");
        Require(actualDemand, "Incident requires an actual operating disruption");
        Current = Cursor.Incident;
        IncidentActive = true;
        IncidentStabilized = true;
    }

    [Rule("Loop.Postmortem")]
    public void Postmortem(bool selectedStabilizedSet)
    {
        Require(Current == Cursor.Incident && IncidentActive && IncidentStabilized, "Postmortem follows one or more stabilized Incidents");
        Require(selectedStabilizedSet, "Postmortem requires a selected stabilized incident set");
        Current = Cursor.Postmortem;
        LearningPending = true;
        RepairPending = true;
    }

    [Rule("Loop.Repair")]
    public void Repair(bool repairItemDemand)
    {
        Require(Current == Cursor.Postmortem && RepairPending, "Repair follows Postmortem with a repair item");
        Require(repairItemDemand, "Repair requires a concrete repair item");
        Current = Cursor.Repair;
    }

    [Rule("Loop.RefactorScan")]
    public void RefactorScan(bool capacity, bool directionChange, bool architectureImpact)
    {
        Require(Current == Cursor.Ready, "RefactorScan starts from a ready product");
        Require(capacity, "RefactorScan requires explicit stewardship capacity");
        DirectionChangePending = directionChange;
        ArchitectureImpactPending = architectureImpact;
        if (directionChange || architectureImpact)
        {
            Current = Cursor.ScanPending;
        }
    }

    [Rule("Loop.LearningsPyramid")]
    public void LearningsPyramid(bool capacity)
    {
        Require(capacity, "LearningsPyramid requires explicit stewardship capacity");
        Require(LearningPending, "LearningsPyramid requires accepted learning refresh demand");
        LearningPending = false;
    }

    [AcceptingCondition]
    public bool Accepting() => Current == Cursor.Ready || Current == Cursor.Disposition || Current == Cursor.Incident || Current == Cursor.Postmortem;
}
