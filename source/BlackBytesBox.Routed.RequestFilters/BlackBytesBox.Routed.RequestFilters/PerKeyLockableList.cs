using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;

namespace BlackBytesBox.Routed.RequestFilters
{
    /// <summary>
    /// Provides a thread-safe collection that maps a key (string) to a list of items, 
    /// using a per‑key asynchronous lock to allow non-blocking access across different keys.
    /// </summary>
    /// <typeparam name="T">The type of items stored in the lists.</typeparam>
    public class PerKeyLockableList<T>
    {
        // The dictionary holding the lists for each key.
        private readonly ConcurrentDictionary<string, List<T>> _data = new();

        // The dictionary holding a SemaphoreSlim for each key.
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

        /// <summary>
        /// Gets or creates a SemaphoreSlim lock for the given key.
        /// </summary>
        /// <param name="key">The key for which to get the lock.</param>
        /// <returns>A SemaphoreSlim instance used to protect operations on that key.</returns>
        private SemaphoreSlim GetLockForKey(string key)
        {
            return _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        }

        /// <summary>
        /// Asynchronously adds an item to the list associated with the specified key.
        /// If the key does not exist, a new list is created.
        /// </summary>
        /// <param name="key">The key to which the item should be added.</param>
        /// <param name="item">The item to add.</param>
        public async Task AddAsync(string key, T item)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var keyLock = GetLockForKey(key);
            await keyLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_data.TryGetValue(key, out var list))
                {
                    list = new List<T>();
                    _data[key] = list;
                }
                list.Add(item);
            }
            finally
            {
                keyLock.Release();
            }
        }

        /// <summary>
        /// Asynchronously retrieves a snapshot of the list associated with the specified key.
        /// A new array is returned so that the caller can iterate without blocking modifications.
        /// </summary>
        /// <param name="key">The key whose list is to be retrieved.</param>
        /// <returns>A read-only snapshot of the list, or an empty array if the key does not exist.</returns>
        public async Task<IReadOnlyList<T>> GetAsync(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var keyLock = GetLockForKey(key);
            await keyLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_data.TryGetValue(key, out var list))
                {
                    // Return a snapshot to avoid holding the lock while the caller enumerates.
                    return list.ToArray();
                }
                else
                {
                    return Array.Empty<T>();
                }
            }
            finally
            {
                keyLock.Release();
            }
        }

        /// <summary>
        /// Asynchronously retrieves a snapshot of all keys and their associated lists.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a dictionary
        /// mapping each key to a read-only snapshot of its list.
        /// </returns>
        /// <remarks>
        /// This method iterates over a snapshot of the keys, acquiring each key’s lock in turn to produce a safe copy.
        /// This approach avoids holding multiple locks concurrently, thereby reducing deadlock risks.
        /// </remarks>
        public async Task<Dictionary<string, IReadOnlyList<T>>> GetAllAsync()
        {
            var snapshot = new Dictionary<string, IReadOnlyList<T>>();
            // Take a snapshot of the keys to prevent issues with concurrent modifications.
            var keys = _data.Keys.ToList();
            foreach (var key in keys)
            {
                var keyLock = GetLockForKey(key);
                await keyLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (_data.TryGetValue(key, out var list))
                    {
                        // Copy the list to avoid exposing internal state.
                        snapshot[key] = list.ToArray();
                    }
                }
                finally
                {
                    keyLock.Release();
                }
            }
            return snapshot;
        }


        /// <summary>
        /// Asynchronously updates the list associated with the specified key using an asynchronous update function.
        /// If the key does not exist, a new list is created.
        /// </summary>
        /// <param name="key">The key whose list should be updated.</param>
        /// <param name="updateFunc">
        /// An asynchronous delegate that receives the list associated with the key for updating.
        /// </param>
        public async Task UpdateAsync(string key, Func<List<T>, Task> updateFunc)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (updateFunc == null) throw new ArgumentNullException(nameof(updateFunc));

            var keyLock = GetLockForKey(key);
            await keyLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_data.TryGetValue(key, out var list))
                {
                    list = new List<T>();
                    _data[key] = list;
                }
                await updateFunc(list).ConfigureAwait(false);
            }
            finally
            {
                keyLock.Release();
            }
        }

        /// <summary>
        /// Asynchronously updates the list associated with the specified key using a synchronous update action.
        /// If the key does not exist, a new list is created.
        /// </summary>
        /// <param name="key">The key whose list should be updated.</param>
        /// <param name="updateAction">
        /// A synchronous delegate that receives the list associated with the key for updating.
        /// </param>
        public async Task UpdateAsync(string key, Action<List<T>> updateAction)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));

            var keyLock = GetLockForKey(key);
            await keyLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_data.TryGetValue(key, out var list))
                {
                    list = new List<T>();
                    _data[key] = list;
                }
                updateAction(list);
            }
            finally
            {
                keyLock.Release();
            }
        }

        /// <summary>
        /// Asynchronously removes an item from the list associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose list the item should be removed from.</param>
        /// <param name="item">The item to remove.</param>
        /// <returns>True if the item was found and removed; otherwise, false.</returns>
        public async Task<bool> RemoveAsync(string key, T item)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var keyLock = GetLockForKey(key);
            await keyLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_data.TryGetValue(key, out var list))
                {
                    return list.Remove(item);
                }
                return false;
            }
            finally
            {
                keyLock.Release();
            }
        }
    }

    /// <summary>
    /// Provides a thread‑safe collection that maps a key (string) to a value of type <typeparamref name="T"/>,
    /// using a per‑key asynchronous lock to allow non‑blocking access across different keys.
    /// </summary>
    /// <typeparam name="T">The type of value stored.</typeparam>
    public class PerKeyLockable<T>
    {
        // The dictionary holding the values for each key.
        private readonly ConcurrentDictionary<string, T> _data = new();

        // The dictionary holding a SemaphoreSlim for each key.
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

        /// <summary>
        /// Gets or creates a SemaphoreSlim lock for the given key.
        /// </summary>
        /// <param name="key">The key for which to get the lock.</param>
        /// <returns>A SemaphoreSlim instance used to protect operations on that key.</returns>
        private SemaphoreSlim GetLockForKey(string key)
        {
            return _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        }

        /// <summary>
        /// Asynchronously retrieves the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose value is to be retrieved.</param>
        /// <returns>
        /// The value associated with the key, or the default value of <typeparamref name="T"/> if the key does not exist.
        /// </returns>
        public async Task<T> GetAsync(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var keyLock = GetLockForKey(key);
            await keyLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return _data.TryGetValue(key, out T value) ? value : default;
            }
            finally
            {
                keyLock.Release();
            }
        }

        /// <summary>
        /// Asynchronously retrieves a snapshot of all keys and their associated values.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a dictionary
        /// mapping each key to its stored value.
        /// </returns>
        /// <remarks>
        /// Similar to the list version, this method iterates over a snapshot of keys and acquires each key’s lock
        /// to safely copy its value. It prevents potential deadlocks by locking one key at a time.
        /// </remarks>
        public async Task<Dictionary<string, T>> GetAllAsync()
        {
            var snapshot = new Dictionary<string, T>();
            var keys = _data.Keys.ToList();
            foreach (var key in keys)
            {
                var keyLock = GetLockForKey(key);
                await keyLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (_data.TryGetValue(key, out var value))
                    {
                        snapshot[key] = value;
                    }
                }
                finally
                {
                    keyLock.Release();
                }
            }
            return snapshot;
        }


        /// <summary>
        /// Asynchronously sets the value for the specified key.
        /// </summary>
        /// <param name="key">The key whose value is to be set.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SetAsync(string key, T value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var keyLock = GetLockForKey(key);
            await keyLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _data[key] = value;
            }
            finally
            {
                keyLock.Release();
            }
        }

        /// <summary>
        /// Asynchronously updates the value associated with the specified key using an asynchronous update function.
        /// If the key does not exist, the default value of <typeparamref name="T"/> is used.
        /// </summary>
        /// <param name="key">The key whose value is to be updated.</param>
        /// <param name="updateFunc">
        /// An asynchronous delegate that receives the current value and returns an updated value.
        /// </param>
        /// <returns>A task representing the asynchronous update operation.</returns>
        public async Task UpdateAsync(string key, Func<T, Task<T>> updateFunc)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (updateFunc == null) throw new ArgumentNullException(nameof(updateFunc));

            var keyLock = GetLockForKey(key);
            await keyLock.WaitAsync().ConfigureAwait(false);
            try
            {
                T current = _data.TryGetValue(key, out var value) ? value : default;
                T updated = await updateFunc(current).ConfigureAwait(false);
                _data[key] = updated;
            }
            finally
            {
                keyLock.Release();
            }
        }

        /// <summary>
        /// Asynchronously updates the value associated with the specified key using a synchronous update function.
        /// If the key does not exist, the default value of <typeparamref name="T"/> is used.
        /// </summary>
        /// <param name="key">The key whose value is to be updated.</param>
        /// <param name="updateFunc">
        /// A synchronous delegate that receives the current value and returns an updated value.
        /// </param>
        /// <returns>A task representing the asynchronous update operation.</returns>
        public async Task UpdateAsync(string key, Func<T, T> updateFunc)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (updateFunc == null) throw new ArgumentNullException(nameof(updateFunc));

            var keyLock = GetLockForKey(key);
            await keyLock.WaitAsync().ConfigureAwait(false);
            try
            {
                T current = _data.TryGetValue(key, out var value) ? value : default;
                T updated = updateFunc(current);
                _data[key] = updated;
            }
            finally
            {
                keyLock.Release();
            }
        }

        /// <summary>
        /// Asynchronously removes the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose value is to be removed.</param>
        /// <returns>True if the key existed and was removed; otherwise, false.</returns>
        public async Task<bool> RemoveAsync(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var keyLock = GetLockForKey(key);
            await keyLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return _data.TryRemove(key, out _);
            }
            finally
            {
                keyLock.Release();
            }
        }
    }
}
