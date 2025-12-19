using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer;

/// <summary>
/// String formatter.
/// </summary>
class StringFormatter
{
    // Static fields.
	static readonly Regex ParamRegex = new("(?<!\\{)\\{(?<Structured>@)?(?<Name>[\\w\\d]+)(\\,(?<Alignment>[\\+\\-]?[\\d]+))?(\\:(?<Format>[^\\}]+))?\\}");

    
    // Fields.
    readonly string format;
    readonly Func<object?, string, object?> parameterProvider;
    readonly Func<object, StringBuilder, bool>? structuredParameterFormatter;
    readonly List<int> structuredParameterIndices = new();


    /// <summary>
    /// Initialize new <see cref="StringFormatter"/> instance.
    /// </summary>
    /// <param name="format">String format.</param>
    /// <param name="paramProvider">Function to get named parameters.</param>
    /// <param name="structuredParamFormatter">Function to format structured parameter.</param>
    public StringFormatter(string format, Func<object?, string, object?> paramProvider, Func<object, StringBuilder, bool>? structuredParamFormatter = null)
    {
        // keep provider
        this.parameterProvider = paramProvider;
        
        // keep formatter
        this.structuredParameterFormatter = structuredParamFormatter;

        // convert format
        var formatStart = 0;
        var formatBuilder = new StringBuilder();
        var parameterNames = ImmutableList.CreateBuilder<string>();
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
            if (match.Groups["Structured"].Success && structuredParamFormatter is not null)
                this.structuredParameterIndices.Add(paramIndex);
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
        this.ParameterNames = parameterNames.ToImmutable();
    }


    // Format enumerable parameter.
    static void FormatEnumerableParameter(IEnumerable enumerable, StringBuilder buffer)
    {
        var isFirstElement = true;
        buffer.Append("[ ");
        foreach (var value in enumerable)
        {
            if (!isFirstElement)
                buffer.Append(", ");
            else
                isFirstElement = false;
            if (value is not string && value is IEnumerable enumerableValue)
                FormatEnumerableParameter(enumerableValue, buffer);
            else
                buffer.Append(value);
        }
        buffer.Append(" ]");
    }


    /// <summary>
    /// Get list of parameter names described in format.
    /// </summary>
    public IList<string> ParameterNames { get; }


    /// <inheritdoc/>
    public override string ToString() =>
        this.ToString(target: null);
    
    
    /// <summary>
    /// Get formatted string.
    /// </summary>
    /// <param name="buffer"><see cref="StringBuilder"/> to receive formatted string.</param>
    /// <param name="append">True to append formatted string without clearing <paramref name="buffer"/>.</param>
    /// <returns>Formatted string.</returns>
    public void ToString(StringBuilder buffer, bool append = false) =>
        this.ToString(null, buffer, append);


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
        var paramProvider = this.parameterProvider;
        var structuredParamFormatter = this.structuredParameterFormatter;
        var structuredParamIndices = this.structuredParameterIndices;
        var parameters = new object?[paramCount];
        StringBuilder? paramBuffer = null;
        for (var i = paramCount - 1; i >= 0; --i)
        {
            var param = paramProvider(target, paramNames[i]);
            if (param is not null && structuredParamIndices.Contains(i) && structuredParamFormatter is not null)
            {
                paramBuffer ??= new();
                if (structuredParamFormatter(param, paramBuffer))
                    param = paramBuffer.ToString();
                paramBuffer.Clear();
            }
            else if (param is not string && param is IEnumerable enumerable)
            {
                paramBuffer ??= new();
                FormatEnumerableParameter(enumerable, paramBuffer);
                param = paramBuffer.ToString();
                paramBuffer.Clear();
            }
            parameters[i] = param;
        }

        // format
        return string.Format(this.format, parameters);
    }
    
    
    /// <summary>
    /// Get formatted string.
    /// </summary>
    /// <param name="target">Target object.</param>
    /// <param name="buffer"><see cref="StringBuilder"/> to receive formatted string.</param>
    /// <param name="append">True to append formatted string without clearing <paramref name="buffer"/>.</param>
    /// <returns>Formatted string.</returns>
    public void ToString(object? target, StringBuilder buffer, bool append = false)
    {
        // clear buffer
        if (!append)
            buffer.Clear();
        
        // output without format
        var paramNames = this.ParameterNames;
        var paramCount = paramNames.Count;
        if (paramCount == 0)
        {
            buffer.Append(this.format);
            return;
        }

        // get parameters
        var paramProvider = this.parameterProvider;
        var structuredParamFormatter = this.structuredParameterFormatter;
        var structuredParamIndices = this.structuredParameterIndices;
        var parameters = new object?[paramCount];
        StringBuilder? paramBuffer = null;
        for (var i = paramCount - 1; i >= 0; --i)
        {
            var param = paramProvider(target, paramNames[i]);
            if (param is not null && structuredParamIndices.Contains(i) && structuredParamFormatter is not null)
            {
                paramBuffer ??= new();
                if (structuredParamFormatter(param, paramBuffer))
                    param = paramBuffer.ToString();
                paramBuffer.Clear();
            }
            else if (param is not string && param is IEnumerable enumerable)
            {
                paramBuffer ??= new();
                FormatEnumerableParameter(enumerable, paramBuffer);
                param = paramBuffer.ToString();
                paramBuffer.Clear();
            }
            parameters[i] = param;
        }
        
        // format
        buffer.AppendFormat(this.format, parameters);
    }
}