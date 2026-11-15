using System;
using System.Drawing;
using System.Windows.Forms;
using EveOPreview.Services;

namespace EveOPreview.View
{
	/// <summary>
	/// Modal dialog that captures a gamepad button to bind to a client. It
	/// listens on the gamepad hook so the actual button the user presses on the
	/// controller is captured.
	/// </summary>
	public sealed class GamepadButtonInputDialog : Form
	{
		private readonly IGamepadHook _gamepadHook;
		private readonly Label _captureLabel;

		public string SelectedButton { get; private set; }

		public GamepadButtonInputDialog(string clientTitle, string currentButton, IGamepadHook gamepadHook)
		{
			this._gamepadHook = gamepadHook;
			this.SelectedButton = currentButton;

			this.Text = "Assign Gamepad Button";
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.StartPosition = FormStartPosition.CenterParent;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.ShowInTaskbar = false;
			this.TopMost = true;
			this.ClientSize = new Size(360, 150);

			Label prompt = new Label
			{
				AutoSize = false,
				Location = new Point(12, 12),
				Size = new Size(336, 32),
				Text = $"Press a button on your controller to switch to\n\"{StripPrefix(clientTitle)}\"."
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

			Button okButton = new Button
			{
				Text = "OK",
				DialogResult = DialogResult.OK,
				Location = new Point(192, 108),
				Size = new Size(75, 28)
			};

			Button cancelButton = new Button
			{
				Text = "Cancel",
				DialogResult = DialogResult.Cancel,
				Location = new Point(273, 108),
				Size = new Size(75, 28)
			};

			Button clearButton = new Button
			{
				Text = "Clear",
				Location = new Point(12, 108),
				Size = new Size(75, 28)
			};
			clearButton.Click += (s, e) =>
			{
				this.SelectedButton = null;
				this.UpdateCaptureLabel();
			};

			this.Controls.Add(prompt);
			this.Controls.Add(this._captureLabel);
			this.Controls.Add(clearButton);
			this.Controls.Add(okButton);
			this.Controls.Add(cancelButton);

			this.AcceptButton = okButton;
			this.CancelButton = cancelButton;

			this.UpdateCaptureLabel();

			this._gamepadHook.GamepadButtonPressed += this.OnGamepadButtonPressed;
			this.FormClosed += (s, e) => this._gamepadHook.GamepadButtonPressed -= this.OnGamepadButtonPressed;
		}

		private void OnGamepadButtonPressed(string button)
		{
			if (string.IsNullOrEmpty(button))
			{
				return;
			}

			if (this.InvokeRequired)
			{
				this.BeginInvoke(new Action(() => this.OnGamepadButtonPressed(button)));
				return;
			}

			this.SelectedButton = button;
			this.UpdateCaptureLabel();
		}

		private void UpdateCaptureLabel()
		{
			this._captureLabel.Text = string.IsNullOrEmpty(this.SelectedButton)
				? "(none - press a controller button, or Clear)"
				: FormatButton(this.SelectedButton);
		}

		private static string FormatButton(string button)
		{
			switch (button)
			{
				case "DPadUp":
					return "D-Pad Up";
				case "DPadDown":
					return "D-Pad Down";
				case "DPadLeft":
					return "D-Pad Left";
				case "DPadRight":
					return "D-Pad Right";
				case "LeftShoulder":
					return "Left Shoulder (LB)";
				case "RightShoulder":
					return "Right Shoulder (RB)";
				case "LeftThumb":
					return "Left Stick (click)";
				case "RightThumb":
					return "Right Stick (click)";
				default:
					return button;
			}
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
