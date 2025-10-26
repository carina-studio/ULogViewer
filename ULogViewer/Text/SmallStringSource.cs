using CarinaStudio.Diagnostics;
using System;
using System.Buffers;
using System.Threading;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Implementation of <see cref="IStringSource"/> which is optimized for small string.
/// </summary>
public class SmallStringSource : IStringSource
{
    /// <summary>
    /// Maximum number of characters supported to be stored in <see cref="SmallStringSource"/>.
    /// </summary>
    public const int MaxLength = 8;
    
    
    // Static fields.
    static readonly ArrayPool<char> CharArrayPool = ArrayPool<char>.Create();
    static readonly Lock CharArrayPoolLock = new();
    
    
    // Static fields.
    static readonly long BaseByteCount = Memory.EstimateInstanceSize<SmallStringSource>();
    static unsafe readonly delegate*<SmallStringSource, Span<char>, void>[] CopyCharsFuncs = 
    {
        null,
        &Copy1Char,
        &Copy2Chars,
        &Copy3Chars,
        &Copy4Chars,
        &Copy5Chars,
        &Copy6Chars,
        &Copy7Chars,
        &Copy8Chars,
    };
    static unsafe readonly delegate*<SmallStringSource, ReadOnlySpan<char>, void>[] InitFuncs = 
    {
        null,
        &InitWith1Char,
        &InitWith2Chars,
        &InitWith3Chars,
        &InitWith4Chars,
        &InitWith5Chars,
        &InitWith6Chars,
        &InitWith7Chars,
        &InitWith8Chars,
    };
    
    
    // Fields.
    uint c01;
    uint c23;
    uint c45;
    uint c67;
    
    
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
    public unsafe SmallStringSource(ReadOnlySpan<char> s)
    {
        var length = s.Length;
        if (length > MaxLength)
            throw new InvalidOperationException($"Number of characters cannot be greater than {MaxLength}.");
        if (length > 0)
        {
            InitFuncs[length](this, s);
            this.Length = s.Length;
        }
    }


    /// <inheritdoc/>
    public long ByteCount => BaseByteCount;


    // Copy string with 1 character.
    static void Copy1Char(SmallStringSource source, Span<char> buffer)
    {
        buffer[0] = (char)source.c01;
    }


    // Copy string with 2 characters.
    static unsafe void Copy2Chars(SmallStringSource source, Span<char> buffer)
    {
        fixed (char* p = buffer)
        {
            *(uint*)p = source.c01;
        }
    }


    // Copy string with 3 characters.
    static unsafe void Copy3Chars(SmallStringSource source, Span<char> buffer)
    {
        fixed (char* p = buffer)
        {
            var cPtr = (uint*)p;
            *cPtr = source.c01;
            buffer[2] = (char)source.c23;
        }
    }


    // Copy string with 4 characters.
    static unsafe void Copy4Chars(SmallStringSource source, Span<char> buffer)
    {
        fixed (char* p = buffer)
        {
            var cPtr = (uint*)p;
            *(cPtr++) = source.c01;
            *cPtr = source.c23;
        }
    }


    // Copy string with 5 characters.
    static unsafe void Copy5Chars(SmallStringSource source, Span<char> buffer)
    {
        fixed (char* p = buffer)
        {
            var cPtr = (uint*)p;
            *(cPtr++) = source.c01;
            *(cPtr++) = source.c23;
            buffer[4] = (char)source.c45;
        }
    }


    // Copy string with 6 characters.
    static unsafe void Copy6Chars(SmallStringSource source, Span<char> buffer)
    {
        fixed (char* p = buffer)
        {
            var cPtr = (uint*)p;
            *(cPtr++) = source.c01;
            *(cPtr++) = source.c23;
            *cPtr = source.c45;
        }
    }


    // Copy string with 7 characters.
    static unsafe void Copy7Chars(SmallStringSource source, Span<char> buffer)
    {
        fixed (char* p = buffer)
        {
            var cPtr = (uint*)p;
            *(cPtr++) = source.c01;
            *(cPtr++) = source.c23;
            *cPtr = source.c45;
            buffer[6] = (char)source.c67;
        }
    }


    // Copy string with 8 characters.
    static unsafe void Copy8Chars(SmallStringSource source, Span<char> buffer)
    {
        fixed (char* p = buffer)
        {
            var cPtr = (uint*)p;
            *(cPtr++) = source.c01;
            *(cPtr++) = source.c23;
            *(cPtr++) = source.c45;
            *cPtr = source.c67;
        }
    }


    // Initialize with 1 character.
    static void InitWith1Char(SmallStringSource source, ReadOnlySpan<char> s)
    {
        source.c01 = s[0];
    }


    // Initialize with 2 characters.
    static unsafe void InitWith2Chars(SmallStringSource source, ReadOnlySpan<char> s)
    {
        fixed (char* p = s)
        {
            source.c01 = *(uint*)p;
        }
    }


    // Initialize with 3 characters.
    static unsafe void InitWith3Chars(SmallStringSource source, ReadOnlySpan<char> s)
    {
        fixed (char* p = s)
        {
            var cPtr = (uint*)p;
            source.c01 = *cPtr++;
            source.c23 = s[2];
        }
    }


    // Initialize with 4 characters.
    static unsafe void InitWith4Chars(SmallStringSource source, ReadOnlySpan<char> s)
    {
        fixed (char* p = s)
        {
            var cPtr = (uint*)p;
            source.c01 = *cPtr++;
            source.c23 = *cPtr;
        }
    }


    // Initialize with 5 characters.
    static unsafe void InitWith5Chars(SmallStringSource source, ReadOnlySpan<char> s)
    {
        fixed (char* p = s)
        {
            var cPtr = (uint*)p;
            source.c01 = *cPtr++;
            source.c23 = *cPtr;
            source.c45 = s[4];
        }
    }


    // Initialize with 6 characters.
    static unsafe void InitWith6Chars(SmallStringSource source, ReadOnlySpan<char> s)
    {
        fixed (char* p = s)
        {
            var cPtr = (uint*)p;
            source.c01 = *cPtr++;
            source.c23 = *cPtr++;
            source.c45 = *cPtr;
        }
    }


    // Initialize with 7 characters.
    static unsafe void InitWith7Chars(SmallStringSource source, ReadOnlySpan<char> s)
    {
        fixed (char* p = s)
        {
            var cPtr = (uint*)p;
            source.c01 = *cPtr++;
            source.c23 = *cPtr++;
            source.c45 = *cPtr;
            source.c67 = s[6];
        }
    }


    // Initialize with 8 characters.
    static unsafe void InitWith8Chars(SmallStringSource source, ReadOnlySpan<char> s)
    {
        fixed (char* p = s)
        {
            var cPtr = (uint*)p;
            source.c01 = *cPtr++;
            source.c23 = *cPtr++;
            source.c45 = *cPtr++;
            source.c67 = *cPtr;
        }
    }


    /// <inheritdoc/>
    public int Length { get; }


    /// <inheritdoc/>
    public override string ToString()
    {
        if (this.Length == 0)
            return "";
        using var _ = CharArrayPoolLock.EnterScope();
        var buffer = CharArrayPool.Rent(MaxLength);
        this.TryCopyTo(buffer.AsSpan());
        try
        {
            return new string(buffer, 0, this.Length);
        }
        finally
        {
            CharArrayPool.Return(buffer);
        }
    }


    /// <inheritdoc/>
    public unsafe bool TryCopyTo(Span<char> buffer)
    {
        var length = this.Length;
        if (length == 0)
            return true;
        if (buffer.IsEmpty)
            return false;
        CopyCharsFuncs[Math.Min(length, buffer.Length)](this, buffer);
        return true;
    }
}