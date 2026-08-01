using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private IReadOnlyList<AddMobyTemplate> GetCreateBinSafeObjectGalleryTemplates(
        IEnumerable<AddMobyTemplate> templates)
    {
        string levelKey = _currentLevel?.Key ?? "";
        return templates
            .Where(template => IsCreateBinSafeObjectGalleryTemplate(template, levelKey))
            .GroupBy(
                ObjectGalleryDeduplicationKey,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(template => ObjectGalleryRepresentativeRank(group.Key, template))
                .ThenByDescending(template => template.SafeExportSlots)
                .ThenBy(template => BuildObjectGalleryDisplayName(template), StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(template => ObjectGalleryFamilyRank(template))
            .ThenBy(template => BuildObjectGalleryDisplayName(template), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool IsCreateBinSafeObjectGalleryTemplate(AddMobyTemplate template, string levelKey)
    {
        if (template.FromCrossLevelTemplate)
        {
            return template.CurrentLevelReady &&
                template.CurrentLevelPlaceable &&
                HasSpecialChestObjectGalleryCapacity(template, levelKey);
        }

        if (!template.FromLevelTemplate)
        {
            return IsReleaseSafeTrueAddIdentity(
                template.Type,
                template.SourceByte36,
                template.SourceByte37,
                template.SourceByte4F,
                template.Flag4A,
                template.Flag4B);
        }

        int safeSlots = CountSafeNativeCloneExtraExportSlots(template, _currentMobys);
        if (safeSlots > 0 || IsReleaseSafeTrueAddTemplate(template))
            return true;

        string sourceLevelKey = string.IsNullOrWhiteSpace(template.SourceLevelKey)
            ? levelKey
            : template.SourceLevelKey;
        if (IsUnlimitedPromotedNativeCloneAppendIdentity(
                sourceLevelKey,
                template.Type,
                template.SourceByte36,
                template.SourceByte37,
                template.SourceByte4F,
                template.Flag4A,
                template.Flag4B))
        {
            return true;
        }

        if (!IsReleaseLimitedNativeCloneAppendIdentity(
                sourceLevelKey,
                template.Type,
                template.SourceByte36,
                template.SourceByte37,
                template.SourceByte4F,
                template.Flag4A,
                template.Flag4B))
        {
            return false;
        }

        int existingAdds = _currentMobys.Count(moby =>
            moby.IsAdded &&
            !moby.IsRemoved &&
            (string.IsNullOrWhiteSpace(moby.SourceCloneLevelKey) ||
             string.Equals(
                 LevelCatalog.NormalizeKey(moby.SourceCloneLevelKey),
                 LevelCatalog.NormalizeKey(sourceLevelKey),
                 StringComparison.OrdinalIgnoreCase)) &&
            MatchesNativeCloneAutoSlotFamily(
                moby,
                template.Type,
                template.State,
                template.SourceByte36,
                template.SourceByte37,
                template.SourceByte4F,
                template.Flag4A,
                template.Flag4B));
        int originalReusableSlots = _currentMobys.Count(candidate =>
            !candidate.IsAdded &&
            !candidate.IsRemoved &&
            !candidate.HasAnyEdit &&
            candidate.TrueIndex >= 0 &&
            _currentLevel != null &&
            candidate.TrueIndex < _currentLevel.SourceRecordCount &&
            candidate.TrueIndex != template.SourceTrueIndex &&
            (!_releaseMode || !IsReleaseProtectedControlMoby(candidate)) &&
            MatchesNativeCloneAutoSlotFamily(
                candidate,
                template.Type,
                template.State,
                template.SourceByte36,
                template.SourceByte37,
                template.SourceByte4F,
                template.Flag4A,
                template.Flag4B));
        return existingAdds < originalReusableSlots + 1;
    }

    private bool HasSpecialChestObjectGalleryCapacity(AddMobyTemplate template, string levelKey)
    {
        if (!SpecialChestEditorTemplateGate.TryMapFamily(template.Family, out SpecialChestFamily family))
            return true;

        SpecialChestBundleProfile? profile = SpecialChestBundleProfileRegistry.Find(
            family,
            levelKey,
            SpecialChestBundleProfileRegistry.CleanUsaImageSha256);
        if (profile is not { NormalCreateBinReady: true } ||
            profile.StructuralBudget.MaxBundleInstances is not int maximum)
        {
            return false;
        }

        string[] existingBundleIds = _currentMobys
            .Where(moby =>
                !moby.IsRemoved &&
                string.Equals(moby.SpecialChestProfileId, profile.Id, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(moby.SpecialChestBundleId))
            .Select(moby => moby.SpecialChestBundleId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (existingBundleIds.Length < maximum)
            return true;

        if (family != SpecialChestFamily.LockedChest)
            return false;

        int actorId = ((template.SourceByte37 & 0xFF) << 8) | (template.SourceByte36 & 0xFF);
        bool addsKey = actorId == 0x00AD;
        bool addsLockedChest = actorId == 0x00AE;
        return existingBundleIds.Any(bundleId =>
        {
            Moby[] members = _currentMobys
                .Where(moby =>
                    !moby.IsRemoved &&
                    string.Equals(moby.SpecialChestBundleId, bundleId, StringComparison.Ordinal))
                .ToArray();
            bool hasKey = members.Any(member => NativeActorId(member) == 0x00AD);
            bool hasLockedChest = members.Any(member => NativeActorId(member) == 0x00AE);
            return addsKey && !hasKey && hasLockedChest ||
                addsLockedChest && hasKey && !hasLockedChest;
        });
    }

    private static int ObjectGalleryFamilyRank(AddMobyTemplate template)
    {
        if (IsObjectGalleryGemTemplate(template))
            return 0;
        return template.Family switch
        {
            "key" => 1,
            "lockedChest" => 2,
            "springChest" => 3,
            "fireworkChest" => 4,
            "multiGemChest" => 5,
            _ => 9
        };
    }

    private static bool IsObjectGalleryGemTemplate(AddMobyTemplate template) =>
        template.UsesGem ||
        template.DefaultGem is GemValue &&
        StripNewPrefix(template.DefaultLabel).Contains("gem", StringComparison.OrdinalIgnoreCase);

    private static string ObjectGalleryDeduplicationKey(AddMobyTemplate template)
    {
        if (IsObjectGalleryGemTemplate(template))
            return "gem/treasure";

        string displayName = BuildObjectGalleryDisplayName(template);
        return TryGetCanonicalChestGalleryName(template, out string canonicalChestName)
            ? $"chest/{canonicalChestName}"
            : displayName;
    }

    private static int ObjectGalleryRepresentativeRank(string groupKey, AddMobyTemplate template)
    {
        if (string.Equals(groupKey, "gem/treasure", StringComparison.OrdinalIgnoreCase))
        {
            if (!template.FromLevelTemplate &&
                !template.FromCrossLevelTemplate &&
                string.Equals(template.Name, "Gem / treasure", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            GemValue gem = template.DefaultGem ??
                (template.UsesGem &&
                 GemValue.TryFromIdByte(template.SourceByte36, out GemValue sourceGem)
                    ? sourceGem
                    : GemValue.Unknown);
            return gem == GemValue.Red ? 1 : 2;
        }

        if (groupKey.StartsWith("chest/", StringComparison.OrdinalIgnoreCase))
        {
            string label = StripNewPrefix(template.DefaultLabel);
            if (label.Contains("(Red reward)", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (!label.Contains(" reward)", StringComparison.OrdinalIgnoreCase))
                return 1;
            return 2;
        }

        return 0;
    }

    private static string BuildObjectGalleryDisplayName(AddMobyTemplate template)
    {
        if (IsObjectGalleryGemTemplate(template))
            return "Gem / treasure";
        if (!string.IsNullOrWhiteSpace(template.CompanionTemplateId))
            return "Key + Locked Chest";
        if (template.CopiesSelected)
            return StripNewPrefix(template.DefaultLabel).Replace("Copy of ", "Copy ", StringComparison.OrdinalIgnoreCase);

        string label = StripNewPrefix(template.DefaultLabel).Trim();
        string[] technicalQualifiers =
        [
            " (live-tested)",
            " (live tested)",
            " (native)",
            " (ready here)",
            " (runtime-proven)",
            " (runtime proven)"
        ];
        foreach (string qualifier in technicalQualifiers)
            label = label.Replace(qualifier, "", StringComparison.OrdinalIgnoreCase);
        if (TryGetCanonicalChestGalleryName(template, out string canonicalChestName))
            return canonicalChestName;
        return string.IsNullOrWhiteSpace(label) ? "Object" : label;
    }

    private static bool TryGetCanonicalChestGalleryName(
        AddMobyTemplate template,
        out string canonicalName)
    {
        int actorId = ((template.SourceByte37 & 0xFF) << 8) |
            (template.SourceByte36 & 0xFF);
        if (actorId == 0x00C2)
        {
            canonicalName = "Flame/charge chest";
            return true;
        }
        if (actorId == 0x00C3)
        {
            canonicalName = "Charge chest";
            return true;
        }

        string label = StripNewPrefix(template.DefaultLabel);
        string normalized = label.Trim();
        if (normalized.StartsWith("Flame/charge chest", StringComparison.OrdinalIgnoreCase))
        {
            canonicalName = "Flame/charge chest";
            return true;
        }
        if (normalized.StartsWith("Charge chest", StringComparison.OrdinalIgnoreCase))
        {
            canonicalName = "Charge chest";
            return true;
        }

        canonicalName = "";
        return false;
    }

    private Moby BuildObjectGalleryPreviewMoby(AddMobyTemplate template)
    {
        if (template.CopiesSelected && _selectedMoby is { IsRemoved: false } selected)
            return selected;

        if (template.FromLevelTemplate)
        {
            Moby? nativeSource = _currentMobys.FirstOrDefault(moby =>
                !moby.IsRemoved &&
                !moby.IsAdded &&
                moby.TrueIndex == template.SourceTrueIndex);
            if (nativeSource != null)
                return nativeSource;
        }

        GemValue gem = template.DefaultGem ?? (template.UsesGem ? GemValue.Red : GemValue.Unknown);
        return new Moby
        {
            Type = template.Type,
            State = template.State,
            SourceByte36 = template.UsesGem ? gem.IdByte : template.SourceByte36,
            SourceByte37 = template.SourceByte37,
            SourceByte4F = template.UsesGem ? gem.ValueByte : template.SourceByte4F,
            Flag4A = template.Flag4A,
            Flag4B = template.UsesGem && template.Flag4B != 0xFF
                ? gem.IdByte
                : template.Flag4B,
            Color = template.UsesGem
                ? gem.Color
                : template.DefaultGem is GemValue defaultGem
                    ? defaultGem.Color
                    : template.DefaultColor ?? Moby.ColorForType(template.Type),
            Label = template.UsesGem ? "New Red gem" : template.DefaultLabel,
            CandidateKind = template.CandidateKind,
            BehaviorNote = template.TemplateNote,
            CrossLevelFamily = template.Family
        };
    }

    private async Task<AddMobyTemplate?> ShowObjectGalleryAsync(
        Window owner,
        IReadOnlyList<AddMobyTemplate> templates,
        AddMobyTemplate selectedTemplate)
    {
        ObjectGalleryItem[] items = templates
            .Select(template => new ObjectGalleryItem(
                template,
                BuildObjectGalleryDisplayName(template),
                IsCreateBinSafeObjectGalleryTemplate(template, _currentLevel?.Key ?? "")))
            .Where(item => item.IsCreateBinSafe)
            .ToArray();

        Window dialog = new()
        {
            Title = "Choose Object",
            Width = 920,
            Height = 680,
            MinWidth = 720,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        ListBox gallery = new()
        {
            Name = "ObjectGalleryList",
            ItemsSource = items,
            SelectedItem = items.FirstOrDefault(item => ReferenceEquals(item.Template, selectedTemplate)),
            MinHeight = 420,
            ItemsPanel = new FuncTemplate<Panel?>(() => new UniformGrid
            {
                Columns = 5,
                RowSpacing = 10,
                ColumnSpacing = 10
            })
        };

        Control BuildTile(ObjectGalleryItem item)
        {
            ObjectGalleryIconPreview preview = new(
                BuildObjectGalleryPreviewMoby(item.Template),
                item.DisplayName)
            {
                Name = "ObjectGalleryPreview",
                Width = 126,
                Height = 126,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            StackPanel content = new()
            {
                Spacing = 7,
                Children =
                {
                    preview,
                    new TextBlock
                    {
                        Text = item.DisplayName,
                        FontSize = 12,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(ModernInk),
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        MaxLines = 2
                    }
                }
            };
            Button tile = new()
            {
                Name = "ObjectGalleryTileButton",
                Tag = item,
                Padding = new Thickness(8),
                MinHeight = 176,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(ModernLine),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Content = content
            };
            tile.Click += (_, _) => dialog.Close(item.Template);
            return tile;
        }

        gallery.ItemTemplate = new FuncDataTemplate<ObjectGalleryItem>((item, _) =>
            item == null ? new TextBlock() : BuildTile(item));

        Button cancel = NewButton("Cancel");
        StyleModernSecondaryButton(cancel);
        cancel.Click += (_, _) => dialog.Close(null);

        StackPanel body = new()
        {
            Spacing = 10,
            Margin = new Thickness(18, 16, 18, 12),
            Children =
            {
                BuildModernDialogHeader(
                    "Choose an object",
                    $"{items.Length} object type(s) are currently available for this level and normal Create BIN. Click a picture to select it."),
                new Border
                {
                    BorderBrush = new SolidColorBrush(ModernLine),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    ClipToBounds = true,
                    Child = gallery
                }
            }
        };
        dialog.Content = BuildModernDialogFrame(body, cancel);
        return await dialog.ShowDialog<AddMobyTemplate?>(owner);
    }

    private sealed record ObjectGalleryItem(
        AddMobyTemplate Template,
        string DisplayName,
        bool IsCreateBinSafe);

    private sealed class ObjectGalleryIconPreview : Control
    {
        private readonly Moby _moby;
        private readonly string _displayName;

        internal ObjectGalleryIconPreview(Moby moby, string displayName)
        {
            _moby = moby;
            _displayName = displayName;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            Rect bounds = new(0, 0, Bounds.Width, Bounds.Height);
            context.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(237, 243, 247)),
                null,
                new RoundedRect(bounds, 5));

            if (ObjectGalleryIconCatalog.TryDraw(context, bounds, _displayName))
                return;

            double markerSize = Math.Clamp(
                Math.Min(bounds.Width, bounds.Height) * 0.33,
                24,
                42);
            EditorViewport.DrawMobyMarkerPreview(
                context,
                bounds.Center,
                _moby,
                markerSize);
        }
    }
}
