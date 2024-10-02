using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Aerolithe
{
    public partial class AutoCloseMessageBox : Form
    {
        private DialogResult dialogResult;
        private Timer timer;
        private int duration;
        public AutoCloseMessageBox(string message, int width, int height)
        {
            InitializeComponent();
            DisplayMessage(message, width, height);
            StartPosition = FormStartPosition.CenterParent;            
        }
        public AutoCloseMessageBox(string message, int width, int height, int duration = 3000)
        {
            InitializeComponent();
            DisplayMessage(message, width, height);
            timer = new Timer();
            timer.Interval = duration;
            timer.Tick += new EventHandler(Timer_Tick);
            timer.Start();
            StartPosition = FormStartPosition.CenterParent;
        }

        private void DisplayMessage(string message, int width, int height)
        {
            lbl_Message.Text = message;
            lbl_Message.TextAlign = ContentAlignment.MiddleLeft;
            lbl_Message.MaximumSize = new Size(width - 20, 0); // Set a maximum width for the label
            lbl_Message.AutoSize = true;
            this.ClientSize = new Size(width, height);
            lbl_Message.Location = new Point((this.ClientSize.Width - lbl_Message.Width) / 2, (this.ClientSize.Height - lbl_Message.Height) / 2);
            
        }

        public static DialogResult ShowPressClose(string message, int width, int height)
        {
            AutoCloseMessageBox autoCloseAlert = new AutoCloseMessageBox(message, width, height);
            autoCloseAlert.ShowDialog();
            return autoCloseAlert.dialogResult;
        }
        public static void ShowAutoClose(string message, int width, int height, int duration)
        {
            AutoCloseMessageBox autoCloseAlert = new AutoCloseMessageBox(message, width, height);            
            autoCloseAlert.ShowDialog();            
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            timer.Stop();
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            dialogResult = DialogResult.OK;
            Close();
        }


       
    }
}
