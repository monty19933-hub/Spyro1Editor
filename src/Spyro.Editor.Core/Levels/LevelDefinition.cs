namespace Spyro.Editor.Core.Levels;

public sealed record LevelDefinition
{
    public string Key { get; init; } = "";
    public string ScriptKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int LevelId { get; init; } = -1;
    public int SourceWadEntry { get; init; } = -1;
    public string SourceTableWadOffset { get; init; } = "";
    public string SourceTableRelativeOffset { get; init; } = "";
    public int SourceRecordCount { get; init; }
    public string Confidence { get; init; } = "";
    public string RuntimeMobyPointer { get; init; } = "";

    public bool ApplyStoneHillMetadata => string.Equals(Key, "stonehill", StringComparison.OrdinalIgnoreCase);
    public bool HasSourceTable => SourceWadEntry >= 0 && SourceRecordCount > 0 && !string.IsNullOrWhiteSpace(SourceTableWadOffset);

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(DisplayName) ? Key : DisplayName;
    }
}
