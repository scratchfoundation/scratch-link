// <copyright file="IOReturn.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Corresponds to <c>IOReturn</c> in <c>IOKit</c>, also known as <c>kern_return_t</c>.
/// </summary>
public enum IOReturn : int
{
    /// <summary>
    /// Success.
    /// </summary>
    Success = 0,
}

/// <summary>
/// Extensions for <see cref="IOReturn"/>.
/// </summary>
public static class IOReturnExtensions
{
    /// <summary>
    /// Converts an <see cref="IOReturn"/> value to a developer-friendly string.
    /// This uses <c>mach_error_string</c> from the macOS system.
    /// The resulting string might help a developer but they're not usually friendly to lay-people.
    /// </summary>
    /// <param name="ioReturn">The value to convert.</param>
    /// <returns>A developer-readable string corresponding to the error code.</returns>
    public static string ToDebugString(this IOReturn ioReturn)
    {
        var ptr = Mach_error_string(ioReturn);
        var str = Marshal.PtrToStringAuto(ptr);
        return str;
    }

    [DllImport("__Internal", EntryPoint = "mach_error_string")]
    private static extern IntPtr Mach_error_string([MarshalAs(UnmanagedType.I4)] IOReturn ioReturn);
}
