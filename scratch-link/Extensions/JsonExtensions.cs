// <copyright file="JsonExtensions.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Extensions;

using System.Text.Json;

/// <summary>
/// Extensions for use with <see cref="System.Text.Json"/>.
/// </summary>
public static class JsonExtensions
{
    /// <summary>
    /// Check if the argument is truthy in a JavaScript sense.
    /// </summary>
    /// <param name="element">The JSON element to check.</param>
    /// <returns>True if JavaScript considers the element truthy, false otherwise.</returns>
    public static bool IsTruthy(this JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object or JsonValueKind.True => true,
            JsonValueKind.Array => element.GetArrayLength() > 0,
            JsonValueKind.String => element.GetString().Length > 0,
            JsonValueKind.Number => element.GetDouble() != 0,
            _ => false,
        };
}
