using ScottPlot.Statistics;
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
        private FocusStackTaskInfo? taskInfo;
        private const double TurntableStepsPerRotation = 4096.0;

        public FocusStackReportControl()
        {
            InitializeComponent();
            Aerolithe.Instance.ApplyBundledPhosphorFontToControl(this);
            ConfigureFocusStackActionButton(btn_RepriseRoutine);
            ConfigureFocusStackActionButton(btn_ReprendreFocusStack);
        }

        public void SetTaskInfo(FocusStackTaskInfo info)
        {
            taskInfo = info;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (taskInfo == null)
            {
                lbl_PicReport.ForeColor = Color.LightGray;
                lbl_PicReport.Text = "La file d'attente est vide.";
                lbl_Status.Text = string.Empty;
                return;
            }

            lbl_Serie.Text = taskInfo.Cote;
            lbl_Elevation.Text = taskInfo.Elevation.ToString() + "°";
            lbl_Rotation.Text = FormatTurntableRotationDegrees(taskInfo.Rotation);
            lbl_ImageNumber.Text = taskInfo.ImageNumber.ToString();
            lbl_PicReport.ForeColor = taskInfo.IsRetry ? Color.LightSkyBlue : Color.White;
            lbl_PicReport.BackColor = taskInfo.IsRetry ? Color.FromArgb(22, 48, 58) : Color.FromArgb(40, 40, 40);
            lbl_PicReport.Text = taskInfo.IsRetry ? "Reprise - " + taskInfo.Filename : taskInfo.Filename;

            switch (taskInfo.Status)
            {
                case "Terminé":
                    lbl_Status.ForeColor = Color.LimeGreen;
                    break;
                case "En cours":
                    lbl_Status.ForeColor = Color.Orange;
                    break;
                case "Erreur":
                    lbl_Status.ForeColor = Color.Red;
                    break;
                case "En attente":
                    lbl_Status.ForeColor = Color.DeepSkyBlue;
                    break;
                default:
                    lbl_Status.ForeColor = Color.White;
                    break;
            }

            lbl_Status.Text = taskInfo.Status;
        }

        private static string FormatTurntableRotationDegrees(double rotationSteps)
        {
            double degrees = rotationSteps / TurntableStepsPerRotation * 360.0;
            return Math.Round(degrees).ToString("0") + "°";
        }

        private static void ConfigureFocusStackActionButton(Button button)
        {
            button.UseCompatibleTextRendering = true;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Paint -= FocusStackActionButton_Paint;
            button.Paint += FocusStackActionButton_Paint;
        }

        private static void FocusStackActionButton_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            e.Graphics.Clear(button.BackColor);

            using Pen borderPen = new Pen(Color.FromArgb(64, 64, 64));
            Rectangle border = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
            e.Graphics.DrawRectangle(borderPen, border);

            TextRenderer.DrawText(
                e.Graphics,
                button.Text,
                button.Font,
                button.ClientRectangle,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private async void btn_RepriseRoutine_Click(object sender, EventArgs e)
        {
            if (taskInfo == null) return;
            await Aerolithe.Instance.ReprendrePrisePhotoFocusStackAsync(taskInfo);
        }

        private async void btn_ReprendreFocusStack_Click(object sender, EventArgs e)
        {
            if (taskInfo == null) return;
            await Aerolithe.Instance.ReprendreFocusStackSeulementAsync(taskInfo);
        }
    }

    public class FocusStackTaskInfo
    {
        public Guid TaskId { get; set; }
        public string Serie { get; set; } = string.Empty;
        public double Elevation { get; set; }
        public double Rotation { get; set; }
        public int RotationSerieIncrement { get; set; }
        public int ImageNumber { get; set; }
        public int CoteIndex { get; set; }
        public string Cote { get; set; } = string.Empty;
        public string[] ImagePaths { get; set; } = Array.Empty<string>();
        public string OutputPath { get; set; } = string.Empty;
        public string MaskPath { get; set; } = string.Empty;
        public bool ApplyMask { get; set; }
        public string Filename { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsRetry { get; set; }
    }
}
