// <copyright file="ContainerExtensions.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Extensions;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Extensions for use with containers or other enumerables.
/// </summary>
internal static class ContainerExtensions
{
    /// <summary>
    /// If this enumerable is null, return an empty enumerable of the same type.
    /// Otherwise, return the enumerable as-is.
    /// </summary>
    /// <typeparam name="T">The type of object enumerated by the enumerable.</typeparam>
    /// <param name="original">The possibly-null enumerable.</param>
    /// <returns>A never-null enumerable.</returns>
    public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T> original) =>
        original ?? Enumerable.Empty<T>();

    /// <summary>
    /// Attempt to retrieve a value and, if that succeeds, cast it. Good for dictionaries containing <c>object</c>.
    /// If the key is not found, the output variable is set to the type's default value.
    /// Adapted from <a href="https://stackoverflow.com/a/63203652">this Stack Overflow answer</a>.
    /// </summary>
    /// <typeparam name="TValueAs">The type to cast the value to.</typeparam>
    /// <typeparam name="TKey">The type of key in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of value in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to look in.</param>
    /// <param name="key">The dictionary to look for.</param>
    /// <param name="value">The variable to receive the casted value. Set to <c>default</c> if the key is not found.</param>
    /// <returns>
    /// True if the key was found, regardless of what happens in the cast, or false if the key was not found.
    /// </returns>
    public static bool TryGetValueAs<TValueAs, TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key, out TValueAs value)
        where TValueAs : TValue
    {
        if (dictionary.TryGetValue(key, out TValue rawValue))
        {
            value = (TValueAs)rawValue;
            return true;
        }

        value = default;
        return false;
    }
}
