using System;
using System.Collections.Generic;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Whim;

public class WindowManager : IWindowManager
{
	private readonly IConfigContext _configContext;

	public Commander Commander { get; } = new();

	public event EventHandler<WindowEventArgs>? WindowRegistered;
	public event EventHandler<WindowUpdateEventArgs>? WindowUpdated;
	public event EventHandler<WindowEventArgs>? WindowFocused;
	public event EventHandler<WindowEventArgs>? WindowUnregistered;

	/// <summary>
	/// Map of <see cref="HWND"/> to <see cref="IWindow"/> for easy <see cref="IWindow"/> lookup.
	/// </summary>
	private readonly Dictionary<HWND, IWindow> _windows = new();

	/// <summary>
	/// All the hooks registered with <see cref="PInvoke.SetWinEventHook"/>.
	/// </summary>
	private readonly UnhookWinEventSafeHandle[] _registeredHooks = new UnhookWinEventSafeHandle[6];

	/// <summary>
	/// The delegate for handling all events triggered by <see cref="PInvoke.SetWinEventHook"/>.
	/// </summary>
	private readonly WINEVENTPROC _hookDelegate;

	/// <summary>
	/// Indicates whether values have been disposed.
	/// </summary>
	private bool disposedValue;

	public WindowManager(IConfigContext configContext)
	{
		_configContext = configContext;
		_hookDelegate = new WINEVENTPROC(WindowsEventHook);
	}

	public void Initialize()
	{
		Logger.Debug("Initializing window manager...");

		// Each of the following hooks register just one or two event constants from https://docs.microsoft.com/en-us/windows/win32/winauto/event-constants
		_registeredHooks[0] = Win32Helper.SetWindowsEventHook(PInvoke.EVENT_OBJECT_DESTROY, PInvoke.EVENT_OBJECT_SHOW, _hookDelegate);
		_registeredHooks[1] = Win32Helper.SetWindowsEventHook(PInvoke.EVENT_OBJECT_CLOAKED, PInvoke.EVENT_OBJECT_UNCLOAKED, _hookDelegate);
		_registeredHooks[2] = Win32Helper.SetWindowsEventHook(PInvoke.EVENT_SYSTEM_MINIMIZESTART, PInvoke.EVENT_SYSTEM_MINIMIZEEND, _hookDelegate);
		_registeredHooks[3] = Win32Helper.SetWindowsEventHook(PInvoke.EVENT_SYSTEM_MOVESIZESTART, PInvoke.EVENT_SYSTEM_MOVESIZEEND, _hookDelegate);
		_registeredHooks[4] = Win32Helper.SetWindowsEventHook(PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND, _hookDelegate);
		_registeredHooks[5] = Win32Helper.SetWindowsEventHook(PInvoke.EVENT_OBJECT_LOCATIONCHANGE, PInvoke.EVENT_OBJECT_LOCATIONCHANGE, _hookDelegate);

		// If any of the above hooks are invalid, we dispose the WindowManager instance and return false.
		for (int i = 0; i < _registeredHooks.Length; i++)
		{
			if (_registeredHooks[i].IsInvalid)
			{
				// Disposing is handled by the caller.
				throw new InvalidOperationException($"Failed to register hook {i}");
			}
		}
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				foreach (UnhookWinEventSafeHandle? hook in _registeredHooks)
				{
					if (hook == null || hook.IsClosed || hook.IsInvalid)
					{
						continue;
					}

					hook.Dispose();
				}
			}

			// free unmanaged resources (unmanaged objects) and override finalizer
			// set large fields to null
			disposedValue = true;
		}
	}

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		System.GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Verifies that the event is for the provided <see cref="HWND"/>. <br/>
	///
	/// Documentation here is based on https://docs.microsoft.com/en-us/windows/win32/api/winuser/nc-winuser-wineventproc
	/// </summary>
	/// <param name="idChild">
	/// Identifies whether the event was triggered by an object or a child element of the object.
	/// If this value is CHILDID_SELF, the event was triggered by the object; otherwise, this value
	/// is the child ID of the element that triggered the event.
	/// </param>
	/// <param name="idObject">
	/// Identifies the object associated with the event. This is one of the object identifiers or a
	/// custom object ID.
	/// </param>
	/// <param name="hwnd">
	/// Handle to the window that generates the event, or NULL if no window is associated with the
	/// event. For example, the mouse pointer is not associated with a window.
	/// </param>
	/// <returns></returns>
	private static bool IsEventWindowValid(int idChild, int idObject, HWND? hwnd) =>
		// When idChild is CHILDID_SELF (0), the event was triggered
		// by the object.
		idChild == PInvoke.CHILDID_SELF
			   // When idObject == OBJID_WINDOW (0), the event is
			   // associated with the window (not a child object).
			   && idObject == (int)OBJECT_IDENTIFIER.OBJID_WINDOW
			   // The handle is not null.
			   && hwnd != null;

	/// <summary>
	/// Event hook for <see cref="PInvoke.SetWinEventHook"/>. <br />
	///
	/// For more, see https://docs.microsoft.com/en-us/windows/win32/api/winuser/nc-winuser-wineventproc
	/// </summary>
	/// <param name="hWinEventHook"></param>
	/// <param name="eventType"></param>
	/// <param name="hwnd"></param>
	/// <param name="idObject"></param>
	/// <param name="idChild"></param>
	/// <param name="idEventThread"></param>
	/// <param name="dwmsEventTime"></param>
	private void WindowsEventHook(HWINEVENTHOOK hWinEventHook, uint eventType, HWND hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
	{
		if (!IsEventWindowValid(idChild, idObject, hwnd)) { return; }

		// Handle registering and unregistering of windows.
		if (eventType == PInvoke.EVENT_OBJECT_SHOW)
		{
			if (!Win32Helper.IsSplashScreen(hwnd)
				&& Win32Helper.IsStandardWindow(hwnd)
				&& Win32Helper.HasNoVisibleOwner(hwnd))
			{
				RegisterWindow(hwnd);
			}

			return;
		}
		else if (eventType == PInvoke.EVENT_OBJECT_DESTROY)
		{
			TryUnregisterWindow(hwnd);
			return;
		}

		// Handle other events within the window.
		if (_windows.TryGetValue(hwnd, out IWindow? window) && window != null)
		{
			window.HandleEvent(eventType);
		}
	}

	/// <summary>
	/// Register the given <see cref="HWND"/> as an <see cref="IWindow"/> inside this
	/// <see cref="IWindowManager"/>.
	/// </summary>
	/// <param name="hwnd"></param>
	/// <returns></returns>
	private IWindow? RegisterWindow(HWND hwnd)
	{
		Pointer pointer = new(hwnd);
		Logger.Debug($"Registering window {pointer}");

		Window? window = Window.RegisterWindow(pointer, _configContext);

		if (window == null || _configContext.FilterManager.FilterWindow(window))
		{
			return null;
		}

		// Try add the window to the dictionary.
		if (!_windows.TryAdd(hwnd, window))
		{
			Logger.Debug($"Failed to register {window}");
			return null;
		}

		Logger.Debug($"Registered {window}");
		WindowRegistered?.Invoke(this, new WindowEventArgs(window));
		return window;
	}

	/// <summary>
	/// Try unregister the given <see cref="HWND"/>'s associated <see cref="IWindow"/> from this
	/// <see cref="IWindowManager"/>.
	/// </summary>
	/// <param name="hwnd"></param>
	private void TryUnregisterWindow(HWND hwnd)
	{
		Logger.Debug($"Unregistering {hwnd.Value}");

		if (!_windows.TryGetValue(hwnd, out IWindow? window) || window == null)
		{
			Logger.Debug($"Window {hwnd.Value} is not registered");
			return;
		}

		_windows.Remove(hwnd);
		if (window is Window win)
		{
			WindowUnregistered?.Invoke(this, new WindowEventArgs(win));
		}
	}

	public void TriggerWindowUpdated(WindowUpdateEventArgs args)
	{
		WindowUpdated?.Invoke(this, args);
	}

	public void TriggerWindowFocused(WindowEventArgs args)
	{
		WindowFocused?.Invoke(this, args);
	}

	public void TriggerWindowUnregistered(WindowEventArgs args)
	{
		WindowUnregistered?.Invoke(this, args);
	}
}
