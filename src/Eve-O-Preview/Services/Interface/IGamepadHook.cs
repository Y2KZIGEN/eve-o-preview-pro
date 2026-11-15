using System;

namespace EveOPreview.Services
{
	/// <summary>
	/// Polls XInput gamepads and raises an event when a button is pressed.
	/// Buttons are reported by name (e.g. "A", "LeftShoulder", "DPadUp") so the
	/// configuration can store them in a device independent, human readable form.
	/// </summary>
	public interface IGamepadHook
	{
		event Action<string> GamepadButtonPressed;

		void Start();
		void Stop();
	}
}
