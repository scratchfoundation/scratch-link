import CoreBluetooth

struct GATTBlockListStatus: OptionSet {
    let rawValue: Int

    static let Include: GATTBlockListStatus = []
    static let ExcludeReads = GATTBlockListStatus(rawValue: 1 << 0)
    static let ExcludeWrites = GATTBlockListStatus(rawValue: 1 << 1)
    static let Exclude: GATTBlockListStatus = [.ExcludeReads, .ExcludeWrites]
}

/// Helper methods to deal with GATT names & UUID values.
/// Most methods correspond to a similarly named item in the Web Bluetooth specification.
/// See [here](https://webbluetoothcg.github.io/web-bluetooth/) for more info.
class GATTHelpers {
    /// Resolve a Web Bluetooth GATT service name to a canonical UUID.
    /// See [here](https://webbluetoothcg.github.io/web-bluetooth/) for more info.
    ///
    /// - parameters:
    ///   - service: A short UUID in integer form, a full UUID string, or an assigned number's name
    /// - returns: a UUID on success or nil on failure
    public static func getUUID(forService service: Any) -> CBUUID? {
        return resolveUUID(fromName: service, withTable: AssignedServices)
    }

    /// Resolve a Web Bluetooth GATT characteristic name to a canonical UUID.
    /// See [here](https://webbluetoothcg.github.io/web-bluetooth/) for more info.
    ///
    /// - parameters:
    ///   - service: A short UUID in integer form, a full UUID string, or an assigned number's name
    /// - returns: a UUID on success or nil on failure
    public static func getUUID(forCharacteristic characteristic: Any) -> CBUUID? {
        return resolveUUID(fromName: characteristic, withTable: AssignedCharacteristics)
    }

    typealias AssignedNumbersTable = [String: uint16]

    /// Resolve a Web Bluetooth GATT "name" to a canonical UUID, using an assigned numbers table if necessary.
    /// See [here](https://webbluetoothcg.github.io/web-bluetooth/#resolveuuidname) for more info.
    ///
    /// - parameters:
    ///   - name: A short UUID in integer form, a full UUID, or the name of an assigned number
    ///   - assignedNumbers: The table of assigned numbers to resolve integer names
    /// - returns: a UUID on success or nil on failure
    /// - throws: a JSONRpcError on failure
    public static func resolveUUID(fromName name: Any, withTable assignedNumbers: AssignedNumbersTable) -> CBUUID? {
        if let shortServiceNum = name as? uint32 {
            return canonicalUUID(fromAlias: shortServiceNum)
        }

        if let nameString = name as? String {
            if let validUuidRegex = try? NSRegularExpression(
                    pattern: "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$") {
                if validUuidRegex.numberOfMatches(
                    in: nameString, range: NSRange(location: 0, length: nameString.count)) > 0 {
                    return CBUUID(string: nameString)
                }
                // else it's not a UUID string so continue below
            } else {
                // This should never happen
                print("failed to build UUID regex")
            }

            if let assignedNumber = assignedNumbers[nameString] {
                return canonicalUUID(fromAlias: uint32(assignedNumber))
            }
        }

        return nil
    }

    /// Generate a full UUID given a 16-bit or 32-bit "short UUID" alias.
    /// See [here](https://webbluetoothcg.github.io/web-bluetooth/#dom-bluetoothuuid-canonicaluuid) for more info.
    ///
    /// - parameters:
    ///   - alias: A 16- or 32-bit UUID alias
    /// - returns: The associated canonical UUID
    public static func canonicalUUID(fromAlias alias: uint32) -> CBUUID {
        let uuidBytes: [uint8] = [
                uint8((alias >> 24) & 0xff),
                uint8((alias >> 16) & 0xff),
                uint8((alias >> 8) & 0xff),
                uint8(alias & 0xff),
                0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb
        ]
        return CBUUID(data: Data(uuidBytes))
    }

    /// Check if a service, characteristic, or descriptor is blocked.
    /// - parameters:
    ///   - uuid: the UUID of a service, characteristic, or descriptor
    /// - returns: the status of the UUID on the block-list, if present.
    public static func getBlockListStatus(ofUUID uuid: CBUUID) -> GATTBlockListStatus? {
        return BlockList[uuid]
    }

    /// Table of well-known GATT service UUIDs.
    /// See [here](https://www.bluetooth.com/specifications/gatt/services) for more info.
    private static let AssignedServices: AssignedNumbersTable = [
        "alert_notification": 0x1811,
        "automation_io": 0x1815,
        "battery_service": 0x180F,
        "blood_pressure": 0x1810,
        "body_composition": 0x181B,
        "bond_management": 0x181E,
        "continuous_glucose_monitoring": 0x181F,
        "current_time": 0x1805,
        "cycling_power": 0x1818,
        "cycling_speed_and_cadence": 0x1816,
        "device_information": 0x180A,
        "environmental_sensing": 0x181A,
        "fitness_machine": 0x1826,
        "generic_access": 0x1800,
        "generic_attribute": 0x1801,
        "glucose": 0x1808,
        "health_thermometer": 0x1809,
        "heart_rate": 0x180D,
        "http_proxy": 0x1823,
        "human_interface_device": 0x1812,
        "immediate_alert": 0x1802,
        "indoor_positioning": 0x1821,
        "internet_protocol_support": 0x1820,
        "link_loss": 0x1803,
        "location_and_navigation": 0x1819,
        "mesh_provisioning": 0x1827,
        "mesh_proxy": 0x1828,
        "next_dst_change": 0x1807,
        "object_transfer": 0x1825,
        "phone_alert_status": 0x180E,
        "pulse_oximeter": 0x1822,
        "reconnection_configuration": 0x1829,
        "reference_time_update": 0x1806,
        "running_speed_and_cadence": 0x1814,
        "scan_parameters": 0x1813,
        "transport_discovery": 0x1824,
        "tx_power": 0x1804,
        "user_data": 0x181C,
        "weight_scale": 0x181D
    ]

    /// Table of well-known GATT characteristic UUIDs.
    /// See <a href="https://www.bluetooth.com/specifications/gatt/characteristics">here</a> for more info.
    private static let AssignedCharacteristics: AssignedNumbersTable = [
        "aerobic_heart_rate_lower_limit": 0x2A7E,
        "aerobic_heart_rate_upper_limit": 0x2A84,
        "aerobic_threshold": 0x2A7F,
        "age": 0x2A80,
        "aggregate": 0x2A5A,
        "alert_category_id": 0x2A43,
        "alert_category_id_bit_mask": 0x2A42,
        "alert_level": 0x2A06,
        "alert_notification_control_point": 0x2A44,
        "alert_status": 0x2A3F,
        "altitude": 0x2AB3,
        "anaerobic_heart_rate_lower_limit": 0x2A81,
        "anaerobic_heart_rate_upper_limit": 0x2A82,
        "anaerobic_threshold": 0x2A83,
        "analog": 0x2A58,
        "analog_output": 0x2A59,
        "apparent_wind_direction": 0x2A73,
        "apparent_wind_speed": 0x2A72,
        "barometric_pressure_trend": 0x2AA3,
        "battery_level": 0x2A19,
        "battery_level_state": 0x2A1B,
        "battery_power_state": 0x2A1A,
        "blood_pressure_feature": 0x2A49,
        "blood_pressure_measurement": 0x2A35,
        "body_composition_feature": 0x2A9B,
        "body_composition_measurement": 0x2A9C,
        "body_sensor_location": 0x2A38,
        "bond_management_control_point": 0x2AA4,
        "bond_management_feature": 0x2AA5,
        "boot_keyboard_input_report": 0x2A22,
        "boot_keyboard_output_report": 0x2A32,
        "boot_mouse_input_report": 0x2A33,
        "cgm_feature": 0x2AA8,
        "cgm_measurement": 0x2AA7,
        "cgm_session_run_time": 0x2AAB,
        "cgm_session_start_time": 0x2AAA,
        "cgm_specific_ops_control_point": 0x2AAC,
        "cgm_status": 0x2AA9,
        "cross_trainer_data": 0x2ACE,
        "csc_feature": 0x2A5C,
        "csc_measurement": 0x2A5B,
        "current_time": 0x2A2B,
        "cycling_power_control_point": 0x2A66,
        "cycling_power_feature": 0x2A65,
        "cycling_power_measurement": 0x2A63,
        "cycling_power_vector": 0x2A64,
        "database_change_increment": 0x2A99,
        "date_of_birth": 0x2A85,
        "date_of_threshold_assessment": 0x2A86,
        "date_time": 0x2A08,
        "day_date_time": 0x2A0A,
        "day_of_week": 0x2A09,
        "descriptor_value_changed": 0x2A7D,
        "dew_point": 0x2A7B,
        "digital": 0x2A56,
        "digital_output": 0x2A57,
        "dst_offset": 0x2A0D,
        "elevation": 0x2A6C,
        "email_address": 0x2A87,
        "exact_time_100": 0x2A0B,
        "exact_time_256": 0x2A0C,
        "fat_burn_heart_rate_lower_limit": 0x2A88,
        "fat_burn_heart_rate_upper_limit": 0x2A89,
        "firmware_revision_string": 0x2A26,
        "first_name": 0x2A8A,
        "fitness_machine_control_point": 0x2AD9,
        "fitness_machine_feature": 0x2ACC,
        "fitness_machine_status": 0x2ADA,
        "five_zone_heart_rate_limits": 0x2A8B,
        "floor_number": 0x2AB2,
        "gap.appearance": 0x2A01,
        "gap.central_address_resolution": 0x2AA6,
        "gap.device_name": 0x2A00,
        "gap.peripheral_preferred_connection_parameters": 0x2A04,
        "gap.peripheral_privacy_flag": 0x2A02,
        "gap.reconnection_address": 0x2A03,
        "gatt.service_changed": 0x2A05,
        "gender": 0x2A8C,
        "glucose_feature": 0x2A51,
        "glucose_measurement": 0x2A18,
        "glucose_measurement_context": 0x2A34,
        "gust_factor": 0x2A74,
        "hardware_revision_string": 0x2A27,
        "heart_rate_control_point": 0x2A39,
        "heart_rate_max": 0x2A8D,
        "heart_rate_measurement": 0x2A37,
        "heat_index": 0x2A7A,
        "height": 0x2A8E,
        "hid_control_point": 0x2A4C,
        "hid_information": 0x2A4A,
        "hip_circumference": 0x2A8F,
        "http_control_point": 0x2ABA,
        "http_entity_body": 0x2AB9,
        "http_headers": 0x2AB7,
        "http_status_code": 0x2AB8,
        "https_security": 0x2ABB,
        "humidity": 0x2A6F,
        "ieee_11073-20601_regulatory_certification_data_list": 0x2A2A,
        "indoor_bike_data": 0x2AD2,
        "indoor_positioning_configuration": 0x2AAD,
        "intermediate_cuff_pressure": 0x2A36,
        "intermediate_temperature": 0x2A1E,
        "irradiance": 0x2A77,
        "language": 0x2AA2,
        "last_name": 0x2A90,
        "latitude": 0x2AAE,
        "ln_control_point": 0x2A6B,
        "ln_feature": 0x2A6A,
        "local_east_coordinate": 0x2AB1,
        "local_north_coordinate": 0x2AB0,
        "local_time_information": 0x2A0F,
        "location_and_speed": 0x2A67,
        "location_name": 0x2AB5,
        "Longitude": 0x2AAF,
        "magnetic_declination": 0x2A2C,
        "Magnetic_flux_density_2D": 0x2AA0,
        "Magnetic_flux_density_3D": 0x2AA1,
        "manufacturer_name_string": 0x2A29,
        "maximum_recommended_heart_rate": 0x2A91,
        "measurement_interval": 0x2A21,
        "model_number_string": 0x2A24,
        "navigation": 0x2A68,
        "network_availability": 0x2A3E,
        "new_alert": 0x2A46,
        "object_action_control_point": 0x2AC5,
        "object_changed": 0x2AC8,
        "object_first_created": 0x2AC1,
        "object_id": 0x2AC3,
        "object_last_modified": 0x2AC2,
        "object_list_control_point": 0x2AC6,
        "object_list_filter": 0x2AC7,
        "object_name": 0x2ABE,
        "object_properties": 0x2AC4,
        "object_size": 0x2AC0,
        "object_type": 0x2ABF,
        "ots_feature": 0x2ABD,
        "plx_continuous_measurement": 0x2A5F,
        "plx_features": 0x2A60,
        "plx_spot_check_measurement": 0x2A5E,
        "pnp_id": 0x2A50,
        "pollen_concentration": 0x2A75,
        "position_2d": 0x2A2F,
        "position_3d": 0x2A30,
        "position_quality": 0x2A69,
        "pressure": 0x2A6D,
        "protocol_mode": 0x2A4E,
        "pulse_oximetry_control_point": 0x2A62,
        "rainfall": 0x2A78,
        "rc_feature": 0x2B1D,
        "rc_settings": 0x2B1E,
        "reconnection_configuration_control_point": 0x2B1F,
        "record_access_control_point": 0x2A52,
        "reference_time_information": 0x2A14,
        "removable": 0x2A3A,
        "report": 0x2A4D,
        "report_map": 0x2A4B,
        "resolvable_private_address_only": 0x2AC9,
        "resting_heart_rate": 0x2A92,
        "ringer_control_point": 0x2A40,
        "ringer_setting": 0x2A41,
        "rower_data": 0x2AD1,
        "rsc_feature": 0x2A54,
        "rsc_measurement": 0x2A53,
        "sc_control_point": 0x2A55,
        "scan_interval_window": 0x2A4F,
        "scan_refresh": 0x2A31,
        "scientific_temperature_celsius": 0x2A3C,
        "secondary_time_zone": 0x2A10,
        "sensor_location": 0x2A5D,
        "serial_number_string": 0x2A25,
        "service_required": 0x2A3B,
        "software_revision_string": 0x2A28,
        "sport_type_for_aerobic_and_anaerobic_thresholds": 0x2A93,
        "stair_climber_data": 0x2AD0,
        "step_climber_data": 0x2ACF,
        "string": 0x2A3D,
        "supported_heart_rate_range": 0x2AD7,
        "supported_inclination_range": 0x2AD5,
        "supported_new_alert_category": 0x2A47,
        "supported_power_range": 0x2AD8,
        "supported_resistance_level_range": 0x2AD6,
        "supported_speed_range": 0x2AD4,
        "supported_unread_alert_category": 0x2A48,
        "system_id": 0x2A23,
        "tds_control_point": 0x2ABC,
        "temperature": 0x2A6E,
        "temperature_celsius": 0x2A1F,
        "temperature_fahrenheit": 0x2A20,
        "temperature_measurement": 0x2A1C,
        "temperature_type": 0x2A1D,
        "three_zone_heart_rate_limits": 0x2A94,
        "time_accuracy": 0x2A12,
        "time_broadcast": 0x2A15,
        "time_source": 0x2A13,
        "time_update_control_point": 0x2A16,
        "time_update_state": 0x2A17,
        "time_with_dst": 0x2A11,
        "time_zone": 0x2A0E,
        "training_status": 0x2AD3,
        "treadmill_data": 0x2ACD,
        "true_wind_direction": 0x2A71,
        "true_wind_speed": 0x2A70,
        "two_zone_heart_rate_limit": 0x2A95,
        "tx_power_level": 0x2A07,
        "uncertainty": 0x2AB4,
        "unread_alert_status": 0x2A45,
        "uri": 0x2AB6,
        "user_control_point": 0x2A9F,
        "user_index": 0x2A9A,
        "uv_index": 0x2A76,
        "vo2_max": 0x2A96,
        "waist_circumference": 0x2A97,
        "weight": 0x2A98,
        "weight_measurement": 0x2A9D,
        "weight_scale_feature": 0x2A9E,
        "wind_chill": 0x2A79
    ]

    /// Dictionary of UUIDs which are blocked from Web Bluetooth access for security or privacy reasons. See
    /// [the Web Bluetooth Registries repository](https://github.com/WebBluetoothCG/registries) for more information.
    ///
    /// Collected from https://github.com/WebBluetoothCG/registries @ 693db2fe6050bee27d198e1584d11fc2732cdbd8
    private static let BlockList: [CBUUID: GATTBlockListStatus] = [
        // Services

        // org.bluetooth.service.human_interface_device
        // Direct access to HID devices like keyboards would let web pages become keyloggers.
        CBUUID(string: "00001812-0000-1000-8000-00805f9b34fb"): .Exclude,

        // Firmware update services that don't check the update's signature present a risk of devices'
        // software being modified by malicious web pages. Users may connect to a device believing they are
        // enabling only simple interaction or that they're interacting with the device's manufacturer, but
        // the site might instead persistently compromise the device.

        // Nordic's Legacy Device Firmware Update service,
        // http://infocenter.nordicsemi.com/topic/com.nordic.infocenter.sdk5.v11.0.0/examples_ble_dfu.html
        CBUUID(string: "00001530-1212-efde-1523-785feabcd123"): .Exclude,

        // TI's Over-the-Air Download service, http://www.ti.com/lit/ug/swru271g/swru271g.pdf
        CBUUID(string: "f000ffc0-0451-4000-b000-000000000000"): .Exclude,

        // Cypress's Bootloader service.
        // Documentation at http://www.cypress.com/file/175561/download requires an account.
        // Linked as CYPRESS BOOTLOADER SERVICE_001-97547.pdf from
        // http://www.cypress.com/documentation/software-and-drivers/cypresss-custom-ble-profiles-and-services
        CBUUID(string: "00060000-0000-1000-8000-00805f9b34fb"): .Exclude,

        // The FIDO Bluetooth Specification at
        // https://fidoalliance.org/specs/fido-u2f-bt-protocol-id-20150514.pdf
        // section 6.7.1 "Bluetooth pairing: Client considerations" warns that system-wide pairing poses
        // security risks. Specifically, a website could use raw GATT commands to impersonate another website
        // to the FIDO device.
        CBUUID(string: "0000fffd-0000-1000-8000-00805f9b34fb"): .Exclude,

        // Characteristics

        // org.bluetooth.characteristic.gap.peripheral_privacy_flag
        // Don't let web pages turn off privacy mode.
        CBUUID(string: "00002a02-0000-1000-8000-00805f9b34fb"): .ExcludeWrites,

        // org.bluetooth.characteristic.gap.reconnection_address
        // Disallow messing with connection parameters
        CBUUID(string: "00002a03-0000-1000-8000-00805f9b34fb"): .Exclude,

        // org.bluetooth.characteristic.serial_number_string
        // Block access to standardized unique identifiers, for privacy reasons.
        CBUUID(string: "00002a25-0000-1000-8000-00805f9b34fb"): .Exclude,

        // Descriptors

        // org.bluetooth.descriptor.gatt.client_characteristic_configuration
        // Writing to this would let a web page interfere with other pages' notifications and indications.
        CBUUID(string: "00002902-0000-1000-8000-00805f9b34fb"): .ExcludeWrites,

        // org.bluetooth.descriptor.gatt.server_characteristic_configuration
        // Writing to this would let a web page interfere with the broadcasted services.
        CBUUID(string: "00002903-0000-1000-8000-00805f9b34fb"): .ExcludeWrites
    ]
}
