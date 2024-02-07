using Windows.Win32.Graphics.Gdi;

namespace Whim;

internal interface IInternalMonitorManager
{
	/// <summary>
	/// The last <see cref="IMonitor"/> which received an event sent by Windows which Whim did not ignore.
	/// </summary>
	IMonitor LastWhimActiveMonitor { get; set; }

	/// <summary>
	/// Try to get the <see cref="IMonitor"/> for the given <paramref name="hmonitor"/>.
	/// </summary>
	/// <param name="hmonitor"></param>
	/// <returns></returns>
	IMonitor? GetMonitorByHandle(HMONITOR hmonitor);

	/// <summary>
	/// Called when the window has been focused.
	/// </summary>
	/// <param name="window"></param>
	void OnWindowFocused(IWindow? window);

	/// <summary>
	/// Set the active monitor to the given monitor. This monitor must have no windows.
	/// </summary>
	/// <param name="monitor"></param>
	void ActivateEmptyMonitor(IMonitor monitor);
}
