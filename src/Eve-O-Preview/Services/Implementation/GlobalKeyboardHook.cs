using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EveOPreview.Services
{
	/// <summary>
	/// Installs a WH_KEYBOARD_LL hook on the thread that calls <see cref="Start"/>
	/// (the UI thread, which pumps a message loop) and raises
	/// <see cref="KeyComboPressed"/> when a key is tapped while Insert is held.
	///
	/// Insert itself is never swallowed, so Insert keeps working normally. Only an
	/// action key that the handler reports as bound (returns true) is swallowed, so
	/// it does not also reach the game. Auto-repeat of a held action key is
	/// suppressed so a client is switched to once per physical press, not per repeat.
	/// </summary>
	sealed class GlobalKeyboardHook : IGlobalKeyboardHook, IDisposable
	{
		private const int WH_KEYBOARD_LL = 13;
		private const int WM_KEYDOWN = 0x0100;
		private const int WM_SYSKEYDOWN = 0x0104;
		private const int WM_KEYUP = 0x0101;
		private const int WM_SYSKEYUP = 0x0105;
		private const int VK_INSERT = 0x2D;

		private readonly LowLevelKeyboardProc _proc;
		private IntPtr _hookId = IntPtr.Zero;

		private bool _insertDown;
		// Action keys currently held down (to suppress keyboard auto-repeat) and the
		// subset of those whose combo was handled (so repeats stay swallowed too).
		private readonly HashSet<int> _actionKeysDown = new HashSet<int>();
		private readonly HashSet<int> _swallowedKeysDown = new HashSet<int>();

		public event Func<Keys, bool> KeyComboPressed;

		public GlobalKeyboardHook()
		{
			// Keep a reference to the delegate so it is not garbage collected while
			// the native hook still points at it.
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
				this._hookId = SetWindowsHookEx(WH_KEYBOARD_LL, this._proc, GetModuleHandle(module.ModuleName), 0);
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
			this._insertDown = false;
			this._actionKeysDown.Clear();
			this._swallowedKeysDown.Clear();
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
				// vkCode is the first field of KBDLLHOOKSTRUCT.
				int vkCode = Marshal.ReadInt32(lParam);

				if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
				{
					if (vkCode == VK_INSERT)
					{
						this._insertDown = true;
					}
					else if (this._insertDown)
					{
						// Auto-repeat of a still-held action key: keep the original
						// decision but do not switch clients again.
						if (!this._actionKeysDown.Add(vkCode))
						{
							return this._swallowedKeysDown.Contains(vkCode)
								? (IntPtr)1
								: CallNextHookEx(this._hookId, nCode, wParam, lParam);
						}

						if (this.RaiseComboPressed((Keys)vkCode))
						{
							this._swallowedKeysDown.Add(vkCode);
							// Swallow the action key so the game does not also receive it.
							return (IntPtr)1;
						}
					}
				}
				else if (message == WM_KEYUP || message == WM_SYSKEYUP)
				{
					if (vkCode == VK_INSERT)
					{
						this._insertDown = false;
					}

					this._actionKeysDown.Remove(vkCode);
					this._swallowedKeysDown.Remove(vkCode);
				}
			}

			return CallNextHookEx(this._hookId, nCode, wParam, lParam);
		}

		private bool RaiseComboPressed(Keys key)
		{
			try
			{
				return this.KeyComboPressed?.Invoke(key) ?? false;
			}
			catch
			{
				// Never let an exception escape into the native hook chain.
				return false;
			}
		}

		#region Native methods
		private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

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
