// <copyright file="WinSerialPortEnumerator.cs" company="ALUX">
// Copyright (c) 2026 ALUX, Inc. All rights reserved.
// Based on scratch-link by Scratch Foundation, licensed under AGPL-3.0-only.
// </copyright>

namespace AluxLabs.Link.Win.Serial;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Text.RegularExpressions;
using AluxLabs.Link.Serial;

/// <summary>
/// Enumerates USB serial ports on Windows via WMI (Win32_PnPEntity), extracting
/// the COM port name plus USB VID/PID from the PNPDeviceID. Used by
/// <see cref="WinSerialSession"/> for discovery.
/// </summary>
internal static class WinSerialPortEnumerator
{
    private const string WmiQuery =
        "SELECT DeviceID, PNPDeviceID, Caption, Name FROM Win32_PnPEntity " +
        "WHERE PNPClass = 'Ports' AND PNPDeviceID LIKE 'USB%'";

    private static readonly Regex VidPidRegex = new (
        @"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ComPortRegex = new (
        @"\((COM\d+)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Synchronously query WMI for USB serial ports matching any of the given filters.
    /// </summary>
    /// <param name="filters">Filter list. Empty means "return all matching USB serial ports".</param>
    /// <returns>List of matching ports. May be empty.</returns>
    public static IReadOnlyList<WinSerialPortInfo> Query(IReadOnlyList<SerialDiscoveryFilter> filters)
    {
        var results = new List<WinSerialPortInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher(WmiQuery);
            using var collection = searcher.Get();

            foreach (var item in collection)
            {
                using var mo = (ManagementObject)item;
                var info = BuildPortInfo(mo);
                if (info == null)
                {
                    continue;
                }

                if (!MatchesAnyFilter(info, filters))
                {
                    continue;
                }

                results.Add(info);
            }
        }
        catch (ManagementException e)
        {
            Trace.WriteLine($"WMI query failed during serial port enumeration: {e}");
        }
        catch (Exception e)
        {
            Trace.WriteLine($"Unexpected error during serial port enumeration: {e}");
        }

        return results;
    }

    private static WinSerialPortInfo BuildPortInfo(ManagementObject mo)
    {
        var pnpId = mo["PNPDeviceID"] as string ?? string.Empty;
        var caption = mo["Caption"] as string ?? string.Empty;
        var name = mo["Name"] as string ?? caption;

        var comMatch = ComPortRegex.Match(caption);
        if (!comMatch.Success)
        {
            // No COM port number means this isn't a usable serial port from our point of view.
            return null;
        }

        int? vendorId = null;
        int? productId = null;
        var vidPidMatch = VidPidRegex.Match(pnpId);
        if (vidPidMatch.Success)
        {
            vendorId = int.Parse(vidPidMatch.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            productId = int.Parse(vidPidMatch.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return new WinSerialPortInfo
        {
            Path = comMatch.Groups[1].Value,
            DisplayName = name,
            VendorId = vendorId,
            ProductId = productId,
            PnpDeviceId = pnpId,
        };
    }

    private static bool MatchesAnyFilter(WinSerialPortInfo info, IReadOnlyList<SerialDiscoveryFilter> filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return true;
        }

        foreach (var filter in filters)
        {
            if (filter.UsbVendorId.HasValue && filter.UsbVendorId.Value != info.VendorId)
            {
                continue;
            }

            if (filter.UsbProductId.HasValue && filter.UsbProductId.Value != info.ProductId)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(filter.PathHint))
            {
                if (info.Path == null ||
                    info.Path.IndexOf(filter.PathHint, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
            }

            return true;
        }

        return false;
    }
}
