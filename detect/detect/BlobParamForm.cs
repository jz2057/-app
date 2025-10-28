using System;
using System.Windows.Forms;

namespace YourNamespace
{
    public partial class BlobParamForm : Form
    {
        public int MinGray { get; private set; }
        public int MaxGray { get; private set; }
        public int MinArea { get; private set; }

        private TextBox txtMinGray;
        private TextBox txtMaxGray;
        private TextBox txtMinArea;

        public BlobParamForm(int defaultMinGray, int defaultMaxGray, int defaultMinArea)
        {
            this.Text = "设置Blob参数";
            this.Width = 300;
            this.Height = 200;
            this.StartPosition = FormStartPosition.CenterParent;

            Label lbl1 = new Label { Text = "最小灰度:", Left = 10, Top = 20, AutoSize = true };
            Label lbl2 = new Label { Text = "最大灰度:", Left = 10, Top = 60, AutoSize = true };
            Label lbl3 = new Label { Text = "最小面积:", Left = 10, Top = 100, AutoSize = true };

            txtMinGray = new TextBox { Left = 100, Top = 20, Width = 150, Text = defaultMinGray.ToString() };
            txtMaxGray = new TextBox { Left = 100, Top = 60, Width = 150, Text = defaultMaxGray.ToString() };
            txtMinArea = new TextBox { Left = 100, Top = 100, Width = 150, Text = defaultMinArea.ToString() };

            Button btnOk = new Button { Text = "确定", Left = 100, Top = 140, Width = 80 };
            btnOk.Click += BtnOk_Click;

            this.Controls.Add(lbl1);
            this.Controls.Add(lbl2);
            this.Controls.Add(lbl3);
            this.Controls.Add(txtMinGray);
            this.Controls.Add(txtMaxGray);
            this.Controls.Add(txtMinArea);
            this.Controls.Add(btnOk);
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            try
            {
                MinGray = int.Parse(txtMinGray.Text);
                MaxGray = int.Parse(txtMaxGray.Text);
                MinArea = int.Parse(txtMinArea.Text);
                this.DialogResult = DialogResult.OK;
            }
            catch
            {
                MessageBox.Show("请输入正确的数字！");
            }
        }
    }
}
