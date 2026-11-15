using System.Text;
using System.Windows.Forms;

namespace EveOPreview.View
{
	/// <summary>
	/// Produces short, human readable labels for key combinations.
	/// </summary>
	public static class HotkeyFormatter
	{
		public static string Format(Keys keyData)
		{
			if (keyData == Keys.None)
			{
				return "(none)";
			}

			StringBuilder builder = new StringBuilder();

			if ((keyData & Keys.Control) == Keys.Control)
			{
				builder.Append("Ctrl + ");
			}
			if ((keyData & Keys.Alt) == Keys.Alt)
			{
				builder.Append("Alt + ");
			}
			if ((keyData & Keys.Shift) == Keys.Shift)
			{
				builder.Append("Shift + ");
			}

			Keys keyCode = keyData & Keys.KeyCode;
			builder.Append(FormatKeyCode(keyCode));

			return builder.ToString();
		}

		private static string FormatKeyCode(Keys keyCode)
		{
			switch (keyCode)
			{
				case Keys.D0:
				case Keys.D1:
				case Keys.D2:
				case Keys.D3:
				case Keys.D4:
				case Keys.D5:
				case Keys.D6:
				case Keys.D7:
				case Keys.D8:
				case Keys.D9:
					return keyCode.ToString().Substring(1); // "D5" -> "5"
				case Keys.Oemplus:
					return "+";
				case Keys.OemMinus:
					return "-";
				default:
					return keyCode.ToString();
			}
		}
	}
}
