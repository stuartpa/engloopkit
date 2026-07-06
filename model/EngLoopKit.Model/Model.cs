using Sek.Modeling;

namespace EngLoopKit.Model
{
    /// <summary>
    /// A SEK model of the EngLoopKit engineering loop. State is the current stage; each
    /// rule is a stage transition guarded by <see cref="ModelProgram.Require"/> so the
    /// exploration enumerates exactly the legal stage sequences. This is an independent
    /// specification of the loop; the system-under-test is <c>EngLoopKit.Core.Loop</c>.
    ///
    /// Rule names are <c>Loop.&lt;Stage&gt;</c> so generated tests reflect into
    /// <c>EngLoopKit.Core.Loop.&lt;Stage&gt;()</c> (the binding).
    /// </summary>
    public sealed class LoopModel : ModelProgram
    {
        /// <summary>The loop's stages, plus a synthetic start marker.</summary>
        public enum S
        {
            None,
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

        /// <summary>The only state field: which stage the loop is in.</summary>
        public S Current { get; set; } = S.None;

        [Rule("Loop.Seed")]
        public void Seed()
        {
            // A loop begins at Seed, or a new Delivery loop is seeded by the monthly scan.
            Require(Current == S.None || Current == S.RefactorScan, "Seed starts a loop or follows RefactorScan");
            Current = S.Seed;
        }

        [Rule("Loop.Bridge")]
        public void Bridge()
        {
            Require(Current == S.Seed, "Bridge follows Seed");
            Current = S.Bridge;
        }

        [Rule("Loop.Architect")]
        public void Architect()
        {
            Require(Current == S.Bridge, "Architect follows Bridge");
            Current = S.Architect;
        }

        [Rule("Loop.RefactorToFinal")]
        public void RefactorToFinal()
        {
            // Reached after architecture, and re-entered by a repair.
            Require(Current == S.Architect || Current == S.Repair, "RefactorToFinal follows Architect or Repair");
            Current = S.RefactorToFinal;
        }

        [Rule("Loop.Model")]
        public void Model()
        {
            Require(Current == S.RefactorToFinal, "Model follows RefactorToFinal");
            Current = S.Model;
        }

        [Rule("Loop.Explore")]
        public void Explore()
        {
            // Entered from Model, and re-entered from Coverage (the Verification loop).
            Require(Current == S.Model || Current == S.Coverage, "Explore follows Model or Coverage");
            Current = S.Explore;
        }

        [Rule("Loop.Coverage")]
        public void Coverage()
        {
            Require(Current == S.Explore, "Coverage follows Explore");
            Current = S.Coverage;
        }

        [Rule("Loop.Incident")]
        public void Incident()
        {
            // Operations loop opens from a covered product; multiple incidents may stack.
            Require(Current == S.Coverage || Current == S.Incident, "Incident follows Coverage or another Incident");
            Current = S.Incident;
        }

        [Rule("Loop.Postmortem")]
        public void Postmortem()
        {
            Require(Current == S.Incident, "Postmortem follows one or more Incidents");
            Current = S.Postmortem;
        }

        [Rule("Loop.Repair")]
        public void Repair()
        {
            Require(Current == S.Postmortem, "Repair follows Postmortem");
            Current = S.Repair;
        }

        [Rule("Loop.RefactorScan")]
        public void RefactorScan()
        {
            // The monthly Evolution loop runs from a stable, covered product.
            Require(Current == S.Coverage, "RefactorScan follows Coverage");
            Current = S.RefactorScan;
        }

        /// <summary>
        /// Stable resting points of the loop: the product is covered (end of the
        /// Verification loop) or a fresh refactor has been scanned (end of Evolution).
        /// </summary>
        [AcceptingCondition]
        public bool Accepting() => Current == S.Coverage || Current == S.RefactorScan;
    }
}
