// -----------------------------------------------------------------------------
// GlobalPushToTalkHook.cs
// Author: Kurt Mitchell
//
// IPushToTalkSource via a WH_KEYBOARD_LL low-level keyboard hook. The HUD
// window is click-through and never focused, so push-to-talk must be captured
// system-wide (CLAUDE.md Phase 3). The hook fires on the thread that installed
// it, which must pump messages — install from the WPF UI thread.
//
// The key event is passed through to the rest of the system (not swallowed);
// F8 is chosen precisely because other apps rarely bind it. Auto-repeat
// key-downs while held are filtered so Pressed fires exactly once per hold.
// -----------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using Infinyte.RayNeo.Hud.Interop;

namespace Infinyte.RayNeo.Hud.Voice;

using Infinyte.RayNeo.Voice;

/// <summary>System-wide hold-to-talk key monitor.</summary>
public sealed class GlobalPushToTalkHook : IPushToTalkSource
{
    private readonly int _virtualKey;

    // The delegate is stored in a field so the GC cannot collect it while the
    // OS still holds the callback pointer — a classic hook pitfall.
    private readonly NativeMethods.LowLevelKeyboardProc _proc;

    private IntPtr _hook = IntPtr.Zero;
    private bool _isDown;

    /// <summary>Creates the hook for <paramref name="virtualKey"/> (e.g. 0x77 for F8).</summary>
    public GlobalPushToTalkHook(int virtualKey)
    {
        _virtualKey = virtualKey;
        _proc = HookCallback;
    }

    /// <inheritdoc/>
    public event EventHandler? Pressed;

    /// <inheritdoc/>
    public event EventHandler? Released;

    /// <summary>Installs the hook. Call from a thread with a message pump (the UI thread).</summary>
    /// <exception cref="InvalidOperationException">The hook could not be installed.</exception>
    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }
        _hook = NativeMethods.SetWindowsHookExW(
            NativeMethods.WH_KEYBOARD_LL, _proc,
            NativeMethods.GetModuleHandleW(null), 0);
        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Failed to install the push-to-talk keyboard hook (error {Marshal.GetLastWin32Error()}).");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if (info.vkCode == (uint)_virtualKey)
            {
                long message = wParam.ToInt64();
                if (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
                {
                    if (!_isDown) // filter keyboard auto-repeat while held
                    {
                        _isDown = true;
                        Pressed?.Invoke(this, EventArgs.Empty);
                    }
                }
                else if (message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
                {
                    if (_isDown)
                    {
                        _isDown = false;
                        Released?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}
