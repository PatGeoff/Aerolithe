using System.Text.Json;
using System.Drawing.Imaging;
using System.Globalization;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Aerolithe
{
    public sealed class GlbViewerForm : Form
    {
        private readonly GLControl _glControl;
        private readonly System.Windows.Forms.Timer _renderTimer;
        private readonly Label _titleLabel;
        private readonly TextBox _longestDimensionTextBox;
        private readonly TextBox _volumeTextBox;
        private readonly Button _boundingBoxButton;
        private GlbModel? _model;
        private ModelMetrics? _metrics;
        private readonly List<int> _textureIds = new();
        private bool _dragging;
        private Point _lastMouse;
        private float _yaw = -25f;
        private float _pitch = 20f;
        private float _distance = 4f;
        private bool _showBoundingBox;

        public GlbViewerForm(string? glbPath = null)
        {
            Text = "Visualisateur GLB";
            Icon? appIcon = GetAerolitheIcon();
            if (appIcon != null)
            {
                Icon = appIcon;
            }

            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1100, 760);
            BackColor = Color.FromArgb(24, 24, 24);
            ForeColor = Color.White;

            _titleLabel = new Label
            {
                Text = "Aucun GLB chargé",
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 10, 0),
                ForeColor = Color.Gainsboro,
                BackColor = Color.FromArgb(32, 32, 32),
                AutoEllipsis = true
            };

            _longestDimensionTextBox = new TextBox
            {
                Text = "Dimension max: -",
                Dock = DockStyle.Left,
                Width = 270,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ForeColor = Color.FromArgb(170, 235, 190),
                BackColor = Color.FromArgb(32, 32, 32),
                Font = new Font(Font.FontFamily, 12.5f, FontStyle.Bold),
                Margin = new Padding(0)
            };

            _volumeTextBox = new TextBox
            {
                Text = "Volume: -",
                Dock = DockStyle.Left,
                Width = 270,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.FromArgb(170, 235, 190),
                Font = new Font(Font.FontFamily, 12.5f, FontStyle.Bold),
                Margin = new Padding(0)
            };

            Button openButton = new()
            {
                Text = "Ouvrir",
                Dock = DockStyle.Right,
                Width = 110
            };
            openButton.Click += (_, __) => OpenGlbFromDialog();

            _boundingBoxButton = new Button
            {
                Text = "Boîte: Off",
                Dock = DockStyle.Right,
                Width = 110
            };
            _boundingBoxButton.Click += (_, __) => ToggleBoundingBox();

            Panel header = new()
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(32, 32, 32)
            };
            Panel metricsPanel = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 32, 32)
            };
            metricsPanel.Controls.Add(_volumeTextBox);
            metricsPanel.Controls.Add(_longestDimensionTextBox);
            header.Controls.Add(metricsPanel);
            header.Controls.Add(_titleLabel);
            header.Controls.Add(_boundingBoxButton);
            header.Controls.Add(openButton);

            _glControl = new GLControl(GraphicsMode.Default)
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            _glControl.Load += (_, __) => InitializeGl();
            _glControl.Paint += (_, __) => Render();
            _glControl.Resize += (_, __) => ConfigureViewport();
            _glControl.MouseDown += GlControl_MouseDown;
            _glControl.MouseMove += GlControl_MouseMove;
            _glControl.MouseUp += (_, __) => _dragging = false;
            _glControl.MouseWheel += GlControl_MouseWheel;

            _renderTimer = new System.Windows.Forms.Timer { Interval = 33 };
            _renderTimer.Tick += (_, __) => _glControl.Invalidate();
            _renderTimer.Start();

            Controls.Add(_glControl);
            Controls.Add(header);

            if (!string.IsNullOrWhiteSpace(glbPath))
            {
                LoadGlb(glbPath);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DeleteUploadedTextures();
                _renderTimer.Stop();
                _renderTimer.Dispose();
                _glControl.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeGl()
        {
            _glControl.MakeCurrent();
            GL.ClearColor(Color.FromArgb(18, 18, 18));
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Normalize);
            GL.Enable(EnableCap.ColorMaterial);
            GL.Enable(EnableCap.Lighting);
            GL.Enable(EnableCap.Light0);
            GL.Light(LightName.Light0, LightParameter.Position, new[] { 0.35f, 0.7f, 1.0f, 0.0f });
            GL.Light(LightName.Light0, LightParameter.Diffuse, new[] { 0.9f, 0.9f, 0.86f, 1.0f });
            GL.Light(LightName.Light0, LightParameter.Ambient, new[] { 0.25f, 0.25f, 0.25f, 1.0f });
            ConfigureViewport();
            UploadModelTextures();
        }

        private void OpenGlbFromDialog()
        {
            using OpenFileDialog dialog = new()
            {
                Title = "Ouvrir un modèle GLB",
                Filter = "Modèles GLB (*.glb)|*.glb|Tous les fichiers (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                LoadGlb(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Impossible d'ouvrir ce GLB.\n\n" + dialog.FileName + "\n\n" + ex.Message,
                    "Visualisateur GLB",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void LoadGlb(string glbPath)
        {
            DeleteUploadedTextures();
            _model = GlbModel.Load(glbPath);
            _metrics = ModelMetrics.TryLoadForGlb(glbPath);
            _distance = Math.Max(0.05f, _model.Radius * 2.15f);
            _yaw = -25f;
            _pitch = 20f;
            Text = "Visualisateur GLB - " + Path.GetFileName(glbPath);
            _titleLabel.Text = glbPath + "   |   " + _model.DimensionsText;
            _longestDimensionTextBox.Text = "Dimension max: " + _model.LongestDimensionText;
            _volumeTextBox.Text = _metrics?.VolumeCm3Text ?? "Volume: non disponible";
            ConfigureViewport();
            UploadModelTextures();
            _glControl.Invalidate();
        }

        private void ToggleBoundingBox()
        {
            _showBoundingBox = !_showBoundingBox;
            _boundingBoxButton.Text = _showBoundingBox ? "Boîte: On" : "Boîte: Off";
            _glControl.Invalidate();
        }

        private static Icon? GetAerolitheIcon()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form is Aerolithe && form.Icon != null)
                {
                    return form.Icon;
                }
            }

            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }

        private void UploadModelTextures()
        {
            if (_model == null || !_glControl.IsHandleCreated)
            {
                return;
            }

            if (!_glControl.Context.IsCurrent)
            {
                _glControl.MakeCurrent();
            }

            foreach (GlbPrimitive primitive in _model.Primitives)
            {
                if (primitive.TextureId != 0 || primitive.TextureBytes == null || primitive.TextureBytes.Length == 0)
                {
                    continue;
                }

                using MemoryStream stream = new(primitive.TextureBytes);
                using Bitmap source = new(stream);
                using Bitmap bitmap = new(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.DrawImage(source, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                }

                Rectangle bounds = new(0, 0, bitmap.Width, bitmap.Height);
                BitmapData data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    int textureId = GL.GenTexture();
                    GL.BindTexture(TextureTarget.Texture2D, textureId);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    GL.TexImage2D(
                        TextureTarget.Texture2D,
                        0,
                        PixelInternalFormat.Rgba,
                        bitmap.Width,
                        bitmap.Height,
                        0,
                        OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                        PixelType.UnsignedByte,
                        data.Scan0);
                    primitive.TextureId = textureId;
                    _textureIds.Add(textureId);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void DeleteUploadedTextures()
        {
            if (_textureIds.Count == 0 || !_glControl.IsHandleCreated)
            {
                _textureIds.Clear();
                return;
            }

            try
            {
                if (!_glControl.Context.IsCurrent)
                {
                    _glControl.MakeCurrent();
                }

                foreach (int textureId in _textureIds)
                {
                    GL.DeleteTexture(textureId);
                }
            }
            catch
            {
                // Le contexte OpenGL peut déjà être détruit pendant la fermeture.
            }

            _textureIds.Clear();
            if (_model == null) return;
            foreach (GlbPrimitive primitive in _model.Primitives)
            {
                primitive.TextureId = 0;
            }
        }

        private void ConfigureViewport()
        {
            if (!_glControl.IsHandleCreated)
            {
                return;
            }

            if (!_glControl.Context.IsCurrent)
            {
                _glControl.MakeCurrent();
            }

            int width = Math.Max(1, _glControl.Width);
            int height = Math.Max(1, _glControl.Height);
            GL.Viewport(0, 0, width, height);
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(45f),
                width / (float)height,
                0.01f,
                Math.Max(1000f, (_model?.Radius ?? 1f) * 100f));
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref projection);
        }

        private void Render()
        {
            if (!_glControl.IsHandleCreated)
            {
                return;
            }

            if (!_glControl.Context.IsCurrent)
            {
                _glControl.MakeCurrent();
            }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            if (_model == null)
            {
                _glControl.SwapBuffers();
                return;
            }

            Matrix4 view = Matrix4.LookAt(new Vector3(0, 0, _distance), Vector3.Zero, Vector3.UnitY);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref view);
            GL.Rotate(_pitch, 1f, 0f, 0f);
            GL.Rotate(_yaw, 0f, 1f, 0f);
            GL.Translate(-_model.Center);

            GL.Color3(Color.FromArgb(188, 184, 174));
            foreach (GlbPrimitive primitive in _model.Primitives)
            {
                bool hasTexture = primitive.TextureId != 0 && primitive.TexCoords.Count == primitive.Vertices.Count;
                if (hasTexture)
                {
                    GL.Disable(EnableCap.CullFace);
                    GL.Enable(EnableCap.Texture2D);
                    GL.BindTexture(TextureTarget.Texture2D, primitive.TextureId);
                    GL.Color3(Color.White);
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new[] { 1f, 1f, 1f, 1f });
                }
                else
                {
                    GL.Disable(EnableCap.CullFace);
                    GL.Disable(EnableCap.Texture2D);
                    GL.Color3(Color.FromArgb(188, 184, 174));
                }

                GL.Begin(PrimitiveType.Triangles);
                foreach (int index in primitive.Indices)
                {
                    if (primitive.Normals.Count == primitive.Vertices.Count)
                    {
                        Vector3 normal = primitive.Normals[index];
                        GL.Normal3(normal);
                    }

                    if (hasTexture)
                    {
                        Vector2 texCoord = primitive.TexCoords[index];
                        GL.TexCoord2(texCoord);
                    }

                    Vector3 vertex = primitive.Vertices[index];
                    GL.Vertex3(vertex);
                }
                GL.End();
            }

            GL.Disable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            if (_showBoundingBox)
            {
                DrawBoundingBox(_model);
            }

            _glControl.SwapBuffers();
        }

        private static void DrawBoundingBox(GlbModel model)
        {
            Vector3 min = model.Min;
            Vector3 max = model.Max;
            Vector3[] corners =
            [
                new(min.X, min.Y, min.Z),
                new(max.X, min.Y, min.Z),
                new(max.X, max.Y, min.Z),
                new(min.X, max.Y, min.Z),
                new(min.X, min.Y, max.Z),
                new(max.X, min.Y, max.Z),
                new(max.X, max.Y, max.Z),
                new(min.X, max.Y, max.Z)
            ];

            int[] edges =
            [
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7
            ];

            GL.Disable(EnableCap.Lighting);
            GL.Disable(EnableCap.Texture2D);
            GL.Enable(EnableCap.DepthTest);
            GL.LineWidth(2.0f);
            GL.Color3(Color.FromArgb(170, 235, 190));
            GL.Begin(PrimitiveType.Lines);
            foreach (int index in edges)
            {
                GL.Vertex3(corners[index]);
            }
            GL.End();
            GL.LineWidth(1.0f);
            GL.Enable(EnableCap.Lighting);
        }

        private void GlControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            _lastMouse = e.Location;
        }

        private void GlControl_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_dragging) return;

            int dx = e.X - _lastMouse.X;
            int dy = e.Y - _lastMouse.Y;
            _yaw += dx * 0.45f;
            _pitch += dy * 0.45f;
            _pitch = Math.Clamp(_pitch, -89f, 89f);
            _lastMouse = e.Location;
            _glControl.Invalidate();
        }

        private void GlControl_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (_model == null) return;

            float factor = e.Delta > 0 ? 0.88f : 1.14f;
            _distance = Math.Clamp(_distance * factor, Math.Max(0.05f, _model.Radius * 0.25f), Math.Max(10f, _model.Radius * 20f));
            _glControl.Invalidate();
        }

        private sealed class GlbModel
        {
            public required List<GlbPrimitive> Primitives { get; init; }
            public Vector3 Center { get; init; }
            public float Radius { get; init; }
            public Vector3 Min { get; init; }
            public Vector3 Max { get; init; }
            public Vector3 Size => Max - Min;
            public string DimensionsText => "Dimensions X/Y/Z: " + FormatLength(Size.X) + " x " + FormatLength(Size.Y) + " x " + FormatLength(Size.Z);
            public string LongestDimensionText
            {
                get
                {
                    float x = Math.Abs(Size.X);
                    float y = Math.Abs(Size.Y);
                    float z = Math.Abs(Size.Z);
                    if (x >= y && x >= z) return "(x) " + FormatLength(Size.X);
                    if (y >= x && y >= z) return "(y) " + FormatLength(Size.Y);
                    return "(z) " + FormatLength(Size.Z);
                }
            }

            public static GlbModel Load(string path)
            {
                using BinaryReader reader = new(File.OpenRead(path));
                uint magic = reader.ReadUInt32();
                uint version = reader.ReadUInt32();
                uint length = reader.ReadUInt32();
                if (magic != 0x46546C67 || version != 2 || length == 0)
                {
                    throw new InvalidDataException("Format GLB non supporté.");
                }

                string json = string.Empty;
                byte[]? bin = null;
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    uint chunkLength = reader.ReadUInt32();
                    uint chunkType = reader.ReadUInt32();
                    byte[] chunk = reader.ReadBytes((int)chunkLength);
                    if (chunkType == 0x4E4F534A)
                    {
                        json = System.Text.Encoding.UTF8.GetString(chunk);
                    }
                    else if (chunkType == 0x004E4942)
                    {
                        bin = chunk;
                    }
                }

                if (string.IsNullOrWhiteSpace(json) || bin == null)
                {
                    throw new InvalidDataException("GLB incomplet: JSON ou BIN manquant.");
                }

                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                JsonElement accessors = root.GetProperty("accessors");
                JsonElement bufferViews = root.GetProperty("bufferViews");
                JsonElement meshes = root.GetProperty("meshes");
                GlbMaterial[] materials = ReadMaterials(root, bufferViews, bin);
                List<GlbPrimitive> primitives = new();
                Vector3 min = new(float.MaxValue);
                Vector3 max = new(float.MinValue);

                foreach (JsonElement mesh in meshes.EnumerateArray())
                {
                    foreach (JsonElement primitive in mesh.GetProperty("primitives").EnumerateArray())
                    {
                        if (primitive.TryGetProperty("mode", out JsonElement mode) && mode.GetInt32() != 4)
                        {
                            continue;
                        }

                        JsonElement attributes = primitive.GetProperty("attributes");
                        if (!attributes.TryGetProperty("POSITION", out JsonElement positionAccessor))
                        {
                            continue;
                        }

                        List<Vector3> vertices = ReadVec3Accessor(accessors[positionAccessor.GetInt32()], bufferViews, bin);
                        List<Vector3> normals = attributes.TryGetProperty("NORMAL", out JsonElement normalAccessor)
                            ? ReadVec3Accessor(accessors[normalAccessor.GetInt32()], bufferViews, bin)
                            : new List<Vector3>();
                        List<Vector2> texCoords = attributes.TryGetProperty("TEXCOORD_0", out JsonElement texCoordAccessor)
                            ? ReadVec2Accessor(accessors[texCoordAccessor.GetInt32()], bufferViews, bin)
                            : new List<Vector2>();
                        List<int> indices = primitive.TryGetProperty("indices", out JsonElement indexAccessor)
                            ? ReadIndexAccessor(accessors[indexAccessor.GetInt32()], bufferViews, bin)
                            : Enumerable.Range(0, vertices.Count).ToList();
                        GlbMaterial material = GlbMaterial.Empty;
                        if (primitive.TryGetProperty("material", out JsonElement materialIndexElement))
                        {
                            int materialIndex = materialIndexElement.GetInt32();
                            if (materialIndex >= 0 && materialIndex < materials.Length)
                            {
                                material = materials[materialIndex];
                            }
                        }

                        foreach (Vector3 vertex in vertices)
                        {
                            min = Vector3.ComponentMin(min, vertex);
                            max = Vector3.ComponentMax(max, vertex);
                        }

                        primitives.Add(new GlbPrimitive(vertices, normals, texCoords, indices, material.TextureBytes, material.DoubleSided));
                    }
                }

                if (primitives.Count == 0)
                {
                    throw new InvalidDataException("Aucune primitive triangulée trouvée dans le GLB.");
                }

                Vector3 center = (min + max) * 0.5f;
                float radius = Math.Max(0.001f, (max - min).Length * 0.5f);
                return new GlbModel { Primitives = primitives, Center = center, Radius = radius, Min = min, Max = max };
            }

            private static string FormatLength(float meters)
            {
                float value = Math.Abs(meters);
                if (value < 0.01f)
                {
                    return (meters * 1000f).ToString("0.##") + " mm";
                }

                if (value < 1f)
                {
                    return (meters * 100f).ToString("0.##") + " cm";
                }

                return meters.ToString("0.###") + " m";
            }

            private static GlbMaterial[] ReadMaterials(JsonElement root, JsonElement bufferViews, byte[] bin)
            {
                if (!root.TryGetProperty("materials", out JsonElement materials))
                {
                    return Array.Empty<GlbMaterial>();
                }

                GlbMaterial[] result = new GlbMaterial[materials.GetArrayLength()];
                for (int materialIndex = 0; materialIndex < materials.GetArrayLength(); materialIndex++)
                {
                    JsonElement material = materials[materialIndex];
                    bool doubleSided = material.TryGetProperty("doubleSided", out JsonElement doubleSidedElement) &&
                        doubleSidedElement.ValueKind == JsonValueKind.True;
                    if (!material.TryGetProperty("pbrMetallicRoughness", out JsonElement pbr) ||
                        !pbr.TryGetProperty("baseColorTexture", out JsonElement baseColorTexture) ||
                        !baseColorTexture.TryGetProperty("index", out JsonElement textureIndexElement))
                    {
                        result[materialIndex] = new GlbMaterial(Array.Empty<byte>(), doubleSided);
                        continue;
                    }

                    byte[]? textureBytes = ReadTextureBytes(root, textureIndexElement.GetInt32(), bufferViews, bin);
                    result[materialIndex] = new GlbMaterial(textureBytes ?? Array.Empty<byte>(), doubleSided);
                }

                return result;
            }

            private static byte[]? ReadTextureBytes(JsonElement root, int textureIndex, JsonElement bufferViews, byte[] bin)
            {
                if (!root.TryGetProperty("textures", out JsonElement textures) ||
                    textureIndex < 0 ||
                    textureIndex >= textures.GetArrayLength())
                {
                    return null;
                }

                JsonElement texture = textures[textureIndex];
                if (!texture.TryGetProperty("source", out JsonElement sourceElement))
                {
                    return null;
                }

                int imageIndex = sourceElement.GetInt32();
                if (!root.TryGetProperty("images", out JsonElement images) ||
                    imageIndex < 0 ||
                    imageIndex >= images.GetArrayLength())
                {
                    return null;
                }

                JsonElement image = images[imageIndex];
                if (image.TryGetProperty("bufferView", out JsonElement imageBufferView))
                {
                    return ReadBufferViewBytes(bufferViews[imageBufferView.GetInt32()], bin);
                }

                if (image.TryGetProperty("uri", out JsonElement uriElement))
                {
                    string uri = uriElement.GetString() ?? string.Empty;
                    const string base64Marker = ";base64,";
                    int markerIndex = uri.IndexOf(base64Marker, StringComparison.OrdinalIgnoreCase);
                    if (markerIndex >= 0)
                    {
                        return Convert.FromBase64String(uri[(markerIndex + base64Marker.Length)..]);
                    }
                }

                return null;
            }

            private static byte[] ReadBufferViewBytes(JsonElement bufferView, byte[] bin)
            {
                int viewOffset = bufferView.TryGetProperty("byteOffset", out JsonElement viewByteOffset) ? viewByteOffset.GetInt32() : 0;
                int viewLength = bufferView.GetProperty("byteLength").GetInt32();
                byte[] result = new byte[viewLength];
                System.Buffer.BlockCopy(bin, viewOffset, result, 0, viewLength);
                return result;
            }

            private static List<Vector3> ReadVec3Accessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
            {
                if (accessor.GetProperty("componentType").GetInt32() != 5126 ||
                    !string.Equals(accessor.GetProperty("type").GetString(), "VEC3", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Accessor VEC3 float attendu.");
                }

                int count = accessor.GetProperty("count").GetInt32();
                int accessorOffset = accessor.TryGetProperty("byteOffset", out JsonElement accessorByteOffset) ? accessorByteOffset.GetInt32() : 0;
                JsonElement bufferView = bufferViews[accessor.GetProperty("bufferView").GetInt32()];
                int viewOffset = bufferView.TryGetProperty("byteOffset", out JsonElement viewByteOffset) ? viewByteOffset.GetInt32() : 0;
                int stride = bufferView.TryGetProperty("byteStride", out JsonElement byteStride) ? byteStride.GetInt32() : 12;
                int start = viewOffset + accessorOffset;
                List<Vector3> values = new(count);
                for (int i = 0; i < count; i++)
                {
                    int offset = start + i * stride;
                    values.Add(new Vector3(
                        BitConverter.ToSingle(bin, offset),
                        BitConverter.ToSingle(bin, offset + 4),
                        BitConverter.ToSingle(bin, offset + 8)));
                }

                return values;
            }

            private static List<Vector2> ReadVec2Accessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
            {
                if (accessor.GetProperty("componentType").GetInt32() != 5126 ||
                    !string.Equals(accessor.GetProperty("type").GetString(), "VEC2", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Accessor VEC2 float attendu.");
                }

                int count = accessor.GetProperty("count").GetInt32();
                int accessorOffset = accessor.TryGetProperty("byteOffset", out JsonElement accessorByteOffset) ? accessorByteOffset.GetInt32() : 0;
                JsonElement bufferView = bufferViews[accessor.GetProperty("bufferView").GetInt32()];
                int viewOffset = bufferView.TryGetProperty("byteOffset", out JsonElement viewByteOffset) ? viewByteOffset.GetInt32() : 0;
                int stride = bufferView.TryGetProperty("byteStride", out JsonElement byteStride) ? byteStride.GetInt32() : 8;
                int start = viewOffset + accessorOffset;
                List<Vector2> values = new(count);
                for (int i = 0; i < count; i++)
                {
                    int offset = start + i * stride;
                    values.Add(new Vector2(
                        BitConverter.ToSingle(bin, offset),
                        BitConverter.ToSingle(bin, offset + 4)));
                }

                return values;
            }

            private static List<int> ReadIndexAccessor(JsonElement accessor, JsonElement bufferViews, byte[] bin)
            {
                int componentType = accessor.GetProperty("componentType").GetInt32();
                int count = accessor.GetProperty("count").GetInt32();
                int accessorOffset = accessor.TryGetProperty("byteOffset", out JsonElement accessorByteOffset) ? accessorByteOffset.GetInt32() : 0;
                JsonElement bufferView = bufferViews[accessor.GetProperty("bufferView").GetInt32()];
                int viewOffset = bufferView.TryGetProperty("byteOffset", out JsonElement viewByteOffset) ? viewByteOffset.GetInt32() : 0;
                int start = viewOffset + accessorOffset;
                List<int> values = new(count);
                for (int i = 0; i < count; i++)
                {
                    values.Add(componentType switch
                    {
                        5121 => bin[start + i],
                        5123 => BitConverter.ToUInt16(bin, start + i * 2),
                        5125 => unchecked((int)BitConverter.ToUInt32(bin, start + i * 4)),
                        _ => throw new InvalidDataException("Type d'index GLB non supporté.")
                    });
                }

                return values;
            }
        }

        private sealed class GlbPrimitive
        {
            public GlbPrimitive(List<Vector3> vertices, List<Vector3> normals, List<Vector2> texCoords, List<int> indices, byte[]? textureBytes, bool doubleSided)
            {
                Vertices = vertices;
                Normals = normals;
                TexCoords = texCoords;
                Indices = indices;
                TextureBytes = textureBytes;
                DoubleSided = doubleSided;
            }

            public List<Vector3> Vertices { get; }
            public List<Vector3> Normals { get; }
            public List<Vector2> TexCoords { get; }
            public List<int> Indices { get; }
            public byte[]? TextureBytes { get; }
            public bool DoubleSided { get; }
            public int TextureId { get; set; }
        }

        private sealed record GlbMaterial(byte[] TextureBytes, bool DoubleSided)
        {
            public static GlbMaterial Empty { get; } = new(Array.Empty<byte>(), true);
        }

        private sealed class ModelMetrics
        {
            public double? VolumeCm3 { get; init; }
            public string VolumeCm3Text => VolumeCm3.HasValue
                ? "Volume: " + VolumeCm3.Value.ToString("0.###", CultureInfo.InvariantCulture) + " cm³"
                : "Volume: non disponible";

            public static ModelMetrics? TryLoadForGlb(string glbPath)
            {
                string? metricsPath = FindMetricsPath(glbPath);
                if (metricsPath == null)
                {
                    return null;
                }

                try
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(metricsPath));
                    JsonElement root = document.RootElement;
                    double? volumeCm3 = root.TryGetProperty("volume_cm3", out JsonElement volumeElement) &&
                        volumeElement.TryGetDouble(out double parsedVolume)
                            ? parsedVolume
                            : null;

                    return new ModelMetrics { VolumeCm3 = volumeCm3 };
                }
                catch
                {
                    return null;
                }
            }

            private static string? FindMetricsPath(string glbPath)
            {
                string? directory = Path.GetDirectoryName(glbPath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return null;
                }

                string baseName = Path.GetFileNameWithoutExtension(glbPath);
                string projectName = baseName.EndsWith("_LR", StringComparison.OrdinalIgnoreCase) ||
                    baseName.EndsWith("_HR", StringComparison.OrdinalIgnoreCase)
                        ? baseName[..^3]
                        : baseName;

                string preferredPath = Path.Combine(directory, projectName + "_metrics.json");
                if (File.Exists(preferredPath))
                {
                    return preferredPath;
                }

                string[] candidates = Directory.GetFiles(directory, "*_metrics.json");
                return candidates.Length == 1 ? candidates[0] : null;
            }
        }
    }
}
