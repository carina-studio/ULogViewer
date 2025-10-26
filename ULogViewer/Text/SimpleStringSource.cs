using CarinaStudio.Diagnostics;
using System;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Implementation of <see cref="IStringSource"/> which uses <see cref="string"/> to store string.
/// </summary>
public class SimpleStringSource : IStringSource
{
    // Static fields.
    static readonly long BaseByteCount = Memory.EstimateInstanceSize<SimpleStringSource>();
    
    
    // Fields.
    readonly string @string;


    /// <summary>
    /// Initialize new instance of <see cref="SimpleStringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public SimpleStringSource(string s) =>
        this.@string = s;


    /// <summary>
    /// Initialize new instance of <see cref="SimpleStringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public SimpleStringSource(ReadOnlyMemory<char> s) =>
        this.@string = new(s.Span);


    /// <summary>
    /// Initialize new instance of <see cref="SimpleStringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public SimpleStringSource(ReadOnlySpan<char> s) =>
        this.@string = new(s);


    /// <inheritdoc/>
    public long ByteCount => BaseByteCount + Memory.EstimateInstanceSize<string>(this.@string.Length);


    /// <inheritdoc/>
    public int Length => this.@string.Length;
    
    
    /// <inheritdoc/>
    public override string ToString() => this.@string;


    /// <inheritdoc/>
    public bool TryCopyTo(Span<char> buffer)
    {
        if (buffer.Length >= this.@string.Length)
            return this.@string.AsSpan().TryCopyTo(buffer);
        return this.@string.AsSpan().Slice(0, buffer.Length).TryCopyTo(buffer);
    }
}