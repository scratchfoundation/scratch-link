// <copyright file="BundleInfo.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac;

using System;
using Foundation;

/// <summary>
/// Helper methods to retrieve info from the app's main bundle.
/// </summary>
public static class BundleInfo
{
    private static string defaultTitle = "Scratch Link";
    private static string defaultVersion = "(unknown version)";

    /// <summary>
    /// Gets the app title as specified in the main bundle's <c>CFBundleDisplayName</c> property.
    /// </summary>
    public static string Title => GetMainBundleInfoString("CFBundleDisplayName") ?? defaultTitle;

    /// <summary>
    /// Gets a basic version string for the app, as specified in the main bundle's <c>CFBundleShortVersionString</c> property.
    /// </summary>
    public static string Version => GetMainBundleInfoString("CFBundleShortVersionString") ?? defaultVersion;

    /// <summary>
    /// Gets a build number for the app, as specified in the main bundle's <c>CFBundleVersion</c> property.
    /// </summary>
    public static string BuildNumber => GetMainBundleInfoString("CFBundleVersion") ?? defaultVersion;

    /// <summary>
    /// Gets a string containing detailed version information for the app, as specified in the main bundle's <c>ScratchVersionDetail</c> property.
    /// </summary>
    public static string VersionDetail
    {
        get
        {
            return GetMainBundleInfoString("ScratchVersionDetail") ?? defaultVersion;
        }
    }

    private static string GetMainBundleInfoString(string key)
    {
        var value = NSBundle.MainBundle.ObjectForInfoDictionary(key);
        return value as NSString;
    }
}
