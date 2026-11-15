using System;
using System.Windows.Forms;

namespace EveOPreview.Services
{
	/// <summary>
	/// System-wide low level mouse hook. Used to detect extra mouse button
	/// presses (X1 / X2 / Middle) even while an EVE client has focus, so they
	/// can be bound to client switching.
	/// </summary>
	public interface IGlobalMouseHook
	{
		event Action<MouseButtons> MouseButtonPressed;

		void Start();
		void Stop();
	}
}
