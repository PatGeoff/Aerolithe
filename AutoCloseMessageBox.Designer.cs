namespace Aerolithe
{
    partial class AutoCloseMessageBox
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
            lbl_Message = new Label();
            button1 = new Button();
            SuspendLayout();
            // 
            // lbl_Message
            // 
            lbl_Message.Dock = DockStyle.Top;
            lbl_Message.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lbl_Message.Location = new Point(0, 0);
            lbl_Message.Name = "lbl_Message";
            lbl_Message.Size = new Size(624, 103);
            lbl_Message.TabIndex = 0;
            lbl_Message.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // button1
            // 
            button1.BackColor = Color.FromArgb(40, 40, 40);
            button1.Dock = DockStyle.Bottom;
            button1.Font = new Font("Segoe UI", 10.125F, FontStyle.Regular, GraphicsUnit.Point, 0);
            button1.Location = new Point(0, 100);
            button1.Name = "button1";
            button1.Size = new Size(624, 99);
            button1.TabIndex = 1;
            button1.Text = "OK";
            button1.UseVisualStyleBackColor = false;
            button1.Click += button1_Click;
            // 
            // AutoCloseMessageBox
            // 
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(30, 30, 30);
            ClientSize = new Size(624, 199);
            Controls.Add(button1);
            Controls.Add(lbl_Message);
            ForeColor = Color.White;
            Name = "AutoCloseMessageBox";
            Text = "Alerte";
            ResumeLayout(false);
        }

        #endregion

        private Label lbl_Message;
        private Button button1;
    }
}