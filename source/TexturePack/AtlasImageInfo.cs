//****************************************************************************************************************************************************
// The Unlicense
//
// This is free and unencumbered software released into the public domain.
//
// Anyone is free to copy, modify, publish, use, compile, sell, or
// distribute this software, either in source code form or as a compiled
// binary, for any purpose, commercial or non-commercial, and by any
// means.
//
// In jurisdictions that recognize copyright laws, the author or authors
// of this software dedicate any and all copyright interest in the
// software to the public domain.We make this dedication for the benefit
// of the public at large and to the detriment of our heirs and
// successors.We intend this dedication to be an overt act of
// relinquishment in perpetuity of all present and future rights to this
// software under copyright law.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
// OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
// For more information, please refer to<https://unlicense.org>
//****************************************************************************************************************************************************

using MB.Base.MathEx.Pixel;
using System;
using System.Diagnostics.CodeAnalysis;

namespace MB.RectangleBinPack.TexturePack
{
  /// <summary>
  /// Source atlas image information
  /// </summary>
  public struct AtlasImageInfo : IEquatable<AtlasImageInfo>
  {
    public PxRectangle SrcRectPx;
    public object SourceTag;

    public AtlasImageInfo(PxRectangle srcRectPx, object sourceTag)
    {
      SrcRectPx = srcRectPx;
      SourceTag = sourceTag;
    }

    public static bool operator ==(AtlasImageInfo lhs, AtlasImageInfo rhs) => lhs.SrcRectPx == rhs.SrcRectPx && lhs.SourceTag == rhs.SourceTag;

    public static bool operator !=(AtlasImageInfo lhs, AtlasImageInfo rhs) => !(lhs == rhs);


    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is AtlasImageInfo info && (this == info);


    public readonly override int GetHashCode() => GetHashCode(this);


    public readonly bool Equals(AtlasImageInfo other) => this == other;

    public override string ToString() => $"SrcRectPx: {SrcRectPx} SourceTag: {SourceTag}";

    public static int GetHashCode(AtlasImageInfo info)
    {
      return info.SrcRectPx.GetHashCode() ^ (info.SourceTag != null ? info.SourceTag.GetHashCode() : 0);
    }
  }
}
