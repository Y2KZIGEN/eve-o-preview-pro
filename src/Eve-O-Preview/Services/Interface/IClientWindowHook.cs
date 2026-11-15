using System;

namespace EveOPreview.Services
{
	/// <summary>
	/// Raises an event when any top-level window finishes an interactive move or
	/// resize (the user releases the mouse). Used to persist EVE client window
	/// positions the instant they are moved, without waiting for a client switch.
	/// </summary>
	public interface IClientWindowHook
	{
		event Action<IntPtr> WindowMoveResizeEnded;

		void Start();
		void Stop();
	}
}
