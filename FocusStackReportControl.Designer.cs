namespace Aerolithe
{
    partial class FocusStackReportControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            tableLayoutPanel1 = new TableLayoutPanel();
            lbl_Rotation = new Label();
            lbl_Elevation = new Label();
            richTextBox_PicReport = new RichTextBox();
            lbl_Serie = new Label();
            btn_RepriseRoutine = new Button();
            tableLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 5;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54F));
            tableLayoutPanel1.Controls.Add(lbl_Rotation, 2, 0);
            tableLayoutPanel1.Controls.Add(lbl_Elevation, 1, 0);
            tableLayoutPanel1.Controls.Add(richTextBox_PicReport, 3, 0);
            tableLayoutPanel1.Controls.Add(lbl_Serie, 0, 0);
            tableLayoutPanel1.Controls.Add(btn_RepriseRoutine, 4, 0);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 1;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new Size(610, 20);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // lbl_Rotation
            // 
            lbl_Rotation.AutoSize = true;
            lbl_Rotation.Dock = DockStyle.Fill;
            lbl_Rotation.ForeColor = Color.White;
            lbl_Rotation.Location = new Point(123, 0);
            lbl_Rotation.Name = "lbl_Rotation";
            lbl_Rotation.Padding = new Padding(5, 0, 0, 0);
            lbl_Rotation.Size = new Size(54, 20);
            lbl_Rotation.TabIndex = 3;
            lbl_Rotation.Text = "1";
            lbl_Rotation.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lbl_Elevation
            // 
            lbl_Elevation.AutoSize = true;
            lbl_Elevation.Dock = DockStyle.Fill;
            lbl_Elevation.ForeColor = Color.White;
            lbl_Elevation.Location = new Point(63, 0);
            lbl_Elevation.Name = "lbl_Elevation";
            lbl_Elevation.Padding = new Padding(5, 0, 0, 0);
            lbl_Elevation.Size = new Size(54, 20);
            lbl_Elevation.TabIndex = 2;
            lbl_Elevation.Text = "1";
            lbl_Elevation.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // richTextBox_PicReport
            // 
            richTextBox_PicReport.BackColor = Color.FromArgb(40, 40, 40);
            richTextBox_PicReport.BorderStyle = BorderStyle.None;
            richTextBox_PicReport.Dock = DockStyle.Fill;
            richTextBox_PicReport.Location = new Point(183, 3);
            richTextBox_PicReport.Multiline = false;
            richTextBox_PicReport.Name = "richTextBox_PicReport";
            richTextBox_PicReport.ScrollBars = RichTextBoxScrollBars.None;
            richTextBox_PicReport.Size = new Size(370, 14);
            richTextBox_PicReport.TabIndex = 0;
            richTextBox_PicReport.Text = "";
            richTextBox_PicReport.WordWrap = false;
            // 
            // lbl_Serie
            // 
            lbl_Serie.AutoSize = true;
            lbl_Serie.Dock = DockStyle.Fill;
            lbl_Serie.ForeColor = Color.White;
            lbl_Serie.Location = new Point(3, 0);
            lbl_Serie.Name = "lbl_Serie";
            lbl_Serie.Padding = new Padding(5, 0, 0, 0);
            lbl_Serie.Size = new Size(54, 20);
            lbl_Serie.TabIndex = 1;
            lbl_Serie.Text = "1";
            lbl_Serie.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // btn_RepriseRoutine
            // 
            btn_RepriseRoutine.Dock = DockStyle.Fill;
            btn_RepriseRoutine.FlatAppearance.BorderColor = Color.FromArgb(64, 64, 64);
            btn_RepriseRoutine.FlatStyle = FlatStyle.Flat;
            btn_RepriseRoutine.Location = new Point(559, 3);
            btn_RepriseRoutine.Name = "btn_RepriseRoutine";
            btn_RepriseRoutine.Size = new Size(48, 14);
            btn_RepriseRoutine.TabIndex = 4;
            btn_RepriseRoutine.UseVisualStyleBackColor = true;
            btn_RepriseRoutine.Click += btn_RepriseRoutine_Click;
            // 
            // FocusStackReportControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(40, 40, 40);
            Controls.Add(tableLayoutPanel1);
            Name = "FocusStackReportControl";
            Size = new Size(610, 20);
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private RichTextBox richTextBox_PicReport;
        private Label lbl_Serie;
        private Label lbl_Rotation;
        private Label lbl_Elevation;
        private Button btn_RepriseRoutine;
    }
}
