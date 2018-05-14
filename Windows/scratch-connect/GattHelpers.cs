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
        /// Check if a service, characteristic, or descriptor is blocked.
        /// </summary>
        /// <param name="uuid">The UUID of a service, characteristic, or descriptor.</param>
        /// <returns>The status of the UUID on the block-list, or "Included" if it is not blocked.</returns>
        public static BlockListStatus GetBlockListStatus(Guid uuid)
        {
            return BlockList.TryGetValue(uuid, out var status) ? status : BlockListStatus.Include;
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

        [Flags]
        public enum BlockListStatus
        {
            /// <summary>
            /// This UUID is not blocked: it may be read or written.
            /// </summary>
            Include = 0,

            /// <summary>
            /// This UUID may be written but may not be read.
            /// </summary>
            ExcludeReads = 1,

            /// <summary>
            /// This UUID may be read but may not be written.
            /// </summary>
            ExcludeWrites = 2,

            /// <summary>
            /// This UUID may not be read or written.
            /// </summary>
            Exclude = ExcludeReads | ExcludeWrites
        }

        /// <summary>
        /// Dictionary of UUIDs which are blocked from Web Bluetooth access for security or privacy reasons. See
        /// <a href="https://github.com/WebBluetoothCG/registries">the Web Bluetooth Registries repository</a> for
        /// more information.
        /// </summary>
        private static readonly IReadOnlyDictionary<Guid, BlockListStatus> BlockList =
            // Collected from https://github.com/WebBluetoothCG/registries @ 693db2fe6050bee27d198e1584d11fc2732cdbd8
            new ReadOnlyDictionary<Guid, BlockListStatus>(new Dictionary<Guid, BlockListStatus> {

                // Services

                // org.bluetooth.service.human_interface_device
                // Direct access to HID devices like keyboards would let web pages become keyloggers.
                {new Guid("00001812-0000-1000-8000-00805f9b34fb"), BlockListStatus.Exclude},

                // Firmware update services that don't check the update's signature present a risk of devices'
                // software being modified by malicious web pages. Users may connect to a device believing they are
                // enabling only simple interaction or that they're interacting with the device's manufacturer, but
                // the site might instead persistently compromise the device.

                // Nordic's Legacy Device Firmware Update service,
                // http://infocenter.nordicsemi.com/topic/com.nordic.infocenter.sdk5.v11.0.0/examples_ble_dfu.html
                {new Guid("00001530-1212-efde-1523-785feabcd123"), BlockListStatus.Exclude},

                // TI's Over-the-Air Download service, http://www.ti.com/lit/ug/swru271g/swru271g.pdf
                {new Guid("f000ffc0-0451-4000-b000-000000000000"), BlockListStatus.Exclude},

                // Cypress's Bootloader service.
                // Documentation at http://www.cypress.com/file/175561/download requires an account.
                // Linked as CYPRESS BOOTLOADER SERVICE_001-97547.pdf from
                // http://www.cypress.com/documentation/software-and-drivers/cypresss-custom-ble-profiles-and-services
                {new Guid("00060000-0000-1000-8000-00805f9b34fb"), BlockListStatus.Exclude},

                // The FIDO Bluetooth Specification at
                // https://fidoalliance.org/specs/fido-u2f-bt-protocol-id-20150514.pdf
                // section 6.7.1 "Bluetooth pairing: Client considerations" warns that system-wide pairing poses
                // security risks. Specifically, a website could use raw GATT commands to impersonate another website
                // to the FIDO device.
                {new Guid("0000fffd-0000-1000-8000-00805f9b34fb"), BlockListStatus.Exclude},

                // Characteristics

                // org.bluetooth.characteristic.gap.peripheral_privacy_flag
                // Don't let web pages turn off privacy mode.
                {new Guid("00002a02-0000-1000-8000-00805f9b34fb"), BlockListStatus.ExcludeWrites},

                // org.bluetooth.characteristic.gap.reconnection_address
                // Disallow messing with connection parameters
                {new Guid("00002a03-0000-1000-8000-00805f9b34fb"), BlockListStatus.Exclude},

                // org.bluetooth.characteristic.serial_number_string
                // Block access to standardized unique identifiers, for privacy reasons.
                {new Guid("00002a25-0000-1000-8000-00805f9b34fb"), BlockListStatus.Exclude},

                // Descriptors

                // org.bluetooth.descriptor.gatt.client_characteristic_configuration
                // Writing to this would let a web page interfere with other pages' notifications and indications.
                {new Guid("00002902-0000-1000-8000-00805f9b34fb"), BlockListStatus.ExcludeWrites},

                // org.bluetooth.descriptor.gatt.server_characteristic_configuration
                // Writing to this would let a web page interfere with the broadcasted services.
                {new Guid("00002903-0000-1000-8000-00805f9b34fb"), BlockListStatus.ExcludeWrites},
            });
    }
}
