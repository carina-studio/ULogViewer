using System;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;

/// <summary>
/// Action for contextual-based log analysis.
/// </summary>
#pragma warning disable CS0659
#pragma warning disable CS0661
abstract class ContextualBasedAnalysisAction : IEquatable<ContextualBasedAnalysisAction>
#pragma warning restore CS0659
#pragma warning restore CS0661
{
    /// <inheritdoc/>
    public abstract bool Equals(ContextualBasedAnalysisAction? action);


    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is ContextualBasedAnalysisAction action
        && this.Equals(action);
    

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(ContextualBasedAnalysisAction? lhs, ContextualBasedAnalysisAction? rhs) =>
        lhs?.Equals(rhs) ?? object.ReferenceEquals(rhs, null);
    

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(ContextualBasedAnalysisAction? lhs, ContextualBasedAnalysisAction? rhs) =>
        object.ReferenceEquals(lhs, null) ? !object.ReferenceEquals(rhs, null) : !lhs.Equals(rhs);
    

    /// <summary>
    /// Perform action.
    /// </summary>
    /// <param name="context">Context.</param>
    /// <param name="log">Log.</param>
    /// <param name="parameters">Extra parameters.</param>
    /// <returns>True if action performed successfully.</returns>
    public abstract bool Perform(ContextualBasedAnalysisContext context, DisplayableLog log, params object?[] parameters);
    

    /// <summary>
    /// Save condition in JSON format.
    /// </summary>
    /// <param name="writer">JSON data writer.</param>
    public abstract void Save(Utf8JsonWriter writer);


    /// <summary>
    /// Try loading JSON format data as <see cref="ContextualBasedAnalysisAction"/>.
    /// </summary>
    /// <param name="jsonElement">JSON element.</param>
    /// <param name="action">Loaded action.</param>
    /// <returns>True if action loaded successfully.</returns>
    public static bool TryLoad(JsonElement jsonElement, out ContextualBasedAnalysisAction? action)
    {
        action = null;
        return false;
    }
}


/// <summary>
/// Action to copy value of variable to another variable.
/// </summary>
class CopyVariableAction : ContextualBasedAnalysisAction
{
    /// <summary>
    /// Initialize new <see cref="CopyVariableAction"/> instance.
    /// </summary>
    /// <param name="sourceVar">Name of source variable.</param>
    /// <param name="targetVar">Name of target variable.</param>
    public CopyVariableAction(string sourceVar, string targetVar)
    {
        this.SourceVariable = sourceVar;
        this.TargetVariable = targetVar;
    }


    /// <inheritdoc/>
    public override bool Equals(ContextualBasedAnalysisAction? action) =>
        action is CopyVariableAction cvAction
        && this.SourceVariable == cvAction.SourceVariable
        && this.TargetVariable == cvAction.TargetVariable;
    

    /// <summary>
    /// Load action from JSON format data.
    /// </summary>
    /// <param name="jsonElement">JSON element.</param>
    /// <returns>Loaded action.</returns>
    public static CopyVariableAction Load(JsonElement jsonElement)
    {
        var srcVar = jsonElement.GetProperty(nameof(SourceVariable)).GetString().AsNonNull();
        var targetVar = jsonElement.GetProperty(nameof(TargetVariable)).GetString().AsNonNull();
        return new(srcVar, targetVar);
    }
    

    /// <inheritdoc/>
    public override bool Perform(ContextualBasedAnalysisContext context, DisplayableLog log, params object?[] parameters)
    {
       throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override void Save(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("Type", this.GetType().Name);
        writer.WriteString(nameof(SourceVariable), this.SourceVariable);
        writer.WriteString(nameof(TargetVariable), this.TargetVariable);
        writer.WriteEndObject();
    }


    /// <summary>
    /// Get name of source variable.
    /// </summary>
    public string SourceVariable { get; }


    /// <inheritdoc/>
    public override string? ToString() =>
        App.CurrentOrNull?.GetFormattedString("CopyVariableAction", this.SourceVariable, this.TargetVariable) ?? base.ToString();


    /// <summary>
    /// Get name of target variable.
    /// </summary>
    public string TargetVariable { get; }
}


/// <summary>
/// Action to dequeue value to given variable.
/// </summary>
class DequeueToVariableAction : VariableAction
{
    /// <summary>
    /// Initialize new <see cref="DequeueToVariableAction"/> instance.
    /// </summary>
    /// <param name="queue">Name of queue.</param>
    /// <param name="variable">Name of variable.</param>
    public DequeueToVariableAction(string queue, string variable) : base(variable)
    {
        this.Queue = queue;
    }


    /// <inheritdoc/>
    public override bool Equals(ContextualBasedAnalysisAction? action) =>
        action is DequeueToVariableAction dtvAction
        && this.Queue == dtvAction.Queue
        && this.Variable == dtvAction.Variable;
    

    /// <summary>
    /// Load action from JSON format data.
    /// </summary>
    /// <param name="jsonElement">JSON element.</param>
    /// <returns>Loaded action.</returns>
    public static DequeueToVariableAction Load(JsonElement jsonElement)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override bool Perform(ContextualBasedAnalysisContext context, DisplayableLog log, params object?[] parameters)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Get name of queue to dequeue value.
    /// </summary>
    public string Queue { get; }


    /// <inheritdoc/>
    public override void Save(Utf8JsonWriter writer)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override string? ToString() =>
        App.CurrentOrNull?.GetFormattedString("DequeueToVariableAction", this.Queue, this.Variable) ?? base.ToString();
}


/// <summary>
/// Action to enqueue variable.
/// </summary>
class EnqueueVariableAction : VariableAction
{
    /// <summary>
    /// Initialize new <see cref="EnqueueVariableAction"/> instance.
    /// </summary>
    /// <param name="variable">Name of variable.</param>
    /// <param name="queue">Name of queue.</param>
    public EnqueueVariableAction(string variable, string queue) : base(variable)
    {
        this.Queue = queue;
    }


    /// <inheritdoc/>
    public override bool Equals(ContextualBasedAnalysisAction? action) =>
        action is EnqueueVariableAction evAction
        && this.Queue == evAction.Queue
        && this.Variable == evAction.Variable;
    

    /// <summary>
    /// Load action from JSON format data.
    /// </summary>
    /// <param name="jsonElement">JSON element.</param>
    /// <returns>Loaded action.</returns>
    public static EnqueueVariableAction Load(JsonElement jsonElement)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override bool Perform(ContextualBasedAnalysisContext context, DisplayableLog log, params object?[] parameters)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Get name of queue to enqueue variable.
    /// </summary>
    public string Queue { get; }


    /// <inheritdoc/>
    public override void Save(Utf8JsonWriter writer)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override string? ToString() =>
        App.CurrentOrNull?.GetFormattedString("EnqueueVariableAction", this.Variable, this.Queue) ?? base.ToString();
}


/// <summary>
/// Peek value in front of queue and put to variable.
/// </summary>
class PeekQueueToVariableAction : VariableAction
{
    /// <summary>
    /// Initialize new <see cref="PeekQueueToVariableAction"/> instance.
    /// </summary>
    /// <param name="queue">Name of queue.</param>
    /// <param name="variable">Name of variable.</param>
    public PeekQueueToVariableAction(string queue, string variable) : base(variable)
    {
        this.Queue = queue;
    }


    /// <inheritdoc/>
    public override bool Equals(ContextualBasedAnalysisAction? action) =>
        action is EnqueueVariableAction evAction
        && this.Queue == evAction.Queue
        && this.Variable == evAction.Variable;
    

    /// <summary>
    /// Load action from JSON format data.
    /// </summary>
    /// <param name="jsonElement">JSON element.</param>
    /// <returns>Loaded action.</returns>
    public static EnqueueVariableAction Load(JsonElement jsonElement)
    {
        var queue = jsonElement.GetProperty(nameof(Queue)).GetString().AsNonNull();
        var variable = jsonElement.GetProperty(nameof(Variable)).GetString().AsNonNull();
        return new(variable, queue);
    }


    /// <inheritdoc/>
    public override bool Perform(ContextualBasedAnalysisContext context, DisplayableLog log, params object?[] parameters)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Get name of queue to enqueue variable.
    /// </summary>
    public string Queue { get; }


    /// <inheritdoc/>
    public override void Save(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("Type", this.GetType().Name);
        writer.WriteString(nameof(Queue), this.Queue);
        writer.WriteString(nameof(Variable), this.Variable);
        writer.WriteEndObject();
    }


    /// <inheritdoc/>
    public override string? ToString() =>
        App.CurrentOrNull?.GetFormattedString("PeekQueueToVariableAction", this.Queue, this.Variable) ?? base.ToString();
}


/// <summary>
/// Peek value at top of stack and put to variable.
/// </summary>
class PeekStackToVariableAction : VariableAction
{
    /// <summary>
    /// Initialize new <see cref="PeekStackToVariableAction"/> instance.
    /// </summary>
    /// <param name="stack">Name of stack.</param>
    /// <param name="variable">Name of variable.</param>
    public PeekStackToVariableAction(string stack, string variable) : base(variable)
    {
        this.Stack = stack;
    }


    /// <inheritdoc/>
    public override bool Equals(ContextualBasedAnalysisAction? action) =>
        action is PopToVariableAction ptvAction
        && this.Stack == ptvAction.Stack
        && this.Variable == ptvAction.Variable;
    

    /// <summary>
    /// Load action from JSON format data.
    /// </summary>
    /// <param name="jsonElement">JSON element.</param>
    /// <returns>Loaded action.</returns>
    public static PopToVariableAction Load(JsonElement jsonElement)
    {
        var variable = jsonElement.GetProperty(nameof(Variable)).GetString().AsNonNull();
        var stack = jsonElement.GetProperty(nameof(Stack)).GetString().AsNonNull();
        return new(stack, variable);
    }


    /// <inheritdoc/>
    public override bool Perform(ContextualBasedAnalysisContext context, DisplayableLog log, params object?[] parameters)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override void Save(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("Type", this.GetType().Name);
        writer.WriteString(nameof(Stack), this.Stack);
        writer.WriteString(nameof(Variable), this.Variable);
        writer.WriteEndObject();
    }


    /// <summary>
    /// Get name of stack to pop value.
    /// </summary>
    public string Stack { get; }


    /// <inheritdoc/>
    public override string? ToString() =>
        App.CurrentOrNull?.GetFormattedString("PeekStackToVariableAction", this.Stack, this.Variable) ?? base.ToString();
}


/// <summary>
/// Action to pop value to given variable.
/// </summary>
class PopToVariableAction : VariableAction
{
    /// <summary>
    /// Initialize new <see cref="PopToVariableAction"/> instance.
    /// </summary>
    /// <param name="stack">Name of stack.</param>
    /// <param name="variable">Name of variable.</param>
    public PopToVariableAction(string stack, string variable) : base(variable)
    {
        this.Stack = stack;
    }


    /// <inheritdoc/>
    public override bool Equals(ContextualBasedAnalysisAction? action) =>
        action is PopToVariableAction ptvAction
        && this.Stack == ptvAction.Stack
        && this.Variable == ptvAction.Variable;
    

    /// <summary>
    /// Load action from JSON format data.
    /// </summary>
    /// <param name="jsonElement">JSON element.</param>
    /// <returns>Loaded action.</returns>
    public static PopToVariableAction Load(JsonElement jsonElement)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override bool Perform(ContextualBasedAnalysisContext context, DisplayableLog log, params object?[] parameters)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override void Save(Utf8JsonWriter writer)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Get name of stack to pop value.
    /// </summary>
    public string Stack { get; }


    /// <inheritdoc/>
    public override string? ToString() =>
        App.CurrentOrNull?.GetFormattedString("PopToVariableAction", this.Stack, this.Variable) ?? base.ToString();
}


/// <summary>
/// Action to push variable ti stack.
/// </summary>
class PushVariableAction : VariableAction
{
    /// <summary>
    /// Initialize new <see cref="PushVariableAction"/> instance.
    /// </summary>
    /// <param name="variable">Name of variable.</param>
    /// <param name="stack">Name of stack.</param>
    public PushVariableAction(string variable, string stack) : base(variable)
    {
        this.Stack = stack;
    }


    /// <inheritdoc/>
    public override bool Equals(ContextualBasedAnalysisAction? action) =>
        action is PushVariableAction pvAction
        && this.Stack == pvAction.Stack
        && this.Variable == pvAction.Variable;
    

    /// <summary>
    /// Load action from JSON format data.
    /// </summary>
    /// <param name="jsonElement">JSON element.</param>
    /// <returns>Loaded action.</returns>
    public static PushVariableAction Load(JsonElement jsonElement)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override bool Perform(ContextualBasedAnalysisContext context, DisplayableLog log, params object?[] parameters)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override void Save(Utf8JsonWriter writer)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Get name of stack to push variable.
    /// </summary>
    public string Stack { get; }


    /// <inheritdoc/>
    public override string? ToString() =>
        App.CurrentOrNull?.GetFormattedString("PushVariableAction", this.Variable, this.Variable) ?? base.ToString();
}


/// <summary>
/// Action which is related to a variable.
/// </summary>
abstract class VariableAction : ContextualBasedAnalysisAction
{
    /// <summary>
    /// Initialize new <see cref="VariableAction"/> instance.
    /// </summary>
    /// <param name="variable">Name of variable.</param>
    public VariableAction(string variable)
    {
        this.Variable = variable;
    }


    /// <inheritdoc/>
    public override int GetHashCode() =>
        this.Variable.GetHashCode();

    
    /// <summary>
    /// Get name of variable.
    /// </summary>
    public string Variable { get; }
}