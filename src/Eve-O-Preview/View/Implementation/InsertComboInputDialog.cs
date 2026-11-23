using System;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View
{
	/// <summary>
	/// Captures the action key for an "Insert + key" combo. Insert is the fixed
	/// prefix (it cannot be a Windows hotkey modifier, so these combos are matched
	/// by a low-level keyboard hook, not RegisterHotKey). The user holds Insert and
	/// taps the key they want; only that action key is stored.
	/// </summary>
	public sealed class InsertComboInputDialog : Form
	{
		private readonly Label _promptLabel;
		private readonly Label _captureLabel;
		private readonly Button _okButton;
		private readonly Button _cancelButton;
		private readonly Button _clearButton;

		public Keys SelectedActionKey { get; private set; }

		public InsertComboInputDialog(string clientTitle, Keys currentActionKey)
		{
			// Only the base key matters - never any modifier flags.
			this.SelectedActionKey = currentActionKey & Keys.KeyCode;

			this.Text = "Assign Insert Combo";
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.StartPosition = FormStartPosition.CenterParent;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.ShowInTaskbar = false;
			this.TopMost = true;
			this.ClientSize = new Size(360, 150);
			this.KeyPreview = true;

			this._promptLabel = new Label
			{
				AutoSize = false,
				Location = new Point(12, 12),
				Size = new Size(336, 32),
				Text = $"Hold Insert and press a key to switch to\n\"{StripPrefix(clientTitle)}\"."
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
				this.SelectedActionKey = Keys.None;
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
			// captured as the action key instead of driving dialog navigation.
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
					this.SelectedActionKey = Keys.None;
					this.UpdateCaptureLabel();
					return true;
				}

				// Ignore the Insert prefix itself and lone modifier presses - wait
				// for the real action key.
				if (keyCode == Keys.Insert
					|| keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey
					|| keyCode == Keys.Menu || keyCode == Keys.LWin || keyCode == Keys.RWin
					|| keyCode == Keys.None)
				{
					return true;
				}

				// Store only the base key - the Insert prefix is implied.
				this.SelectedActionKey = keyCode;
				this.UpdateCaptureLabel();
				return true;
			}

			return base.ProcessCmdKey(ref msg, keyData);
		}

		private void UpdateCaptureLabel()
		{
			this._captureLabel.Text = this.SelectedActionKey == Keys.None
				? "(none - hold Insert and press a key, or Clear)"
				: "Insert + " + HotkeyFormatter.Format(this.SelectedActionKey);
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
