﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Windows.Forms;
using AcTools;
using AcTools.AcdEncryption;
using AcTools.AcdFile;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Render.Wrapper;
using CommandLine;

namespace CustomShowroom {
    public class Program {
        private static void UpdateAmbientShadows(string kn5) {
            using (var renderer = new AmbientShadowRenderer(kn5, null) {
                Iterations = 8000,
                HideWheels = false
            }) {
                var dir = Path.GetDirectoryName(kn5);
                renderer.Shot(dir, null, default);
                // Process.Start(Path.Combine(dir, "tyre_0_shadow.png"));
            }
        }

        private static void ExtractUv(string kn5, string extractUvTexture) {
            using (var renderer = new UvRenderer(kn5)) {
                var dir = Path.GetDirectoryName(kn5) ?? "";
                var output = Path.Combine(dir, "extracted_uv.png");
                renderer.Shot(output, extractUvTexture, null);
                Process.Start(output);
            }
        }

        [STAThread]
        private static int Main(string[] a){
            if (!Debugger.IsAttached) {
                SetUnhandledExceptionHandler();
            }

            var packedHelper = new PackedHelper("AcTools_CustomShowroom", "References", null);
            packedHelper.PrepareUnmanaged("Magick.NET-Q8-x86.Native");
            AppDomain.CurrentDomain.AssemblyResolve += packedHelper.Handler;
            return MainInner(a);
        }

        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        public static void SetUnhandledExceptionHandler() {
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += UnhandledExceptionHandler;
        }

        private class TrackMapRendererFilter : ITrackMapRendererFilter {
            public bool Filter(string name) {
                return name?.Contains("TARMAC") == true;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int MainInner(string[] args) {
            Acd.Factory = new AcdFactory();

            var argsFile = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? "", "Arguments.txt");
            if (File.Exists(argsFile)) {
                args = File.ReadAllLines(argsFile).Concat(args).ToArray();
            }

            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options) || options.Help) {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var form = new Form {
                    Width = 640,
                    Height = 480,
                    FormBorderStyle = FormBorderStyle.FixedToolWindow,
                    StartPosition = FormStartPosition.CenterScreen
                };

                var message = new TextBox {
                    Multiline = true,
                    ReadOnly = true,
                    BackColor = Control.DefaultBackColor,
                    BorderStyle = BorderStyle.None,
                    Text = options.GetUsage(),
                    Location = new Point(20, 8),
                    Size = new Size(form.ClientSize.Width - 40, form.ClientSize.Height - 16),
                    Padding = new Padding(20),
                    Font = new Font("Consolas", 9f),
                    TabStop = false
                };

                var button = new Button {
                    Text = "OK",
                    Location = new Point(form.ClientSize.Width / 2 - 80, form.ClientSize.Height - 40),
                    Size = new Size(160, 32)
                };

                button.Click += (sender, eventArgs) => form.Close();

                form.Controls.Add(button);
                form.Controls.Add(message);
                form.ShowDialog();

                return 1;
            }

            var filename = Assembly.GetEntryAssembly()?.Location;
            if (options.Verbose || filename.IndexOf("log", StringComparison.OrdinalIgnoreCase) != -1
                    || filename.IndexOf("debug", StringComparison.OrdinalIgnoreCase) != -1) {
                // TODO
            }

            var inputItems = options.Items;
#if DEBUG
            inputItems = inputItems.Any() ? inputItems : new[] { DebugHelper.GetCarKn5(), DebugHelper.GetShowroomKn5() };
            options.MagickOverride = true;
#endif

            if (inputItems.Count == 0) {
                var dialog = new OpenFileDialog {
                    Title = @"Select KN5",
                    Filter = @"KN5 Files (*.kn5)|*.kn5"
                };
                if (dialog.ShowDialog() != DialogResult.OK) return 2;

                inputItems = new[] { dialog.FileName };
            }

            var kn5File = inputItems.ElementAtOrDefault(0);
            if (kn5File == null || !File.Exists(kn5File)) {
                MessageBox.Show(@"File is missing", @"Custom Showroom", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 3;
            }

            if (options.Mode == Mode.UpdateAmbientShadows) {
                MessageBox.Show("Started");
                var sw = Stopwatch.StartNew();
                UpdateAmbientShadows(kn5File);
                MessageBox.Show($@"Time taken: {sw.Elapsed.TotalSeconds:F2}s");
                return 0;
            }

            if (options.Mode == Mode.ExtractUv) {
                if (string.IsNullOrWhiteSpace(options.ExtractUvTexture)) {
                    MessageBox.Show(@"Texture to extract is not specified", @"Custom Showroom", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 4;
                }
                ExtractUv(kn5File, options.ExtractUvTexture);
                return 0;
            }

            var showroomKn5File = inputItems.ElementAtOrDefault(1);
            if (showroomKn5File == null && options.ShowroomId != null) {
                showroomKn5File = Path.Combine(
                        Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(kn5File))) ?? "",
                        "showroom", options.ShowroomId, options.ShowroomId + ".kn5");
            }

            if (!File.Exists(showroomKn5File)) {
                showroomKn5File = null;
            }

            if (options.Mode == Mode.Lite) {
                using (var renderer = new ToolsKn5ObjectRenderer(new CarDescription(kn5File))) {
                    renderer.UseMsaa = options.UseMsaa;
                    renderer.UseFxaa = options.UseFxaa;
                    renderer.UseSsaa = options.UseSsaa;
                    renderer.MagickOverride = options.MagickOverride;
                    new LiteShowroomFormWrapper(renderer).Run();
                }
            } else if (options.Mode == Mode.Dark) {
                using (var renderer = new DarkKn5ObjectRenderer(new CarDescription(kn5File), showroomKn5File)) {
                    // UI
                    renderer.UseSprite = true;
                    renderer.VisibleUi = true;

                    /*renderer.UseDof = true;
                    renderer.UseAccumulationDof = true;
                    renderer.AccumulationDofApertureSize = 0f;
                    renderer.AccumulationDofBokeh = false;
                    renderer.AccumulationDofIterations = 100;*/

#if DEBUG
                    renderer.AoOpacity = 0.9f;
                    renderer.AoDebug = true;
                    renderer.UseAo = true;
                    renderer.AoType = AoType.Hbao;

                    /*renderer.BackgroundColor = Color.Black;
                    renderer.LightBrightness = 0.2f;
                    renderer.AmbientBrightness = 0.2f;
                    /*renderer.BackgroundBrightness = 0.02f;
                    renderer.FlatMirror = true;*/
                    /*renderer.FlatMirrorReflectedLight = true;
                    renderer.TryToGuessCarLights = true;

                    renderer.FlatMirrorBlurred = true;
                    renderer.FlatMirror = true;*/

                    //renderer.AddCar(new CarDescription(@"D:\Games\Assetto Corsa\content\cars\ferrari_f40\ferrari_f40.kn5"));
#else

        // renderer.FlatMirror = true;
                    renderer.UseMsaa = options.UseMsaa;
                    renderer.UseFxaa = options.UseFxaa;
                    renderer.UseSsaa = options.UseSsaa;
#endif

                    renderer.MagickOverride = options.MagickOverride;
                    new LiteShowroomFormWrapper(renderer) {
                        ReplaceableShowroom = true
                    }.Run(() => {
                        // ReSharper disable once AccessToDisposedClosure
                        var r = renderer;

                        if (r.CarNode != null) {
                            // r.CarNode.AlignWheelsByData = true;
                        }
                    });
                }
            } else if (options.Mode == Mode.TrackMap) {
                using (var renderer = new TrackMapPreparationRenderer(kn5File)) {
                    renderer.UseFxaa = options.UseFxaa;
                    renderer.SetFilter(new TrackMapRendererFilter());
                    new BaseKn5FormWrapper(renderer, "Track", 800, 800).Run();
                }
            }

            GCHelper.CleanUp();
            return 0;
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args) {
            var e = args.ExceptionObject as Exception;

            var text = "Unhandled exception:\n\n" + (e?.ToString() ?? "null");

            try {
                MessageBox.Show(text, @"Oops!", MessageBoxButtons.OK);
            } catch (Exception) {
                // ignored
            }

            try {
                var logFilename = AppDomain.CurrentDomain.BaseDirectory + "/custom_showroom_crash_" + DateTime.Now.Ticks + ".txt";
                File.WriteAllText(logFilename, text);
            } catch (Exception) {
                // ignored
            }

            Environment.Exit(1);
        }
    }
}
