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

// Based on the Public Domain Rect.h source by Jukka Jylänki
// https://github.com/juj/RectangleBinPack/
//
// Ported to C# from
// - Rect.h    Revision: 0cc6a52520e5148f43326302010cf27d5189746c

#if DEBUG
using MB.Base.MathEx.Pixel;
using System.Collections.Generic;

namespace TexturePacker.RectangleBinPack.BinPack
{
  internal sealed class DisjointRectCollection
  {
    public List<PxRectangleM> rects = new List<PxRectangleM>();

    public bool Add(PxRectangleM r)
    {
      // Degenerate rectangles are ignored.
      if (r.Width == 0 || r.Height == 0)
        return true;

      if (!Disjoint(r))
        return false;
      rects.Add(r);
      return true;
    }

    public void Clear()
    {
      rects.Clear();
    }

    public bool Disjoint(PxRectangleM r)
    {
      // Degenerate rectangles are ignored.
      if (r.Width == 0 || r.Height == 0)
        return true;

      for (int i = 0; i < rects.Count; ++i)
        if (!Disjoint(rects[i], r))
          return false;
      return true;
    }

    private static bool Disjoint(PxRectangleM a, PxRectangleM b)
    {
      if (a.X + a.Width <= b.X ||
        b.X + b.Width <= a.X ||
        a.Y + a.Height <= b.Y ||
        b.Y + b.Height <= a.Y)
        return true;
      return false;
    }
  }
}
#endif
