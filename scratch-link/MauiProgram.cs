// <copyright file="MauiProgram.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

/// <summary>
/// This class hosts the cross-platform entry point.
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Create and return a MauiAppBuilder which will build an instance to host our app.
    /// </summary>
    /// <returns>A new instance of <see cref="MauiApp"/> configured for our app.</returns>
    public static MauiAppBuilder CreateMauiAppBuilder()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<ScratchLinkApp>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        return builder;
    }
}
