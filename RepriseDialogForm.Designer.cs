namespace Aerolithe
{
    partial class RepriseDialogForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            btn_SerieSeulement = new Button();
            btn_ToutesSeries = new Button();
            btn_Cancel = new Button();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label1.ForeColor = Color.White;
            label1.Location = new Point(78, 39);
            label1.Name = "label1";
            label1.Size = new Size(312, 25);
            label1.TabIndex = 0;
            label1.Text = "Reprise des photos pour focus stack";
            // 
            // btn_SerieSeulement
            // 
            btn_SerieSeulement.BackColor = Color.FromArgb(40, 40, 40);
            btn_SerieSeulement.FlatAppearance.BorderColor = Color.FromArgb(64, 64, 64);
            btn_SerieSeulement.FlatStyle = FlatStyle.Flat;
            btn_SerieSeulement.ForeColor = Color.White;
            btn_SerieSeulement.Location = new Point(88, 91);
            btn_SerieSeulement.Name = "btn_SerieSeulement";
            btn_SerieSeulement.Size = new Size(282, 36);
            btn_SerieSeulement.TabIndex = 1;
            btn_SerieSeulement.Text = "Cette série seulement";
            btn_SerieSeulement.UseVisualStyleBackColor = false;
            btn_SerieSeulement.Click += btn_SerieSeulement_Click;
            // 
            // btn_ToutesSeries
            // 
            btn_ToutesSeries.BackColor = Color.FromArgb(40, 40, 40);
            btn_ToutesSeries.FlatAppearance.BorderColor = Color.FromArgb(64, 64, 64);
            btn_ToutesSeries.FlatStyle = FlatStyle.Flat;
            btn_ToutesSeries.ForeColor = Color.White;
            btn_ToutesSeries.Location = new Point(88, 143);
            btn_ToutesSeries.Name = "btn_ToutesSeries";
            btn_ToutesSeries.Size = new Size(282, 36);
            btn_ToutesSeries.TabIndex = 2;
            btn_ToutesSeries.Text = "Toutes les séries à partir de celle-ci";
            btn_ToutesSeries.UseVisualStyleBackColor = false;
            btn_ToutesSeries.Click += btn_ToutesSeries_Click;
            // 
            // btn_Cancel
            // 
            btn_Cancel.BackColor = Color.FromArgb(50, 40, 40);
            btn_Cancel.FlatAppearance.BorderColor = Color.FromArgb(64, 64, 64);
            btn_Cancel.FlatStyle = FlatStyle.Flat;
            btn_Cancel.ForeColor = Color.White;
            btn_Cancel.Location = new Point(88, 201);
            btn_Cancel.Name = "btn_Cancel";
            btn_Cancel.Size = new Size(282, 36);
            btn_Cancel.TabIndex = 3;
            btn_Cancel.Text = "Cancel";
            btn_Cancel.UseVisualStyleBackColor = false;
            btn_Cancel.Click += btn_Cancel_Click;
            // 
            // RepriseDialogForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(30, 30, 30);
            ClientSize = new Size(466, 258);
            Controls.Add(btn_Cancel);
            Controls.Add(btn_ToutesSeries);
            Controls.Add(btn_SerieSeulement);
            Controls.Add(label1);
            FormBorderStyle = FormBorderStyle.None;
            Name = "RepriseDialogForm";
            Text = "RepriseDialogForm";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private Button btn_SerieSeulement;
        private Button btn_ToutesSeries;
        private Button btn_Cancel;
    }
}