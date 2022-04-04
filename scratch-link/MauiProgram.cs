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
    /// Build and return a MauiApp instance to host our app.
    /// </summary>
    /// <returns>A new instance of <see cref="MauiApp"/> configured for our app.</returns>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        return builder.Build();
    }
}
