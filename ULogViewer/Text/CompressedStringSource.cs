using CarinaStudio.Diagnostics;
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Unicode;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Implementation of <see cref="IStringSource"/> which stores string as compressed data.
/// </summary>
public class CompressedStringSource : IStringSource
{
    // Static fields.
    static readonly long BaseByteCount = Memory.EstimateInstanceSize<CompressedStringSource>();
    [ThreadStatic] 
    static ArrayPool<byte>? ByteArrayPool;
    [ThreadStatic] 
    static ArrayPool<char>? CharArrayPool;
    [ThreadStatic]
    static MemoryStream? CompressionMemoryStream;
    [ThreadStatic]
    static MemoryStream? DecompressionMemoryStream;
    
    
    // Fields.
    readonly byte[]? data;
    readonly uint flags;


    /// <summary>
    /// Initialize new instance of <see cref="CompressedStringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public CompressedStringSource(string s) : this(s.AsSpan())
    { }


    /// <summary>
    /// Initialize new instance of <see cref="CompressedStringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public CompressedStringSource(ReadOnlyMemory<char> s) : this(s.Span)
    { }
    
    
    /// <summary>
    /// Initialize new instance of <see cref="CompressedStringSource"/>.
    /// </summary>
    /// <param name="s">String.</param>
    public CompressedStringSource(ReadOnlySpan<char> s)
    {
        this.Length = s.Length;
        if (this.Length > 0)
        {
            ByteArrayPool ??= ArrayPool<byte>.Create();
            var utf8ByteCount = Encoding.UTF8.GetByteCount(s);
            var utf8 = ByteArrayPool.Rent(utf8ByteCount);
            Utf8.FromUtf16(s, utf8.AsSpan(), out _, out _);
            CompressionMemoryStream ??= new();
            using (var stream = new DeflateStream(CompressionMemoryStream, CompressionLevel.SmallestSize, true))
                stream.Write(utf8);
            this.data = CompressionMemoryStream.ToArray();
            if (this.data.Length < utf8ByteCount)
            {
                this.flags = 0x80000000u | (uint)utf8ByteCount;
                ByteArrayPool.Return(utf8);
            }
            else
            {
                this.data = utf8;
                this.flags = (uint)utf8ByteCount;
            }
            CompressionMemoryStream.SetLength(0);
        }
    }
    
    
    /// <inheritdoc/>
    public long ByteCount => BaseByteCount + (this.data is null ? 0 : Memory.EstimateArrayInstanceSize<byte>(this.data.Length));
    
    
    /// <inheritdoc/>
    public int Length { get; }


    /// <inheritdoc/>
    public override string ToString()
    {
        if (this.data is null)
            return "";
        CharArrayPool ??= ArrayPool<char>.Create();
        var buffer = CharArrayPool.Rent(this.Length);
        try
        {
            if (this.TryCopyTo(buffer.AsSpan()))
                return new string(buffer);
            return "";
        }
        finally
        {
            CharArrayPool.Return(buffer);
        }
    }


    /// <inheritdoc/>
    public bool TryCopyTo(Span<char> buffer)
    {
        if (this.data is null)
            return true;
        if (buffer.IsEmpty)
            return false;
        byte[]? utf8;
        int utf8ByteCount;
        ByteArrayPool ??= ArrayPool<byte>.Create();
        var bufferPool = ByteArrayPool;
        if ((this.flags & 0x80000000u) == 0)
        {
            utf8 = this.data;
            utf8ByteCount = utf8.Length;
        }
        else
        {
            DecompressionMemoryStream ??= new();
            DecompressionMemoryStream.Write(this.data);
            DecompressionMemoryStream.Position = 0;
            utf8ByteCount = (int)(this.flags & 0x7fffffffu);
            utf8 = new DeflateStream(DecompressionMemoryStream, CompressionMode.Decompress, true).Use(stream =>
            {
                var bufferLength = utf8ByteCount;
                var buffer = bufferPool.Rent(bufferLength);
                var totalReadCount = stream.Read(buffer);
                while (totalReadCount < utf8ByteCount && bufferLength < (utf8ByteCount << 1) && bufferLength + 64 < int.MaxValue)
                {
                    bufferLength += 64;
                    var newBuffer = bufferPool.Rent(bufferLength);
                    Array.Copy(buffer, newBuffer, totalReadCount);
                    bufferPool.Return(buffer);
                    buffer = newBuffer;
                    totalReadCount += stream.Read(buffer, totalReadCount, bufferLength - totalReadCount);
                }
                return buffer;
            });
            DecompressionMemoryStream.SetLength(0);
        }
        Utf8.ToUtf16(utf8.AsSpan(0, utf8ByteCount), buffer, out _, out _);
        if (!ReferenceEquals(this.data, utf8))
            bufferPool.Return(utf8);
        return true;
    }
}