using System;
using System.Windows.Forms;

namespace Simulant.UI
{
    public partial class SimpleInputForm : Form
    {
        public SimpleInputForm()
        {
            InitializeComponent();
            Shown += (_, __) =>
            {
                TopMost = true;
                Activate();
                BringToFront();
                TopMost = false;
            };
        }

        public SimpleInputForm(string title, string description, string defaultValue = "") : this()
        {
            Text = title;

            lblDesc.Text = description;
            if (string.IsNullOrWhiteSpace(description))
                lblDesc.Visible = false;

            txtInput.Text = defaultValue;
            txtInput.SelectAll();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        public string GetInput() => ShowDialog() == DialogResult.OK ? txtInput.Text : null;
    }
}
