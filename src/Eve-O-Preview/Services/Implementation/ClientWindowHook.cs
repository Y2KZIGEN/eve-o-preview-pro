using System;
using System.Runtime.InteropServices;

namespace EveOPreview.Services
{
	/// <summary>
	/// Installs a passive WinEvent hook for EVENT_SYSTEM_MOVESIZEEND on the thread
	/// that calls <see cref="Start"/> (the UI thread, which pumps a message loop).
	/// The OS invokes the callback only when a window finishes an interactive
	/// move/resize - there is no polling, so it is essentially free when idle.
	/// </summary>
	sealed class ClientWindowHook : IClientWindowHook, IDisposable
	{
		private const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
		private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
		private const int OBJID_WINDOW = 0;
		private const int CHILDID_SELF = 0;

		private readonly WinEventDelegate _proc;
		private IntPtr _hook = IntPtr.Zero;

		public event Action<IntPtr> WindowMoveResizeEnded;

		public ClientWindowHook()
		{
			// Keep a reference so the delegate is not collected while the native
			// hook still points at it.
			this._proc = this.Callback;
		}

		public void Start()
		{
			if (this._hook != IntPtr.Zero)
			{
				return;
			}

			this._hook = SetWinEventHook(
				EVENT_SYSTEM_MOVESIZEEND,
				EVENT_SYSTEM_MOVESIZEEND,
				IntPtr.Zero,
				this._proc,
				0,
				0,
				WINEVENT_OUTOFCONTEXT);
		}

		public void Stop()
		{
			if (this._hook == IntPtr.Zero)
			{
				return;
			}

			UnhookWinEvent(this._hook);
			this._hook = IntPtr.Zero;
		}

		public void Dispose()
		{
			this.Stop();
		}

		private void Callback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
		{
			// Only react to top-level window move/resize, not child objects
			if (hwnd == IntPtr.Zero || idObject != OBJID_WINDOW || idChild != CHILDID_SELF)
			{
				return;
			}

			try
			{
				this.WindowMoveResizeEnded?.Invoke(hwnd);
			}
			catch
			{
				// Never let an exception escape into the native event chain
			}
		}

		#region Native methods
		private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
		#endregion
	}
}
