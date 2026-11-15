using System;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View
{
	/// <summary>
	/// Small modal dialog that captures a single keyboard hotkey combination.
	/// Because EVE-O Preview registers client hotkeys as global system hotkeys
	/// (RegisterHotKey), anything that emits a keystroke - including a Stream Deck
	/// button configured with a "Hotkey" action - is captured here as well.
	/// </summary>
	public sealed class HotkeyInputDialog : Form
	{
		private readonly Label _promptLabel;
		private readonly Label _captureLabel;
		private readonly Button _okButton;
		private readonly Button _cancelButton;
		private readonly Button _clearButton;

		public Keys SelectedHotkey { get; private set; }

		public HotkeyInputDialog(string clientTitle, Keys currentHotkey)
		{
			this.SelectedHotkey = currentHotkey;

			this.Text = "Assign Keyboard Hotkey";
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.StartPosition = FormStartPosition.CenterParent;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.ShowInTaskbar = false;
			this.ClientSize = new Size(360, 150);
			this.KeyPreview = true;

			this._promptLabel = new Label
			{
				AutoSize = false,
				Location = new Point(12, 12),
				Size = new Size(336, 32),
				Text = $"Press the key combination to switch to\n\"{StripPrefix(clientTitle)}\"."
			};

			this._captureLabel = new Label
			{
				AutoSize = false,
				BorderStyle = BorderStyle.FixedSingle,
				TextAlign = ContentAlignment.MiddleCenter,
				Font = new Font(this.Font.FontFamily, 11f, FontStyle.Bold),
				Location = new Point(12, 52),
				Size = new Size(336, 40)
			};

			this._okButton = new Button
			{
				Text = "OK",
				DialogResult = DialogResult.OK,
				Location = new Point(192, 108),
				Size = new Size(75, 28)
			};

			this._cancelButton = new Button
			{
				Text = "Cancel",
				DialogResult = DialogResult.Cancel,
				Location = new Point(273, 108),
				Size = new Size(75, 28)
			};

			this._clearButton = new Button
			{
				Text = "Clear",
				Location = new Point(12, 108),
				Size = new Size(75, 28)
			};
			this._clearButton.Click += (s, e) =>
			{
				this.SelectedHotkey = Keys.None;
				this.UpdateCaptureLabel();
			};

			this.Controls.Add(this._promptLabel);
			this.Controls.Add(this._captureLabel);
			this.Controls.Add(this._clearButton);
			this.Controls.Add(this._okButton);
			this.Controls.Add(this._cancelButton);

			this.AcceptButton = this._okButton;
			this.CancelButton = this._cancelButton;

			this.UpdateCaptureLabel();
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			// Intercept the key here so that keys like Tab / Enter / arrows can be
			// captured as hotkeys instead of being consumed by dialog navigation.
			const int WM_KEYDOWN = 0x0100;
			const int WM_SYSKEYDOWN = 0x0104;

			if (msg.Msg == WM_KEYDOWN || msg.Msg == WM_SYSKEYDOWN)
			{
				Keys keyCode = keyData & Keys.KeyCode;

				// Escape cancels the dialog
				if (keyCode == Keys.Escape)
				{
					this.DialogResult = DialogResult.Cancel;
					this.Close();
					return true;
				}

				// Back / Delete clears the current assignment
				if (keyCode == Keys.Back || keyCode == Keys.Delete)
				{
					this.SelectedHotkey = Keys.None;
					this.UpdateCaptureLabel();
					return true;
				}

				// Ignore lone modifier presses - wait for the real key
				if (keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey
					|| keyCode == Keys.Menu || keyCode == Keys.LWin || keyCode == Keys.RWin
					|| keyCode == Keys.None)
				{
					return true;
				}

				this.SelectedHotkey = keyData;
				this.UpdateCaptureLabel();
				return true;
			}

			return base.ProcessCmdKey(ref msg, keyData);
		}

		private void UpdateCaptureLabel()
		{
			this._captureLabel.Text = this.SelectedHotkey == Keys.None
				? "(none - press a key, or Clear)"
				: HotkeyFormatter.Format(this.SelectedHotkey);
		}

		private static string StripPrefix(string title)
		{
			if (string.IsNullOrEmpty(title))
			{
				return title;
			}

			return title.Replace("EVE - ", "").Replace("EVE Frontier - ", "");
		}
	}
}
