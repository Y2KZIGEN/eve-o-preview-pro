using System;
using System.Drawing;
using System.Windows.Forms;
using EveOPreview.Services;

namespace EveOPreview.View
{
	/// <summary>
	/// Modal dialog that captures an extra mouse button (X1 / X2 / Middle) to
	/// bind to a client. It listens on the global mouse hook so the actual
	/// button the user presses is captured, while the left / right buttons stay
	/// free to operate the dialog's own controls.
	/// </summary>
	public sealed class MouseButtonInputDialog : Form
	{
		private readonly IGlobalMouseHook _mouseHook;
		private readonly Label _captureLabel;

		public MouseButtons SelectedButton { get; private set; }

		public MouseButtonInputDialog(string clientTitle, MouseButtons currentButton, IGlobalMouseHook mouseHook)
		{
			this._mouseHook = mouseHook;
			this.SelectedButton = currentButton;

			this.Text = "Assign Mouse Button";
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
				Text = $"Press an extra mouse button (X1 / X2 / Middle)\nto switch to \"{StripPrefix(clientTitle)}\"."
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
				this.SelectedButton = MouseButtons.None;
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

			this._mouseHook.MouseButtonPressed += this.OnMouseButtonPressed;
			this.FormClosed += (s, e) => this._mouseHook.MouseButtonPressed -= this.OnMouseButtonPressed;
		}

		private void OnMouseButtonPressed(MouseButtons button)
		{
			if (button == MouseButtons.None)
			{
				return;
			}

			// The hook fires on the UI thread, but guard with Invoke to be safe.
			if (this.InvokeRequired)
			{
				this.BeginInvoke(new Action(() => this.OnMouseButtonPressed(button)));
				return;
			}

			this.SelectedButton = button;
			this.UpdateCaptureLabel();
		}

		private void UpdateCaptureLabel()
		{
			this._captureLabel.Text = this.SelectedButton == MouseButtons.None
				? "(none - press X1 / X2 / Middle, or Clear)"
				: FormatButton(this.SelectedButton);
		}

		private static string FormatButton(MouseButtons button)
		{
			switch (button)
			{
				case MouseButtons.XButton1:
					return "Mouse Button 4 (X1)";
				case MouseButtons.XButton2:
					return "Mouse Button 5 (X2)";
				case MouseButtons.Middle:
					return "Middle Mouse Button";
				default:
					return button.ToString();
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
