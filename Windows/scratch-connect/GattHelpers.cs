using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace scratch_connect
{
    /// <summary>
    /// Helper methods to deal with GATT names & UUID values.
    /// Most methods correspond to a similarly named item in the Web Bluetooth specification.
    /// See <a href="https://webbluetoothcg.github.io/web-bluetooth/">here</a> for more info.
    /// </summary>
    internal static class GattHelpers
    {
        /// <summary>
        /// Resolve a Web Bluetooth GATT service name to a canonical UUID.
        /// </summary>
        /// <see cref="ResolveUuidName"/>
        /// <param name="nameToken">A short UUID in integer form, a full UUID, or an assigned number's name</param>
        /// <returns></returns>
        public static Guid GetServiceUuid(JToken nameToken)
        {
            return ResolveUuidName(nameToken, AssignedServices);
        }

        /// <summary>
        /// Resolve a Web Bluetooth GATT "name" to a canonical UUID, using an assigned numbers table if necessary.
        /// See <a href="https://webbluetoothcg.github.io/web-bluetooth/#resolveuuidname">here</a> for more info.
        /// </summary>
        /// <param name="nameToken">A short UUID in integer form, a full UUID, or the name of an assigned number</param>
        /// <param name="assignedNumbersTable">The table of assigned numbers to resolve integer names</param>
        /// <returns>The UUID associated with the token. Throws if not possible.</returns>
        public static Guid ResolveUuidName(JToken nameToken, IReadOnlyDictionary<string, short> assignedNumbersTable)
        {
            if (nameToken.Type == JTokenType.Integer)
            {
                return CanonicalUuid(nameToken.ToObject<int>());
            }

            var name = nameToken.ToObject<string>();

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

            throw JsonRpcException.InvalidParams($"unknown or invalid GATT name: {nameToken}");
        }

        /// <summary>
        /// Generate a full UUID given a 16-bit or 32-bit "short UUID" alias
        /// See <a href="https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetoothuuid-canonicaluuid">here</a> for
        /// more info.
        /// </summary>
        /// <param name="alias">A 16- or 32-bit UUID alias</param>
        /// <returns>The associated canonical UUID</returns>
        public static Guid CanonicalUuid(int alias)
        {
            return new Guid(alias, 0x0000, 0x1000, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb);
        }

                /// <summary>
        /// Table of well-known GATT service UUIDs.
        /// See <a href="https://www.bluetooth.com/specifications/gatt/services">here</a> for more info.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, short> AssignedServices =
            new ReadOnlyDictionary<string, short>(new Dictionary<string, short>
            {
                {"generic_access", 0x1800},
                {"alert_notification", 0x1811},
                {"automation_io", 0x1815},
                {"battery_service", 0x180F},
                {"blood_pressure", 0x1810},
                {"body_composition", 0x181B},
                {"bond_management", 0x181E},
                {"continuous_glucose_monitoring", 0x181F},
                {"current_time", 0x1805},
                {"cycling_power", 0x1818},
                {"cycling_speed_and_cadence", 0x1816},
                {"device_information", 0x180A},
                {"environmental_sensing", 0x181A},
                {"fitness_machine", 0x1826},
                {"generic_attribute", 0x1801},
                {"glucose", 0x1808},
                {"health_thermometer", 0x1809},
                {"heart_rate", 0x180D},
                {"http_proxy", 0x1823},
                {"human_interface_device", 0x1812},
                {"immediate_alert", 0x1802},
                {"indoor_positioning", 0x1821},
                {"internet_protocol_support", 0x1820},
                {"link_loss", 0x1803},
                {"location_and_navigation", 0x1819},
                {"mesh_provisioning", 0x1827},
                {"mesh_proxy", 0x1828},
                {"next_dst_change", 0x1807},
                {"object_transfer", 0x1825},
                {"phone_alert_status", 0x180E},
                {"pulse_oximeter", 0x1822},
                {"reconnection_configuration", 0x1829},
                {"reference_time_update", 0x1806},
                {"running_speed_and_cadence", 0x1814},
                {"scan_parameters", 0x1813},
                {"transport_discovery", 0x1824},
                {"tx_power", 0x1804},
                {"user_data", 0x181C},
                {"weight_scale", 0x181D},
            });
    }
}
