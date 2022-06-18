using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer;

/// <summary>
/// String formatter.
/// </summary>
class StringFormatter
{
    // Static fields.
	static readonly Regex ParamRegex = new Regex("\\{(?<Name>[\\w\\d]+)(\\,(?<Alignment>[\\+\\-]?[\\d]+))?(\\:(?<Format>[^\\}]+))?\\}");

    
    // Fields.
    readonly string format;
    readonly Func<object?, string, object?> parameterProvider;


    /// <summary>
    /// Initialize new <see cref=""/> instance.
    /// </summary>
    /// <param name="format">String format.</param>
    /// <param name="paramProvider">Function to get named parameters.</param>
    public StringFormatter(string format, Func<object?, string, object?> paramProvider)
    {
        // keep provider
        this.parameterProvider = paramProvider;

        // convert format
        var formatStart = 0;
        var formatBuilder = new StringBuilder();
        var parameterNames = new List<string>();
        var match = ParamRegex.Match(format);
        while (match.Success)
        {
            // flush normal string
            formatBuilder.Append(format.Substring(formatStart, match.Index - formatStart));
            formatStart = match.Index + match.Length;

            // get parameter position
            var paramName = match.Groups["Name"].Value;
            var paramIndex = parameterNames.IndexOf(paramName);
            if (paramIndex < 0)
            {
                paramIndex = parameterNames.Count;
                parameterNames.Add(paramName);
            }

            // convert parameter format
            formatBuilder.Append($"{{{paramIndex}");
            match.Groups["Alignment"].Let(it =>
            {
                if (it.Success)
                    formatBuilder.Append($",{it.Value}");
            });
            match.Groups["Format"].Let(it =>
            {
                if (it.Success)
                    formatBuilder.Append($":{it.Value}");
            });
            formatBuilder.Append('}');

            // find next parameter
            match = match.NextMatch();
        }
        formatBuilder.Append(format.Substring(formatStart));
        this.format = formatBuilder.ToString();
        this.ParameterNames = parameterNames.AsReadOnly();
    }


    /// <summary>
    /// Get list of parameter names described in format.
    /// </summary>
    public IList<string> ParameterNames { get; }


    /// <inheritdoc/>
    public override string ToString() =>
        this.ToString(null);


    /// <summary>
    /// Get formatted string.
    /// </summary>
    /// <param name="target">Target object.</param>
    /// <returns>Formatted string.</returns>
    public string ToString(object? target)
    {
        // output without format
        var paramNames = this.ParameterNames;
        var paramCount = paramNames.Count;
        if (paramCount == 0)
            return this.format;

        // get parameters
        var parameters = new object?[paramCount];
        for (var i = paramCount - 1; i >= 0; --i)
            parameters[i] = this.parameterProvider(target, paramNames[i]);
        
        // format
        return string.Format(this.format, parameters);
    }
}