// <copyright file="PeripheralSession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fleck;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;

/// <summary>
/// A kind of session which discovers and connects to peripheral devices by some sort of address.
/// One session can search for, connect to, and interact with one peripheral device.
/// Handles address privacy.
/// </summary>
/// <typeparam name="TDiscoveredPeripheral">The type used to track discovered peripheral devices. Passed to <c>DoConnect</c>.</typeparam>
/// <typeparam name="TPeripheralAddress">The type of address (UUID, path, etc.) used by this session to identify a peripheral device.</typeparam>
public abstract class PeripheralSession<TDiscoveredPeripheral, TPeripheralAddress> : Session
    where TDiscoveredPeripheral : class
{
    private readonly Dictionary<TPeripheralAddress, string> peripheralAddressToId = new ();
    private readonly Dictionary<string, TDiscoveredPeripheral> discoveredPeripherals = new ();

    /// <summary>
    /// Initializes a new instance of the <see cref="PeripheralSession{TPeripheral, TPeripheralAddress}"/> class.
    /// </summary>
    /// <param name="webSocket">The WebSocket which this session will use for communication.</param>
    public PeripheralSession(IWebSocketConnection webSocket)
        : base(webSocket)
    {
        this.Handlers["connect"] = this.HandleConnect;
    }

    /// <summary>
    /// Gets a value indicating whether this session is connected to a peripheral device.
    /// </summary>
    protected abstract bool IsConnected { get; }

    /// <summary>
    /// Implement the JSON-RPC "connect" request to connect to a particular peripheral device.
    /// Valid in the discovery state; transitions to connected state on success.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("connect").</param>
    /// <param name="args">
    /// A JSON object containing the ID of a peripheral found by the most recent discovery request.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task<object> HandleConnect(string methodName, JsonElement? args)
    {
        if (this.IsConnected)
        {
            throw JsonRpc2Error.InvalidRequest("cannot connect when already connected").ToException();
        }

        var peripheralId = args?.TryGetProperty("peripheralId")?.GetString();

        if (peripheralId == null)
        {
            throw JsonRpc2Error.InvalidParams("connect request must include peripheralId").ToException();
        }

        var discoveredPeripheral = this.GetDiscoveredPeripheral(peripheralId);

        if (discoveredPeripheral == null)
        {
            throw JsonRpc2Error.InvalidRequest(string.Format("peripheral {0} not available for connection", peripheralId)).ToException();
        }

        return await this.DoConnect(discoveredPeripheral, args);
    }

    /// <summary>
    /// Platform-specific implementation for connecting to a peripheral device.
    /// </summary>
    /// <param name="discoveredPeripheral">The requested peripheral device.</param>
    /// <param name="args">
    /// A JSON object containing the args passed by the client, in case the platform-specific implementation needs them.
    /// </param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    protected abstract Task<object> DoConnect(TDiscoveredPeripheral discoveredPeripheral, JsonElement? args);

    /// <summary>
    /// Store the peripheral in the "discovered peripherals" list using a session-specific peripheral ID.
    /// Storing a peripheral with the same address several times during the same session will result in the same ID each time.
    /// </summary>
    /// <param name="discoveredPeripheral">The peripheral information being registered.</param>
    /// <param name="peripheralAddress">The peripheral device's address.</param>
    /// <returns>An anonymized, session-specific peripheral ID.</returns>
    protected string RegisterPeripheral(TDiscoveredPeripheral discoveredPeripheral, TPeripheralAddress peripheralAddress)
    {
        if (!this.peripheralAddressToId.TryGetValue(peripheralAddress, out var peripheralId))
        {
            peripheralId = Guid.NewGuid().ToString();
            this.peripheralAddressToId[peripheralAddress] = peripheralId;
        }

        this.discoveredPeripherals[peripheralId] = discoveredPeripheral;

        return peripheralId;
    }

    /// <summary>
    /// Retrieve a peripheral registered during discovery.
    /// </summary>
    /// <param name="peripheralId">The anonymized peripheral ID.</param>
    /// <returns>The peripheral if found, otherwise null.</returns>
    protected TDiscoveredPeripheral GetDiscoveredPeripheral(string peripheralId)
    {
        return this.discoveredPeripherals.GetValueOrDefault(peripheralId, null);
    }

    /// <summary>
    /// Clear all peripherals registered during discovery.
    /// The mapping of peripheral address to ID will not be cleared. To clear that, start a new session.
    /// </summary>
    protected void ClearDiscoveredPeripherals()
    {
        this.discoveredPeripherals.Clear();
    }
}
