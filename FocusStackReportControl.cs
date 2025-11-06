using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aerolithe
{
    public partial class FocusStackReportControl : UserControl
    {
        private List<FocusStackTaskInfo> taskInfos = new List<FocusStackTaskInfo>();
        private FocusStackTaskInfo taskInfo;

        public FocusStackReportControl()
        {
            InitializeComponent();
        }

        public void SetTaskInfo(FocusStackTaskInfo info)
        {
            taskInfo = info;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            richTextBox_PicReport.Clear();

            if (taskInfo == null)
            {
                richTextBox_PicReport.SelectionColor = Color.LightGray;
                richTextBox_PicReport.AppendText(" La file d'attente est vide.");
                return;
            }

            string prefix = $"📷 {taskInfo.Filename}\t  — Statut :  ";

            lbl_Serie.Text = taskInfo.Serie.ToString();
            lbl_Elevation.Text = taskInfo.Elevation.ToString() + "°";
            lbl_Rotation.Text = taskInfo.Rotation.ToString() + "°";

            richTextBox_PicReport.SelectionColor = Color.White;
            richTextBox_PicReport.AppendText(prefix);

            switch (taskInfo.Status)
            {
                case "Terminé":
                    richTextBox_PicReport.SelectionColor = Color.LimeGreen;
                    break;
                case "En cours":
                    richTextBox_PicReport.SelectionColor = Color.Orange;
                    break;
                case "Erreur":
                    richTextBox_PicReport.SelectionColor = Color.Red;
                    break;
                case "En attente":
                    richTextBox_PicReport.SelectionColor = Color.DeepSkyBlue;
                    break;
                default:
                    richTextBox_PicReport.SelectionColor = Color.White;
                    break;
            }

            richTextBox_PicReport.AppendText(taskInfo.Status + "\n");
            richTextBox_PicReport.SelectionColor = Color.White;
            richTextBox_PicReport.Refresh();
            richTextBox_PicReport.ScrollToCaret();
        }

        private void btn_RepriseRoutine_Click(object sender, EventArgs e)
        {
            using (var dialog = new RepriseDialogForm())
            {
                var result = dialog.ShowDialog();
                int choix = dialog.ChoixUtilisateur;

                switch (choix)
                {
                    case 0:
                        // Cette série seulement
                        MessageBox.Show(choix.ToString());
                        break;
                    case 1:
                        // Toutes les séries à partir de celle-ci
                        MessageBox.Show(choix.ToString());
                        break;
                    case 2:
                        // Annuler
                        break;
                }
            }
        }
    }

    public class FocusStackTaskInfo
    {
        public string Serie { get; set; }
        public double Elevation { get; set; }
        public double Rotation { get; set; }
        public string Filename { get; set; }
        public string Status { get; set; }
    }
}
