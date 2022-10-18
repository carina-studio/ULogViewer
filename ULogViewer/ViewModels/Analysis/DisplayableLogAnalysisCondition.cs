using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Type of value comparison.
/// </summary>
enum ComparisonType
{
    /// <summary>
    /// Equivalent to.
    /// </summary>
    Equivalent,
    /// <summary>
    /// Not equivalent to.
    /// </summary>
    NotEquivalent,
    /// <summary>
    /// Greater than.
    /// </summary>
    Greater,
    /// <summary>
    /// Greater than or equivalent to.
    /// </summary>
    GreaterOrEquivalent,
    /// <summary>
    /// Smaller than.
    /// </summary>
    Smaller,
    /// <summary>
    /// Smaller than or equivalent to.
    /// </summary>
    SmallerOrEquivalent,
}


/// <summary>
/// Condition of log analysis.
/// </summary>
#pragma warning disable CS0659
#pragma warning disable CS0661
abstract class DisplayableLogAnalysisCondition : IEquatable<DisplayableLogAnalysisCondition>
#pragma warning restore CS0659
#pragma warning restore CS0661
{
    // Static fields.
    static readonly ConcurrentDictionary<string, MethodInfo> LoadingMethods = new();


    /// <inheritdoc/>
    public abstract bool Equals(DisplayableLogAnalysisCondition? condition);


    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is DisplayableLogAnalysisCondition condition
        && this.Equals(condition);


    /// <summary>
    /// Check whether the condition is matched by given log/parameters or not.
    /// </summary>
    /// <param name="context">Analysis context.</param>
    /// <param name="log">Log.</param>
    /// <param name="parameters">List of parameters.</param>
    /// <returns>True if condition matched.</returns>
    public abstract bool Match(DisplayableLogAnalysisContext context, DisplayableLog log, params object?[] parameters);


    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(DisplayableLogAnalysisCondition? lhs, DisplayableLogAnalysisCondition? rhs) =>
        lhs?.Equals(rhs) ?? rhs is null;
    
    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(DisplayableLogAnalysisCondition? lhs, DisplayableLogAnalysisCondition? rhs) =>
        lhs is null ? rhs is not null : !lhs.Equals(rhs);
    

    /// <summary>
    /// Save condition in JSON format.
    /// </summary>
    /// <param name="writer">JSON data writer.</param>
    public abstract void Save(Utf8JsonWriter writer);


    /// <summary>
    /// Try loading JSON format data as <see cref="ContextualBasedAnalysisCondition"/>.
    /// </summary>
    /// <param name="jsonElement">JSON element.</param>
    /// <param name="condition">Loaded condition.</param>
    /// <returns>True if condition loaded successfully.</returns>
    public static bool TryLoad(JsonElement jsonElement, [NotNullWhen(true)] out DisplayableLogAnalysisCondition? condition)
    {
        condition = null;
        return false;
    }
}


/// <summary>
/// Condition based-on comparison of values.
/// </summary>
abstract class ValuesComparisonCondition : DisplayableLogAnalysisCondition
{
    /// <summary>
    /// Initialize new <see cref="ValuesComparisonCondition"/> instance.
    /// </summary>
    /// <param name="comparisonType">Type of comparison.</param>
    protected ValuesComparisonCondition(ComparisonType comparisonType)
    {
        this.ComparisonType = comparisonType;
    }


    /// <summary>
    /// Get type of comparison.
    /// </summary>
    public ComparisonType ComparisonType { get; }


    /// <inheritdoc/>
    public sealed override bool Match(DisplayableLogAnalysisContext context, DisplayableLog log, params object?[] parameters)
    {
        return false;
    }


    /// <summary>
    /// Try getting values to compare.
    /// </summary>
    /// <param name="context">Analysis context.</param>
    /// <param name="log">Log.</param>
    /// <param name="parameters">List of parameters.</param>
    /// <param name="lhs">Value as left hand side.</param>
    /// <param name="rhs">Value as right hand side.</param>
    /// <returns>True if values are got successfully.</returns>
    protected abstract bool TryGetValues(DisplayableLogAnalysisContext context, DisplayableLog log, object?[] parameters, out string? lhs, out string? rhs);
}


/// <summary>
/// Condition based-on comparison of variable and constant.
/// </summary>
class VariableAndConstantComparisonCondition : ValuesComparisonCondition
{
    /// <summary>
    /// Initialize new <see cref="VariableAndConstantComparisonCondition"/> instance.
    /// </summary>
    /// <param name="varName">Name of variable at left hand side.</param>
    /// <param name="comparisonType">Type of comparison.</param>
    /// <param name="constant">Constant at right hand side.</param>
    public VariableAndConstantComparisonCondition(string varName, ComparisonType comparisonType, string constant) : base(comparisonType)
    {
        this.Constant = constant;
        this.Variable = varName;
    }


    /// <summary>
    /// Get constant at right hand side of comparison.
    /// </summary>
    public string Constant { get; }


    /// <inheritdoc/>
    public override bool Equals(DisplayableLogAnalysisCondition? condition) =>
        condition is VariableAndConstantComparisonCondition vccCondition
        && vccCondition.ComparisonType == this.ComparisonType
        && vccCondition.Variable == this.Variable
        && vccCondition.Constant == this.Constant;


    /// <inheritdoc/>
    public override int GetHashCode() =>
        ((int)(this.ComparisonType) & 0xf) | (this.Variable.GetHashCode() << 4);
    

    /// <summary>
    /// Load condition from JSON format data.
    /// </summary>
    /// <param name="jsonElement">JSON element.</param>
    /// <returns>Loaded condition.</returns>
    public static VariableAndConstantComparisonCondition Load(JsonElement jsonElement)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    public override void Save(Utf8JsonWriter writer)
    {
    }


    /// <inheritdoc/>
    public override string? ToString() => App.CurrentOrNull?.Let(app =>
    {
        var c = app.GetStringNonNull($"ComparisonType.{this.ComparisonType}", this.ComparisonType.ToString());
        return app.GetFormattedString("VariableAndConstantComparisonCondition", this.Variable, c, this.Constant);
    }) ?? base.ToString();


    /// <inheritdoc/>
    protected override bool TryGetValues(DisplayableLogAnalysisContext context, DisplayableLog log, object?[] parameters, out string? lhs, out string? rhs)
    {
        lhs = context.GetVariable(this.Variable);
        rhs = this.Constant;
        return true;
    }


    /// <summary>
    /// Get name of variable at left hand side of comparison.
    /// </summary>
    public string Variable { get; }
}


/// <summary>
/// Condition based-on comparison of variables.
/// </summary>
class VariablesComparisonCondition : ValuesComparisonCondition
{
    /// <summary>
    /// Initialize new <see cref="VariablesComparisonCondition"/> instance.
    /// </summary>
    /// <param name="lhsVarName">Name of variable at left hand side.</param>
    /// <param name="comparisonType">Type of comparison.</param>
    /// <param name="rhsVarName">Name of variable at right hand side.</param>
    public VariablesComparisonCondition(string lhsVarName, ComparisonType comparisonType, string rhsVarName) : base(comparisonType)
    {
        this.LhsVariable = lhsVarName;
        this.RhsVariable = rhsVarName;
    }


    /// <inheritdoc/>
    public override bool Equals(DisplayableLogAnalysisCondition? condition) =>
        condition is VariablesComparisonCondition vcCondition
        && vcCondition.ComparisonType == this.ComparisonType
        && vcCondition.LhsVariable == this.LhsVariable
        && vcCondition.RhsVariable == this.RhsVariable;


    /// <inheritdoc/>
    public override int GetHashCode() =>
        ((int)(this.ComparisonType) & 0xf) | (this.LhsVariable.GetHashCode() << 4);


    /// <summary>
    /// Get name of variable at left hand side of comparison.
    /// </summary>
    public string LhsVariable { get; }


    /// <summary>
    /// Load condition from JSON format data.
    /// </summary>
    /// <param name="jsonElement">JSON element.</param>
    /// <returns>Loaded condition.</returns>
    public static VariablesComparisonCondition Load(JsonElement jsonElement)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Get name of variable at right hand side of comparison.
    /// </summary>
    public string RhsVariable { get; }


    /// <inheritdoc/>
    public override void Save(Utf8JsonWriter writer)
    {
    }


    /// <inheritdoc/>
    public override string? ToString() => App.CurrentOrNull?.Let(app =>
    {
        var c = app.GetStringNonNull($"ComparisonType.{this.ComparisonType}", this.ComparisonType.ToString());
        return app.GetFormattedString("VariablesComparisonCondition", this.LhsVariable, c, this.RhsVariable);
    }) ?? base.ToString();


    /// <inheritdoc/>
    protected override bool TryGetValues(DisplayableLogAnalysisContext context, DisplayableLog log, object?[] parameters, out string? lhs, out string? rhs)
    {
        lhs = context.GetVariable(this.LhsVariable);
        rhs = context.GetVariable(this.RhsVariable);
        return true;
    }
}