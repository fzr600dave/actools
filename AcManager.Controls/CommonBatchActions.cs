using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public static class CommonBatchActions {
        public static readonly BatchAction[] DefaultSet = {
            BatchAction_AddToFavourites.Instance,
            BatchAction_RemoveFromFavourites.Instance,
            BatchAction_SetRating.Instance
        };

        public static readonly BatchAction[] AcCommonObjectSet = new BatchAction[] {
            BatchAction_Delete.Instance,
            BatchAction_Enable.Instance,
            BatchAction_Disable.Instance
        }.Append(DefaultSet).ToArray();

        private static readonly BatchAction[] AcJsonObjectNewSet = new BatchAction[] {
            BatchAction_AddTag.Instance
        }.Append(AcCommonObjectSet).ToArray();

        public static IEnumerable<BatchAction> GetDefaultSet<T>() where T : AcObjectNew {
            if (typeof(T).IsSubclassOf(typeof(AcJsonObjectNew))) {
                return AcJsonObjectNewSet;
            }

            if (typeof(T).IsSubclassOf(typeof(AcCommonObject))) {
                return AcCommonObjectSet;
            }

            return DefaultSet;
        }

        #region JSON objects
        public class BatchAction_AddTag : BatchAction<AcJsonObjectNew> {
            public static readonly BatchAction_AddTag Instance = new BatchAction_AddTag();

            public BatchAction_AddTag() : base("Add tag", "Modify list of tags for several cars easily", "UI", "Batch.AddTag") {
                DisplayApply = "Apply";
                Tags = new BetterObservableCollection<string>();
            }

            public override void OnActionSelected() {
                Tags.Clear();
            }

            #region Properies
            public BetterObservableCollection<string> Tags { get; }
            private List<string> _originalTags;

            private bool _sortTags = ValuesStorage.Get("_ba.addTag.sort", true);
            public bool SortTags {
                get => _sortTags;
                set {
                    if (Equals(value, _sortTags)) return;
                    _sortTags = value;
                    ValuesStorage.Set("_ba.addTag.sort", value);
                    OnPropertyChanged();
                }
            }

            private bool _cleanUp = ValuesStorage.Get("_ba.addTag.clean", false);
            public bool CleanUp {
                get => _cleanUp;
                set {
                    if (Equals(value, _cleanUp)) return;
                    _cleanUp = value;
                    ValuesStorage.Set("_ba.addTag.clean", value);
                    OnPropertyChanged();
                }
            }
            #endregion

            public override bool IsAvailable(AcJsonObjectNew obj) {
                return true;
            }

            public override int OnSelectionChanged(IEnumerable<AcJsonObjectNew> enumerable) {
                IEnumerable<string> removed, added;

                if (_originalTags != null) {
                    removed = _originalTags.Where(x => !Tags.Contains(x)).ToList();
                    added = Tags.Where(x => !_originalTags.Contains(x)).ToList();
                } else {
                    removed = added = null;
                }

                List<string> list = null;
                var count = 0;
                foreach (var car in enumerable) {
                    count++;

                    if (list == null) {
                        list = car.Tags.ToList();
                    } else {
                        for (var i = list.Count - 1; i >= 0; i--) {
                            if (car.Tags.ContainsIgnoringCase(list[i])) continue;
                            list.RemoveAt(i);
                            if (list.Count == 0) goto End;
                        }
                    }
                }

                End:
                _originalTags = list ?? new List<string>(0);
                Tags.ReplaceEverythingBy_Direct(_originalTags.ApartFrom(removed)
                                                             .If(x => added == null ? x : x.Concat(added))
                                                             .OrderBy(x => x, TagsComparer.Instance));

                return count;
            }

            protected override void ApplyOverride(AcJsonObjectNew obj) {
                var updatedList = obj.Tags.ApartFrom(_originalTags.Where(x => !Tags.Contains(x)))
                                     .Concat(Tags.Where(x => !_originalTags.Contains(x))).Distinct();

                if (_cleanUp) {
                    updatedList = TagsCollection.CleanUp(updatedList);
                }

                if (_sortTags) {
                    updatedList = updatedList.OrderBy(x => x, TagsComparer.Instance);
                }

                obj.Tags = new TagsCollection(updatedList);
            }
        }
        #endregion

        #region Rating
        public class BatchAction_SetRating : BatchAction<AcObjectNew> {
            public static readonly BatchAction_SetRating Instance = new BatchAction_SetRating();

            public BatchAction_SetRating() : base("Set rating", "Set rating of several objects at once", "Rating", "Batch.SetRating") {
                DisplayApply = RemoveRating ? "Un-rate" : "Rate";
            }

            private double _rating = ValuesStorage.Get("_ba.setRating.value", 4d);
            public double Rating {
                get => _rating;
                set {
                    if (Equals(value, _rating)) return;
                    _rating = value;
                    ValuesStorage.Set("_ba.setRating.value", value);
                    OnPropertyChanged();
                    RaiseAvailabilityChanged();
                }
            }

            private bool _removeRating = ValuesStorage.Get("_ba.setRating.remove", true);
            public bool RemoveRating {
                get => _removeRating;
                set {
                    if (Equals(value, _removeRating)) return;
                    _removeRating = value;
                    ValuesStorage.Set("_ba.setRating.remove", value);
                    OnPropertyChanged();
                    RaiseAvailabilityChanged();
                    DisplayApply = value ? "Un-rate" : "Rate";
                }
            }

            protected override void ApplyOverride(AcObjectNew obj) {
                if (RemoveRating) {
                    obj.Rating = null;
                } else {
                    obj.Rating = Rating;
                }
            }

            public override bool IsAvailable(AcObjectNew obj) {
                return RemoveRating ? obj.Rating != null : obj.Rating != Rating;
            }
        }

        public class BatchAction_AddToFavourites : BatchAction<AcObjectNew> {
            public static readonly BatchAction_AddToFavourites Instance = new BatchAction_AddToFavourites();

            public BatchAction_AddToFavourites() : base("Add to favourites", "Add several objects at once", "Rating", null) {
                DisplayApply = "Favourite";
            }

            public override bool IsAvailable(AcObjectNew obj) {
                return !obj.IsFavourite;
            }

            protected override void ApplyOverride(AcObjectNew obj) {
                obj.IsFavourite = true;
            }
        }

        public class BatchAction_RemoveFromFavourites : BatchAction<AcObjectNew> {
            public static readonly BatchAction_RemoveFromFavourites Instance = new BatchAction_RemoveFromFavourites();

            public BatchAction_RemoveFromFavourites() : base("Remove from favourites", "Remove several objects at once", "Rating", null) {
                DisplayApply = "Unfavourite";
                Priority = -1;
            }

            public override bool IsAvailable(AcObjectNew obj) {
                return obj.IsFavourite;
            }

            protected override void ApplyOverride(AcObjectNew obj) {
                obj.IsFavourite = false;
            }
        }
        #endregion

        #region Disabling and removal
        public class BatchAction_Delete : BatchAction<AcCommonObject> {
            public static readonly BatchAction_Delete Instance = new BatchAction_Delete();

            public BatchAction_Delete() : base("Remove", "Remove to the Recycle Bin", "Files", null) {
                Priority = -10;
                DisplayApply = "Delete";
            }

            public override bool IsAvailable(AcCommonObject obj) {
                return true;
            }

            public override Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                var objs = OfType(list).ToList();
                if (objs.Count == 0) return Task.Delay(0);

                var manager = objs.First().FileAcManager;
                return manager.DeleteAsync(objs.Select(x => x.Id));
            }
        }

        public class BatchAction_Enable : BatchAction<AcCommonObject> {
            public static readonly BatchAction_Enable Instance = new BatchAction_Enable();

            public BatchAction_Enable() : base("Enable", "Enable disabled objects", "Files", null) {
                Priority = -5;
                DisplayApply = "Enable";
            }

            public override bool IsAvailable(AcCommonObject obj) {
                return !obj.Enabled;
            }

            public override Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                var objs = OfType(list).ToList();
                if (objs.Count == 0) return Task.Delay(0);

                var manager = objs.First().FileAcManager;
                return manager.ToggleAsync(objs.Select(x => x.Id), true);
            }
        }

        public class BatchAction_Disable : BatchAction<AcCommonObject> {
            public static readonly BatchAction_Disable Instance = new BatchAction_Disable();

            public BatchAction_Disable() : base("Disable", "Disable enabled objects", "Files", null) {
                Priority = -5;
                DisplayApply = "Disable";
            }

            public override bool IsAvailable(AcCommonObject obj) {
                return obj.Enabled;
            }

            public override Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                var objs = OfType(list).ToList();
                if (objs.Count == 0) return Task.Delay(0);

                var manager = objs.First().FileAcManager;
                return manager.ToggleAsync(objs.Select(x => x.Id), false);
            }
        }
        #endregion

        #region Miscellaneous
        public abstract class BatchAction_Pack<T> : BatchAction<T> where T : AcCommonObject {
            protected BatchAction_Pack([CanBeNull] string paramsTemplateKey) : base("Pack", "Pack only important files", null, paramsTemplateKey) {
                InternalWaitingDialog = true;
                DisplayApply = "Pack";
                Priority = 1;
            }

            protected BatchAction_Pack() : base("Pack", "Pack only important files", null, "Batch.Pack") {
                InternalWaitingDialog = true;
                DisplayApply = "Pack";
                Priority = 1;
            }

            private bool _packSeparately = ValuesStorage.Get("_ba.pack.separately", false);

            public bool PackSeparately {
                get => _packSeparately;
                set {
                    if (Equals(value, _packSeparately)) return;
                    _packSeparately = value;
                    ValuesStorage.Set("_ba.pack.separately", value);
                    OnPropertyChanged();
                }
            }

            public override bool IsAvailable(T obj) {
                return true;
            }

            [CanBeNull]
            protected abstract AcCommonObject.AcCommonObjectPackerParams GetParams();

            public override async Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                try {
                    var objs = OfType(list).ToList();
                    if (objs.Count == 0) return;

                    if (PackSeparately && objs.Count > 1) {
                        var filename = GetPackedFilename(new[]{ objs[0] }, ".zip", true);
                        if (filename == null) return;

                        var toView = new List<string>();
                        using (var waiting = WaitingDialog.Create("Packing…")) {
                            await Task.Run(() => {
                                for (var i = 0; i < objs.Count; i++) {
                                    var obj = objs[i];
                                    var thisFilename = objs[0] == obj ? filename :
                                            filename.Replace(objs[0].Id, obj.Id);
                                    using (var output = File.Create(FileUtils.EnsureUnique(thisFilename))) {
                                        var index = i;
                                        AcCommonObject.Pack(new[] { obj }, output, GetParams(),
                                                new Progress<string>(x => waiting.Report(new AsyncProgressEntry($"Packing: {x}…", index, objs.Count))),
                                                waiting.CancellationToken);
                                    }

                                    toView.Add(thisFilename);
                                }

                                if (waiting.CancellationToken.IsCancellationRequested) return;
                                ShowSelectedInExplorer.FilesOrFolders(toView);
                            });
                        }
                    } else {
                        var filename = GetPackedFilename(objs, ".zip");
                        if (filename == null) return;

                        using (var waiting = WaitingDialog.Create("Packing…")) {
                            await Task.Run(() => {
                                using (var output = File.Create(filename)) {
                                    AcCommonObject.Pack(objs, output, GetParams(),
                                            new Progress<string>(x => waiting.Report(AsyncProgressEntry.FromStringIndetermitate($"Packing: {x}…"))),
                                            waiting.CancellationToken);
                                }

                                if (waiting.CancellationToken.IsCancellationRequested) return;
                                WindowsHelper.ViewFile(filename);
                            });
                        }
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t pack", e);
                }
            }
        }
        #endregion

        #region Utils
        [CanBeNull]
        private static string GetPackedFilename([NotNull] IEnumerable<AcObjectNew> o, string extension, bool forceSeveralName = false, bool useNames = false) {
            var objs = o.ToIReadOnlyListIfItIsNot();
            if (objs.Count == 0) return null;

            var last = $"-{DateTime.Now:yyyyMMdd-HHmmss}{extension}";
            var name = !forceSeveralName && objs.Count == 1 ? $"{GetObjectName(objs[0])}-{(objs[0] as IAcObjectVersionInformation)?.Version ?? "0"}{last}" :
                    $"{objs.Select(GetObjectName).OrderBy(x => x).JoinToString('-')}{last}";
            if (name.Length > 160) {
                name = name.Substring(0, 160 - last.Length) + last;
            }

            return FileRelatedDialogs.Save(new SaveDialogParams {
                Title = objs.Count == 1 ? $"Pack {objs[0].DisplayName}" : $"Pack {objs.Count} {PluralizingConverter.Pluralize(objs.Count, "Object")}",
                Filters = {
                    extension == ".zip" ? DialogFilterPiece.ZipFiles : extension == ".exe" ? DialogFilterPiece.Applications :
                            extension == ".tar.gz" ? DialogFilterPiece.TarGZipFiles : DialogFilterPiece.Archives
                },
                DetaultExtension = extension,
                DirectorySaveKey = "packDir",
                DefaultFileName = name
            });

            string GetObjectName(AcObjectNew x) {
                return useNames ? FileUtils.EnsureFileNameIsValid(x.DisplayName, true) : x.Id;
            }
        }

        [CanBeNull]
        public static string GetPackedFilename([NotNull] AcObjectNew o, string extension, bool useNames = false) {
            return GetPackedFilename(new[] { o }, extension, useNames: useNames);
        }
        #endregion
    }
}