using System;
using System.Windows.Forms;

namespace SSLCertificateMaker
{
	public partial class PasswordPrompt : Form
	{
		/// <summary>
		/// If true, the OK button was clicked.  If false, it was not.
		/// </summary>
		public bool OkWasClicked = false;
		/// <summary>
		/// The user-entered password.
		/// </summary>
		public string EnteredPassword
		{
			get
			{
				return txtPassword.Text;
			}
		}
		public PasswordPrompt(string title = "Password Prompt", string labelText = "Enter the password:")
		{
			InitializeComponent();
			Text = title;
			lblPasswordPrompt.Text = labelText;
			CbMask_CheckedChanged(null, null);
		}

		private void CbMask_CheckedChanged(object sender, EventArgs e)
		{
			txtPassword.UseSystemPasswordChar = cbMask.Checked;
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
			txtPassword.Clear();
			Close();
		}
	}
}
