using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
namespace Spyro.Editor.Core.Exporting;

/// <summary>The complete original native stream parser stops at the -1 word.
/// This authority issues only a four-byte zero gap between that exact stream
/// and the following model; the public strict stream codec remains unchanged.</summary>
internal sealed class NativePlayerAnimationModelAlignment
{
 internal const string ParserSha256="0cca46f0e44c16b8adea088e3081637ec5b04a512dc2c8eac494f9d16732cefb";
 private NativePlayerAnimationModelAlignment(){}
 internal static NativePlayerAnimationModelAlignment Capture(ReadOnlySpan<byte> executable)
 {
  if(executable.Length!=0x66000||NativePlayerAnimationStreamCodec.Hash(executable.Slice(0x3A30,0x1B0))!=ParserSha256)
   throw new InvalidDataException("Player model alignment requires the complete original native stream parser.");
  return new();
 }
 internal (NativePlayerAnimationStream Stream,NativePlayerAnimationAlignmentPadding? Padding) DecodeModelOwner(ReadOnlySpan<byte> bytes,ReadOnlySpan<byte> geometry)
 {
  // A canonical model gap is always exactly four zero bytes after a complete
  // strictly decoded stream. Extra, nonzero or truncated suffixes never qualify.
  if(bytes.Length>=8&&bytes.Length%4==0&&BinaryPrimitives.ReadInt32LittleEndian(bytes[^8..])==-1&&!bytes[^4..].ContainsAnyExcept((byte)0))
  {
   var stream=NativePlayerAnimationStreamCodec.Decode(bytes[..^4],geometry);
   var owner=new NativePlayerAnimationAlignmentPadding(this,bytes,stream);owner.Require(bytes);return(stream,owner);
  }
  return(NativePlayerAnimationStreamCodec.Decode(bytes,geometry),null);
 }
}

/// <summary>Immutable issued postterminator storage; animation bytes are exact.</summary>
internal sealed class NativePlayerAnimationAlignmentPadding
{
 private readonly byte[] _owner;
 internal NativePlayerAnimationAlignmentPadding(NativePlayerAnimationModelAlignment authority,ReadOnlySpan<byte> bytes,NativePlayerAnimationStream stream)
 {
  ArgumentNullException.ThrowIfNull(authority);
  if(bytes.Length!=stream.Encode().Length+4||!bytes[..^4].SequenceEqual(stream.Encode())||bytes[^4..].ContainsAnyExcept((byte)0)||BinaryPrimitives.ReadInt32LittleEndian(bytes[^8..])!=-1)
   throw new InvalidDataException("Player alignment storage must follow the exact complete -1-terminated animation stream.");
  _owner=bytes.ToArray();TerminatorOffset=stream.TerminatorOffset;
 }
 internal int ByteLength=>4;
 internal int TerminatorOffset{get;}
 internal string NativeParserSha256=>NativePlayerAnimationModelAlignment.ParserSha256;
 internal void Require(ReadOnlySpan<byte> bytes)
 {if(!bytes.SequenceEqual(_owner))throw new InvalidDataException("Issued player animation stream or four-byte zero alignment gap changed.");}
}
