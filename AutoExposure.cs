using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Globalization;
using System.Drawing.Imaging;

namespace Aerolithe
{
    public partial class Aerolithe
    {
        private string? _autoExposureCapturePath;
        private string? _lastAutoExposureCapturePath;
        private AutoExposureReference? _autoExposureReference;
        private bool _autoExposureTestRunning;
        private bool _autoExposureSequenceEnabled;
        private bool _autoExposureTotalSequenceActive;
        private string? _autoExposureOriginalShutterLabel;
        private int _autoExposureOriginalShutterIndex = -1;

        private sealed record AutoExposureReference(
            string ImagePath,
            string ShutterLabel,
            double ActuatorAngle,
            double MeanLuminance);

        private sealed record AutoExposureSuggestion(
            string Direction,
            double DeltaStops,
            string SuggestedShutterLabel,
            bool HasSuggestedShutter)
        {
            public bool IsCloseEnough => Math.Abs(DeltaStops) <= 0.15;
        }

        private void InitializeAutoExposureEvents()
        {
            btn_autoExposureTest.Click -= btn_autoExposureTest_Click;
            btn_autoExposureTest.Click += btn_autoExposureTest_Click;
            btn_autoExposure.Click -= btn_autoExposure_Click;
            btn_autoExposure.Click += btn_autoExposure_Click;
            btn_AutoExpoMode.Click -= btn_AutoExpoMode_Click;
            btn_AutoExpoMode.Click += btn_AutoExpoMode_Click;
            btn_AutoExpoMode2.Click -= btn_AutoExpoMode_Click;
            btn_AutoExpoMode2.Click += btn_AutoExpoMode_Click;
            UpdateAutoExposureToggleVisual();
            UpdateAutoExposureModeVisual();
        }

        private void btn_autoExposure_Click(object? sender, EventArgs e)
        {
            SetAutoExposureSequenceEnabled(!_autoExposureSequenceEnabled);
            projet.AutoExposureEnabled = _autoExposureSequenceEnabled;
            if (!string.IsNullOrWhiteSpace(appSettings?.ProjectPath))
            {
                projet.Save(appSettings.ProjectPath);
            }

            AppendTextToConsoleNL(_autoExposureSequenceEnabled
                ? "AutoExposure séquence activée."
                : "AutoExposure séquence désactivée.");
        }

        private async void btn_autoExposureTest_Click(object? sender, EventArgs e)
        {
            if (_autoExposureTestRunning)
            {
                AppendTextToConsoleNL("AutoExposure test déjà en cours.");
                return;
            }

            _autoExposureTestRunning = true;
            btn_autoExposureTest.Enabled = false;

            try
            {
                string role = _autoExposureReference == null ? "ref" : "test";
                double angle = await RequestActuatorAngleAsync(TimeSpan.FromMilliseconds(800), CancellationToken.None) ?? actuatorAngle;
                string shutterLabel = GetCurrentShutterLabel();
                string outputPath = BuildAutoExposureTempImagePath(role, angle, shutterLabel);

                _lastAutoExposureCapturePath = null;
                _autoExposureCapturePath = outputPath;
                await takePictureAsync();

                string savedPath = _lastAutoExposureCapturePath ?? outputPath;
                double meanLuminance = await CalculateAutoExposureMeanLuminanceAsync(savedPath);

                if (_autoExposureReference == null)
                {
                    _autoExposureReference = new AutoExposureReference(savedPath, shutterLabel, angle, meanLuminance);
                    AppendAutoExposureConsoleLine(
                        ("AutoExposure référence: angle " + FormatAngle(angle) + ", shutter ", txtBox_Console.ForeColor),
                        (shutterLabel, Color.LightGreen),
                        ($", luminance moyenne {meanLuminance:0.0}, fichier {Path.GetFileName(savedPath)}", txtBox_Console.ForeColor));
                    return;
                }

                double ratio = meanLuminance <= 0.001 ? 0 : _autoExposureReference.MeanLuminance / meanLuminance;
                double deltaStops = ratio <= 0 ? 0 : Math.Log(ratio, 2);
                AutoExposureSuggestion suggestion = BuildAutoExposureSuggestion(deltaStops, shutterLabel);
                AppendAutoExposureTestLog(angle, shutterLabel, meanLuminance, deltaStops, savedPath, suggestion);
            }
            catch (Exception ex)
            {
                _autoExposureCapturePath = null;
                AppendTextToConsoleNL("Erreur AutoExposure test: " + ex.Message, Color.Red);
                MessageBox.Show(
                    "Erreur pendant le test d'exposition automatique.\n\n" + ex.Message,
                    "Auto Exposition",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                btn_autoExposureTest.Enabled = true;
                _autoExposureTestRunning = false;
            }
        }

        private async Task BeginAutoExposureTotalSequenceAsync(CancellationToken cancellationToken)
        {
            if (!_autoExposureSequenceEnabled)
            {
                _autoExposureTotalSequenceActive = false;
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _autoExposureReference = null;
            _autoExposureTotalSequenceActive = true;
            _autoExposureOriginalShutterLabel = GetCurrentShutterLabel();
            _autoExposureOriginalShutterIndex = GetCurrentShutterIndex();

            AppendAutoExposureConsoleLine(
                ("AutoExposure routine totale: exposition originale mémorisée ", txtBox_Console.ForeColor),
                (_autoExposureOriginalShutterLabel, Color.LightGreen),
                (". Elle sera restaurée à la fin ou en cas d'annulation.", txtBox_Console.ForeColor));

            await Task.CompletedTask;
        }

        private async Task RestoreAutoExposureOriginalShutterAsync(string reason)
        {
            if (!_autoExposureTotalSequenceActive || _autoExposureOriginalShutterIndex < 0)
            {
                _autoExposureTotalSequenceActive = false;
                return;
            }

            string originalLabel = _autoExposureOriginalShutterLabel ?? "inconnue";
            try
            {
                await ApplyShutterIndexAsync(_autoExposureOriginalShutterIndex);
                AppendAutoExposureConsoleLine(
                    ($"AutoExposure routine totale: exposition originale restaurée ({reason}) ", txtBox_Console.ForeColor),
                    (originalLabel, Color.LightGreen),
                    (".", txtBox_Console.ForeColor));
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"AutoExposure routine totale: restauration de l'exposition originale {originalLabel} échouée: {ex.Message}", Color.Red);
            }
            finally
            {
                _autoExposureTotalSequenceActive = false;
            }
        }

        private async Task CalibrateAutoExposureForCurrentSequenceAngleAsync(int angle, CancellationToken cancellationToken)
        {
            if (!_autoExposureTotalSequenceActive || !_autoExposureSequenceEnabled)
            {
                return;
            }

            await WaitIfSequencePausedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            string role = _autoExposureReference == null ? "ref" : "test";
            string shutterLabel = GetCurrentShutterLabel();
            string outputPath = BuildAutoExposureTempImagePath(role, angle, shutterLabel);

            _lastAutoExposureCapturePath = null;
            _autoExposureCapturePath = outputPath;

            AppendAutoExposureConsoleLine(
                ($"AutoExposure routine totale: photo {(_autoExposureReference == null ? "référence" : "test")} temporaire à {angle}°, shutter ", txtBox_Console.ForeColor),
                (shutterLabel, _autoExposureReference == null ? Color.LightGreen : Color.Khaki),
                (".", txtBox_Console.ForeColor));

            await takePictureAsync();
            cancellationToken.ThrowIfCancellationRequested();

            string savedPath = _lastAutoExposureCapturePath ?? outputPath;
            double meanLuminance = await CalculateAutoExposureMeanLuminanceAsync(savedPath);

            if (_autoExposureReference == null)
            {
                _autoExposureReference = new AutoExposureReference(savedPath, shutterLabel, angle, meanLuminance);
                AppendAutoExposureConsoleLine(
                    ($"AutoExposure routine totale: référence créée à {FormatAngle(angle)}, shutter ", txtBox_Console.ForeColor),
                    (shutterLabel, Color.LightGreen),
                    ($", luminance {meanLuminance:0.0}, fichier {Path.GetFileName(savedPath)}.", txtBox_Console.ForeColor));
                return;
            }

            double ratio = meanLuminance <= 0.001 ? 0 : _autoExposureReference.MeanLuminance / meanLuminance;
            double deltaStops = ratio <= 0 ? 0 : Math.Log(ratio, 2);
            AutoExposureSuggestion suggestion = BuildAutoExposureSuggestion(deltaStops, shutterLabel);
            AppendAutoExposureTestLog(angle, shutterLabel, meanLuminance, deltaStops, savedPath, suggestion);

            if (suggestion.HasSuggestedShutter && TryFindShutterIndex(suggestion.SuggestedShutterLabel, out int suggestedIndex))
            {
                await ApplyShutterIndexAsync(suggestedIndex);
                AppendAutoExposureConsoleLine(
                    ("AutoExposure routine totale: exposition temporaire appliquée ", txtBox_Console.ForeColor),
                    (suggestion.SuggestedShutterLabel, Color.Khaki),
                    ($" pour les vraies photos à {FormatAngle(angle)}.", txtBox_Console.ForeColor));
            }
        }

        private string BuildAutoExposureTempImagePath(string role, double angle, string shutterLabel)
        {
            if (string.IsNullOrWhiteSpace(projet.ImageFolderPath))
            {
                throw new InvalidOperationException("Aucun dossier d'images projet n'est défini.");
            }

            string folder = Path.Combine(projet.ImageFolderPath, "tempa", "autoExposure");
            string projectName = string.IsNullOrWhiteSpace(projet.ImageNameBase) ? "Projet" : SanitizeFileNamePart(projet.ImageNameBase);
            string angleLabel = FormatAngleForFileName(angle);
            string shutterPart = SanitizeFileNamePart(shutterLabel).Replace("_", "-");
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string fileName = $"{projectName}_autoexp_{role}_angle{angleLabel}_shutter{shutterPart}_{timestamp}.jpg";
            return Path.Combine(folder, fileName);
        }

        private string GetCurrentShutterLabel()
        {
            object? selected = comboBox_shutterTime.SelectedItem ?? comboBox_shutterTime_2.SelectedItem;
            if (selected != null)
            {
                return selected.ToString() ?? "unknown";
            }

            int selectedIndex = comboBox_shutterTime.SelectedIndex >= 0 ? comboBox_shutterTime.SelectedIndex : comboBox_shutterTime_2.SelectedIndex;
            return selectedIndex >= 0 ? "index" + selectedIndex.ToString(CultureInfo.InvariantCulture) : "unknown";
        }

        private async Task<double> CalculateAutoExposureMeanLuminanceAsync(string imagePath)
        {
            if (projet?.AutoExposureUseMask != true)
            {
                double fullImageMean = CalculateMeanLuminance(imagePath);
                DisplayAutoExposureThumbnail(imagePath);
                return fullImageMean;
            }

            byte[] jpegBytes = await File.ReadAllBytesAsync(imagePath);
            using Mat mask = await BuildRegisteredMaskFromCapturedJpegAsync(jpegBytes, GetCurrentMaskThreshold());

            string directory = Path.GetDirectoryName(imagePath) ?? ".";
            string filename = Path.GetFileNameWithoutExtension(imagePath);
            string extension = Path.GetExtension(imagePath);
            string maskPath = Path.Combine(directory, filename + "_mask.png");
            string tempMaskedPath = Path.Combine(directory, filename + "_masked_tmp" + extension);

            await SaveMaskAsPngNoTransparency(mask, maskPath);
            double meanLuminance = CalculateMeanLuminance(imagePath, mask);

            using (Bitmap original = new(imagePath))
            using (Bitmap masked = ApplyMask(original, mask))
            {
                masked.Save(tempMaskedPath, ImageFormat.Jpeg);
            }

            File.Copy(tempMaskedPath, imagePath, true);
            File.Delete(tempMaskedPath);
            AppendTextToConsoleNL($"AutoExposure: masque temporaire appliqué ({Path.GetFileName(maskPath)}), luminance mesurée dans le masque.");
            DisplayAutoExposureThumbnail(imagePath);

            return meanLuminance;
        }

        private void DisplayAutoExposureThumbnail(string imagePath)
        {
            void display()
            {
                AfficherMiniatures(projet.ImageNameBase, imagePath, panelSize, Color.FromArgb(145, 200, 255));
            }

            if (InvokeRequired)
            {
                Invoke((Action)display);
            }
            else
            {
                display();
            }
        }

        private static double CalculateMeanLuminance(string imagePath)
        {
            using Bitmap bitmap = new(imagePath);
            long count = 0;
            double total = 0;
            int stride = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 600);

            for (int y = 0; y < bitmap.Height; y += stride)
            {
                for (int x = 0; x < bitmap.Width; x += stride)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    total += 0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B;
                    count++;
                }
            }

            return count == 0 ? 0 : total / count;
        }

        private static double CalculateMeanLuminance(string imagePath, Mat maskMat)
        {
            using Bitmap bitmap = new(imagePath);
            using Image<Gray, byte> maskGray = maskMat.ToImage<Gray, byte>();
            using Image<Gray, byte> resizedMask = maskGray.Resize(bitmap.Width, bitmap.Height, Inter.Nearest);
            using Mat binaryMask = CreateBinaryMaskWithInset(resizedMask.Mat, 0);
            using Image<Gray, byte> binaryMaskImage = binaryMask.ToImage<Gray, byte>();

            long count = 0;
            double total = 0;
            int stride = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 600);

            for (int y = 0; y < bitmap.Height; y += stride)
            {
                for (int x = 0; x < bitmap.Width; x += stride)
                {
                    if (binaryMaskImage.Data[y, x, 0] == 0)
                    {
                        continue;
                    }

                    Color pixel = bitmap.GetPixel(x, y);
                    total += 0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B;
                    count++;
                }
            }

            return count == 0 ? CalculateMeanLuminance(imagePath) : total / count;
        }

        private AutoExposureSuggestion BuildAutoExposureSuggestion(double deltaStops, string shutterLabel)
        {
            const double toleranceStops = 0.15;

            if (Math.Abs(deltaStops) <= toleranceStops)
            {
                return new AutoExposureSuggestion("exposition proche de la référence; garder cette vitesse", deltaStops, shutterLabel, false);
            }

            string direction = deltaStops < 0
                ? $"test trop clair de {Math.Abs(deltaStops):0.00} stop; diminuer l'exposition"
                : $"test trop sombre de {Math.Abs(deltaStops):0.00} stop; augmenter l'exposition";

            return TrySuggestAvailableShutter(shutterLabel, deltaStops, out string suggestedShutter)
                ? new AutoExposureSuggestion(direction, deltaStops, suggestedShutter, true)
                : new AutoExposureSuggestion(direction, deltaStops, string.Empty, false);
        }

        private bool TrySuggestAvailableShutter(string shutterLabel, double deltaStops, out string suggestedShutter)
        {
            suggestedShutter = string.Empty;

            if (!TryParseShutterSeconds(shutterLabel, out double currentSeconds) || currentSeconds <= 0)
            {
                return false;
            }

            double targetSeconds = currentSeconds * Math.Pow(2, deltaStops);
            if (targetSeconds <= 0)
            {
                return false;
            }

            return TryFindClosestAvailableShutter(targetSeconds, out suggestedShutter);
        }

        private static bool TryParseShutterSeconds(string shutterLabel, out double seconds)
        {
            seconds = 0;
            string value = shutterLabel
                .Trim()
                .Replace("\"", string.Empty)
                .Replace("sec", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("s", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (value.Contains('/'))
            {
                string[] parts = value.Split('/', 2);
                if (parts.Length == 2
                    && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double numerator)
                    && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double denominator)
                    && denominator > 0)
                {
                    seconds = numerator / denominator;
                    return true;
                }
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)
                || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out seconds);
        }

        private bool TryFindClosestAvailableShutter(double targetSeconds, out string shutterLabel)
        {
            shutterLabel = string.Empty;
            double bestDistanceStops = double.MaxValue;

            foreach (object item in GetAvailableShutterItems())
            {
                string candidateLabel = item.ToString() ?? string.Empty;
                if (!TryParseShutterSeconds(candidateLabel, out double candidateSeconds) || candidateSeconds <= 0)
                {
                    continue;
                }

                double distanceStops = Math.Abs(Math.Log(candidateSeconds / targetSeconds, 2));
                if (distanceStops < bestDistanceStops)
                {
                    bestDistanceStops = distanceStops;
                    shutterLabel = candidateLabel;
                }
            }

            return !string.IsNullOrWhiteSpace(shutterLabel);
        }

        private bool TryFindShutterIndex(string shutterLabel, out int shutterIndex)
        {
            shutterIndex = -1;
            ComboBox source = comboBox_shutterTime.Items.Count > 0 ? comboBox_shutterTime : comboBox_shutterTime_2;
            for (int i = 0; i < source.Items.Count; i++)
            {
                if (string.Equals(source.Items[i]?.ToString(), shutterLabel, StringComparison.Ordinal))
                {
                    shutterIndex = i;
                    return true;
                }
            }

            return false;
        }

        private int GetCurrentShutterIndex()
        {
            if (comboBox_shutterTime.SelectedIndex >= 0)
            {
                return comboBox_shutterTime.SelectedIndex;
            }

            return comboBox_shutterTime_2.SelectedIndex;
        }

        private async Task ApplyShutterIndexAsync(int shutterIndex)
        {
            if (shutterIndex < 0)
            {
                return;
            }

            await InvokeAsyncOnUi(() =>
            {
                if (device == null)
                {
                    throw new InvalidOperationException("Caméra Nikon non initialisée.");
                }

                Nikon.NikonEnum exposureTime = device.GetEnum(Nikon.eNkMAIDCapability.kNkMAIDCapability_ShutterSpeed);
                if (shutterIndex >= exposureTime.Length)
                {
                    throw new InvalidOperationException($"Index shutter invalide: {shutterIndex}.");
                }

                exposureTime.Index = shutterIndex;
                device.SetEnum(Nikon.eNkMAIDCapability.kNkMAIDCapability_ShutterSpeed, exposureTime);
                SetShutterComboBoxesIndex(shutterIndex);
            });
        }

        private void SetShutterComboBoxesIndex(int shutterIndex)
        {
            _isSyncingShutterTimeComboBoxes = true;
            try
            {
                if (shutterIndex >= 0 && shutterIndex < comboBox_shutterTime.Items.Count)
                {
                    comboBox_shutterTime.SelectedIndex = shutterIndex;
                }

                if (shutterIndex >= 0 && shutterIndex < comboBox_shutterTime_2.Items.Count)
                {
                    comboBox_shutterTime_2.SelectedIndex = shutterIndex;
                }
            }
            finally
            {
                _isSyncingShutterTimeComboBoxes = false;
            }
        }

        private Task InvokeAsyncOnUi(Action action)
        {
            if (!InvokeRequired)
            {
                action();
                return Task.CompletedTask;
            }

            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            BeginInvoke(new Action(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));
            return tcs.Task;
        }

        private void AppendAutoExposureTestLog(
            double angle,
            string shutterLabel,
            double meanLuminance,
            double deltaStops,
            string savedPath,
            AutoExposureSuggestion suggestion)
        {
            string suggestionPrefix = suggestion.HasSuggestedShutter ? "; essayer " : string.Empty;
            AppendAutoExposureConsoleLine(
                ("AutoExposure test: conclusion: " + suggestion.Direction + suggestionPrefix, txtBox_Console.ForeColor),
                (suggestion.HasSuggestedShutter ? suggestion.SuggestedShutterLabel : string.Empty, Color.Khaki),
                (". Angle " + FormatAngle(angle) + ", shutter ", txtBox_Console.ForeColor),
                (shutterLabel, Color.Khaki),
                ($", luminance test {meanLuminance:0.0} vs référence {_autoExposureReference?.MeanLuminance ?? 0:0.0}, correction {deltaStops:+0.00;-0.00;0.00} stop, fichier {Path.GetFileName(savedPath)}", txtBox_Console.ForeColor));
        }

        private void AppendAutoExposureConsoleLine(params (string Text, Color Color)[] segments)
        {
            RichTextBox textbox = txtBox_Console;
            string timestamp = $"{DateTime.Now:HH:mm:ss:ff} - ";

            void append()
            {
                (string Text, Color Color)[] lineSegments = new (string Text, Color Color)[segments.Length + 2];
                lineSegments[0] = (timestamp, Color.Gray);
                Array.Copy(segments, 0, lineSegments, 1, segments.Length);
                lineSegments[^1] = (Environment.NewLine, textbox.ForeColor);
                AppendFormattedTextLine(textbox, lineSegments);
            }

            if (textbox.InvokeRequired)
            {
                textbox.Invoke((Action)append);
            }
            else
            {
                append();
            }
        }

        private void UpdateAutoExposureToggleVisual()
        {
            void update()
            {
                btn_autoExposure.BackColor = Color.FromArgb(35, 35, 35);
                btn_autoExposure.Text = _autoExposureSequenceEnabled ? "" : "";
            }

            if (btn_autoExposure.InvokeRequired)
            {
                btn_autoExposure.Invoke((Action)update);
            }
            else
            {
                update();
            }
        }

        private void btn_AutoExpoMode_Click(object? sender, EventArgs e)
        {
            projet.AutoExposureUseMask = !projet.AutoExposureUseMask;
            UpdateAutoExposureModeVisual();
            if (!string.IsNullOrWhiteSpace(appSettings?.ProjectPath))
            {
                projet.Save(appSettings.ProjectPath);
            }

            AppendTextToConsoleNL(projet.AutoExposureUseMask
                ? "AutoExposure: luminance calculée dans le masque."
                : "AutoExposure: luminance calculée sur l'image complète.");
        }

        private void UpdateAutoExposureModeVisual()
        {
            void update()
            {
                btn_AutoExpoMode.BackColor = Color.FromArgb(35, 35, 35);
                btn_AutoExpoMode.Text = projet?.AutoExposureUseMask == true ? "" : "";
                btn_AutoExpoMode2.BackColor = Color.FromArgb(35, 35, 35);
                btn_AutoExpoMode2.Text = projet?.AutoExposureUseMask == true ? "" : "";
            }

            if (btn_AutoExpoMode.InvokeRequired)
            {
                btn_AutoExpoMode.Invoke((Action)update);
            }
            else
            {
                update();
            }
        }

        private void SetAutoExposureSequenceEnabled(bool enabled)
        {
            _autoExposureSequenceEnabled = enabled;
            UpdateAutoExposureToggleVisual();
        }

        private IEnumerable<object> GetAvailableShutterItems()
        {
            ComboBox source = comboBox_shutterTime.Items.Count > 0 ? comboBox_shutterTime : comboBox_shutterTime_2;
            foreach (object item in source.Items)
            {
                yield return item;
            }
        }

        private static string FormatAngle(double angle)
        {
            return angle.ToString("0.#", CultureInfo.InvariantCulture) + "°";
        }

        private static string FormatAngleForFileName(double angle)
        {
            string sign = angle < 0 ? "m" : string.Empty;
            return sign + Math.Abs(angle).ToString("000.#", CultureInfo.InvariantCulture).Replace(".", "p") + "deg";
        }

        private static string SanitizeFileNamePart(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = new(value.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch).ToArray());
            sanitized = sanitized.Replace("/", "-").Replace("\\", "-").Replace(":", "-");
            return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
        }
    }
}
