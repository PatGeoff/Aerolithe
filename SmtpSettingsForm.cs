using System.Net.Mail;

namespace Aerolithe
{
    public sealed class SmtpSettingsForm : Form
    {
        private const float DialogFontSize = 8.25F;
        private const int FieldRowHeight = 42;
        private const int ButtonRowHeight = 64;
        private static readonly Font DialogFont = new("Segoe UI", DialogFontSize, FontStyle.Regular, GraphicsUnit.Point);
        private readonly AppSettings _settings;
        private readonly TextBox _hostTextBox = new();
        private readonly NumericUpDown _portNumeric = new();
        private readonly CheckBox _sslCheckBox = new();
        private readonly TextBox _userTextBox = new();
        private readonly TextBox _fromTextBox = new();
        private readonly TextBox _passwordTextBox = new();
        private readonly Button _testButton = new();
        private readonly Button _saveButton = new();
        private readonly Button _cancelButton = new();
        private bool _passwordLoadedMasked;

        public SmtpSettingsForm(AppSettings settings)
        {
            _settings = settings;
            InitializeLayout();
            LoadSettings();
        }

        private void InitializeLayout()
        {
            Text = "Réglages du serveur d'envoi";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(720, 350);
            Font = DialogFont;

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(14),
                BackColor = Color.FromArgb(32, 32, 32)
            };

            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Clear();
            for (int row = 0; row < 6; row++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, FieldRowHeight));
            }
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, ButtonRowHeight));

            AddTextRow(table, 0, "Serveur SMTP", _hostTextBox);

            _portNumeric.Minimum = 1;
            _portNumeric.Maximum = 65535;
            _portNumeric.Dock = DockStyle.Fill;
            AddControlRow(table, 1, "Port", _portNumeric);

            _sslCheckBox.Text = "Utiliser SSL/TLS";
            _sslCheckBox.AutoSize = false;
            _sslCheckBox.Font = DialogFont;
            _sslCheckBox.ForeColor = Color.White;
            _sslCheckBox.BackColor = Color.FromArgb(32, 32, 32);
            _sslCheckBox.Dock = DockStyle.Fill;
            _sslCheckBox.TextAlign = ContentAlignment.MiddleLeft;
            _sslCheckBox.CheckAlign = ContentAlignment.MiddleLeft;
            _sslCheckBox.Padding = new Padding(0, 1, 0, 0);

            AddControlRow(table, 2, "Sécurité", _sslCheckBox);

            AddTextRow(table, 3, "Utilisateur", _userTextBox);
            AddTextRow(table, 4, "Expéditeur", _fromTextBox);
            AddTextRow(table, 5, "Mot de passe", _passwordTextBox);
            _passwordTextBox.Enter += PasswordTextBox_Enter;
            _passwordTextBox.TextChanged += PasswordTextBox_TextChanged;

            var buttonsPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.FromArgb(32, 32, 32),
                Height = ButtonRowHeight
            };

            _saveButton.Text = "Sauvegarder";
            StyleDialogButton(_saveButton, 160);
            _saveButton.Click += SaveButton_Click;

            _cancelButton.Text = "Annuler";
            StyleDialogButton(_cancelButton, 110);
            _cancelButton.Click += (s, e) => DialogResult = DialogResult.Cancel;

            _testButton.Text = "Tester";
            StyleDialogButton(_testButton, 110);
            _testButton.Click += TestButton_Click;

            buttonsPanel.Controls.Add(_saveButton);
            buttonsPanel.Controls.Add(_cancelButton);
            buttonsPanel.Controls.Add(_testButton);

            table.Controls.Add(buttonsPanel, 0, 6);
            table.SetColumnSpan(buttonsPanel, 2);

            Controls.Add(table);
        }

        private static void StyleDialogButton(Button button, int width)
        {
            button.Width = width;
            button.Height = 38;
            button.Margin = new Padding(8, 0, 0, 0);
            button.Padding = new Padding(0);
            button.Font = DialogFont;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = Color.FromArgb(45, 45, 45);
            button.ForeColor = Color.White;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseVisualStyleBackColor = false;
        }

        private static void AddTextRow(TableLayoutPanel table, int row, string labelText, TextBox textBox)
        {
            textBox.Dock = DockStyle.Fill;
            textBox.Font = DialogFont;
            AddControlRow(table, row, labelText, textBox);
        }

        private static void AddControlRow(TableLayoutPanel table, int row, string labelText, Control control)
        {
            var label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White,
                Font = DialogFont
            };

            control.Margin = new Padding(0, 6, 0, 6);

            table.Controls.Add(label, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private void LoadSettings()
        {
            _hostTextBox.Text = _settings.SmtpHost;
            _portNumeric.Value = Math.Clamp(_settings.SmtpPort <= 0 ? 587 : _settings.SmtpPort, 1, 65535);
            _sslCheckBox.Checked = _settings.SmtpEnableSsl;
            _userTextBox.Text = _settings.SmtpUser;
            _fromTextBox.Text = _settings.MailFrom;
            _passwordTextBox.Text = _settings.SmtpPassword;
            _passwordTextBox.UseSystemPasswordChar = !string.IsNullOrEmpty(_passwordTextBox.Text);
            _passwordLoadedMasked = _passwordTextBox.UseSystemPasswordChar;
        }

        private void SaveToSettings()
        {
            _settings.SmtpHost = _hostTextBox.Text.Trim();
            _settings.SmtpPort = (int)_portNumeric.Value;
            _settings.SmtpEnableSsl = _sslCheckBox.Checked;
            _settings.SmtpUser = _userTextBox.Text.Trim();
            _settings.MailFrom = _fromTextBox.Text.Trim();
            _settings.SmtpPassword = _passwordTextBox.Text;
        }

        private void PasswordTextBox_Enter(object? sender, EventArgs e)
        {
            if (!_passwordLoadedMasked) return;

            _passwordTextBox.UseSystemPasswordChar = false;
            _passwordTextBox.SelectAll();
        }

        private void PasswordTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (!_passwordTextBox.Focused) return;

            _passwordLoadedMasked = false;
            _passwordTextBox.UseSystemPasswordChar = false;
        }

        private bool ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(_hostTextBox.Text))
            {
                MessageBox.Show(this, "Le serveur SMTP est obligatoire.", "SMTP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_fromTextBox.Text))
            {
                MessageBox.Show(this, "L'adresse expéditeur est obligatoire.", "SMTP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            try
            {
                _ = new MailAddress(_fromTextBox.Text.Trim());
            }
            catch
            {
                MessageBox.Show(this, "L'adresse expéditeur n'est pas valide.", "SMTP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (!ValidateSettings()) return;

            SaveToSettings();
            _settings.Save();
            _passwordTextBox.UseSystemPasswordChar = !string.IsNullOrEmpty(_passwordTextBox.Text);
            _passwordLoadedMasked = _passwordTextBox.UseSystemPasswordChar;
            DialogResult = DialogResult.OK;
        }

        private async void TestButton_Click(object? sender, EventArgs e)
        {
            if (!ValidateSettings()) return;

            string recipient = Microsoft.VisualBasic.Interaction.InputBox(
                "Adresse courriel de test:",
                "Test SMTP",
                _settings.MessagingUsers.FirstOrDefault(user => user.Send)?.Email ?? _fromTextBox.Text.Trim());

            if (string.IsNullOrWhiteSpace(recipient)) return;

            try
            {
                SaveToSettings();
                var service = new EmailNotificationService(_settings);
                await service.SendAsync(
                    new[] { recipient.Trim() },
                    "Aerolithe - Test SMTP",
                    "Ceci est un test d'envoi SMTP depuis Aerolithe.");

                MessageBox.Show(this, "Courriel de test envoyé.", "SMTP", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Erreur d'envoi SMTP:\n\n" + ex.Message, "SMTP", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
