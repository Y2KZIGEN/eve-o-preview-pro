using System;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View
{
	/// <summary>
	/// Small modal dialog that captures a target EVE client window resolution
	/// (the render / client area, width x height). Used to force a fixed size on
	/// a single client or - when <paramref name="isAllClients"/> is set - as the
	/// default applied to every client that has no per-client override.
	/// </summary>
	public sealed class WindowSizeInputDialog : Form
	{
		private readonly NumericUpDown _widthInput;
		private readonly NumericUpDown _heightInput;
		private readonly ComboBox _presetCombo;

		private bool _suppressPresetSync;

		/// <summary>
		/// The chosen size. <see cref="Size.Empty"/> means the user cleared the
		/// assignment (no forced size).
		/// </summary>
		public Size SelectedSize { get; private set; }

		private static readonly (string Label, int Width, int Height)[] Presets =
		{
			("Custom", 0, 0),
			("1280 x 720", 1280, 720),
			("1600 x 900", 1600, 900),
			("1920 x 1080", 1920, 1080),
			("2560 x 1440", 2560, 1440),
			("3440 x 1440", 3440, 1440),
			("3840 x 2160", 3840, 2160)
		};

		public WindowSizeInputDialog(string clientTitle, Size currentSize, bool isAllClients)
		{
			this.SelectedSize = currentSize;

			this.Text = isAllClients ? "Set Window Size (All Clients)" : "Set Window Size";
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.StartPosition = FormStartPosition.CenterParent;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.ShowInTaskbar = false;
			this.TopMost = true;
			this.ClientSize = new Size(340, 186);

			string target = isAllClients ? "all clients" : $"\"{StripPrefix(clientTitle)}\"";

			Label promptLabel = new Label
			{
				AutoSize = false,
				Location = new Point(12, 12),
				Size = new Size(316, 32),
				Text = $"Set the window resolution for {target}.\nPosition is left to EVE; only the size is forced."
			};

			Label presetLabel = new Label
			{
				AutoSize = true,
				Location = new Point(12, 54),
				Text = "Preset:"
			};

			this._presetCombo = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Location = new Point(96, 50),
				Size = new Size(140, 24)
			};
			foreach ((string label, _, _) in Presets)
			{
				this._presetCombo.Items.Add(label);
			}
			this._presetCombo.SelectedIndex = 0;
			this._presetCombo.SelectedIndexChanged += this.PresetChanged;

			Label widthLabel = new Label
			{
				AutoSize = true,
				Location = new Point(12, 92),
				Text = "Width:"
			};

			this._widthInput = new NumericUpDown
			{
				Location = new Point(96, 88),
				Size = new Size(80, 24),
				Minimum = 320,
				Maximum = 7680,
				Increment = 1
			};

			Label heightLabel = new Label
			{
				AutoSize = true,
				Location = new Point(188, 92),
				Text = "Height:"
			};

			this._heightInput = new NumericUpDown
			{
				Location = new Point(248, 88),
				Size = new Size(80, 24),
				Minimum = 240,
				Maximum = 4320,
				Increment = 1
			};

			// Seed the inputs with the current size, or a sensible 1920x1080 default.
			int seedWidth = currentSize.Width >= 320 ? currentSize.Width : 1920;
			int seedHeight = currentSize.Height >= 240 ? currentSize.Height : 1080;
			this._widthInput.Value = Math.Min(Math.Max(seedWidth, 320), 7680);
			this._heightInput.Value = Math.Min(Math.Max(seedHeight, 240), 4320);

			this._widthInput.ValueChanged += this.DimensionChanged;
			this._heightInput.ValueChanged += this.DimensionChanged;
			this.SyncPresetToDimensions();

			Button okButton = new Button
			{
				Text = "OK",
				DialogResult = DialogResult.OK,
				Location = new Point(172, 146),
				Size = new Size(75, 28)
			};
			okButton.Click += (s, e) =>
			{
				this.SelectedSize = new Size((int)this._widthInput.Value, (int)this._heightInput.Value);
			};

			Button cancelButton = new Button
			{
				Text = "Cancel",
				DialogResult = DialogResult.Cancel,
				Location = new Point(253, 146),
				Size = new Size(75, 28)
			};

			Button clearButton = new Button
			{
				Text = "Clear",
				Location = new Point(12, 146),
				Size = new Size(75, 28)
			};
			clearButton.Click += (s, e) =>
			{
				// Clearing removes the forced size entirely.
				this.SelectedSize = Size.Empty;
				this.DialogResult = DialogResult.OK;
				this.Close();
			};

			this.Controls.Add(promptLabel);
			this.Controls.Add(presetLabel);
			this.Controls.Add(this._presetCombo);
			this.Controls.Add(widthLabel);
			this.Controls.Add(this._widthInput);
			this.Controls.Add(heightLabel);
			this.Controls.Add(this._heightInput);
			this.Controls.Add(clearButton);
			this.Controls.Add(okButton);
			this.Controls.Add(cancelButton);

			this.AcceptButton = okButton;
			this.CancelButton = cancelButton;
		}

		private void PresetChanged(object sender, EventArgs e)
		{
			if (this._suppressPresetSync)
			{
				return;
			}

			int index = this._presetCombo.SelectedIndex;
			if (index <= 0 || index >= Presets.Length)
			{
				return; // "Custom" - leave the numeric values as-is
			}

			this._suppressPresetSync = true;
			this._widthInput.Value = Presets[index].Width;
			this._heightInput.Value = Presets[index].Height;
			this._suppressPresetSync = false;
		}

		private void DimensionChanged(object sender, EventArgs e)
		{
			if (this._suppressPresetSync)
			{
				return;
			}

			this.SyncPresetToDimensions();
		}

		private void SyncPresetToDimensions()
		{
			this._suppressPresetSync = true;

			int width = (int)this._widthInput.Value;
			int height = (int)this._heightInput.Value;
			int matchIndex = 0; // default to "Custom"

			for (int i = 1; i < Presets.Length; i++)
			{
				if (Presets[i].Width == width && Presets[i].Height == height)
				{
					matchIndex = i;
					break;
				}
			}

			this._presetCombo.SelectedIndex = matchIndex;
			this._suppressPresetSync = false;
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
