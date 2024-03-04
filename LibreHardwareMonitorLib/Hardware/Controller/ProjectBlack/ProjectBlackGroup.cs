// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

// project.black devices.
// By wlkmanist, 2023-2024.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Security;
using System.Text;
using System.Threading;
using HidSharp;
using Microsoft.Win32;

namespace LibreHardwareMonitor.Hardware.Controller.ProjectBlack;

internal class ProjectBlackGroup : IGroup
{
    private readonly List<IHardware> _hardware = new();
    private readonly StringBuilder _report = new();

    public ProjectBlackGroup(ISettings settings)
    {
        // No implementation for ProjectBlack devices on Unix systems
        if (Software.OperatingSystem.IsUnix)
            return;



        string[] portNames = SerialPort.GetPortNames();
        for (int i = 0; i < portNames.Length; i++)
        {
            try
            {
                using SerialPort serialPort = new(portNames[i], 115200, Parity.None, 8, StopBits.One);
                _report.Append("Port Name: ");
                _report.AppendLine(portNames[i]);
                try
                {
                    serialPort.Open();
                }
                catch (UnauthorizedAccessException)
                {
                    _report.AppendLine("Exception: Access Denied");
                }

                if (serialPort.IsOpen)
                {
                    Thread.Sleep(10);
                    serialPort.DiscardInBuffer();
                    serialPort.DiscardOutBuffer();

                    try
                    {
                        ushort vid = readRegWord(serialPort, REG_VID);

                        if (vid != 0xB1AC)
                        {
                            serialPort.Close();
                            _report.AppendLine("Status: Wrong Vendor ID: 0x" + vid.ToString("X4") +
                                        ", not a project.black device");
                            _report.AppendLine();
                            continue;
                        }
                    }
                    catch (TimeoutException)
                    {
                        _report.AppendLine("Status: Timeout Reading Vendor ID");
                    }

                    ushort pid = readRegWord(serialPort, REG_PID);
                    byte rev = readRegByte(serialPort, REG_REV);

                    serialPort.Close();

                    switch (pid)
                    {
                        case 0xE320:
                        case 0xE321:
                        case 0xE322:
                        case 0xE323:
                            _hardware.Add(new E320(serialPort, pid, rev, settings));
                            _report.AppendLine("Status: OK");
                            _report.AppendLine("Device name: project.black " + pid.ToString("X4"));
                            _report.AppendLine("Device HW revision: " + ((rev & 0xF0) >> 4).ToString("X1") + "." + (rev & 0x0F).ToString("X1"));
                            break;

                        default:
                            _report.AppendLine("Status: Unsupported project.black device:");
                            _report.AppendLine("        ID: 0x" + pid.ToString("X4") + ", rev: " +
                                        ((rev & 0xF0) >> 4).ToString("X1") + "." + (rev & 0x0F).ToString("X1"));
                            break;
                    }
                }
                else
                {
                    _report.AppendLine("Status: Port not Open");
                }
            }
            catch (Exception e)
            {
                _report.AppendLine(e.ToString());
            }

            _report.AppendLine();
        }
    }

    public IReadOnlyList<IHardware> Hardware => _hardware;

    public string GetReport()
    {
        if (_report.Length > 0)
        {
            StringBuilder r = new();
            r.AppendLine("Serial Port Heatmaster");
            r.AppendLine();
            r.Append(_report);
            r.AppendLine();
            return r.ToString();
        }

        return null;
    }

    public void Close()
    {
        foreach (IHardware iHardware in _hardware)
        {
            if (iHardware is Hardware hardware)
                hardware.Close();
        }
    }

    public static byte readRegByte(SerialPort port, byte addr)
    {
        byte[] buff = new byte[1] { addr };
        port.Write(buff, 0, 1);
        port.Read(buff, 0, 1);
        return buff[0];
    }

    public static void writeRegByte(SerialPort port, byte addr, byte cmd)
    {
        byte[] buff = new byte[3] { 0x00, addr, cmd };
        port.Write(buff, 0, 3);
    }

    public static ushort readRegWord(SerialPort port, byte addr)
    {
        return (ushort)((readRegByte(port, addr) << 8) | readRegByte(port, (byte)(addr + 1)));
    }

    private const byte REG_MCUREV = 0x50;
    private const byte REG_MCUDEV = 0x52;
    private const byte REG_PID = 0x5A;
    private const byte REG_REV = 0x5C;
    private const byte REG_VID = 0x5D;
}
