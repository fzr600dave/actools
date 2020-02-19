﻿using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Render.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace AcManager.CustomShowroom {
    public partial class Kn5TextureDialog {
        private ViewModel Model => (ViewModel)DataContext;

        public Kn5TextureDialog([CanBeNull] BaseRenderer renderer, [CanBeNull] CarObject car, [CanBeNull] CarSkinObject activeSkin, [NotNull] IKn5 kn5,
                [NotNull] string textureName, uint materialId, string slotName) {
            DataContext = new ViewModel(renderer, car, activeSkin, kn5, textureName, materialId, slotName) { Close = () => Close() };
            InitializeComponent();

            Buttons = new[] { CloseButton };
        }

        public class ViewModel : NotifyPropertyChanged {
            internal Action Close;

            [CanBeNull]
            private readonly BaseRenderer _renderer;

            [CanBeNull]
            private readonly CarSkinObject _activeSkin;
            private readonly IKn5 _kn5;
            private readonly string _slotName;

            public BakedShadowsRendererViewModel BakedShadows { get; }

            [NotNull]
            public string TextureName { get; }
            public string TextureFormat { get; }

            private string _textureFormatDescription;

            public string TextureFormatDescription {
                get => _textureFormatDescription;
                set => Apply(value, ref _textureFormatDescription);
            }

            private string _textureDimensions;

            public string TextureDimensions {
                get => _textureDimensions;
                set => Apply(value, ref _textureDimensions);
            }

            public byte[] Data { get; set; }

            private bool _loading;

            public bool Loading {
                get => _loading;
                set => Apply(value, ref _loading);
            }

            private BitmapSource _previewImage;

            public BitmapSource PreviewImage {
                get => _previewImage;
                set => Apply(value, ref _previewImage);
            }

            [CanBeNull]
            public Kn5Material Material { get; }

            private string _usedFor;

            public string UsedFor {
                get => _usedFor;
                set => Apply(value, ref _usedFor);
            }

            public bool IsForkAvailable { get; }

            public bool IsChangeAvailable { get; }

            public ViewModel([CanBeNull] BaseRenderer renderer, [CanBeNull] CarObject car, [CanBeNull] CarSkinObject activeSkin, [NotNull] IKn5 kn5,
                    [NotNull] string textureName, uint materialId, string slotName) {
                _renderer = renderer;
                _activeSkin = activeSkin;
                _kn5 = kn5;
                _slotName = slotName;

                TextureName = textureName;
                Material = kn5.GetMaterial(materialId);

                var format = Regex.Match(textureName, @"(?<=\.)([a-zA-Z]{3,4})$").Value;
                TextureFormat = string.IsNullOrWhiteSpace(format) ? null : format.ToUpperInvariant();

                byte[] data;
                Data = kn5.TexturesData.TryGetValue(textureName, out data) ? data : null;

                BakedShadows = BakedShadowsRendererViewModel.ForTexture(renderer, _kn5, TextureName, car);

                var usedFor = (from material in kn5.Materials.Values
                               let slots = (from slot in material.TextureMappings
                                            where string.Equals(slot.Texture, TextureName, StringComparison.OrdinalIgnoreCase)
                                            select slot.Name).ToList()
                               where slots.Count > 0
                               orderby material.Name
                               select new {
                                   Name = $"{material.Name} ({slots.JoinToString(", ")})",
                                   Slots = slots.Count
                               }).ToList();

                IsForkAvailable = usedFor.Sum(x => x.Slots) > 1;
                IsChangeAvailable = kn5.TexturesData.Count > 1;
                UsedFor = usedFor.Select(x => x.Name).JoinToString(", ");
            }

            public async void OnLoaded() {
                Loading = true;

                var loaded = _renderer == null ? await LoadImageUsingMagickNetAsync(Data) : await Task.Run(() => LoadImageUsingDirectX(_renderer, Data));
                PreviewImage = loaded?.Image;
                TextureFormatDescription = loaded?.FormatDescription;
                TextureDimensions = PreviewImage == null ? null : $"{PreviewImage.PixelWidth}×{PreviewImage.PixelHeight}";
                BakedShadows.OriginSize = PreviewImage == null ? (Size?)null : new Size(PreviewImage.PixelWidth, PreviewImage.PixelHeight);
                Loading = false;
            }

            private const string KeyDimensions = "__CarTextureDialog.Dimensions";

            private AsyncCommand<string> _uvCommand;

            public AsyncCommand<string> UvCommand => _uvCommand ?? (_uvCommand = new AsyncCommand<string>(async o => {
                var size = FlexibleParser.TryParseInt(o);
                var filename = FilesStorage.Instance.GetTemporaryFilename(
                        FileUtils.EnsureFileNameIsValid(Path.GetFileNameWithoutExtension(TextureName), true) + " UV.png");

                int width, height;
                switch (size) {
                    case null:
                        var result = Prompt.Show(ControlsStrings.CustomShowroom_ViewMapping_Prompt, ControlsStrings.CustomShowroom_ViewMapping,
                                ValuesStorage.Get(KeyDimensions, PreviewImage != null ? $"{PreviewImage?.PixelWidth}x{PreviewImage?.PixelHeight}" : ""),
                                @"2048x2048");
                        if (string.IsNullOrWhiteSpace(result)) return;

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
                                return;
                            }
                        }
                        break;

                    case -1:
                        width = PreviewImage?.PixelWidth ?? 1024;
                        height = PreviewImage?.PixelHeight ?? 1024;
                        break;

                    default:
                        width = height = size.Value;
                        break;
                }

                await Task.Run(() => {
                    using (var renderer = new UvRenderer(_kn5)) {
                        renderer.Width = width;
                        renderer.Height = height;
                        renderer.Shot(filename, TextureName, null);
                    }
                });

                new ImageViewer(filename) {
                    Model = {
                        Saveable = true,
                        SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                        SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename),
                        SaveDialogFilterPieces = {
                            DialogFilterPiece.PngFiles,
                            DialogFilterPiece.DdsFiles,
                            DialogFilterPiece.JpegFiles,
                        },
                        SaveCallback = SaveCallback,
                    }
                }.ShowDialog();

                Task SaveCallback(int selected, string destination) {
                    return Task.Run(() => {
                        var extension = Path.GetExtension(destination)?.ToLowerInvariant();
                        switch (extension) {
                            case ".dds":
                                DdsEncoder.SaveAsDds(destination, File.ReadAllBytes(filename), PreferredDdsFormat.RGBA4444, null);
                                break;
                            case ".jpg":
                            case ".jpeg":
                                ImageUtils.Convert(filename, destination);
                                break;
                            default:
                                File.Copy(filename, destination, true);
                                break;
                        }
                    });
                }
            }));

            private AsyncCommand _exportCommand;

            public AsyncCommand ExportCommand => _exportCommand ?? (_exportCommand = new AsyncCommand(async () => {
                if (!_kn5.IsEditable) return;
                var filename = FileRelatedDialogs.Save(new SaveDialogParams {
                    InitialDirectory = _activeSkin?.Location ?? Path.GetDirectoryName(_kn5.OriginalFilename),
                    Filters = { DialogFilterPiece.DdsFiles, DialogFilterPiece.ImageFiles },
                    DetaultExtension = TextureFormat.ToLower(),
                    DefaultFileName = TextureName,
                });

                if (filename == null) return;
                try {
                    using (WaitingDialog.Create("Saving…")) {
                        await FileUtils.WriteAllBytesAsync(filename, Data);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(ControlsStrings.CustomShowroom_CannotExport, e);
                }
            }, () => Data != null && _kn5.IsEditable && SettingsHolder.Common.DeveloperMode));

            public async Task UpdateKn5AndClose(bool updateModel) {
                await _kn5.UpdateKn5(updateModel ? _renderer : null, _activeSkin);
                Close?.Invoke();
            }

            private AsyncCommand _replaceCommand;

            public AsyncCommand ReplaceCommand => _replaceCommand ?? (_replaceCommand = new AsyncCommand(async () => {
                var dialog = new OpenFileDialog {
                    InitialDirectory = _activeSkin?.Location ?? Path.GetDirectoryName(_kn5.OriginalFilename) ?? "",
                    Filter = FileDialogFilters.TexturesFilter,
                    FileName = TextureName,
                    CustomPlaces = {
                        new FileDialogCustomPlace(_activeSkin?.Location ?? Path.GetDirectoryName(_kn5.OriginalFilename))
                    }
                };

                if (dialog.ShowDialog() != true) return;

                try {
                    var info = new FileInfo(dialog.FileName);
                    if (!string.Equals(info.Extension, ".dds", StringComparison.OrdinalIgnoreCase)) {
                        if (ShowMessage("Texture is not in DDS format. Are you sure you want to use it?", "Wrong format", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                            return;
                        }
                    } else if (info.Length > 30e6) {
                        if (ShowMessage("Texture is way too big. Are you sure you want to use it?", "Way too big", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                            return;
                        }
                    }

                    using (WaitingDialog.Create("Replacing…")) {
                        _kn5.TexturesData[TextureName] = await FileUtils.ReadAllBytesAsync(dialog.FileName);
                        await UpdateKn5AndClose(true);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t replace texture", e);
                }
            }, () => _kn5.IsEditable));

            private AsyncCommand _renameCommand;

            public AsyncCommand RenameCommand => _renameCommand ?? (_renameCommand = new AsyncCommand(async () => {
                var newName = Prompt.Show("New texture name:", "Rename texture", TextureName, "?", required: true, maxLength: 120)?.Trim();
                if (string.IsNullOrEmpty(newName)) return;

                try {
                    if (_kn5.TexturesData.Keys.Contains(newName, StringComparer.OrdinalIgnoreCase)) {
                        throw new InformativeException("Can’t rename texture", "Name’s already taken.");
                    }

                    using (WaitingDialog.Create("Renaming…")) {
                        _kn5.TexturesData[newName] = _kn5.TexturesData[TextureName];
                        _kn5.Textures[newName] = _kn5.Textures[TextureName];
                        _kn5.Textures[newName].Name = newName;

                        _kn5.TexturesData.Remove(TextureName);
                        _kn5.Textures.Remove(TextureName);

                        foreach (var material in _kn5.Materials.Values) {
                            foreach (var mapping in material.TextureMappings) {
                                if (string.Equals(mapping.Texture, TextureName, StringComparison.OrdinalIgnoreCase)) {
                                    mapping.Texture = newName;
                                }
                            }
                        }
                        await UpdateKn5AndClose(true);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t rename texture", e);
                }
            }, () => _kn5.IsEditable));

            private AsyncCommand _forkCommand;

            public AsyncCommand ForkCommand => _forkCommand ?? (_forkCommand = new AsyncCommand(async () => {
                var material = Material;
                if (material == null) return;

                var newName = Prompt.Show("New texture name:", "Fork texture", TextureName, "?", required: true, maxLength: 120)?.Trim();
                if (string.IsNullOrEmpty(newName)) return;

                try {
                    if (_kn5.TexturesData.Keys.Contains(newName, StringComparer.OrdinalIgnoreCase)) {
                        throw new InformativeException("Can’t fork texture", "Name’s already taken.");
                    }

                    using (WaitingDialog.Create("Forking…")) {
                        var usedElsewhere = _kn5.Materials.Values.ApartFrom(material).Any(m =>
                                m.TextureMappings.Any(slot => string.Equals(slot.Texture, TextureName, StringComparison.OrdinalIgnoreCase)));

                        _kn5.TexturesData[newName] = _kn5.TexturesData[TextureName];
                        _kn5.Textures[newName] = _kn5.Textures[TextureName].Clone();
                        _kn5.Textures[newName].Name = newName;

                        if (!usedElsewhere) {
                            _kn5.TexturesData.Remove(TextureName);
                            _kn5.Textures.Remove(TextureName);
                        }

                        material.TextureMappings.First(x => x.Name == _slotName).Texture = newName;
                        await UpdateKn5AndClose(true);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t fork texture", e);
                }
            }, () => _kn5.IsEditable));

            private AsyncCommand _changeTextureCommand;

            public AsyncCommand ChangeTextureCommand => _changeTextureCommand ?? (_changeTextureCommand = new AsyncCommand(async () => {
                var material = Material;
                if (material == null) return;

                var newName = Prompt.Show("Select texture:", "Change texture", TextureName, "?", required: true, maxLength: 120,
                        suggestions: _kn5.Textures.Keys.OrderBy(x => x), suggestionsFixed: true)?.Trim();
                if (string.IsNullOrEmpty(newName) || newName == TextureName) return;

                try {
                    if (!_kn5.TexturesData.Keys.Contains(newName, StringComparer.OrdinalIgnoreCase)) {
                        throw new InformativeException("Can’t change texture", "Texture with that name not found.");
                    }

                    using (WaitingDialog.Create("Changing…")) {
                        var usedElsewhere = _kn5.Materials.Values.ApartFrom(material).Any(m =>
                                m.TextureMappings.Any(slot => string.Equals(slot.Texture, TextureName, StringComparison.OrdinalIgnoreCase)));

                        if (!usedElsewhere) {
                            _kn5.TexturesData.Remove(TextureName);
                            _kn5.Textures.Remove(TextureName);
                        }

                        material.TextureMappings.First(x => x.Name == _slotName).Texture = newName;
                        await UpdateKn5AndClose(true);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t change texture", e);
                }

            }, () => _kn5.IsEditable));
        }

        internal class LoadedImage {
            public BitmapSource Image;
            public string FormatDescription;
        }

        [CanBeNull]
        internal static LoadedImage LoadImageUsingDirectX(BaseRenderer renderer, byte[] imageData) {
            if (imageData == null || imageData.Length == 0) return null;

            try {
                var pngData = TextureReader.ToPng(renderer.DeviceContextHolder, imageData, true, out var format);
                var image = new BitmapImage();
                using (var stream = new MemoryStream(pngData) {
                    Position = 0
                }) {
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = null;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                image.Freeze();

                return new LoadedImage {
                    Image = image,
                    FormatDescription = format.ToString()
                };
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        [ItemCanBeNull]
        internal static async Task<LoadedImage> LoadImageUsingMagickNetAsync(byte[] imageData) {
            if (imageData == null || imageData.Length == 0) return null;

            try {
                string formatDescription = null;
                if (ImageUtils.IsMagickSupported) {
                    var data = imageData;
                    imageData = await Task.Run(() => ImageUtils.LoadAsConventionalBuffer(data, true, out formatDescription));
                }

                var image = new BitmapImage();
                using (var stream = new MemoryStream(imageData) {
                    Position = 0
                }) {
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = null;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                image.Freeze();

                return new LoadedImage {
                    Image = image,
                    FormatDescription = formatDescription
                };
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        private bool _loaded;
        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            Model.OnLoaded();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
        }

        private void OnPreviewClick(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            new ImageViewer(Model.PreviewImage).ShowDialog();
        }
    }
}
