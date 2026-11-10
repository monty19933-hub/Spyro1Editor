using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>Objects tab, under the selected object's buttons: what an enemy (or a chest, or anything else that pays out
/// when beaten or opened) gives. The game keeps it in one byte of the object's row, the gem its reward routine makes
/// (<see cref="UniversalRewardChoice"/>); any of the five gems can stand there. Edit… offers the same choice.</summary>
public sealed partial class MainWindow
{
    private Border? _rewardCard;
    private ComboBox? _rewardBox;
    private TextBlock? _rewardNote;
    private bool _rewardRefreshing;

    private Control BuildRewardCard()
    {
        StackPanel content = new() { Spacing = 6 };
        _rewardBox = new ComboBox
        {
            Name = "ObjectRewardBox", HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = GemValue.Known.Select(g => g.DisplayName).ToArray()
        };
        ToolTip.SetTip(_rewardBox, "The gem Spyro gets for beating or opening this object. The level's treasure total follows.");
        _rewardBox.SelectionChanged += (_, _) =>
        {
            if (_rewardRefreshing || _rewardBox.SelectedIndex < 0 || _selectedMoby is not { } moby) return;
            ApplyReward(moby, GemValue.Known[_rewardBox.SelectedIndex]);
        };
        _rewardNote = new() { Name = "ObjectRewardNote", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = new SolidColorBrush(ModernMutedInk) };
        content.Children.Add(_rewardBox);
        content.Children.Add(_rewardNote);
        _rewardCard = TerrainCard("What it gives", content);
        _rewardCard.Name = "ObjectRewardCard";
        _rewardCard.IsVisible = false;
        return _rewardCard;
    }

    /// <summary>True when the object pays a gem the editor can change: it gives one of the five gems, and is not itself
    /// a gem, a chest's hidden contents (those are edited with their chest) or a removed object.</summary>
    private static bool CanChooseReward(Moby moby) =>
        !moby.IsRemoved && IsRewardGemCarrier(moby) && !moby.IsChestContent && UniversalRewardChoice.IsGem(moby.Flag4B);

    private void RefreshRewardCard()
    {
        if (_rewardCard == null || _rewardBox == null || _rewardNote == null) return;
        bool show = _selectedMoby is { } moby && CanChooseReward(moby) && !_createBinBusy && !_workspaceTransitionBusy && !IsId65BlankLabObjectInspectionOnly();
        _rewardCard.IsVisible = show;
        if (!show) return;
        _rewardRefreshing = true;
        try { _rewardBox.SelectedIndex = GemValue.Known.ToList().FindIndex(g => g.IdByte == _selectedMoby!.Flag4B); }
        finally { _rewardRefreshing = false; }
        int original = _selectedMoby!.OriginalFlag4B;
        _rewardNote.Text = original != _selectedMoby.Flag4B && GemValue.TryFromIdByte(original, out var came)
            ? $"It came with a {came.Name.ToLowerInvariant()} ({came.Value})."
            : "Beating or opening it gives this gem.";
    }

    /// <summary>Gives <paramref name="moby"/> another gem to pay out; false when nothing changed.</summary>
    internal bool ApplyReward(Moby moby, GemValue gem)
    {
        if (!CanChooseReward(moby) || !UniversalRewardChoice.IsGem(gem.IdByte) || moby.Flag4B == gem.IdByte) return false;
        int before = moby.TreasureValue;
        bool generatedName = Moby.IsGeneratedGemLabel(moby.Label, rewardCarrier: true);
        moby.Flag4B = gem.IdByte;
        // An added object's reward is written into its row by Create BIN only when it was chosen.
        if (moby.IsAdded && moby.CrossLevelRequiredExporterFeature == UniversalMobyExport.RequiredExporterFeature && UniversalSpecialIdOf(moby) == null)
            moby.UniversalRewardByte = gem.IdByte;
        if (generatedName) moby.Label = moby.SuggestedRewardGemLabel(gem);
        moby.HasLoadedNativeEdit = true;
        moby.LoadedNativeEditSummary = $"{gem.DisplayName} reward byte";
        InvalidateBuildSafetySummary();
        _viewport.InvalidateVisual();
        RefreshMobyList(moby);
        RefreshCurrentLevelDetails();
        ShowSelection(ViewportSelectionChangedEventArgs.ForMoby(moby));
        int change = gem.Value - before;
        _statusText.Text = $"{moby.DisplayLabel} now gives a {gem.Name.ToLowerInvariant()} ({gem.Value}): the level's treasure is {(change >= 0 ? "up" : "down")} {Math.Abs(change)}. Save Level or Create BIN keeps it.";
        return true;
    }
}
