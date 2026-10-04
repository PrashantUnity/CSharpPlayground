using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI;

/// <summary>
/// Cross-platform utility that applies OS-level window display affinity and capture protection.
/// Prevents external recording tools (Zoom, Microsoft Teams, Discord, OBS, screen recorders)
/// from capturing confidential AI sessions, proprietary code, or private prompts.
/// </summary>
public static class WindowProtectionService
{
    private const uint WDA_NONE = 0x00000000;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
    private const uint WDA_MONITOR = 0x00000001;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_int(IntPtr receiver, IntPtr selector, int arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern bool objc_msgSend_bool_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    /// <summary>
    /// Applies or removes OS-level screen capture protection for the given Avalonia window.
    /// </summary>
    public static bool SetProtected(Window? window, bool isProtected)
    {
        if (window == null) return false;

        try
        {
            var platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null || platformHandle.Handle == IntPtr.Zero)
            {
                return false;
            }

            var handle = platformHandle.Handle;

            if (OperatingSystem.IsWindows())
            {
                return ApplyWindowsProtection(handle, isProtected);
            }
            if (OperatingSystem.IsMacOS())
            {
                return ApplyMacOsProtection(handle, isProtected);
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WindowProtection] Failed to configure capture protection: {ex.Message}");
            return false;
        }
    }

    private static bool ApplyWindowsProtection(IntPtr hwnd, bool isProtected)
    {
        try
        {
            uint affinity = isProtected ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE;
            bool result = SetWindowDisplayAffinity(hwnd, affinity);
            if (!result && isProtected)
            {
                // Fallback to WDA_MONITOR for older Windows 10 releases (< 2004)
                result = SetWindowDisplayAffinity(hwnd, WDA_MONITOR);
            }
            return result;
        }
        catch
        {
            return false;
        }
    }

    private static bool ApplyMacOsProtection(IntPtr handle, bool isProtected)
    {
        try
        {
            var nsWindow = handle;
            var windowSel = sel_registerName("window");
            var respondsToSelectorSel = sel_registerName("respondsToSelector:");

            // If the handle is an NSView rather than NSWindow, query its parent NSWindow
            if (objc_msgSend_bool_IntPtr(handle, respondsToSelectorSel, windowSel))
            {
                var parentWindow = objc_msgSend_IntPtr(handle, windowSel);
                if (parentWindow != IntPtr.Zero)
                {
                    nsWindow = parentWindow;
                }
            }

            var setSharingTypeSel = sel_registerName("setSharingType:");
            // NSWindowSharingNone = 0 (completely excluded from screen capture/sharing),
            // NSWindowSharingReadOnly = 1 (normal capture permitted)
            int sharingType = isProtected ? 0 : 1;
            objc_msgSend_void_int(nsWindow, setSharingTypeSel, sharingType);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
