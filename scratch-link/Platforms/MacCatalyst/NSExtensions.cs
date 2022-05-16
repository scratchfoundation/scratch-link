// <copyright file="NSExtensions.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Platforms.MacCatalyst;

using Foundation;
using ObjCRuntime;

/// <summary>
/// Extensions for NS data types.
/// </summary>
public static class NSExtensions
{
    /// <summary>
    /// Attempt to retrieve a value of a specific type from the dictionary.
    /// The value will be cast using <c>(<typeparamref name="T"/>)<paramref name="value"/></c> which may throw.
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <param name="dict">The dictionary from which to retrieve a value.</param>
    /// <param name="key">The key to look up within the dictionary.</param>
    /// <param name="value">If successful, the value will be placed here.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool TryGetValue<T>(this NSDictionary dict, NSObject key, out T value)
        where T : NSObject
    {
        var returnValue = dict.TryGetValue(key, out var baseValue);
        value = (T)baseValue;
        return returnValue;
    }
}
