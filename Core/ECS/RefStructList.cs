﻿using System.Diagnostics;

namespace Core.ECS;

public struct RefStructList<T> where T : struct
{
    public ref T this[ulong index]
    {
        get
        {
            // Following trick can reduce the range check by one
            if (index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return ref _data[index];
        }
    }

    public bool IsReadOnly => _data.IsReadOnly;
    public ulong Count { get; private set; }

    // Gets and sets the capacity of this list.  The capacity is the size of
    // the internal array used to hold items.  When set, the internal
    // array of the list is reallocated to the given capacity.
    //
    public ulong Capacity
    {
        get => (ulong)_data.Length;
        set
        {
            if (value < Count) throw new ArgumentOutOfRangeException(nameof(value));

            if (value == (ulong)_data.Length) return;

            if (value > 0)
            {
                var newItems = new T[value];
                if (Count > 0) Array.Copy(_data, newItems, (long)Count);
                _data = newItems;
            }
            else
            {
                _data = DefaultArray;
            }
        }
    }

    private const ulong DefaultCapacity = 10;

    private static readonly T[] DefaultArray = new T[DefaultCapacity];
    private T[] _data;

    public RefStructList()
    {
        Count = 0;
        _data = DefaultArray;
        Capacity = DefaultCapacity;
    }

    public void Add(in T item)
    {
        if (Count < (ulong)_data.LongLength)
        {
            _data[Count] = item;
            Count++;
        }
        else
        {
            var size = Count;
            Grow(size + 1);
            Count = size + 1;
            _data[size] = item;
        }
    }

    public void RemoveAt(ulong index)
    {
        if (index >= Count) throw new ArgumentOutOfRangeException(nameof(index));

        Count--;
        if (index < Count) Array.Copy(_data, (long)index + 1, _data, (long)index, (long)(Count - index));
    }

    public void Clear()
    {
        Count = 0;
    }

    /// <summary>
    ///     Increase the capacity of this list to at least the specified <paramref name="capacity" />.
    /// </summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    private void Grow(ulong capacity)
    {
        Debug.Assert((ulong)_data.LongLength < capacity);

        var newCapacity = 2 * (ulong)_data.LongLength;

        // Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
        // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
        if (newCapacity > (ulong)Array.MaxLength) newCapacity = (ulong)Array.MaxLength;

        // If the computed capacity is still less than specified, set to the original argument.
        // Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
        if (newCapacity < capacity) newCapacity = capacity;

        Capacity = newCapacity;
    }
}