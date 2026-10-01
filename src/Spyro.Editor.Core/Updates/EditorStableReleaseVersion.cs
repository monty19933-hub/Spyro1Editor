using System.Globalization;
namespace Spyro.Editor.Core.Updates;

/// <summary>Stable releases form a separate channel; they are never compared to beta numbers.</summary>
public sealed record EditorStableReleaseVersion : IComparable<EditorStableReleaseVersion>
{
    public EditorStableReleaseVersion(int major,int minor,int patch=0)
    {if(major<=0||minor<0||patch<0)throw new ArgumentOutOfRangeException(nameof(major));Major=major;Minor=minor;Patch=patch;}
    public int Major{get;} public int Minor{get;} public int Patch{get;}
    public string CanonicalVersion => Patch==0?$"{Major}.{Minor}":$"{Major}.{Minor}.{Patch}";
    public string DisplayName => "Spyro Editor "+CanonicalVersion;
    public string PackageStem => "SpyroEditor-V"+CanonicalVersion;
    public string ReleaseTag => "v"+CanonicalVersion;
    public int CompareTo(EditorStableReleaseVersion? other){if(other==null)return 1;int n=Major.CompareTo(other.Major);if(n==0)n=Minor.CompareTo(other.Minor);return n==0?Patch.CompareTo(other.Patch):n;}
    public static bool TryParse(string? value,out EditorStableReleaseVersion version)
    {version=null!;var parts=(value??"").Split('.');if(parts.Length is <2 or >3)return false;int[] numbers=new int[parts.Length];for(int i=0;i<parts.Length;i++)if(!int.TryParse(parts[i],NumberStyles.None,CultureInfo.InvariantCulture,out numbers[i])||numbers[i]<0||parts[i]!=numbers[i].ToString(CultureInfo.InvariantCulture))return false;if(numbers[0]==0||parts.Length==3&&numbers[2]==0)return false;version=new(numbers[0],numbers[1],parts.Length==3?numbers[2]:0);return true;}
    public static bool TryParseDisplayName(string? text,out EditorStableReleaseVersion version){version=null!;return text?.StartsWith("Spyro Editor ",StringComparison.Ordinal)==true&&TryParse(text[13..],out version);}
    public static bool TryParseReleaseTag(string? text,out EditorStableReleaseVersion version){version=null!;return text?.StartsWith("v",StringComparison.Ordinal)==true&&TryParse(text[1..],out version);}
}
