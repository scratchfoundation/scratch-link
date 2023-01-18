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
}
