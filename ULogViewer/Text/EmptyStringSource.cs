using CarinaStudio.Diagnostics;
using System;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Implementation of <see cref="IStringSource"/> which contains empty string.
/// </summary>
class EmptyStringSource : IStringSource
{
    // Static fields.
    static readonly long BaseByteCount = Memory.EstimateInstanceSize<EmptyStringSource>();


    /// <inheritdoc/>
    public long ByteCount => BaseByteCount;

    
    /// <inheritdoc/>
    public int Length => 0;


    /// <inheritdoc/>
    public override string ToString() => "";
    
    
    /// <inheritdoc/>
    public bool TryCopyTo(Span<char> buffer) => true;
}