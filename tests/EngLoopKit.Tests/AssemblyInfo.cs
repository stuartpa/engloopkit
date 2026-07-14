using Xunit;

// Overlay transaction tests create short-lived Git repositories and install local tools.
// Run test collections sequentially so those isolated process/cache transactions cannot
// race each other or leave the test host holding a copied assembly during coverage.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
