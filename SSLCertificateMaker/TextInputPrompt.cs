using System;
using System.Windows.Forms;

namespace SSLCertificateMaker
{
	public partial class TextInputPrompt : Form
	{
		/// <summary>
		/// If true, the OK button was clicked.  If false, it was not.
		/// </summary>
		public bool OkWasClicked = false;

		/// <summary>
		/// The user-entered text.
		/// </summary>
		public string EnteredText
		{
			get
			{
				return txtInput.Text;
			}
		}

		public TextInputPrompt(string title = "Text Input Prompt", string labelText = "Enter some text:")
		{
			InitializeComponent();
			Text = title;
			lblTextInputPrompt.Text = labelText;
		}

		private void BtnOk_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.OK;
			OkWasClicked = true;
			Close();
		}

		private void BtnCancel_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			txtInput.Clear();
			Close();
		}
	}
}
