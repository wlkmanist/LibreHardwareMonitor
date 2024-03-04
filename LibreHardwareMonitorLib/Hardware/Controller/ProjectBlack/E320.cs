// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

// project.black E320 series.
// By wlkmanist, 2023-2024.

using System;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace LibreHardwareMonitor.Hardware.Controller.ProjectBlack;

internal sealed class E320 : Hardware
{
    private const byte REG_TZ_COUNT =   0x01; // byte, ro, TZ count (6 max)
    private const byte REG_FAN_COUNT =	0x02; // byte, ro, PWM/Tacho count
    private const byte REG_VINT_COUNT =	0x03; // byte, ro, Vsense count (2 max)
    private const byte REG_RTZ_COUNT =	0x04; // byte, ro, Remote TZ count (1 max)
    private const byte REG_TOFFSET =	0x0A; // byte, rw, Temp offset integer
    private const byte REG_FAN_MODE	=   0x0B; // byte, rw, undefined
    private const byte REG_DEVRESET	=   0x0E; // byte, wo, software reboot device
    private const byte REG_BAUDRATE	=   0x0F; // byte, rw, current baud rate preset
    private const byte REG_TZ =		    0x10; // word, ro, Thermal Sensor #0 (MSB contains integer (+ REG_TOFFSET) degree, LSB contains 1/256 degree)
    private const byte REG_RTZ =		0x1C; // word, ro, Remote Thermal Sensor (MSB contains integer (+ REG_TOFFSET) degree, LSB contains 1/256 degree) // same as RTZ
    private const byte REG_RHUMIDITY =  0x1E; // byte, ro, Remote Humidity Sensor (0-100%)
    private const byte REG_RBATTERY =   0x1F; // byte, ro, Remote sensor battery level (0-100%)
    private const byte REG_VINT =		0x1C; // word, ro, Vsense #0
    private const byte REG_FAN_PWM =    0x20; // byte, rw, PWM value #0
    private const byte REG_FAN_TACHO =	0x30; // word, ro, Fan tachometer #0
    private const byte REG_MCUREV =		0x50; // word, ro, DBGMCU revision ID
    private const byte REG_MCUDEV =		0x52; // word, ro, DBGMCU chip ID
    private const byte REG_PID =		0x5A; // word, ro, Device ID
    private const byte REG_REV =		0x5C; // byte, ro, board revision
    private const byte REG_VID =        0x5D; // word, ro, Vendor ID

    private SerialPort _port;
    private readonly Sensor[] _temperatures;
    private readonly Sensor[] _temperaturesRemote;
    private readonly Sensor[] _chargeLevel;
    //private readonly Sensor[] _fans;
    //private readonly Sensor[] _controls;
    private readonly byte _tempOffset;
    private bool _available = false;
    private byte _reconnectTicker = 0;

    public E320(SerialPort port, ushort pid, byte rev, ISettings settings)
        : base("project.black E320 Series", new Identifier("serial", port.PortName), settings)
    {
        _port = port;
        try
        {
            // Init
            _port.Open();
            Thread.Sleep(20);
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
                _temperatures[i] = new Sensor("NTC Temperature #" + i,
                                                i,
                                                SensorType.Temperature,
                                                this,
                                                new[] { new ParameterDescription("Offset [°C]", "Temperature offset.", 0) },
                                                settings);

                DeactivateSensor(_temperatures[i]);             // activate later in Update() if sensor is actually connected
            }
            _temperaturesRemote = new Sensor[rtzCount];
            for (int i = 0; i < rtzCount; i++) // RTZ
            {
                _temperaturesRemote[i] = new Sensor("Oregon Sensor #" + i,
                                                tzCount + i,
                                                SensorType.Temperature,
                                                this,
                                                new[] { new ParameterDescription("Offset [°C]", "Temperature offset.", 0) },
                                                settings);

                DeactivateSensor(_temperaturesRemote[i]);       // activate later in Update() if sensor is actually connected
            }

            _chargeLevel = new Sensor[rtzCount];
            for (int i = 0; i < rtzCount; i++) // RTZ battery
            {
                _chargeLevel[i] = new Sensor("Oregon Sensor Charge #" + i, tzCount + rtzCount + i, SensorType.Level, this, settings);
                DeactivateSensor(_chargeLevel[i]);
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
            if (!_port.IsOpen || _reconnectTicker > 0)
            {
                _reconnectTicker = 0;

                // Init
                _port.Open();
                Thread.Sleep(20);
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();
            }

            for (int i = 0; i < _temperatures.Length; i++) // TZ
            {
                byte msb = readRegByte((byte)(REG_TZ + i * 2));
                byte lsb = readRegByte((byte)(REG_TZ + i * 2 + 1));
                float temp = msb - _tempOffset + lsb / 256.0f;

                if (msb != 0xFF && msb != 0x00)
                {
                    //_temperatures[i].Value = temp;
                    _temperatures[i].Value = temp + _temperatures[i].Parameters[0].Value; // temp with offset parameter
                    ActivateSensor(_temperatures[i]);
                }
                else
                {
                    _temperatures[i].Value = null;
                }
            }

            for (int i = 0; i < _temperaturesRemote.Length; i++) // RTZ
            {
                byte msb = readRegByte((byte)(REG_RTZ + i * 2));
                byte lsb = readRegByte((byte)(REG_RTZ + i * 2 + 1));
                float temp = msb - _tempOffset + lsb / 256.0f;

                if (msb != 0xFF && msb != 0x00)
                {
                    //_temperaturesRemote[i].Value = temp;
                    _temperaturesRemote[i].Value = temp + _temperaturesRemote[i].Parameters[0].Value; // temp with offset parameter
                    ActivateSensor(_temperaturesRemote[i]);
                }
                else
                {
                    _temperaturesRemote[i].Value = null;
                }
            }

            for (int i = 0; i < _chargeLevel.Length; i++) // RTZ battery
            {
                byte data = readRegByte((byte)(REG_RBATTERY + i * 2));

                if (data != 0xFF)
                {
                    _chargeLevel[i].Value = data;
                    ActivateSensor(_chargeLevel[i]);
                }
                else
                {
                    _chargeLevel[i].Value = null;
                }
            }
        }
        catch (IOException)
        {
            if (_port.IsOpen)
            {
                try
                {
                    _port.Close(); // Close to reinit in code above, device temporary disconnected
                }
                catch (IOException)
                { }

                if (_reconnectTicker < 1)
                {
                    Thread.Sleep(5000);
                    _reconnectTicker++;
                }
                else // device permanently disconnected
                {
                    for (int i = 0; i < _temperatures.Length; i++) // TZ
                        _temperatures[i].Value = null;
                    for (int i = 0; i < _temperaturesRemote.Length; i++) // RTZ
                        _temperaturesRemote[i].Value = null;

                    _available = false;
                    Close();
                }
            }
        }
        catch (TimeoutException)
        {
            try
            {
                if (_port.IsOpen)
                    _port.Close(); // Close to reinit in code above, device is frozen or after reset
            }
            catch (IOException)
            { }
        }
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
        if (_port.IsOpen)
            _port.Close();
        _port.Dispose();
        _port = null;
        base.Close();
    }
}
