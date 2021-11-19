using System.Diagnostics;

namespace Core.ECS;

public struct RefStructList<T> where T : struct
{
    public ref T this[ulong index]
    {
        get
        {
            // Following trick can reduce the range check by one
            if (index >= _size) throw new ArgumentOutOfRangeException(nameof(index));
            return ref _data[index];
        }
    }

    public bool IsReadOnly => _data.IsReadOnly;
    public int Count => (int)_size;

    // Gets and sets the capacity of this list.  The capacity is the size of
    // the internal array used to hold items.  When set, the internal
    // array of the list is reallocated to the given capacity.
    //
    public ulong Capacity
    {
        get => (ulong)_data.Length;
        set
        {
            if (value < _size) throw new ArgumentOutOfRangeException(nameof(value));

            if (value == (ulong)_data.Length) return;

            if (value > 0)
            {
                var newItems = new T[value];
                if (_size > 0) Array.Copy(_data, newItems, (long)_size);
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

    private ulong _size;

    public RefStructList()
    {
        _size = 0;
        _data = DefaultArray;
        Capacity = DefaultCapacity;
    }

    public void Add(ref T item)
    {
        if (_size < (ulong)_data.LongLength)
        {
            _data[_size] = item;
            _size++;
        }
        else
        {
            var size = _size;
            Grow(size + 1);
            _size = size + 1;
            _data[size] = item;
        }
    }

    public void RemoveAt(ulong index)
    {
        if (index >= _size) throw new ArgumentOutOfRangeException(nameof(index));

        _size--;
        if (index < _size) Array.Copy(_data, (long)index + 1, _data, (long)index, (long)(_size - index));
    }

    public void Clear()
    {
        _size = 0;
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