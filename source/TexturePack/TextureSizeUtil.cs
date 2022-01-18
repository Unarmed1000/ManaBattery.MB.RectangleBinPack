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
using System.Collections.Generic;
using System.Diagnostics;

namespace TexturePacker.RectangleBinPack.TexturePack
{
  public static class TextureSizeUtil
  {
    /// <summary>
    /// Calculate the minimum texture size with enough area and that is >= minimum size
    /// </summary>
    /// <param name="minArea"></param>
    /// <param name="minSizePx"></param>
    /// <returns></returns>
    public static PxSize2D CalcMinimumTextureSize(int minArea, PxSize2D minSizePx, PxThickness borderReservedPx,
                                                  TextureSizeRestriction textureSizeRestriction)
    {
      PxSize2D res = CalcMinimumTextureSize(minArea, borderReservedPx, textureSizeRestriction);
      if (textureSizeRestriction == TextureSizeRestriction.Pow2 || textureSizeRestriction == TextureSizeRestriction.Pow2Square)
      {
        if (res.Width < minSizePx.Width)
        {
          res.Width = ToPowerOfTwo(minSizePx.Width);
          res.Height = ToPowerOfTwo(Math.Max(DivAndRoundUp(minArea, res.Width), minSizePx.Height));
          if (textureSizeRestriction == TextureSizeRestriction.Pow2Square)
          {
            Debug.Assert(res.Height <= res.Width);
            res.Height = res.Width;
          }
        }
        else if (res.Height < minSizePx.Height)
        {
          res.Height = ToPowerOfTwo(minSizePx.Height);
          res.Width = ToPowerOfTwo(Math.Max(DivAndRoundUp(minArea, res.Height), minSizePx.Width));
          if (textureSizeRestriction == TextureSizeRestriction.Pow2Square)
          {
            Debug.Assert(res.Width <= res.Height);
            res.Width = res.Height;
          }
        }
      }
      else if (textureSizeRestriction == TextureSizeRestriction.Any)
      {
        if (res.Width < minSizePx.Width)
        {
          res.Width = minSizePx.Width;
          res.Height = Math.Max(DivAndRoundUp(minArea, res.Width), minSizePx.Height);
        }
        else if (res.Height < minSizePx.Height)
        {
          res.Height = minSizePx.Height;
          res.Width = Math.Max(DivAndRoundUp(minArea, res.Height), minSizePx.Width);
        }
      }
      else
        throw new NotSupportedException("Unknown restriction");

      Debug.Assert((res.Width * res.Height) >= minArea);
      return res;
    }

    private static int DivAndRoundUp(int lhs, int rhs)
    {
      return lhs / rhs + (lhs % rhs != 0 ? 1 : 0);
    }

    /// <summary>
    /// Calculate the minimum texture size with enough area
    /// </summary>
    /// <param name="minArea"></param>
    /// <returns></returns>
    public static PxSize2D CalcMinimumTextureSize(int minArea, PxThickness borderReservedPx, TextureSizeRestriction textureSizeRestriction)
    {
      // Locate the minimum square that fits the area
      int sideLength = System.Convert.ToInt32(Math.Sqrt(minArea));

      int xLen, yLen;
      if (textureSizeRestriction == TextureSizeRestriction.Pow2 || textureSizeRestriction == TextureSizeRestriction.Pow2Square)
      {
        int pow2Length = ToPowerOfTwo(sideLength);
        int area = pow2Length * pow2Length;
        area -= ((pow2Length * borderReservedPx.SumX) + (pow2Length * borderReservedPx.SumY));
        while (area < minArea)
        {
          pow2Length <<= 1;
          area = pow2Length * pow2Length;
          area -= ((pow2Length * borderReservedPx.SumX) + (pow2Length * borderReservedPx.SumY));
          Debug.Assert(area >= minArea);
        }

        xLen = pow2Length;
        if (textureSizeRestriction == TextureSizeRestriction.Pow2Square)
          yLen = pow2Length;
        else
          yLen = (area / 2) >= minArea ? pow2Length / 2 : pow2Length;
      }
      else if (textureSizeRestriction == TextureSizeRestriction.Any)
      {
        xLen = sideLength;
        yLen = sideLength;
        int area = xLen * yLen;
        area -= ((yLen * borderReservedPx.SumX) + (xLen * borderReservedPx.SumY));
        while (area < minArea)
        {
          ++xLen;
          area = xLen * yLen;
          area -= ((yLen * borderReservedPx.SumX) + (xLen * borderReservedPx.SumY));
        }
      }
      else
        throw new NotSupportedException("Unknown restriction");

      // the area should be big enough
      Debug.Assert((xLen * yLen) >= minArea);
      // the area minus the reserved border pixels should be big enough
      Debug.Assert(((xLen * yLen) - ((yLen * borderReservedPx.SumX) + (xLen * borderReservedPx.SumY))) >= minArea);
      return new PxSize2D(xLen, yLen);
    }

    /// <summary>
    /// Build a queue of potential texture sizes based on the input criterias
    /// </summary>
    /// <param name="minArea"></param>
    /// <param name="minSize"></param>
    /// <param name="maxSize"></param>
    public static Queue<PxSize2D> GetPotentialPow2TextureSizes(int minArea, PxSize2D minSize, PxSize2D maxSize, PxThickness borderReservedPx,
                                                               bool forceSquare)
    {
      var queue = new Queue<PxSize2D>();
      EnquePotentialPow2TextureSizes(queue, minArea, minSize, maxSize, borderReservedPx, forceSquare);
      return queue;
    }

    /// <summary>
    /// Build a queue of potential texture sizes based on the input criterias
    /// </summary>
    /// <param name="dst"></param>
    /// <param name="minArea"></param>
    /// <param name="minSize"></param>
    /// <param name="maxSize"></param>
    /// <param name="borderReservedPx"></param>
    /// <param name="forceSquare"></param>
    public static void EnquePotentialPow2TextureSizes(Queue<PxSize2D> dst, int minArea, PxSize2D minSize, PxSize2D maxSize,
                                                      PxThickness borderReservedPx, bool forceSquare)
    {
      if (dst == null)
      {
        throw new ArgumentNullException(nameof(dst));
      }
      dst.Clear();

      if (minArea <= 0)
        return;

      PxSize2D minTextureSize = CalcMinimumTextureSize(minArea, minSize, borderReservedPx,
                                                       forceSquare ? TextureSizeRestriction.Pow2Square : TextureSizeRestriction.Pow2);

      int textureSizeX = minTextureSize.Width;
      int textureSizeY = minTextureSize.Height;
      // Check if the minimum requirements are bigger than the texture constraints
      if (minTextureSize.Width > maxSize.Width || minTextureSize.Height > maxSize.Height)
        return;

      // Check if its a square texture size and if its not then grow it to be square
      if (minTextureSize.Width != minTextureSize.Height)
      {
        Debug.Assert(!forceSquare);

        // Still not found so grow it in one of the directions
        if (minTextureSize.Width < minTextureSize.Height)
        {
          // Grow X
          while (textureSizeX < minTextureSize.Height && textureSizeX < maxSize.Width)
          {
            dst.Enqueue(new PxSize2D(textureSizeX, textureSizeY));
            textureSizeX <<= 1;
          }
        }
        else
        {
          // Grow Y
          while (textureSizeY < minTextureSize.Width && textureSizeY < maxSize.Height)
          {
            dst.Enqueue(new PxSize2D(textureSizeX, textureSizeY));
            textureSizeY <<= 1;
          }
        }
      }

      // Anything texture size below a square of the biggest side has been added, so lets add the rest up to the max
      {
        // Now lets start from square texture size (since we tried everything below)
        int textureSize = Math.Max(textureSizeX, textureSizeY);

        while (textureSize <= maxSize.Width && textureSize <= maxSize.Height)
        {
          // Check square
          dst.Enqueue(new PxSize2D(textureSize, textureSize));

          if (!forceSquare)
          {
            // Check larger width
            if ((textureSizeX * 2) < maxSize.Width)
              dst.Enqueue(new PxSize2D(textureSize * 2, textureSize));
            // Check larger height
            if ((textureSizeY * 2) < maxSize.Height)
              dst.Enqueue(new PxSize2D(textureSize, textureSize * 2));
          }
          textureSize <<= 1;
        }
      }
    }

    public static int ToPowerOfTwo(int value)
    {
      Debug.Assert(value >= 0);
      if (value > 0)
      {
        int tmpValue = value;
        --tmpValue;
        tmpValue |= (tmpValue >> 1);
        tmpValue |= (tmpValue >> 2);
        tmpValue |= (tmpValue >> 4);
        tmpValue |= (tmpValue >> 8);
        tmpValue |= (tmpValue >> 16);
        ++tmpValue; // Val is now the next highest power of 2.
        return tmpValue;
      }
      return 1;
    }

  }

}
