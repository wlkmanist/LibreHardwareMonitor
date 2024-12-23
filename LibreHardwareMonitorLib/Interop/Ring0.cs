﻿// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace LibreHardwareMonitor.Interop;

/// <summary>
/// Driver with access at kernel level.
/// </summary>
internal static class Ring0
{
    public const uint INVALID_PCI_ADDRESS = 0xFFFFFFFF;

    private const uint OLS_TYPE = 40000;

    public static readonly Kernel32.IOControlCode IOCTL_OLS_GET_DRIVER_VERSION = new(OLS_TYPE, 0x800, Kernel32.IOControlCode.Access.Any);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_GET_REFCOUNT = new(OLS_TYPE, 0x801, Kernel32.IOControlCode.Access.Any);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_READ_MSR = new(OLS_TYPE, 0x821, Kernel32.IOControlCode.Access.Any);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_WRITE_MSR = new(OLS_TYPE, 0x822, Kernel32.IOControlCode.Access.Any);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_READ_PMC = new(OLS_TYPE, 0x823, Kernel32.IOControlCode.Access.Any);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_READ_IO_PORT = new(OLS_TYPE, 0x831, Kernel32.IOControlCode.Access.Read);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_WRITE_IO_PORT = new(OLS_TYPE, 0x832, Kernel32.IOControlCode.Access.Write);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_READ_IO_PORT_BYTE = new(OLS_TYPE, 0x833, Kernel32.IOControlCode.Access.Read);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_READ_IO_PORT_WORD = new(OLS_TYPE, 0x834, Kernel32.IOControlCode.Access.Read);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_READ_IO_PORT_DWORD = new(OLS_TYPE, 0x835, Kernel32.IOControlCode.Access.Read);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_WRITE_IO_PORT_BYTE = new(OLS_TYPE, 0x836, Kernel32.IOControlCode.Access.Write);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_WRITE_IO_PORT_WORD = new(OLS_TYPE, 0x837, Kernel32.IOControlCode.Access.Write);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_WRITE_IO_PORT_DWORD = new(OLS_TYPE, 0x838, Kernel32.IOControlCode.Access.Write);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_READ_PCI_CONFIG = new(OLS_TYPE, 0x851, Kernel32.IOControlCode.Access.Read);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_WRITE_PCI_CONFIG = new(OLS_TYPE, 0x852, Kernel32.IOControlCode.Access.Write);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_READ_MEMORY = new(OLS_TYPE, 0x841, Kernel32.IOControlCode.Access.Read);
    public static readonly Kernel32.IOControlCode IOCTL_OLS_WRITE_MEMORY = new(OLS_TYPE, 0x842, Kernel32.IOControlCode.Access.Write);
}
