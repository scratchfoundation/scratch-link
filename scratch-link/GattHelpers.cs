// <copyright file="GattHelpers.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using ScratchLink.JsonRpc;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Helper methods to deal with GATT names and UUID values.
/// Most methods correspond to a similarly named item in the Web Bluetooth specification.
/// See <a href="https://webbluetoothcg.github.io/web-bluetooth/">here</a> for more info.
/// </summary>
internal static class GattHelpers
{
    /// <summary>
    /// Resolve a Web Bluetooth GATT service name to a canonical UUID.
    /// </summary>
    /// <see cref="ResolveUuidName"/>
    /// <param name="nameToken">A short UUID in integer form, a full UUID, or an assigned number's name.</param>
    /// <returns>The UUID associated with the name.</returns>
    public static Guid GetServiceUuid(JsonElement nameToken) => ResolveUuidName(nameToken, GattData.AssignedServices);

    /// <summary>
    /// Resolve a Web Bluetooth GATT "name" to a canonical UUID, using an assigned numbers table if necessary.
    /// See <a href="https://webbluetoothcg.github.io/web-bluetooth/#resolveuuidname">here</a> for more info.
    /// </summary>
    /// <param name="nameToken">A short UUID in integer form, a full UUID, or the name of an assigned number.</param>
    /// <param name="assignedNumbersTable">The table of assigned numbers to resolve integer names.</param>
    /// <returns>The UUID associated with the token.</returns>
    /// <exception cref="JsonRpc2Exception">Thrown if the name cannot be resolved.</exception>
    public static Guid ResolveUuidName(JsonElement nameToken, IReadOnlyDictionary<string, ushort> assignedNumbersTable)
    {
        if (nameToken.ValueKind == JsonValueKind.Number)
        {
            return CanonicalUuid(nameToken.GetUInt32());
        }

        var name = nameToken.GetString();

        // Web Bluetooth demands an exact match to this regex but the .NET Guid constructor is more permissive.
        // See https://webbluetoothcg.github.io/web-bluetooth/#valid-uuid
        var validGuidRegex = new Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");
        if (validGuidRegex.IsMatch(name))
        {
            return new Guid(name);
        }

        // TODO: does Windows / .NET really have no built-in call for this?
        if (assignedNumbersTable.TryGetValue(name, out var id))
        {
            return CanonicalUuid(id);
        }

        throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams($"unknown or invalid GATT name: {nameToken}"));
    }

    /// <summary>
    /// Generate a full UUID given a 16-bit or 32-bit "short UUID" alias.
    /// See <a href="https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetoothuuid-canonicaluuid">here</a> for
    /// more info.
    /// </summary>
    /// <param name="alias">A 16- or 32-bit UUID alias.</param>
    /// <returns>The associated canonical UUID.</returns>
    public static Guid CanonicalUuid(uint alias)
    {
        return new Guid(alias, 0x0000, 0x1000, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb);
    }
}
