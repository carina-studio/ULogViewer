using CarinaStudio.Diagnostics;
using System;
using System.Text;
using System.Text.Unicode;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Implementation of <see cref="IStringSource"/> which stores string in UTF-8 encoding.
/// </summary>
public class Utf8StringSource : IStringSource
{
    // Static fields.
    static readonly long BaseByteCount = Memory.EstimateInstanceSize<Utf8StringSource>();
    
    
    // Fields.
    readonly byte[]? utf8;


    /// <summary>
    /// Initialize new instance of <see cref="Utf8StringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public Utf8StringSource(string s)
    {
        this.Length = s.Length;
        if (this.Length > 0)
            this.utf8 = Encoding.UTF8.GetBytes(s);
    }
    
    
    /// <summary>
    /// Initialize new instance of <see cref="SimpleStringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public Utf8StringSource(ReadOnlyMemory<char> s) : this(s.Span)
    { }
    
    
    /// <summary>
    /// Initialize new instance of <see cref="Utf8StringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public Utf8StringSource(ReadOnlySpan<char> s)
    {
        this.Length = s.Length;
        if (this.Length > 0)
        {
            this.utf8 = new byte[Encoding.UTF8.GetByteCount(s)];
            Utf8.FromUtf16(s, this.utf8.AsSpan(), out _, out _);
        }
    }


    /// <inheritdoc/>
    public long ByteCount => BaseByteCount + (this.utf8 is null ? 0 : Memory.EstimateArrayInstanceSize<byte>(this.utf8.Length));
    
    
    /// <inheritdoc/>
    public int Length { get; }


    /// <inheritdoc/>
    public override string ToString() => this.utf8 is null ? "" : Encoding.UTF8.GetString(this.utf8);


    /// <inheritdoc/>
    public bool TryCopyTo(Span<char> buffer)
    {
        if (this.utf8 is null)
            return true;
        if (buffer.IsEmpty)
            return false;
        Utf8.ToUtf16(this.utf8.AsSpan(), buffer, out _, out _);
        return true;
    }
}