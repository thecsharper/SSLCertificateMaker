using System;
using System.Windows.Forms;

namespace SSLCertificateMaker
{
	public partial class EditMultiSelect : Form
	{
		/// <summary>
		/// A boolean indicating if OK was clicked.
		/// </summary>
		public bool OkWasClicked { get; private set; } = false;
		
		/// <summary>
		/// The array of items available for selection.
		/// </summary>
		public readonly object[] Items;
		
		/// <summary>
		/// Gets an array that indicates which item indices are currently selected.
		/// </summary>
		public bool[] SelectedIndices
		{
			get
			{
				bool[] s = new bool[Items.Length];
				for (int i = 0; i < s.Length; i++) 
				{ 
					s[i] = listOfItems.SelectedIndices.Contains(i);
                }

                return s;
			}
		}

		public EditMultiSelect(string title, object[] items, bool[] selectedIndices)
		{
			InitializeComponent();

			Text = title;
			Items = items;

			listOfItems.Items.AddRange(items);
			for (int i = 0; i < selectedIndices.Length; i++) { 
				if (selectedIndices[i])
				{
                    listOfItems.SelectedIndices.Add(i);
                }
            }
        }

		private void BtnOK_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.OK;
			OkWasClicked = true;
			Close();
		}

		private void BtnCancel_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}
}
