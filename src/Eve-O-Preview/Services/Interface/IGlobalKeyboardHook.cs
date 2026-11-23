using System;
using System.Windows.Forms;

namespace EveOPreview.Services
{
	/// <summary>
	/// System-wide low level keyboard hook used to detect "Insert + key" combos.
	/// While the Insert key is held, tapping another (action) key raises
	/// <see cref="KeyComboPressed"/> with that action key - even while an EVE
	/// client has focus - so it can be bound to client switching.
	///
	/// Insert cannot be a Windows hotkey modifier (RegisterHotKey only accepts
	/// Ctrl/Alt/Shift/Win), which is why this prefix-combo path needs its own hook.
	/// The handler returns <c>true</c> when it consumed the combo; the action key
	/// is then swallowed so it does not also reach the game.
	/// </summary>
	public interface IGlobalKeyboardHook
	{
		/// <summary>
		/// Raised when a key is pressed while Insert is held. Return <c>true</c> to
		/// swallow the action key (combo handled), <c>false</c> to let it through.
		/// </summary>
		event Func<Keys, bool> KeyComboPressed;

		void Start();
		void Stop();
	}
}
