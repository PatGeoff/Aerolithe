// ControlesInteractions.cs


using System.Drawing.Text;
using System.Net;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private Tuple<Button, Label>[] buttonLabelPairs;

        //private void ButtonSetup()
        //{

        //    buttonLabelPairs = new Tuple<Button, Label>[]
        //    {
        //        Tuple.Create(btn_Validation, lbl_E1),
        //        Tuple.Create(btn_Autofocus, lbl_E2),
        //        Tuple.Create(btn_imageFond, lbl_E3),
        //        Tuple.Create(btn_DemarrerPrisePhotos, lbl_E4),
        //    };

        //    foreach (var e in buttonLabelPairs)
        //    {
        //        ApplyButtonStyle(e, false);
        //    }
        //    ApplyButtonStyle(buttonLabelPairs[0], true);
        //}
        //private void ApplyButtonStyle(Tuple<Button, Label> buttonLabelPair, bool enabled)
        //{
        //    Button button = buttonLabelPair.Item1;
        //    Label label = buttonLabelPair.Item2;

        //    button.BackColor = enabled ? Color.FromArgb(30, 30, 30) : Color.FromArgb(20, 20, 20);
        //    button.ForeColor = enabled ? Color.White : Color.DarkGray;
        //    label.ForeColor = enabled ? Color.White : Color.DarkGray;   
            
        //}
                   

        private void btn_getMask_Click(object sender, EventArgs e)
        {
            //backgroundSubstraction();
        }

    }  
}