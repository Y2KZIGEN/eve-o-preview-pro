using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EveOPreview.Services
{
	/// <summary>
	/// Installs a WH_MOUSE_LL hook on the thread that calls <see cref="Start"/>
	/// (expected to be the UI thread, which pumps a message loop) and raises
	/// <see cref="MouseButtonPressed"/> for extra mouse buttons. The hook is
	/// passive - it never swallows the event, so normal mouse behaviour is kept.
	/// </summary>
	sealed class GlobalMouseHook : IGlobalMouseHook, IDisposable
	{
		private const int WH_MOUSE_LL = 14;
		private const int WM_MBUTTONDOWN = 0x0207;
		private const int WM_XBUTTONDOWN = 0x020B;
		private const int XBUTTON1 = 0x0001;
		private const int XBUTTON2 = 0x0002;

		private readonly LowLevelMouseProc _proc;
		private IntPtr _hookId = IntPtr.Zero;

		public event Action<MouseButtons> MouseButtonPressed;

		public GlobalMouseHook()
		{
			// Keep a reference to the delegate so it is not garbage collected
			// while the native hook still points at it.
			this._proc = this.HookCallback;
		}

		public void Start()
		{
			if (this._hookId != IntPtr.Zero)
			{
				return;
			}

			using (Process process = Process.GetCurrentProcess())
			using (ProcessModule module = process.MainModule)
			{
				this._hookId = SetWindowsHookEx(WH_MOUSE_LL, this._proc, GetModuleHandle(module.ModuleName), 0);
			}
		}

		public void Stop()
		{
			if (this._hookId == IntPtr.Zero)
			{
				return;
			}

			UnhookWindowsHookEx(this._hookId);
			this._hookId = IntPtr.Zero;
		}

		public void Dispose()
		{
			this.Stop();
		}

		private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
		{
			if (nCode >= 0)
			{
				int message = wParam.ToInt32();

				if (message == WM_XBUTTONDOWN)
				{
					MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
					int xButton = (int)((hookStruct.mouseData >> 16) & 0xFFFF);

					MouseButtons button = xButton == XBUTTON1
						? MouseButtons.XButton1
						: xButton == XBUTTON2 ? MouseButtons.XButton2 : MouseButtons.None;

					if (button != MouseButtons.None)
					{
						this.RaiseButtonPressed(button);
					}
				}
				else if (message == WM_MBUTTONDOWN)
				{
					this.RaiseButtonPressed(MouseButtons.Middle);
				}
			}

			return CallNextHookEx(this._hookId, nCode, wParam, lParam);
		}

		private void RaiseButtonPressed(MouseButtons button)
		{
			try
			{
				this.MouseButtonPressed?.Invoke(button);
			}
			catch
			{
				// Never let an exception escape into the native hook chain
			}
		}

		#region Native methods
		private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

		[StructLayout(LayoutKind.Sequential)]
		private struct POINT
		{
			public int x;
			public int y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MSLLHOOKSTRUCT
		{
			public POINT pt;
			public uint mouseData;
			public uint flags;
			public uint time;
			public IntPtr dwExtraInfo;
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool UnhookWindowsHookEx(IntPtr hhk);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr GetModuleHandle(string lpModuleName);
		#endregion
	}
}
