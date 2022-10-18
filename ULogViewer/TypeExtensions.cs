using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace CarinaStudio.ULogViewer;

/// <summary>
/// Extensions for <see cref="Type"/>.
/// </summary>
public static class TypeExtensions
{
    // Static fields.
    static readonly int ObjectHeaderSize = IntPtr.Size << 1; // Object header + Method table pointer
    static readonly ConcurrentDictionary<Type, long> ObjectSizes = new();
    static readonly ConcurrentDictionary<Type, long> StructureSizes = new();


    /// <summary>
    /// Estinate size of array instance in bytes.
    /// </summary>
    /// <param name="elementType">Type of element.</param>
    /// <param name="elementCount">Number of element.</param>
    /// <returns>Size of array instance.</returns>
    public static long EstimateArrayInstanceSize(this Type elementType, long elementCount)
    {
        if (elementCount < 0)
            throw new ArgumentOutOfRangeException(nameof(elementCount));
        var elementSize = elementType.IsValueType
            ? EstimateObjectSizeInternal(elementType)
            : IntPtr.Size;
        return EstimateArrayInstanceSizeInternal(elementSize, elementCount);
    }


    /// <summary>
    /// Estinate size of array instance in bytes.
    /// </summary>
    /// <param name="elementSize">Size of element in bytes.</param>
    /// <param name="elementCount">Number of element.</param>
    /// <returns>Size of array instance.</returns>
    public static long EstimateArrayInstanceSize(long elementSize, long elementCount)
    {
        if (elementSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(elementSize));
        if (elementCount < 0)
            throw new ArgumentOutOfRangeException(nameof(elementCount));
        return EstimateArrayInstanceSizeInternal(elementSize, elementCount);
    }


    // Estimate size of array instance.
    static long EstimateArrayInstanceSizeInternal(long elementSize, long elementCount) =>
        ObjectHeaderSize + IntPtr.Size /* Length */ + (elementSize * elementCount);
    

    /// <summary>
    /// Estinate size of collection instance in bytes.
    /// </summary>
    /// <param name="elementType">Type of element.</param>
    /// <param name="elementCount">Number of element.</param>
    /// <returns>Size of collection instance.</returns>
    public static long EstimateCollectionInstanceSize(this Type elementType, long elementCount)
    {
        if (elementCount < 0)
            throw new ArgumentOutOfRangeException(nameof(elementCount));
        var elementSize = elementType.IsValueType
            ? EstimateObjectSizeInternal(elementType)
            : IntPtr.Size;
        return EstimateCollectionInstanceSizeInternal(elementSize, elementCount);
    }


    /// <summary>
    /// Estinate size of collection instance in bytes.
    /// </summary>
    /// <param name="elementSize">Size of element in bytes.</param>
    /// <param name="elementCount">Number of element.</param>
    /// <returns>Size of collection instance.</returns>
    public static long EstimateCollectionInstanceSize(long elementSize, long elementCount)
    {
        if (elementSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(elementSize));
        if (elementCount < 0)
            throw new ArgumentOutOfRangeException(nameof(elementCount));
        return EstimateCollectionInstanceSizeInternal(elementSize, elementCount);
    }
    

    // Estimate size of collection instance.
    static long EstimateCollectionInstanceSizeInternal(long elementSize, long elementCount) =>
        ObjectHeaderSize + sizeof(int) /* Count */ + EstimateArrayInstanceSizeInternal(elementSize, elementCount);
    

    /// <summary>
    /// Estinate size of instance in bytes.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <returns>Size of instance.</returns>
    public static long EstimateInstanceSize(this Type type)
    {
        if (type.IsValueType)
            return EstimateStructureSizeInternal(type);
        if (type.IsPointer)
            return IntPtr.Size;
        if (type.IsClass)
            return EstimateObjectSizeInternal(type);
        throw new NotSupportedException($"Cannot estimate instance size of {type.Name}.");
    }


    // Estimate size of object.
    static long EstimateObjectSizeInternal(Type type)
    {
        // use cached size
        if (ObjectSizes.TryGetValue(type, out var size))
            return size;
        
        // calculate size
        foreach (var field in type.GetRuntimeFields())
        {
            if (field.IsStatic)
                continue;
            var fieldType = field.FieldType;
            if (fieldType.IsValueType)
                size += EstimateStructureSizeInternal(fieldType);
            else
                size += IntPtr.Size;
        }
        if ((size % IntPtr.Size) != 0)
            size = (size / IntPtr.Size + 1) * IntPtr.Size;
        size += ObjectHeaderSize;
        ObjectSizes.TryAdd(type, size);
        return size;
    }


    // Estimate size of structure.
    static long EstimateStructureSizeInternal(Type type, bool boxed = false)
    {
        // get cached size
        var size = 0L;
        if (type == typeof(bool))
            size = sizeof(bool);
        else if (type == typeof(byte) || type == typeof(sbyte))
            size = 1;
        else if (type == typeof(short) || type == typeof(ushort))
            size = 2;
        else if (type == typeof(int) || type == typeof(uint) || type == typeof(float))
            size = 4;
        else if (type == typeof(long) || type == typeof(ulong) || type == typeof(double))
            size = 8;
        else if (type == typeof(nint) || type == typeof(nuint))
            size = IntPtr.Size;
        else if (type == typeof(decimal))
            size = sizeof(decimal);
        else if (StructureSizes.TryGetValue(type, out var cachedSize))
            size = cachedSize;

        // calculate size
        if (size == 0)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                var fieldType = field.FieldType;
                if (fieldType.IsValueType)
                    size += EstimateStructureSizeInternal(fieldType);
                else
                    size += IntPtr.Size;
            }
            StructureSizes.TryAdd(type, size);
        }
        if (boxed)
        {
            if ((size % IntPtr.Size) != 0)
                return ObjectHeaderSize + ((size / IntPtr.Size) + 1) * IntPtr.Size;
            return ObjectHeaderSize + size;
        }
        return size;
    }
}