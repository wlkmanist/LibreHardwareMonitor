﻿// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using HidSharp;
using HidSharp.Reports;

//using static LibreHardwareMonitor.Hardware.Controller.ProjectBlack.ProjectBlackGroup;

namespace LibreHardwareMonitor.Hardware.Controller.ProjectBlack;

internal sealed class E320 : Hardware, IDisposable
{
    private const byte REG_TZ_COUNT =   0x01;
    private const byte REG_FAN_COUNT =	0x02;
    private const byte REG_VINT_COUNT =	0x03;
    private const byte REG_RTZ_COUNT =	0x04;
    private const byte REG_TOFFSET =	0x0A;
    private const byte REG_FAN_MODE	=   0x0B;
    private const byte REG_DEVRESET	=   0x0E;
    private const byte REG_BAUDRATE	=   0x0F;
    private const byte REG_TZ =		    0x10;
    private const byte REG_RTZ =		0x1C;
    private const byte REG_RHUMIDITY =  0x1E;
    private const byte REG_RBATTERY =   0x1F;
    private const byte REG_VINT =		0x1C; // same as RTZ
    private const byte REG_FAN_PWM =    0x20;
    private const byte REG_FAN_TACHO =	0x30;
    private const byte REG_MCUREV =		0x50;
    private const byte REG_MCUDEV =		0x52;
    private const byte REG_PID =		0x5A;
    private const byte REG_REV =		0x5C;
    private const byte REG_VID =        0x5D;

    private SerialPort _port;
    private readonly Sensor[] _temperatures;
    private readonly Sensor[] _temperaturesRemote;
    //private readonly Sensor[] _fans;
    //private readonly Sensor[] _controls;
    private readonly byte _tempOffset;
    private readonly bool _available;


    public E320(SerialPort port, ushort pid, byte rev, ISettings settings) : base("project.black Device", new Identifier("projectblack", port.PortName), settings)
    {
        _port = port;
        try
        {
            // Init
            _port.Open();
            Thread.Sleep(10);
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();

            base.Name = "project.black" + " " + pid.ToString("X4") + " (" + _port.PortName + ")";

            // Read config regs
            _tempOffset  = readRegByte(REG_TOFFSET);
            int tzCount  = Math.Min(readRegByte(REG_TZ_COUNT),  (byte)6);
            int rtzCount = Math.Min(readRegByte(REG_RTZ_COUNT), (byte)1);
            int fanCount = Math.Min(readRegByte(REG_FAN_COUNT), (byte)8);

            //_fans = new Sensor[fanCount];
            //_controls = new Sensor[fanCount];
            //for (int i = 0; i < fanCount; i++)
            //{
            //    int device = 33 + i;
            //    string name = ReadString(device, 'C');
            //    _fans[i] = new Sensor(name, device, SensorType.Fan, this, settings) { Value = ReadInteger(device, 'R') };
            //    ActivateSensor(_fans[i]);
            //    _controls[i] = new Sensor(name, device, SensorType.Control, this, settings) { Value = (100 / 255.0f) * ReadInteger(device, 'P') };
            //    ActivateSensor(_controls[i]);
            //}

            _temperatures = new Sensor[tzCount];
            for (int i = 0; i < tzCount; i++) // TZ
            {
                _temperatures[i] = new Sensor("NTC Temperature #" + i, i, SensorType.Temperature, this, settings);

                byte msb = readRegByte((byte)(REG_TZ + i * 2));
                byte lsb = readRegByte((byte)(REG_TZ + i * 2 + 1));
                float temp = msb - _tempOffset + lsb / 256.0f;

                _temperatures[i].Value = temp;
                if (msb != 0xFF && msb != 0x00)
                    ActivateSensor(_temperatures[i]);
                else
                    DeactivateSensor(_temperatures[i]);
            }
            _temperaturesRemote = new Sensor[rtzCount];
            for (int i = 0; i < rtzCount; i++) // RTZ
            {
                _temperaturesRemote[i] = new Sensor("Oregon Sensor #" + i, tzCount + i, SensorType.Temperature, this, settings);

                byte msb = readRegByte((byte)(REG_RTZ + i * 2));
                byte lsb = readRegByte((byte)(REG_RTZ + i * 2 + 1));
                float temp = msb - _tempOffset + lsb / 256.0f;

                _temperaturesRemote[i].Value = temp;
                if (msb != 0xFF && msb != 0x00)
                    ActivateSensor(_temperaturesRemote[i]);
                else
                    DeactivateSensor(_temperaturesRemote[i]);
            }

            /// set the update rate to 2 Hz
            ///WriteInteger(0, 'L', 2);
            _available = true;
        }
        catch (IOException)
        { }
        catch (TimeoutException)
        { }
    }

    public override HardwareType HardwareType
    {
        get { return HardwareType.Cooler; }
    }

    public override void Update()
    {
        if (!_available)
            return;

        try
        {
            if (!_port.IsOpen)
            {
                // Init
                _port.Open();
                Thread.Sleep(10);
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();
            }

            for (int i = 0; i < _temperatures.Length; i++) // TZ
            {
                byte msb = readRegByte((byte)(REG_TZ + i * 2));
                byte lsb = readRegByte((byte)(REG_TZ + i * 2 + 1));
                float temp = msb - _tempOffset + lsb / 256.0f;

                _temperatures[i].Value = temp;
                if (msb != 0xFF && msb != 0x00)
                    ActivateSensor(_temperatures[i]);
                else
                    DeactivateSensor(_temperatures[i]);
            }
            for (int i = 0; i < _temperaturesRemote.Length; i++) // RTZ
            {
                byte msb = readRegByte((byte)(REG_RTZ + i * 2));
                byte lsb = readRegByte((byte)(REG_RTZ + i * 2 + 1));
                float temp = msb - _tempOffset + lsb / 256.0f;

                _temperaturesRemote[i].Value = temp;
                if (msb != 0xFF && msb != 0x00)
                    ActivateSensor(_temperaturesRemote[i]);
                else
                    DeactivateSensor(_temperaturesRemote[i]);
            }
        }
        catch (IOException)
        { }
        catch (TimeoutException)
        { }
    }

    private byte readRegByte(byte addr)
    {
        byte[] buff = new byte[1] { addr };
        _port.Write(buff, 0, 1);
        _port.Read(buff, 0, 1);
        return buff[0];
    }

    private void writeRegByte(byte addr, byte cmd)
    {
        byte[] buff = new byte[3] { 0x00, addr, cmd };
        _port.Write(buff, 0, 3);
    }

    private ushort readRegWord(byte addr)
    {
        return (ushort)((readRegByte(addr) << 8) | readRegByte((byte)(addr + 1)));
    }

    //public override string GetReport()
    //{
    //    StringBuilder r = new();
    //    //r.Append("Port: ");
    //    //r.AppendLine(_port);
    //    //r.Append("Firmware Revision: ");
    //    //r.AppendLine(_firmwareRevision.ToString(CultureInfo.InvariantCulture));
    //    //r.AppendLine();
    //    return r.ToString();
    //}

    public void Dispose()
    {
        if (_port != null)
        {
            _port.Dispose();
            _port = null;
        }
    }

    public override void Close()
    {
        _port.Close();
        _port.Dispose();
        _port = null;
        base.Close();
    }
}