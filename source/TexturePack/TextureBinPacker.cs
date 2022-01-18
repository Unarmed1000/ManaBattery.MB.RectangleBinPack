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
using MB.RectangleBinPack.BinPack;

namespace MB.RectangleBinPack.TexturePack
{
  /// <summary>
  /// </summary>
  public sealed class TextureBinPacker
  {
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "<Pending>")]
    public struct PackResult
    {
      public PxSize2D Size;
      public List<PackedAtlasImage> Images;

      public PackResult(PxSize2D size, List<PackedAtlasImage> images)
      {
        Size = size;
        Images = images;
      }

      public bool IsValid => Images != null;
    }

    private PxSize2D m_maxTextureSize;
    private TextureSizeRestriction m_textureSizeRestriction;
    private bool m_allowRotatedRegions;
    private PxThickness m_borderReservedPx;

    public TextureBinPacker(PxSize2D maxTextureSize, TextureSizeRestriction textureSizeRestriction, bool allowRotatedRegions,
                            PxThickness borderReservedPx)
    {
      m_maxTextureSize = maxTextureSize;
      m_textureSizeRestriction = textureSizeRestriction;
      m_allowRotatedRegions = allowRotatedRegions;
      m_borderReservedPx = borderReservedPx;
    }


    /// <summary>
    /// Try to create a optimal atlas of the content in srcImages
    /// </summary>
    /// <param name = "src" ></ param >
    /// < returns ></ returns >
    public PackResult TryProcess(List<AtlasImageInfo> srcImages)
    {
      if (srcImages == null)
        throw new ArgumentNullException(nameof(srcImages));

      // Extract blocks
      var sortedSrcImages = new List<AtlasImageInfo>(srcImages);
      if (sortedSrcImages.Count > 0)
      {
        // Sort the rects according to area
        sortedSrcImages.Sort(CompareAccordingToArea);
        //sortedSrcImages.Sort(CompareAccordingToWidth);
        //sortedSrcImages.Sort(CompareAccordingToHeight);

        // Decide which packing scheme to utilize
        var rectInfo = ExamineImageRects(sortedSrcImages);
        if (rectInfo.MinArea > 0)
        {
          if (!rectInfo.IsUniformSize)
          {
            return TryPackRects(rectInfo, m_maxTextureSize, m_textureSizeRestriction, m_borderReservedPx, m_allowRotatedRegions);
          }
          return TryPackUniformRects(rectInfo, m_maxTextureSize, m_textureSizeRestriction, m_borderReservedPx);
        }
        else
        {
          // Trying to pack a empty elements
          var images = new List<PackedAtlasImage>(srcImages.Count);
          for (int i = 0; i < images.Count; ++i)
          {
            Debug.Assert(srcImages[i].SrcRectPx.Size == new PxSize2D());
            images.Add(new PackedAtlasImage(srcImages[i], new PxRectangle(), false));
          }
          var minSize = new PxSize2D(Math.Max(1, m_borderReservedPx.SumX), Math.Max(1, m_borderReservedPx.SumY));
          return new PackResult(minSize, images);
        }

      }
      return new PackResult();
    }

    private static PackResult TryPackUniformRects(PackSourceInfo srcRectInfo, PxSize2D maxTextureSize, TextureSizeRestriction textureSizeRestriction,
                                                  PxThickness borderReservedPx)
    {
      Debug.Assert(srcRectInfo.IsUniformSize);

      var potentialTextureSizes = new Queue<PxSize2D>();
      switch (textureSizeRestriction)
      {
        case TextureSizeRestriction.Pow2Square:
          TextureSizeUtil.EnquePotentialPow2TextureSizes(potentialTextureSizes, srcRectInfo.MinArea, srcRectInfo.MaxSize, maxTextureSize,
                                                         borderReservedPx, true);
          break;
        case TextureSizeRestriction.Pow2:
          TextureSizeUtil.EnquePotentialPow2TextureSizes(potentialTextureSizes, srcRectInfo.MinArea, srcRectInfo.MaxSize, maxTextureSize,
                                                         borderReservedPx, false);
          break;
        case TextureSizeRestriction.Any:
          {
            int minLength = Convert.ToInt32(Math.Sqrt(srcRectInfo.MinArea));
            int area = minLength * minLength;
            if (area < srcRectInfo.MinArea)
              ++minLength;
            area = minLength * minLength;
            Debug.Assert(area >= srcRectInfo.MinArea);
            if (srcRectInfo.MaxSize.Width >= srcRectInfo.MaxSize.Height)
            {
              int leftOvers = minLength % srcRectInfo.MaxSize.Width;
              leftOvers = (leftOvers != 0 ? srcRectInfo.MaxSize.Width - leftOvers : 0);
              int lenX = minLength + leftOvers;
              int lenY = (srcRectInfo.MinArea / lenX) + (srcRectInfo.MinArea % lenX == 0 ? 0 : 1);
              Debug.Assert((lenX * lenY) >= srcRectInfo.MinArea);
              potentialTextureSizes.Enqueue(new PxSize2D(lenX, lenY));
            }
            else
            {
              int leftOvers = minLength % srcRectInfo.MaxSize.Height;
              leftOvers = (leftOvers != 0 ? srcRectInfo.MaxSize.Height - leftOvers : 0);
              int lenY = minLength + leftOvers;
              int lenX = (srcRectInfo.MinArea / lenY) + (srcRectInfo.MinArea % lenY == 0 ? 0 : 1);
              Debug.Assert((lenX * lenY) >= srcRectInfo.MinArea);
              potentialTextureSizes.Enqueue(new PxSize2D(lenX, lenY));
            }
          }
          break;
        default:
          throw new NotSupportedException("Unknown restriction");
      }

      var dstRects = new List<PackedAtlasImage>();

      bool bFound = false;
      var textureSize = new PxSize2D();
      // Run through the potential texture sizes
      while (!bFound && potentialTextureSizes.Count > 0)
      {
        textureSize = potentialTextureSizes.Dequeue();
        bFound = TryBuildUniformDstRects(dstRects, srcRectInfo, textureSize);
      }

      if (bFound)
      {
        Debug.Assert(textureSize.Width <= maxTextureSize.Width && textureSize.Height <= maxTextureSize.Height);
        return new PackResult(textureSize, dstRects);
      }
      return new PackResult();
    }

    private static bool TryBuildUniformDstRects(List<PackedAtlasImage> dstRects, PackSourceInfo srcRectInfo, PxSize2D textureSize)
    {
      Debug.Assert(srcRectInfo.IsUniformSize);

      int rectWidth = srcRectInfo.MaxSize.Width;
      int rectHeight = srcRectInfo.MaxSize.Height;

      // check if we can fit the expected amount of rects on the texture
      int maxRectsX = textureSize.Width / rectWidth;
      int maxRectsY = textureSize.Height / rectHeight;
      if ((maxRectsX * maxRectsY) < srcRectInfo.Images.Count)
        return false;

      var srcImages = srcRectInfo.Images;
      int fullLines = srcImages.Count / maxRectsX;
      int left = srcImages.Count % maxRectsX;
      int srcIndex = 0;

      int lineIdx = 0;
      while (lineIdx < fullLines)
      {
        for (int x = 0; x < maxRectsX; ++x)
        {
          var dstRectangle = new PxRectangle(x * rectWidth, lineIdx * rectHeight, rectWidth, rectHeight);
          dstRects.Add(new PackedAtlasImage(srcImages[srcIndex], dstRectangle, false));
          ++srcIndex;
        }
        ++lineIdx;
      }

      for (int x = 0; x < left; ++x)
      {
        var dstRectangle = new PxRectangle(x * rectWidth, lineIdx * rectHeight, rectWidth, rectHeight);
        dstRects.Add(new PackedAtlasImage(srcImages[srcIndex], dstRectangle, false));
        ++srcIndex;
      }
      return true;
    }

    private static PackResult TryPackRects(PackSourceInfo srcRectInfo, PxSize2D maxTextureSize, TextureSizeRestriction textureSizeRestriction,
                                           PxThickness borderReservedPx, bool allowRotation)
    {
      switch (textureSizeRestriction)
      {
        case TextureSizeRestriction.Pow2:
          return TryPackPow2Rects(srcRectInfo, maxTextureSize, false, borderReservedPx, allowRotation);
        case TextureSizeRestriction.Pow2Square:
          return TryPackPow2Rects(srcRectInfo, maxTextureSize, true, borderReservedPx, allowRotation);
        case TextureSizeRestriction.Any:
          return TryPackAnyRects(srcRectInfo, maxTextureSize, borderReservedPx, allowRotation);
        default:
          throw new NotSupportedException("Unknown texture restriction");
      }
    }

    private static PackResult TryPackAnyRects(PackSourceInfo srcRectInfo, PxSize2D maxTextureSize, PxThickness borderReservedPx, bool allowRotation)
    {
      var dstRects = new List<PackedAtlasImage>();

      bool bFound = false;
      int currentMinArea = srcRectInfo.MinArea;
      int missingFitArea;
      var textureSize = TextureSizeUtil.CalcMinimumTextureSize(currentMinArea, srcRectInfo.MaxSize, borderReservedPx, TextureSizeRestriction.Any);
      // Run through the potential texture sizes
      while (!bFound && textureSize.Width <= maxTextureSize.Width && textureSize.Height <= maxTextureSize.Height)
      {
        bFound = TryBuildDstRects(dstRects, srcRectInfo, textureSize, borderReservedPx, allowRotation, out missingFitArea);
        if (!bFound)
        {
          // Ok were not able to fit the rects onto the current texture size so lets try to increase the minimum area
          currentMinArea += Math.Max(missingFitArea / 10, 1);
          textureSize = TextureSizeUtil.CalcMinimumTextureSize(currentMinArea, srcRectInfo.MaxSize, borderReservedPx, TextureSizeRestriction.Any);
        }
      }

      if (bFound)
      {
        Debug.Assert(textureSize.Width <= maxTextureSize.Width && textureSize.Height <= maxTextureSize.Height);
        return new PackResult(textureSize, dstRects);
      }
      return new PackResult();
    }

    private static int CalcMissingArea(List<AtlasImageInfo> srcImages, int startIndex)
    {
      int area = 0;
      int count = srcImages.Count;
      for (int i = startIndex; i < count; ++i)
      {
        area += srcImages[i].SrcRectPx.Width * srcImages[i].SrcRectPx.Height;
      }
      return area;
    }


    private static PackResult TryPackPow2Rects(PackSourceInfo srcRectInfo, PxSize2D maxTextureSize, bool forceSquareTexture,
                                               PxThickness borderReservedPx, bool allowRotation)
    {
      var dstRects = new List<PackedAtlasImage>();

      bool found = false;
      var textureSize = new PxSize2D();
      var potentialTextureSizes = TextureSizeUtil.GetPotentialPow2TextureSizes(srcRectInfo.MinArea, srcRectInfo.MaxSize, maxTextureSize,
                                                                               borderReservedPx, forceSquareTexture);
      // Run through the potential texture sizes
      int missingFitArea;
      while (!found && potentialTextureSizes.Count > 0)
      {
        textureSize = potentialTextureSizes.Dequeue();
        found = TryBuildDstRects(dstRects, srcRectInfo, textureSize, borderReservedPx, allowRotation, out missingFitArea);
      }

      if (found)
      {
        Debug.Assert(textureSize.Width <= maxTextureSize.Width && textureSize.Height <= maxTextureSize.Height);
        return new PackResult(textureSize, dstRects);
      }
      return new PackResult();
    }

    private static bool TryBuildDstRects(List<PackedAtlasImage> dstRects, PackSourceInfo srcRectInfo, PxSize2D textureSize,
                                         PxThickness borderReservedPx, bool allowRotation, out int missingFitArea)
    {
      MaxRectsBinPack.FreeRectChoiceHeuristic[] heuristics =
      {
        MaxRectsBinPack.FreeRectChoiceHeuristic.RectBestShortSideFit,
        MaxRectsBinPack.FreeRectChoiceHeuristic.RectBestLongSideFit,
        MaxRectsBinPack.FreeRectChoiceHeuristic.RectBottomLeftRule,
        MaxRectsBinPack.FreeRectChoiceHeuristic.RectContactPointRule,
        MaxRectsBinPack.FreeRectChoiceHeuristic.RectBestAreaFit,
      };

      int minMissing = int.MaxValue;
      for (int j = 0; j < heuristics.Length; ++j)
      {
        var freeRectChoiceHeuristic = heuristics[j];
        if (TryBuildDstRects(dstRects, srcRectInfo, textureSize, borderReservedPx, allowRotation, freeRectChoiceHeuristic))
        {
          missingFitArea = 0;
          return true;
        }

        int missingArea = CalcMissingArea(srcRectInfo.Images, dstRects.Count);
        if (missingArea < minMissing)
          minMissing = missingArea;
      }
      missingFitArea = minMissing;
      return false;
    }


    private static bool TryBuildDstRects(List<PackedAtlasImage> dstRects, PackSourceInfo sourceInfo, PxSize2D textureSize,
                                         PxThickness borderReservedPx, bool allowRotation,
                                         MaxRectsBinPack.FreeRectChoiceHeuristic freeRectChoiceHeuristic)
    {
      dstRects.Clear();

      // Check if there is any chance that the content can fit (fast exit)
      if ((textureSize.Width * textureSize.Height) < sourceInfo.MinArea)
        return false;

      var srcImages = sourceInfo.Images;

      Debug.Assert(borderReservedPx.SumX <= textureSize.Width);
      Debug.Assert(borderReservedPx.SumY <= textureSize.Height);

      var pack = new MaxRectsBinPack(textureSize.Width - borderReservedPx.SumX, textureSize.Height - borderReservedPx.SumY, allowRotation);

      for (int i = 0; i < srcImages.Count; ++i)
      {
        var dstRect = new PxRectangle();
        if (srcImages[i].SrcRectPx.Width > 0 && srcImages[i].SrcRectPx.Height > 0)
        {
          dstRect = PxUncheckedTypeConverter.ToPxRectangle(pack.Insert(srcImages[i].SrcRectPx.Width, srcImages[i].SrcRectPx.Height, freeRectChoiceHeuristic));
          if (dstRect.Height == 0)
            return false;
        }

        dstRects.Add(new PackedAtlasImage(srcImages[i], dstRect, srcImages[i].SrcRectPx.Width != dstRect.Width));
      }

      //var occ = pack.Occupancy();
      return true;
    }


    private static int CompareAccordingToArea(AtlasImageInfo lhs, AtlasImageInfo rhs)
    {
      int areaRhs = lhs.SrcRectPx.Width * lhs.SrcRectPx.Height;
      int areaLhs = rhs.SrcRectPx.Width * rhs.SrcRectPx.Height;

      if (areaRhs == areaLhs)
      {
        int priorityLhs = lhs.SrcRectPx.Width + (lhs.SrcRectPx.Height * (8192 * 2));
        int priorityRhs = rhs.SrcRectPx.Width + (rhs.SrcRectPx.Height * (8192 * 2));
        return priorityLhs.CompareTo(priorityRhs);
      }
      return areaLhs.CompareTo(areaRhs);
    }

    private static int CompareAccordingToWidth(AtlasImageInfo lhs, AtlasImageInfo rhs)
    {
      if (lhs.SrcRectPx.Width == rhs.SrcRectPx.Width)
      {
        int priorityLhs = lhs.SrcRectPx.Width + (lhs.SrcRectPx.Height * (8192 * 2));
        int priorityRhs = rhs.SrcRectPx.Width + (rhs.SrcRectPx.Height * (8192 * 2));
        return priorityLhs.CompareTo(priorityRhs);
      }
      return rhs.SrcRectPx.Width.CompareTo(lhs.SrcRectPx.Width);
    }

    private static int CompareAccordingToHeight(AtlasImageInfo lhs, AtlasImageInfo rhs)
    {
      if (lhs.SrcRectPx.Height == rhs.SrcRectPx.Height)
      {
        int priorityLhs = lhs.SrcRectPx.Width + (lhs.SrcRectPx.Height * (8192 * 2));
        int priorityRhs = rhs.SrcRectPx.Width + (rhs.SrcRectPx.Height * (8192 * 2));
        return priorityLhs.CompareTo(priorityRhs);
      }
      return rhs.SrcRectPx.Height.CompareTo(lhs.SrcRectPx.Height);
    }

    /// <summary>
    /// Examine the image rects and extract some vital information that can help guide the texture creation.
    /// </summary>
    /// <param name="images"></param>
    /// <returns></returns>
    private static PackSourceInfo ExamineImageRects(List<AtlasImageInfo> images)
    {
      var min = new PxSize2D();
      var max = new PxSize2D();
      int minArea = 0;
      bool bIsUniformSize = true;

      int count = images.Count;
      if (count > 0)
      {
        min = images[0].SrcRectPx.Size;
        max = min;
        minArea = min.Width * min.Height;

        for (int i = 1; i < count; ++i)
        {
          if (images[i].SrcRectPx.Width > max.Width)
          {
            max.Width = images[i].SrcRectPx.Width;
            bIsUniformSize = false;
          }
          else if (images[i].SrcRectPx.Width < min.Width)
          {
            min.Width = images[i].SrcRectPx.Width;
            bIsUniformSize = false;
          }

          if (images[i].SrcRectPx.Height > max.Height)
          {
            max.Height = images[i].SrcRectPx.Height;
            bIsUniformSize = false;
          }
          else if (images[i].SrcRectPx.Height < min.Height)
          {
            min.Height = images[i].SrcRectPx.Height;
            bIsUniformSize = false;
          }

          minArea += images[i].SrcRectPx.Width * images[i].SrcRectPx.Height;
        }
      }
      return new PackSourceInfo(images, min, max, minArea, bIsUniformSize);
    }
  }
}
