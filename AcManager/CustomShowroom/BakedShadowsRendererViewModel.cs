﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Render.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Size = System.Windows.Size;

namespace AcManager.CustomShowroom {
    public class BakedShadowsRendererViewModel : NotifyPropertyChanged, IUserPresetable {
        public static readonly string PresetableKey = "Baked Shadows";
        public static readonly PresetsCategory PresetableKeyCategory = new PresetsCategory(PresetableKey);

        private class SaveableData {
            public double From, To = 60d, Brightness = 220d, Gamma = 60d, PixelDensity = 4d, Ambient, ShadowBias, ShadowBiasCullBack = 70d;
            public int Iterations = 5000, Padding = 4, ShadowMapSize = 2048;
            public bool UseFxaa = true, FullyTransparent = true, UseDxt5 = true;
        }

        [CanBeNull]
        private readonly BaseRenderer _renderer;

        private readonly IKn5 _kn5;
        private readonly ISaveHelper _saveable;

        [NotNull]
        public readonly string TextureName;

        [CanBeNull]
        public readonly string ObjectPath;

        private Size? _originSize;

        public Size? OriginSize {
            get => _originSize;
            set {
                if (value.Equals(_originSize)) return;
                _originSize = value;
                _size = value;
                OnPropertyChanged();
            }
        }

        private Size? _size;

        [CanBeNull]
        private readonly CarObject _car;

        public static BakedShadowsRendererViewModel ForTexture([CanBeNull] BaseRenderer renderer, [NotNull] IKn5 kn5, [NotNull] string textureName,
                [CanBeNull] CarObject car) {
            return new BakedShadowsRendererViewModel(renderer, kn5, textureName, null, car);
        }

        public static BakedShadowsRendererViewModel ForObject([CanBeNull] BaseRenderer renderer, [NotNull] IKn5 kn5, [NotNull] string objectPath,
                [CanBeNull] CarObject car) {
            return new BakedShadowsRendererViewModel(renderer, kn5, null, objectPath, car);
        }

        private BakedShadowsRendererViewModel([CanBeNull] BaseRenderer renderer, [NotNull] IKn5 kn5,
                [CanBeNull] string textureName, [CanBeNull] string objectPath, [CanBeNull] CarObject car) {
            _renderer = renderer;
            _kn5 = kn5;

            if (textureName == null) {
                if (objectPath == null) throw new ArgumentNullException(nameof(objectPath));

                var node = _kn5.GetNode(objectPath)
                        ?? throw new Exception($"Node “{objectPath}” not found");
                var material = _kn5.GetMaterial(node.MaterialId)
                        ?? throw new Exception($"Material for node “{objectPath}” not found");
                textureName = material.GetMappingByName("txDiffuse")?.Texture ?? material.TextureMappings.FirstOrDefault()?.Texture
                        ?? throw new Exception($"Texture for node “{objectPath}” not found");

                TextureName = textureName;
                ObjectPath = objectPath;
            }

            TextureName = textureName;

            _car = car;
            _saveable = new SaveHelper<SaveableData>("_carTextureDialog", () => new SaveableData {
                From = From,
                To = To,
                Brightness = Brightness,
                Gamma = Gamma,
                Ambient = Ambient,
                Iterations = Iterations,
                ShadowBias = ShadowBiasCullFront,
                ShadowBiasCullBack = ShadowBiasCullBack,
                PixelDensity = PixelDensity,
                Padding = Padding,
                ShadowMapSize = ShadowMapSize,
                UseFxaa = UseFxaa,
                FullyTransparent = FullyTransparent,
                UseDxt5 = UseDxt5,
            }, o => {
                From = o.From;
                To = o.To;
                Brightness = o.Brightness;
                Gamma = o.Gamma;
                Ambient = o.Ambient;
                Iterations = o.Iterations;
                ShadowBiasCullFront = o.ShadowBias;
                ShadowBiasCullBack = o.ShadowBiasCullBack;
                PixelDensity = o.PixelDensity;
                Padding = o.Padding;
                ShadowMapSize = o.ShadowMapSize;
                UseFxaa = o.UseFxaa;
                FullyTransparent = o.FullyTransparent;
                UseDxt5 = o.UseDxt5;
            });

            _saveable.Initialize();
        }

        #region Properties
        private double _from;

        public double From {
            get => _from;
            set => Apply(value, ref _from);
        }

        private double _to = 60;

        public double To {
            get => _to;
            set => Apply(value, ref _to);
        }

        private double _brightness = 220;

        public double Brightness {
            get => _brightness;
            set => Apply(value, ref _brightness);
        }

        private double _gamma = 60;

        public double Gamma {
            get => _gamma;
            set => Apply(value, ref _gamma);
        }

        private double _ambient;

        public double Ambient {
            get => _ambient;
            set => Apply(value, ref _ambient);
        }

        private double _shadowBiasCullFront;

        public double ShadowBiasCullFront {
            get => _shadowBiasCullFront;
            set => Apply(value, ref _shadowBiasCullFront);
        }

        private double _shadowBiasCullBack;

        public double ShadowBiasCullBack {
            get => _shadowBiasCullBack;
            set => Apply(value, ref _shadowBiasCullBack);
        }

        private int _iterations = 5000;

        public int Iterations {
            get => _iterations;
            set {
                value = value.Clamp(10, 1000000);
                if (Equals(value, _iterations)) return;
                _iterations = value;
                OnPropertyChanged();
            }
        }

        private double _pixelDensity = 4d;

        public double PixelDensity {
            get => _pixelDensity;
            set {
                value = value.Clamp(0.1, 16d);
                if (Equals(value, _pixelDensity)) return;
                _pixelDensity = value;
                OnPropertyChanged();
            }
        }

        private int _padding;

        public int Padding {
            get => _padding;
            set {
                value = value.Clamp(0, 1000);
                if (Equals(value, _padding)) return;
                _padding = value;
                OnPropertyChanged();
            }
        }

        private bool _useFxaa;

        public bool UseFxaa {
            get => _useFxaa;
            set => Apply(value, ref _useFxaa);
        }

        private bool _fullyTransparent;

        public bool FullyTransparent {
            get => _fullyTransparent;
            set => Apply(value, ref _fullyTransparent);
        }

        private bool _useDxt5;

        public bool UseDxt5 {
            get => _useDxt5;
            set => Apply(value, ref _useDxt5);
        }

        private int _shadowMapSize;

        public int ShadowMapSize {
            get => _shadowMapSize;
            set {
                if (Equals(value, _shadowMapSize)) return;
                _shadowMapSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShadowMapSizeSetting));
            }
        }

        public SettingEntry ShadowMapSizeSetting {
            get => DarkRendererSettings.ShadowResolutions.GetByIdOrDefault<SettingEntry, int?>(ShadowMapSize) ??
                    new SettingEntry(ShadowMapSize, $"{ShadowMapSize}×{ShadowMapSize}");
            set => ShadowMapSize = value.IntValue ?? 2048;
        }
        #endregion

        #region Generating
        private const string KeyDimensions = "__BakedShadowsRendererViewModel.Dimensions";

        [ItemCanBeNull]
        private async Task<Tuple<byte[], byte[], Size>> CalculateAo(int? size, [CanBeNull] CarObject car) {
            int width, height;
            switch (size) {
                case null:
                    var result = Prompt.Show(ControlsStrings.CustomShowroom_ViewMapping_Prompt, ControlsStrings.CustomShowroom_ViewMapping,
                            ValuesStorage.Get(KeyDimensions, _size.HasValue ? $"{_size?.Width}x{_size?.Height}" : ""), @"2048x2048");
                    if (string.IsNullOrWhiteSpace(result)) return null;

                    ValuesStorage.Set(KeyDimensions, result);

                    var match = Regex.Match(result, @"^\s*(\d+)(?:\s+|\s*\D\s*)(\d+)\s*$");
                    if (match.Success) {
                        width = FlexibleParser.ParseInt(match.Groups[1].Value);
                        height = FlexibleParser.ParseInt(match.Groups[2].Value);
                    } else {
                        if (FlexibleParser.TryParseInt(result, out var value)) {
                            width = height = value;
                        } else {
                            NonfatalError.Notify(ControlsStrings.CustomShowroom_ViewMapping_ParsingFailed,
                                    ControlsStrings.CustomShowroom_ViewMapping_ParsingFailed_Commentary);
                            return null;
                        }
                    }
                    break;

                case -1:
                    width = (int)(_size?.Width ?? 1024);
                    height = (int)(_size?.Height ?? 1024);
                    break;

                default:
                    width = height = size.Value;
                    break;
            }

            using (var waiting = new WaitingDialog(reportValue: "Rendering…") {
                Topmost = false,
                ShowProgressBar = true
            }) {
                var cancellation = waiting.CancellationToken;
                var progress = (IProgress<double>)waiting;

                return await Task.Run(() => {
                    using (var renderer = new BakedShadowsRenderer(_kn5, car?.AcdData) {
                        ΘFromDeg = (float)From,
                        ΘToDeg = (float)To,
                        Iterations = Iterations,
                        SkyBrightnessLevel = (float)Brightness / 100f,
                        Gamma = (float)Gamma / 100f,
                        Ambient = (float)Ambient / 100f,
                        ShadowBiasCullFront = (float)ShadowBiasCullFront / 100f,
                        ShadowBiasCullBack = (float)ShadowBiasCullBack / 100f,
                        UseFxaa = UseFxaa,
                        FullyTransparent = FullyTransparent,
                        Padding = Padding,
                        MapSize = ShadowMapSize,
                        ResolutionMultiplier = Math.Sqrt(PixelDensity)
                    }) {
                        renderer.CopyStateFrom(_renderer as ToolsKn5ObjectRenderer);
                        renderer.Width = width;
                        renderer.Height = height;
                        var shot = renderer.Shot(TextureName, ObjectPath, progress, cancellation);
                        var toDisplay = shot;
                        if (FullyTransparent && shot != null) {
                            waiting.Report("Preparing for preview…");
                            using (var stream = new MemoryStream(shot))
                            using (var output = new MemoryStream()) {
                                ImageUtils.Convert(stream, output, 97);
                                toDisplay = output.ToArray();
                            }
                        }
                        return Tuple.Create(shot, toDisplay, new Size(width, height));
                    }
                });
            }
        }

        private AsyncCommand<string> _calculateAoCommand;

        public AsyncCommand<string> CalculateAoCommand => _calculateAoCommand ?? (_calculateAoCommand = new AsyncCommand<string>(async o => {
            try {
                var calculated = await CalculateAo(FlexibleParser.TryParseInt(o), _car);
                if (calculated?.Item2 == null) return;

                var viewer = new ImageViewer(new object[] { calculated.Item2, await GetOriginal() }.NonNull(),
                        detailsCallback: DetailsCallback) {
                            MaxImageWidth = calculated.Item3.Width,
                            MaxImageHeight = calculated.Item3.Height,
                            Model = {
                                Saveable = true,
                                SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                                SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename),
                                SaveDefaultFileName = TextureName,
                                SaveDialogFilterPieces = {
                                    DialogFilterPiece.DdsFiles,
                                    DialogFilterPiece.JpegFiles,
                                    DialogFilterPiece.PngFiles,
                                },
                                SaveCallback = SaveCallback,
                                CanBeSavedCallback = i => i == 0
                            },
                            ShowInTaskbar = false,
                            DoNotAttachToWaitingDialogs = true
                        };
                viewer.ShowDialog();

                Task<byte[]> GetOriginal() {
                    if (_renderer == null || !_kn5.TexturesData.TryGetValue(TextureName, out var data)) {
                        return Task.FromResult<byte[]>(null);
                    }

                    return Task.Run(() => new TextureReader().ToPng(data, true,
                            new System.Drawing.Size(calculated.Item3.Width.RoundToInt(), calculated.Item3.Height.RoundToInt())));
                }

                object DetailsCallback(int index) {
                    return index == 0 ? "Generated AO map" : "Original texture";
                }

                Task SaveCallback(int index, string destination) {
                    return Task.Run(() => {
                        var extension = Path.GetExtension(destination)?.ToLowerInvariant();
                        switch (extension) {
                            case ".dds":
                                DdsEncoder.SaveAsDds(destination, calculated.Item1,
                                        UseDxt5 ? PreferredDdsFormat.DXT5 : PreferredDdsFormat.LuminanceTransparency, null);
                                break;
                            case ".png":
                                if (FullyTransparent) {
                                    using (var stream = new MemoryStream(calculated.Item1))
                                    using (var jpgStream = new MemoryStream()) {
                                        ImageUtils.Convert(stream, jpgStream, 97);
                                        using (var output = File.Create(destination)) {
                                            jpgStream.Position = 0;
                                            Image.FromStream(jpgStream).Save(output, ImageFormat.Png);
                                        }
                                    }
                                    break;
                                } else goto default;
                            case ".jpg":
                            case ".jpeg":
                                using (var stream = new MemoryStream(calculated.Item1))
                                using (var output = File.Create(destination)) {
                                    ImageUtils.Convert(stream, output, 97);
                                }
                                break;
                            default:
                                File.WriteAllBytes(destination, calculated.Item1);
                                break;
                        }
                    });
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t create AO map", e);
            }
        }));
        #endregion

        #region Presetable
        public bool CanBeSaved => true;
        public PresetsCategory PresetableCategory => PresetableKeyCategory;
        string IUserPresetable.PresetableKey => PresetableKey;

        public string ExportToPresetData() {
            return _saveable.ToSerializedString();
        }

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            _saveable.FromSerializedString(data);
        }
        #endregion

        [NotifyPropertyChangedInvocator]
        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            base.OnPropertyChanged(propertyName);
            if (_saveable.SaveLater()) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}