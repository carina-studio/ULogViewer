using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="TextReader"/> which reads JSON data from underlying reader and generates formatted JSON data.
/// </summary>
class FormattedJsonTextReader : TextReader
{
    // Static fields.
    [ThreadStatic]
    static StringBuilder? LineBuffer;


    // Fields.
    readonly JsonReader jsonReader;
    int nextDepth;
    JsonToken nextToken = JsonToken.None;
    object? nextValue;


    /// <summary>
    /// Initialize new <see cref=""/> instance.
    /// </summary>
    /// <param name="reader">Underlying text reader.</param>
    public FormattedJsonTextReader(TextReader reader)
    {
        this.jsonReader = new JsonTextReader(reader);
    }


    // Create indentation string.
    static string CreateIndentationString(int indentation) => indentation > 0
        ? new string(new char[indentation * 4].Also(it =>
        {
            for (var i = it.Length - 1; i >= 0; --i)
                it[i] = ' ';
        }))
        : "";


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        this.jsonReader.Close();
        base.Dispose(disposing);
    }


    /// <inheritdoc/>
    public override string? ReadLine()
    {
        if (this.nextToken == JsonToken.None && !this.jsonReader.Read())
            return null;
        var lineBuffer = LineBuffer ?? new StringBuilder().Also(it => LineBuffer = it);
        try
        {
            this.ReadToken(lineBuffer);
            var line = lineBuffer.ToString();
            return line;
        }
        finally
        {
            lineBuffer.Clear();
        }
    }


    /// <inheritdoc/>
    void ReadToken(StringBuilder buffer, bool indentation = true)
    {
        var tokenType = this.nextToken == JsonToken.None
            ? this.jsonReader.TokenType
            : this.nextToken;
        var value = this.nextToken == JsonToken.None
            ? this.jsonReader.Value
            : this.nextValue;
        var depth = this.nextToken == JsonToken.None
            ? this.jsonReader.Depth
            : this.nextDepth;
        this.nextToken = JsonToken.None;
        this.nextValue = null;
        this.nextDepth = 0;
        if (indentation)
            buffer.Append(CreateIndentationString(depth));
        switch (tokenType)
        {
            case JsonToken.Boolean:
            case JsonToken.Bytes:
            case JsonToken.Comment:
            case JsonToken.Date:
            case JsonToken.Float:
            case JsonToken.Integer:
            case JsonToken.Null:
            case JsonToken.Raw:
            case JsonToken.Undefined:
                buffer.Append(value);
                break;
            case JsonToken.EndArray:
                buffer.Append(']');
                if (jsonReader.Read())
                {
                    this.nextToken = this.jsonReader.TokenType;
                    this.nextValue = this.jsonReader.Value;
                    this.nextDepth = this.jsonReader.Depth;
                    switch (this.nextToken)
                    {
                        case JsonToken.StartArray:
                        case JsonToken.StartObject:
                        case JsonToken.PropertyName:
                            buffer.Append(',');
                            break;
                        default:
                            break;
                    }
                }
                break;
            case JsonToken.EndObject:
                buffer.Append('}');
                if (jsonReader.Read())
                {
                    this.nextToken = this.jsonReader.TokenType;
                    this.nextValue = this.jsonReader.Value;
                    this.nextDepth = this.jsonReader.Depth;
                    switch (this.nextToken)
                    {
                        case JsonToken.StartArray:
                        case JsonToken.StartObject:
                        case JsonToken.PropertyName:
                            buffer.Append(',');
                            break;
                        default:
                            break;
                    }
                }
                break;
            case JsonToken.PropertyName:
                buffer.Append('\"');
                buffer.Append(value);
                buffer.Append("\": ");
                if (this.jsonReader.Read())
                {
                    tokenType = this.jsonReader.TokenType;
                    this.ReadToken(buffer, false);
                    if (jsonReader.Read())
                    {
                        this.nextToken = this.jsonReader.TokenType;
                        this.nextValue = this.jsonReader.Value;
                        this.nextDepth = this.jsonReader.Depth;
                        if (this.nextToken == JsonToken.PropertyName && tokenType != JsonToken.StartObject)
                            buffer.Append(',');
                    }
                }
                break;
            case JsonToken.StartArray:
                buffer.Append('[');
                break;
            case JsonToken.StartObject:
                buffer.Append('{');
                break;
            case JsonToken.String:
                buffer.Append('\"');
                buffer.Append(value);
                buffer.Append('\"');
                break;
        }
    }
}