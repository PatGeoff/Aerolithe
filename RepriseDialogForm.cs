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
    public partial class RepriseDialogForm : Form
    {
        public int ChoixUtilisateur { get; private set; } = 2; // Par défaut: Cancel
        public RepriseDialogForm()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        private void btn_SerieSeulement_Click(object sender, EventArgs e)
        {
            ChoixUtilisateur = 0; 
            this.DialogResult = DialogResult.OK; 
            this.Close();
        }

        private void btn_ToutesSeries_Click(object sender, EventArgs e)
        {
            ChoixUtilisateur = 1; 
            this.DialogResult = DialogResult.OK; 
            this.Close();
        }

        private void btn_Cancel_Click(object sender, EventArgs e)
        {
            ChoixUtilisateur = 2; 
            this.DialogResult = DialogResult.Cancel; 
            this.Close();
        }
    }
}
