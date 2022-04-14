using System;
using System.Collections;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Collections;

/// <summary>
/// Extensions for <see cref="IList{T}"/>.
/// </summary>
static class ListExtensions
{
    // Enumerator of range of list.
    class RangeEnumerator<T> : IEnumerator<T>, IEnumerable<T>
    {
        // Fields.
        int currentIndex = -1;
        readonly int endIndex;
        readonly IList<T> list;
        readonly int startIndex;

        // Constructor.
        public RangeEnumerator(IList<T> list, int startIndex, int count)
        {
            this.endIndex = startIndex + count;
            this.list = list;
            this.startIndex = startIndex;
        }

        /// <inheritdoc/>
        public T Current 
        { 
            get
            {
                if (this.currentIndex >= this.startIndex && this.currentIndex < this.endIndex)
                    return this.list[this.currentIndex];
                throw new InvalidOperationException();
            } 
        }

        /// <inheritdoc/>
        public void Dispose() =>
            this.currentIndex = this.endIndex;
        
        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator() => this;
        
        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (this.currentIndex < 0)
                this.currentIndex = this.startIndex;
            else if (this.currentIndex < int.MaxValue)
                ++this.currentIndex;
            if (this.currentIndex >= this.endIndex)
                return false;
            return true;
        }

        // Interface implementations.
        IEnumerator IEnumerable.GetEnumerator() => this;
        object? IEnumerator.Current => this.Current;
        bool IEnumerator.MoveNext() => this.MoveNext();
        void IEnumerator.Reset()
        { }
    }


    /// <summary>
    /// Get sub-range of given list as <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <param name="list">List.</param>
    /// <param name="index">Start index of range.</param>
    /// <param name="count">Number of elements in range.</param>
    /// <returns><see cref="IEnumerable{T}"/>.</returns>
    public static IEnumerable<T> GetRangeAsEnumerable<T>(this IList<T> list, int index, int count)
    {
        if (index < 0 || index > list.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (count < 0 || index + count > list.Count)
            throw new ArgumentOutOfRangeException(nameof(count));
        return new RangeEnumerator<T>(list, index, count);
    }
}