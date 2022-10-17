using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;

/// <summary>
/// Context of contextual-based log analysis.
/// </summary>
class ContextualBasedAnalysisContext : DisplayableLogAnalysisContext
{
    // Fields.
    readonly IDictionary<string, Queue<string>> queues = new Dictionary<string, Queue<string>>();
    readonly IDictionary<string, ISet<string>> sets = new Dictionary<string, ISet<string>>();
    readonly IDictionary<string, Stack<string>> stacks = new Dictionary<string, Stack<string>>();


    /// <summary>
    /// Get named queue.
    /// </summary>
    /// <param name="name">Name.</param>
    /// <returns>Queue.</returns>
    public Queue<string> GetQueue(string name) => this.queues.Lock(it =>
    {
        if (it.TryGetValue(name, out var q))
            return q;
        q = new();
        it[name] = q;
        return q;
    });


    /// <summary>
    /// Get named set.
    /// </summary>
    /// <param name="name">Name.</param>
    /// <returns>Set.</returns>
    public ISet<string> GetSet(string name) => this.sets.Lock(it =>
    {
        if (it.TryGetValue(name, out var s))
            return s;
        s = new HashSet<string>();
        it[name] = s;
        return s;
    });


    /// <summary>
    /// Get named stack.
    /// </summary>
    /// <param name="name">Name.</param>
    /// <returns>Stack.</returns>
    public Stack<string> GetStack(string name) => this.stacks.Lock(it =>
    {
        if (it.TryGetValue(name, out var s))
            return s;
        s = new();
        it[name] = s;
        return s;
    });
}