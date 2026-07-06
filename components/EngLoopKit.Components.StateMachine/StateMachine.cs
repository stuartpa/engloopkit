namespace EngLoopKit.Components.StateMachine;

/// <summary>
/// A directed transition graph over a caller-supplied state type: which states may legally
/// follow each state. A <b>component</b> — generic machinery with no domain knowledge.
/// </summary>
public sealed class TransitionGraph<TState>
    where TState : notnull
{
    private readonly IReadOnlyDictionary<TState, TState[]> _edges;

    public TransitionGraph(IReadOnlyDictionary<TState, TState[]> edges) => _edges = edges;

    /// <summary>True if <paramref name="to"/> may legally follow <paramref name="from"/>.</summary>
    public bool IsLegal(TState from, TState to) =>
        _edges.TryGetValue(from, out var outs) && Array.IndexOf(outs, to) >= 0;

    /// <summary>The states that may legally follow <paramref name="from"/>.</summary>
    public IReadOnlyList<TState> Next(TState from) =>
        _edges.TryGetValue(from, out var outs) ? outs : Array.Empty<TState>();
}

/// <summary>
/// A guarded machine over a <see cref="TransitionGraph{TState}"/>: it begins at a fixed
/// initial state and only advances along legal transitions. Generic; the vertical supplies
/// the graph and the initial state.
/// </summary>
public sealed class GuardedMachine<TState>
    where TState : notnull
{
    private readonly TransitionGraph<TState> _graph;
    private readonly TState _initial;

    public GuardedMachine(TransitionGraph<TState> graph, TState initial)
    {
        _graph = graph;
        _initial = initial;
        Current = initial;
    }

    /// <summary>The current state (equals the initial state until <see cref="Begin"/>).</summary>
    public TState Current { get; private set; }

    /// <summary>Whether <see cref="Begin"/> has been called.</summary>
    public bool Started { get; private set; }

    /// <summary>Start the machine at its initial state.</summary>
    public void Begin()
    {
        if (Started)
        {
            throw new InvalidOperationException("machine already started");
        }

        Current = _initial;
        Started = true;
    }

    /// <summary>Advance to <paramref name="to"/>; throws if not started or the transition is illegal.</summary>
    public void Advance(TState to)
    {
        if (!Started)
        {
            throw new InvalidOperationException("call Begin() before advancing");
        }

        if (!_graph.IsLegal(Current, to))
        {
            throw new InvalidOperationException($"illegal transition {Current} -> {to}");
        }

        Current = to;
    }
}
