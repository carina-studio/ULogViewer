using CarinaStudio.Diagnostics;
using System;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Implementation of <see cref="IStringSource"/> which is optimized for small string.
/// </summary>
public class SmallStringSource : IStringSource
{
    /// <summary>
    /// Maximum number of characters supported to be stored in <see cref="SmallStringSource"/>.
    /// </summary>
    public const int MaxLength = 4;
    
    
    // Static fields.
    static readonly long BaseByteCount = Memory.EstimateInstanceSize<SmallStringSource>();
    
    
    // Fields.
    readonly char c0;
    readonly char c1;
    readonly char c2;
    readonly char c3;
    
    
    /// <summary>
    /// Initialize new instance of <see cref="SmallStringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public SmallStringSource(string s) : this(s.AsSpan())
    { }
    
    
    /// <summary>
    /// Initialize new instance of <see cref="SmallStringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public SmallStringSource(ReadOnlyMemory<char> s) : this(s.Span)
    { }


    /// <summary>
    /// Initialize new instance of <see cref="SmallStringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public SmallStringSource(ReadOnlySpan<char> s)
    {
        switch (s.Length)
        {
            case 0:
                break;
            case 1:
                this.c0 = s[0];
                break;
            case 2:
                this.c0 = s[0];
                this.c1 = s[1];
                break;
            case 3:
                this.c0 = s[0];
                this.c1 = s[1];
                this.c2 = s[2];
                break;
            case 4:
                this.c0 = s[0];
                this.c1 = s[1];
                this.c2 = s[2];
                this.c3 = s[3];
                break;
            default:
                throw new InvalidOperationException($"Number of characters cannot be greater than {MaxLength}.");
        }
        this.Length = s.Length;
    }


    /// <inheritdoc/>
    public long ByteCount => BaseByteCount;


    /// <inheritdoc/>
    public int Length { get; }


    /// <inheritdoc/>
    public override string ToString()
    {
        if (this.Length == 0)
            return "";
        var buffer = new char[this.Length];
        this.TryCopyTo(buffer.AsSpan());
        return new string(buffer);
    }


    /// <inheritdoc/>
    public bool TryCopyTo(Span<char> buffer)
    {
        if (this.Length == 0)
            return true;
        if (buffer.Length < this.Length)
            return false;
        switch (this.Length)
        {
            case 1:
                buffer[0] = this.c0;
                break;
            case 2:
                buffer[0] = this.c0;
                buffer[1] = this.c1;
                break;
            case 3:
                buffer[0] = this.c0;
                buffer[1] = this.c1;
                buffer[2] = this.c2;
                break;
            case 4:
                buffer[0] = this.c0;
                buffer[1] = this.c1;
                buffer[2] = this.c2;
                buffer[3] = this.c3;
                break;
        }
        return true;
    }
}