using CarinaStudio.AppSuite.Data;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Base class for rule set of log analysis,
/// </summary>
abstract class DisplayableLogAnalysisRuleSet<TRule> : BaseProfile<IULogViewerApplication>, ILogProfileIconSource where TRule : class, IEquatable<TRule>
{
    // Fields.
    LogProfileIcon icon = LogProfileIcon.Analysis;
    LogProfileIconColor iconColor = LogProfileIconColor.Default;
    IList<TRule> rules = new TRule[0];


    /// <summary>
    /// Initialize new <see cref="DisplayableLogAnalysisRuleSet"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="id">Unique ID.</param>
    /// <param name="isBuiltIn">True if the rule set is built-in.</param>
    protected DisplayableLogAnalysisRuleSet(IULogViewerApplication app, string id, bool isBuiltIn = false) : base(app, id, isBuiltIn)
    { }


    /// <summary>
    /// Initialize new <see cref="DisplayableLogAnalysisRuleSet"/> instance.
    /// </summary>
    /// <param name="template">Template.</param>
    /// <param name="name">Name of rule set.</param>
    /// <param name="id">Unique ID.</param>
    protected DisplayableLogAnalysisRuleSet(DisplayableLogAnalysisRuleSet<TRule> template, string name, string id) : base(template.Application, id, false)
    {
        this.icon = template.icon;
        this.iconColor = template.iconColor;
        this.Name = name;
        this.rules = template.rules;
    }


    /// <summary>
    /// Change the ID.
    /// </summary>
    internal abstract void ChangeId();


    /// <inheritdoc/>
    public override bool Equals(IProfile<IULogViewerApplication>? profile) =>
        profile is DisplayableLogAnalysisRuleSet<TRule> ruleSet
        && this.Id == ruleSet.Id
        && this.Icon == ruleSet.Icon
        && this.IconColor == ruleSet.IconColor
        && this.Name == ruleSet.Name
        && this.Rules.SequenceEqual(ruleSet.Rules);


    /// <summary>
    /// Get or set icon of rule sets.
    /// </summary>
    public LogProfileIcon Icon
    {
        get => this.icon;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.icon == value)
                return;
            this.icon = value;
            this.OnPropertyChanged(nameof(Icon));
        }
    }


    /// <summary>
    /// Get or set color of icon of rule sets.
    /// </summary>
    public LogProfileIconColor IconColor
    {
        get => this.iconColor;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.iconColor == value)
                return;
            this.iconColor = value;
            this.OnPropertyChanged(nameof(IconColor));
        }
    }


    /// <summary>
    /// Check whether data has been upgraded when loading or not.
    /// </summary>
    public bool IsDataUpgraded { get; protected set; }


    /// <summary>
    /// Get or set list of rule for this set.
    /// </summary>
    public IList<TRule> Rules
    {
        get => this.rules;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.rules.SequenceEqual(value))
                return;
            this.rules = ListExtensions.AsReadOnly(value);
            this.OnPropertyChanged(nameof(Rules));
        }
    }
}