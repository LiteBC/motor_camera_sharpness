namespace PiMotorController
{
    using System;
    using System.Collections.Generic;
    using System.Management;
    using Microsoft.Win32;

    /// <summary>
    /// Provides utility methods for retrieving serial port information.
    /// </summary>
    public static class SerialPortUtilities
    {
        /// <summary>
        /// Retrieves a list of serial port information.
        /// </summary>
        /// <returns>A list of <see cref="SerialPortInfo"/> objects containing information about each serial port.</returns>
        public static List<SerialPortInfo> GetSerialPortInfo()
        {
            List<SerialPortInfo> info = new List<SerialPortInfo>();
            using (ManagementClass entity = new ManagementClass("Win32_PnPEntity"))
            {
                foreach (ManagementObject inst in entity.GetInstances())
                {
                    object guid = inst.GetPropertyValue("ClassGuid");
                    if (guid == null || guid.ToString().ToUpper() != "{4D36E978-E325-11CE-BFC1-08002BE10318}")
                    {
                        continue; // Skip all devices except device class "PORTS"
                    }

                    string caption = inst.GetPropertyValue("Caption").ToString();
                    string manufact = inst.GetPropertyValue("Manufacturer").ToString();
                    string deviceId = inst.GetPropertyValue("PnpDeviceID").ToString();
                    string regPath = "HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\Enum\\" + deviceId + "\\Device Parameters";
                    string portName = Registry.GetValue(regPath, "PortName", string.Empty).ToString();

                    int pos = caption.IndexOf(" (COM");
                    if (pos > 0)
                    { // remove COM port from description
                        caption = caption.Substring(0, pos);
                    }

                    SerialPortInfo serialPortInfo = new SerialPortInfo(portName, caption, manufact, deviceId);

                    info.Add(serialPortInfo);
                }
            }

            return info;
        }
    }

    /// <summary>
    /// Represents information about a serial port.
    /// </summary>
    public class SerialPortInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SerialPortInfo"/> class with specified port name, caption, manufacturer, and PnP device ID.
        /// </summary>
        /// <param name="portName">The name of the port.</param>
        /// <param name="caption">The caption of the port.</param>
        /// <param name="manufacturer">The manufacturer of the port.</param>
        /// <param name="pnpDeviceID">The PnP device ID of the port.</param>
        public SerialPortInfo(string portName, string caption, string manufacturer, string pnpDeviceID)
        {
            this.PortName = portName;
            this.Caption = caption;
            this.Manufacturer = manufacturer;
            this.PnpDeviceID = pnpDeviceID;
        }

        /// <summary>
        /// Gets the name of the port.
        /// </summary>
        public string PortName { get; private set; }

        /// <summary>
        /// Gets the caption of the port.
        /// </summary>
        public string Caption { get; private set; }

        /// <summary>
        /// Gets the manufacturer of the port.
        /// </summary>
        public string Manufacturer { get; private set; }

        /// <summary>
        /// Gets the PnP device ID of the port.
        /// </summary>
        public string PnpDeviceID { get; private set; }
    }
}