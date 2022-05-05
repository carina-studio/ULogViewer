using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.Collections;

/// <summary>
/// Read-only <see cref="IList{T}"/> which returns Null or default value if getting item out of range.
/// </summary>
/// <typeparam name="T">Type of element.</typeparam>
class SafeReadOnlyList<T> : IList, IList<T>, INotifyCollectionChanged, INotifyPropertyChanged, IReadOnlyList<T>
{
    // Fields.
    readonly IList<T> list;


    /// <summary>
    /// Initialize new <see cref="SafeReadOnlyList{T}"/> instance.
    /// </summary>
    /// <param name="list">List to be wrapped.</param>
    public SafeReadOnlyList(IList<T> list)
    {
        this.list = list;
        if (list is INotifyCollectionChanged notifyCollectionChanged)
            notifyCollectionChanged.CollectionChanged += (_, e) => this.CollectionChanged?.Invoke(this, e);
        if (list is INotifyPropertyChanged notifyPropertyChanged)
            notifyPropertyChanged.PropertyChanged += (_, e) => this.PropertyChanged?.Invoke(this, e);
    }


    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;


    /// <inheritdoc/>
    public bool Contains(T item) => 
        this.list.Contains(item);
    

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex) =>
        this.list.CopyTo(array, arrayIndex);


    /// <inheritdoc/>
    public int Count { get => this.list.Count; }


    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() =>
        this.list.GetEnumerator();


    /// <inheritdoc/>
    public int IndexOf(T item) => 
        this.list.IndexOf(item);
    

    /// <inheritdoc/>
    public bool IsReadOnly { get => true; }


    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;


    /// <summary>
    /// Get item in the list.
    /// </summary>
    public T? this[int index]
    {
        get
        {
            if (index >= 0 && index < this.list.Count)
                return this.list[index];
            return default;
        }
    }


    // Interface implementations.
    int IList.Add(object? item) =>
        throw new InvalidOperationException();
    void ICollection<T>.Add(T item) =>
        throw new InvalidOperationException();
    void IList.Clear() =>
        throw new InvalidOperationException();
    void ICollection<T>.Clear() =>
        throw new InvalidOperationException();
    bool IList.Contains(object? item) => 
        item is T e && this.list.Contains(e);
    void ICollection.CopyTo(Array array, int arrayIndex)
    {
        var typedArray = new T[this.Count];
        this.list.CopyTo(typedArray, 0);
        for (var i = 0; i < typedArray.Length; ++i, ++arrayIndex)
            array.SetValue(typedArray[i], arrayIndex);
    }
    IEnumerator IEnumerable.GetEnumerator() =>
        this.list.GetEnumerator();
    int IList.IndexOf(object? item) => 
        item is T e ? this.list.IndexOf(e) : -1;
    void IList.Insert(int index, object? item) =>
        throw new InvalidOperationException();
    void IList<T>.Insert(int index, T item) =>
        throw new InvalidOperationException();
    bool IList.IsFixedSize { get => false; }
    bool ICollection.IsSynchronized { get => false; }
    void IList.Remove(object? item) =>
        throw new InvalidOperationException();
    bool ICollection<T>.Remove(T item) =>
        throw new InvalidOperationException();
    void IList.RemoveAt(int index) =>
        throw new InvalidOperationException();
    void IList<T>.RemoveAt(int index) =>
        throw new InvalidOperationException();
    object ICollection.SyncRoot { get => this; }
    object? IList.this[int index]
    {
        get => this[index];
        set => throw new InvalidOperationException();
    }
#pragma warning disable CS8768
    T? IList<T>.this[int index]
    {
        get => this[index];
        set => throw new InvalidOperationException();
    }
#pragma warning restore CS8768
#pragma warning disable CS8603
    T IReadOnlyList<T>.this[int index]
    {
        get => this[index];
    }
#pragma warning restore CS8603
}