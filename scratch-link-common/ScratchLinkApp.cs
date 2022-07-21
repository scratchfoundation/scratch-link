// <copyright file="ScratchLinkApp.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System;
using Microsoft.Extensions.DependencyInjection;
using ScratchLink.BLE;

/// <summary>
/// Main entry point for Scratch Link and central service provider for dependency injection.
/// </summary>
public class ScratchLinkApp
{
    private const int WebSocketPort = 20111;

    private readonly SessionManager sessionManager;
    private readonly WebSocketListener webSocketListener;

    private ScratchLinkApp(IServiceProvider platformServicesProvider)
    {
        this.Services = platformServicesProvider;
        if (Current != null)
        {
            throw new InvalidOperationException("Attempt to create a second app instance");
        }

        Current = this;

        this.sessionManager = this.Services.GetService<SessionManager>();
        this.webSocketListener = new ()
        {
            OnWebSocketConnection = this.sessionManager.ClientDidConnect,
        };
    }

    /// <summary>
    /// Gets the current app instance.
    /// </summary>
    public static ScratchLinkApp Current { get; private set; }

    /// <summary>
    /// Gets the platform-specific services provider.
    /// This provides access to services like the session manager or GATT helpers.
    /// </summary>
    public IServiceProvider Services { get; private set; }

    /// <summary>
    /// Run the app.
    /// </summary>
    public void Run()
    {
        this.webSocketListener.Start(string.Format("http://0.0.0.0:{0}/", WebSocketPort));
    }

    /// <summary>
    /// Quit the app.
    /// </summary>
    public void Quit()
    {
        this.webSocketListener.Stop();
        this.sessionManager.EndAllSessions();
    }

    /// <summary>
    /// Builds a Scratch Link app instance.
    /// Fills the role of the .NET generic host or <c>MauiAppBuilder</c>.
    /// </summary>
    public class Builder
    {
        private string[] arguments;
        private Type sessionManagerType;
        private Type gattHelpersBaseType;
        private Type gattHelpersType;

        /// <summary>
        /// Sets the arguments which will be passed to the app host.
        /// Must be called before Build().
        /// </summary>
        /// <param name="arguments">Command line arguments from app invocation.</param>
        public void SetArguments(string[] arguments)
        {
            this.arguments = arguments;
        }

        /// <summary>
        /// Sets the type which will be used to build the platform-specific session manager.
        /// </summary>
        /// <typeparam name="TSessionManager">The platform-specific session manager type.</typeparam>
        internal void SetSessionManager<TSessionManager>()
            where TSessionManager : SessionManager
        {
            this.sessionManagerType = typeof(TSessionManager);
        }

        /// <summary>
        /// Sets the types which will be used to build the BLE GATT helpers.
        /// </summary>
        /// <typeparam name="TGattHelpers">The platform-specific GATT helpers type.</typeparam>
        /// <typeparam name="TUUID">The platform-specific type for BLE UUID values.</typeparam>
        internal void SetGattHelpers<TGattHelpers, TUUID>()
            where TGattHelpers : GattHelpers<TUUID>
        {
            this.gattHelpersBaseType = typeof(GattHelpers<TUUID>);
            this.gattHelpersType = typeof(TGattHelpers);
        }

        /// <summary>
        /// Builds a Scratch Link app host.
        /// </summary>
        /// <returns>A new Scratch Link app host.</returns>
        internal ScratchLinkApp Build()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProviderOptions = new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true };
            var servicesProvider = new DefaultServiceProviderFactory(serviceProviderOptions)
                .CreateBuilder(serviceCollection)
                .AddSingleton(typeof(SessionManager), this.sessionManagerType)
                .AddSingleton(this.gattHelpersBaseType, this.gattHelpersType)
                .BuildServiceProvider();
            return new ScratchLinkApp(servicesProvider);
        }
    }
}
