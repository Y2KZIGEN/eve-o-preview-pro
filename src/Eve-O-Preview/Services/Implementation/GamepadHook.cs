using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace EveOPreview.Services
{
	/// <summary>
	/// Polls up to four XInput controllers on a dispatcher timer and raises
	/// <see cref="GamepadButtonPressed"/> once per button press (rising edge).
	/// XInput ships with Windows, so no third party dependency is required; it
	/// supports Xbox and the many XInput-compatible controllers.
	/// </summary>
	sealed class GamepadHook : IGamepadHook
	{
		private const int MAX_CONTROLLERS = 4;
		private const int POLL_INTERVAL_MS = 30;

		// XINPUT_GAMEPAD button bit flags
		private static readonly (ushort Flag, string Name)[] ButtonMap =
		{
			(0x0001, "DPadUp"),
			(0x0002, "DPadDown"),
			(0x0004, "DPadLeft"),
			(0x0008, "DPadRight"),
			(0x0010, "Start"),
			(0x0020, "Back"),
			(0x0040, "LeftThumb"),
			(0x0080, "RightThumb"),
			(0x0100, "LeftShoulder"),
			(0x0200, "RightShoulder"),
			(0x1000, "A"),
			(0x2000, "B"),
			(0x4000, "X"),
			(0x8000, "Y")
		};

		private readonly ushort[] _previousButtons = new ushort[MAX_CONTROLLERS];
		private DispatcherTimer _timer;
		private bool _xinputAvailable = true;

		public event Action<string> GamepadButtonPressed;

		public void Start()
		{
			if (this._timer != null)
			{
				return;
			}

			this._timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(POLL_INTERVAL_MS)
			};
			this._timer.Tick += this.Poll;
			this._timer.Start();
		}

		public void Stop()
		{
			if (this._timer == null)
			{
				return;
			}

			this._timer.Stop();
			this._timer.Tick -= this.Poll;
			this._timer = null;
		}

		private void Poll(object sender, EventArgs e)
		{
			if (!this._xinputAvailable)
			{
				this.Stop();
				return;
			}

			for (uint controller = 0; controller < MAX_CONTROLLERS; controller++)
			{
				XINPUT_STATE state = default;

				uint result;
				try
				{
					result = XInputGetState(controller, ref state);
				}
				catch (DllNotFoundException)
				{
					// No XInput on this machine - stop polling entirely
					this._xinputAvailable = false;
					return;
				}

				if (result != ERROR_SUCCESS)
				{
					// Controller not connected
					this._previousButtons[controller] = 0;
					continue;
				}

				ushort current = state.Gamepad.wButtons;
				ushort previous = this._previousButtons[controller];
				ushort newlyPressed = (ushort)(current & ~previous);

				if (newlyPressed != 0)
				{
					foreach ((ushort flag, string name) in ButtonMap)
					{
						if ((newlyPressed & flag) == flag)
						{
							this.RaiseButtonPressed(name);
						}
					}
				}

				this._previousButtons[controller] = current;
			}
		}

		private void RaiseButtonPressed(string button)
		{
			try
			{
				this.GamepadButtonPressed?.Invoke(button);
			}
			catch
			{
				// Swallow handler exceptions so polling keeps running
			}
		}

		#region Native methods
		private const uint ERROR_SUCCESS = 0;

		[StructLayout(LayoutKind.Sequential)]
		private struct XINPUT_GAMEPAD
		{
			public ushort wButtons;
			public byte bLeftTrigger;
			public byte bRightTrigger;
			public short sThumbLX;
			public short sThumbLY;
			public short sThumbRX;
			public short sThumbRY;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct XINPUT_STATE
		{
			public uint dwPacketNumber;
			public XINPUT_GAMEPAD Gamepad;
		}

		[DllImport("xinput1_4.dll")]
		private static extern uint XInputGetState(uint dwUserIndex, ref XINPUT_STATE pState);
		#endregion
	}
}
