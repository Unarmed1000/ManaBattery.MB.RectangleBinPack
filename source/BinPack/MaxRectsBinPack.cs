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

// Implements different bin packer algorithms that use the MAXRECTS data structure.
// Based on the Public Domain MaxRectsBinPack.cpp source by Jukka Jylänki
// https://github.com/juj/RectangleBinPack/
//
// Ported to C# from
// - MaxRectsBinPack.h      Revision: 8eb2520eb851393cb2db4a249c27f0a2696c9c6f
// - MaxRectsBinPack.cpp    Revision: 43f1dd7c4f75cd7ef00e469d2856e834e1610db1
//

using MB.Base.MathEx.Pixel;
using System;
using System.Collections.Generic;

namespace TexturePacker.RectangleBinPack.BinPack
{
  /// <summary>
  /// MaxRectsBinPack implements the MAXRECTS data structure and different bin packing algorithms that use this structure.
  /// </summary>
  public sealed class MaxRectsBinPack
  {
    private int m_binWidth;
    private int m_binHeight;

    private bool m_binAllowRotate;

    private List<PxRectangleM> m_usedRectangles = new List<PxRectangleM>();
    private List<PxRectangleM> m_freeRectangles = new List<PxRectangleM>();


    /// <summary>
    /// Specifies the different heuristic rules that can be used when deciding where to place a new rectangle.
    /// </summary>
    public enum FreeRectChoiceHeuristic
    {
      /// <summary>
      /// -BSSF: Positions the rectangle against the short side of a free rectangle into which it fits the best.
      /// </summary>
      RectBestShortSideFit,
      /// <summary>
      /// -BLSF: Positions the rectangle against the long side of a free rectangle into which it fits the best.
      /// </summary>
      RectBestLongSideFit,
      /// <summary>
      /// -BAF: Positions the rectangle into the smallest free rect into which it fits.
      /// </summary>
      RectBestAreaFit,
      /// <summary>
      /// -BL: Does the Tetris placement.
      /// </summary>
      RectBottomLeftRule,
      /// <summary>
      /// Choosest the placement where the rectangle touches other rects as much as possible.
      /// </summary>
      RectContactPointRule
    };


    /// <summary>
    /// Instantiates a bin of size (0,0). Call Init to create a new bin.
    /// </summary>
    public MaxRectsBinPack()
    {
    }

    /// <summary>
    /// Instantiates a bin of the given size.
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="allowRotate">Specifies whether the packing algorithm is allowed to rotate the input rectangles by 90 degrees
    ///													to consider a better placement</param>
    public MaxRectsBinPack(int width, int height, bool allowRotate = true)
    {
      Init(width, height, allowRotate);
    }

    /// <summary>
    /// (Re)initializes the packer to an empty bin of width x height units. Call whenever
    /// you need to restart with a new bin.
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="allowRotate">Specifies whether the packing algorithm is allowed to rotate the input rectangles by 90 degrees
    ///													to consider a better placement</param>
    public void Init(int width, int height, bool allowRotate = true)
    {
      m_binAllowRotate = allowRotate;
      m_binWidth = width;
      m_binHeight = height;
      m_usedRectangles.Clear();
      m_freeRectangles.Clear();
      m_freeRectangles.Add(new PxRectangleM(0, 0, width, height));
    }

    /// <summary>
    /// Inserts a single rectangle into the bin, possibly rotated.
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="method"></param>
    /// <returns></returns>
    public PxRectangleM Insert(int width, int height, FreeRectChoiceHeuristic method)
    {
      var newNode = new PxRectangleM();
      // Unused in this function. We don't need to know the score after finding the position.
      int score1 = int.MaxValue;
      int score2 = int.MaxValue;
      switch (method)
      {
        case FreeRectChoiceHeuristic.RectBestShortSideFit:
          newNode = FindPositionForNewNodeBestShortSideFit(width, height, out score1, out score2);
          break;
        case FreeRectChoiceHeuristic.RectBottomLeftRule:
          newNode = FindPositionForNewNodeBottomLeft(width, height, out score1, out score2);
          break;
        case FreeRectChoiceHeuristic.RectContactPointRule:
          newNode = FindPositionForNewNodeContactPoint(width, height, out score1);
          break;
        case FreeRectChoiceHeuristic.RectBestLongSideFit:
          newNode = FindPositionForNewNodeBestLongSideFit(width, height, out score2, out score1);
          break;
        case FreeRectChoiceHeuristic.RectBestAreaFit:
          newNode = FindPositionForNewNodeBestAreaFit(width, height, out score1, out score2);
          break;
      }

      if (newNode.Height == 0)
        return newNode;

      var numRectanglesToProcess = m_freeRectangles.Count;
      for (int i = 0; i < numRectanglesToProcess; ++i)
      {
        if (SplitFreeNode(m_freeRectangles[i], newNode))
        {
          m_freeRectangles.RemoveAt(i);
          --i;
          --numRectanglesToProcess;
        }
      }

      PruneFreeList();

      m_usedRectangles.Add(newNode);
      return newNode;
    }

    /// Inserts the given list of rectangles in an offline/batch mode, possibly rotated.
    /// @param rects The list of rectangles to insert. This vector will be destroyed in the process.
    /// @param dst [out] This list will contain the packed rectangles. The indices will not correspond to that of rects.
    /// @param method The rectangle placement rule to use when packing.
    public void Insert(List<PxSize2D> rects, List<PxRectangleM> dst, FreeRectChoiceHeuristic method)
    {
      if (rects == null)
        throw new ArgumentNullException(nameof(rects));
      if (dst == null)
        throw new ArgumentNullException(nameof(dst));

      dst.Clear();

      while (rects.Count > 0)
      {
        int bestScore1 = int.MaxValue;
        int bestScore2 = int.MaxValue;
        int bestRectIndex = -1;
        var bestNode = new PxRectangleM();

        for (int i = 0; i < rects.Count; ++i)
        {
          int score1;
          int score2;
          var newNode = ScoreRect(rects[i].Width, rects[i].Height, method, out score1, out score2);
          if (score1 < bestScore1 || (score1 == bestScore1 && score2 < bestScore2))
          {
            bestScore1 = score1;
            bestScore2 = score2;
            bestNode = newNode;
            bestRectIndex = i;
          }
        }

        if (bestRectIndex == -1)
          return;

        PlaceRect(bestNode);
        dst.Add(bestNode);
        rects.RemoveAt(bestRectIndex);
      }
    }

    /// Places the given rectangle into the bin.
    private void PlaceRect(PxRectangleM node)
    {
      var numRectanglesToProcess = m_freeRectangles.Count;
      for (int i = 0; i < numRectanglesToProcess; ++i)
      {
        if (SplitFreeNode(m_freeRectangles[i], node))
        {
          m_freeRectangles.RemoveAt(i);
          --i;
          --numRectanglesToProcess;
        }
      }

      PruneFreeList();

      m_usedRectangles.Add(node);
    }


    /// Computes the placement score for placing the given rectangle with the given method.
    /// @param score1 [out] The primary placement score will be outputted here.
    /// @param score2 [out] The secondary placement score will be outputted here. This isu sed to break ties.
    /// @return This struct identifies where the rectangle would be placed if it were placed.
    private PxRectangleM ScoreRect(int width, int height, FreeRectChoiceHeuristic method, out int score1, out int score2)
    {
      var newNode = new PxRectangleM();
      score1 = int.MaxValue;
      score2 = int.MaxValue;
      switch (method)
      {
        case FreeRectChoiceHeuristic.RectBestShortSideFit:
          newNode = FindPositionForNewNodeBestShortSideFit(width, height, out score1, out score2);
          break;
        case FreeRectChoiceHeuristic.RectBottomLeftRule:
          newNode = FindPositionForNewNodeBottomLeft(width, height, out score1, out score2);
          break;
        case FreeRectChoiceHeuristic.RectContactPointRule:
          newNode = FindPositionForNewNodeContactPoint(width, height, out score1);
          score1 = -score1; // Reverse since we are minimizing, but for contact point score bigger is better.
          break;
        case FreeRectChoiceHeuristic.RectBestLongSideFit:
          newNode = FindPositionForNewNodeBestLongSideFit(width, height, out score2, out score1);
          break;
        case FreeRectChoiceHeuristic.RectBestAreaFit:
          newNode = FindPositionForNewNodeBestAreaFit(width, height, out score1, out score2);
          break;
      }

      // Cannot fit the current rectangle.
      if (newNode.Height == 0)
      {
        score1 = int.MaxValue;
        score2 = int.MaxValue;
      }
      return newNode;
    }

    /// <summary>
    /// Computes the ratio of used surface area to the total bin area.
    /// </summary>
    public float Occupancy()
    {
      ulong usedSurfaceArea = 0u;
      for (int i = 0; i < m_usedRectangles.Count; ++i)
      {
        usedSurfaceArea += ((ulong)m_usedRectangles[i].Width) * ((ulong)m_usedRectangles[i].Height);
      }
      return (float)usedSurfaceArea / (m_binWidth * m_binHeight);
    }


    private PxRectangleM FindPositionForNewNodeBottomLeft(int width, int height, out int bestY, out int bestX)
    {
      var bestNode = new PxRectangleM();

      bestY = int.MaxValue;
      bestX = int.MaxValue;

      for (int i = 0; i < m_freeRectangles.Count; ++i)
      {
        // Try to place the rectangle in upright (non-flipped) orientation.
        if (m_freeRectangles[i].Width >= width && m_freeRectangles[i].Height >= height)
        {
          int topSideY = m_freeRectangles[i].Y + height;
          if (topSideY < bestY || (topSideY == bestY && m_freeRectangles[i].X < bestX))
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = width;
            bestNode.Height = height;
            bestY = topSideY;
            bestX = m_freeRectangles[i].X;
          }
        }
        if (m_binAllowRotate && m_freeRectangles[i].Width >= height && m_freeRectangles[i].Height >= width)
        {
          int topSideY = m_freeRectangles[i].Y + width;
          if (topSideY < bestY || (topSideY == bestY && m_freeRectangles[i].X < bestX))
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = height;
            bestNode.Height = width;
            bestY = topSideY;
            bestX = m_freeRectangles[i].X;
          }
        }
      }
      return bestNode;
    }

    private PxRectangleM FindPositionForNewNodeBestShortSideFit(int width, int height, out int bestShortSideFit, out int bestLongSideFit)
    {
      var bestNode = new PxRectangleM();

      bestShortSideFit = int.MaxValue;
      bestLongSideFit = int.MaxValue;

      for (int i = 0; i < m_freeRectangles.Count; ++i)
      {
        // Try to place the rectangle in upright (non-flipped) orientation.
        if (m_freeRectangles[i].Width >= width && m_freeRectangles[i].Height >= height)
        {
          int leftoverHoriz = Math.Abs(m_freeRectangles[i].Width - width);
          int leftoverVert = Math.Abs(m_freeRectangles[i].Height - height);
          int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
          int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

          if (shortSideFit < bestShortSideFit || (shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit))
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = width;
            bestNode.Height = height;
            bestShortSideFit = shortSideFit;
            bestLongSideFit = longSideFit;
          }
        }

        if (m_binAllowRotate && m_freeRectangles[i].Width >= height && m_freeRectangles[i].Height >= width)
        {
          int flippedLeftoverHoriz = Math.Abs(m_freeRectangles[i].Width - height);
          int flippedLeftoverVert = Math.Abs(m_freeRectangles[i].Height - width);
          int flippedShortSideFit = Math.Min(flippedLeftoverHoriz, flippedLeftoverVert);
          int flippedLongSideFit = Math.Max(flippedLeftoverHoriz, flippedLeftoverVert);

          if (flippedShortSideFit < bestShortSideFit || (flippedShortSideFit == bestShortSideFit && flippedLongSideFit < bestLongSideFit))
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = height;
            bestNode.Height = width;
            bestShortSideFit = flippedShortSideFit;
            bestLongSideFit = flippedLongSideFit;
          }
        }
      }
      return bestNode;
    }


    private PxRectangleM FindPositionForNewNodeBestLongSideFit(int width, int height, out int bestShortSideFit, out int bestLongSideFit)
    {
      var bestNode = new PxRectangleM();

      bestShortSideFit = int.MaxValue;
      bestLongSideFit = int.MaxValue;

      for (int i = 0; i < m_freeRectangles.Count; ++i)
      {
        // Try to place the rectangle in upright (non-flipped) orientation.
        if (m_freeRectangles[i].Width >= width && m_freeRectangles[i].Height >= height)
        {
          int leftoverHoriz = Math.Abs(m_freeRectangles[i].Width - width);
          int leftoverVert = Math.Abs(m_freeRectangles[i].Height - height);
          int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
          int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

          if (longSideFit < bestLongSideFit || (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit))
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = width;
            bestNode.Height = height;
            bestShortSideFit = shortSideFit;
            bestLongSideFit = longSideFit;
          }
        }

        if (m_binAllowRotate && m_freeRectangles[i].Width >= height && m_freeRectangles[i].Height >= width)
        {
          int leftoverHoriz = Math.Abs(m_freeRectangles[i].Width - height);
          int leftoverVert = Math.Abs(m_freeRectangles[i].Height - width);
          int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
          int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

          if (longSideFit < bestLongSideFit || (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit))
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = height;
            bestNode.Height = width;
            bestShortSideFit = shortSideFit;
            bestLongSideFit = longSideFit;
          }
        }
      }
      return bestNode;
    }

    private PxRectangleM FindPositionForNewNodeBestAreaFit(int width, int height, out int bestAreaFit, out int bestShortSideFit)
    {
      var bestNode = new PxRectangleM();

      bestAreaFit = int.MaxValue;
      bestShortSideFit = int.MaxValue;

      for (int i = 0; i < m_freeRectangles.Count; ++i)
      {
        int areaFit = m_freeRectangles[i].Width * m_freeRectangles[i].Height - width * height;

        // Try to place the rectangle in upright (non-flipped) orientation.
        if (m_freeRectangles[i].Width >= width && m_freeRectangles[i].Height >= height)
        {
          int leftoverHoriz = Math.Abs(m_freeRectangles[i].Width - width);
          int leftoverVert = Math.Abs(m_freeRectangles[i].Height - height);
          int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

          if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = width;
            bestNode.Height = height;
            bestShortSideFit = shortSideFit;
            bestAreaFit = areaFit;
          }
        }

        if (m_binAllowRotate && m_freeRectangles[i].Width >= height && m_freeRectangles[i].Height >= width)
        {
          int leftoverHoriz = Math.Abs(m_freeRectangles[i].Width - height);
          int leftoverVert = Math.Abs(m_freeRectangles[i].Height - width);
          int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

          if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = height;
            bestNode.Height = width;
            bestShortSideFit = shortSideFit;
            bestAreaFit = areaFit;
          }
        }
      }
      return bestNode;
    }

    /// <summary>
    /// Returns 0 if the two intervals i1 and i2 are disjoint, or the length of their overlap otherwise.
    /// </summary>
    /// <param name="i1start"></param>
    /// <param name="i1end"></param>
    /// <param name="i2start"></param>
    /// <param name="i2end"></param>
    /// <returns></returns>
    private static int CommonIntervalLength(int i1start, int i1end, int i2start, int i2end)
    {
      if (i1end < i2start || i2end < i1start)
        return 0;
      return Math.Min(i1end, i2end) - Math.Max(i1start, i2start);
    }

    /// Computes the placement score for the -CP variant.
    private int ContactPointScoreNode(int x, int y, int width, int height)
    {
      int score = 0;

      if (x == 0 || x + width == m_binWidth)
        score += height;
      if (y == 0 || y + height == m_binHeight)
        score += width;

      for (int i = 0; i < m_usedRectangles.Count; ++i)
      {
        if (m_usedRectangles[i].X == x + width || m_usedRectangles[i].X + m_usedRectangles[i].Width == x)
          score += CommonIntervalLength(m_usedRectangles[i].Y, m_usedRectangles[i].Y + m_usedRectangles[i].Height, y, y + height);

        if (m_usedRectangles[i].Y == y + height || m_usedRectangles[i].Y + m_usedRectangles[i].Height == y)
          score += CommonIntervalLength(m_usedRectangles[i].X, m_usedRectangles[i].X + m_usedRectangles[i].Width, x, x + width);
      }
      return score;
    }

    private PxRectangleM FindPositionForNewNodeContactPoint(int width, int height, out int bestContactScore)
    {
      var bestNode = new PxRectangleM();
      bestContactScore = -1;
      for (int i = 0; i < m_freeRectangles.Count; ++i)
      {
        // Try to place the rectangle in upright (non-flipped) orientation.
        if (m_freeRectangles[i].Width >= width && m_freeRectangles[i].Height >= height)
        {
          int score = ContactPointScoreNode(m_freeRectangles[i].X, m_freeRectangles[i].Y, width, height);
          if (score > bestContactScore)
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = width;
            bestNode.Height = height;
            bestContactScore = score;
          }
        }
        if (m_binAllowRotate && m_freeRectangles[i].Width >= height && m_freeRectangles[i].Height >= width)
        {
          int score = ContactPointScoreNode(m_freeRectangles[i].X, m_freeRectangles[i].Y, height, width);
          if (score > bestContactScore)
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = height;
            bestNode.Height = width;
            bestContactScore = score;
          }
        }
      }
      return bestNode;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="freeNode"></param>
    /// <param name="usedNode"></param>
    /// <returns>True if the free node was split.</returns>
    private bool SplitFreeNode(PxRectangleM freeNode, PxRectangleM usedNode)
    {
      // Test with SAT if the rectangles even intersect.
      if (usedNode.X >= (freeNode.X + freeNode.Width) || (usedNode.X + usedNode.Width) <= freeNode.X ||
        usedNode.Y >= (freeNode.Y + freeNode.Height) || (usedNode.Y + usedNode.Height) <= freeNode.Y)
        return false;

      if (usedNode.X < (freeNode.X + freeNode.Width) && (usedNode.X + usedNode.Width) > freeNode.X)
      {
        // New node at the top side of the used node.
        if (usedNode.Y > freeNode.Y && usedNode.Y < freeNode.Y + freeNode.Height)
        {
          var newNode = freeNode;
          newNode.Height = usedNode.Y - newNode.Y;
          m_freeRectangles.Add(newNode);
        }

        // New node at the bottom side of the used node.
        if ((usedNode.Y + usedNode.Height) < (freeNode.Y + freeNode.Height))
        {
          var newNode = freeNode;
          newNode.Y = usedNode.Y + usedNode.Height;
          newNode.Height = freeNode.Y + freeNode.Height - (usedNode.Y + usedNode.Height);
          m_freeRectangles.Add(newNode);
        }
      }

      if (usedNode.Y < (freeNode.Y + freeNode.Height) && (usedNode.Y + usedNode.Height) > freeNode.Y)
      {
        // New node at the left side of the used node.
        if (usedNode.X > freeNode.X && usedNode.X < freeNode.X + freeNode.Width)
        {
          var newNode = freeNode;
          newNode.Width = usedNode.X - newNode.X;
          m_freeRectangles.Add(newNode);
        }

        // New node at the right side of the used node.
        if ((usedNode.X + usedNode.Width) < (freeNode.X + freeNode.Width))
        {
          var newNode = freeNode;
          newNode.X = usedNode.X + usedNode.Width;
          newNode.Width = freeNode.X + freeNode.Width - (usedNode.X + usedNode.Width);
          m_freeRectangles.Add(newNode);
        }
      }
      return true;
    }

    /// <summary>
    /// Goes through the free rectangle list and removes any redundant entries.
    /// </summary>
    private void PruneFreeList()
    {
      /*
        ///  Would be nice to do something like this, to avoid a Theta(n^2) loop through each pair.
        ///  But unfortunately it doesn't quite cut it, since we also want to detect containment.
        ///  Perhaps there's another way to do this faster than Theta(n^2).
        if (freeRectangles.size() > 0)
          clb::sort::QuickSort(&freeRectangles[0], freeRectangles.size(), NodeSortCmp);
        for(size_t i = 0; i < freeRectangles.size()-1; ++i)
          if (freeRectangles[i].x == freeRectangles[i+1].x &&
              freeRectangles[i].y == freeRectangles[i+1].y &&
              freeRectangles[i].width == freeRectangles[i+1].width &&
              freeRectangles[i].height == freeRectangles[i+1].height)
          {
            freeRectangles.erase(freeRectangles.begin() + i);
            --i;
          }
        */

      /// Go through each pair and remove any rectangle that is redundant.
      for (int i = 0; i < m_freeRectangles.Count; ++i)
      {
        for (int j = i + 1; j < m_freeRectangles.Count; ++j)
        {
          if (IsContainedIn(m_freeRectangles[i], m_freeRectangles[j]))
          {
            m_freeRectangles.RemoveAt(i);
            --i;
            break;
          }
          if (IsContainedIn(m_freeRectangles[j], m_freeRectangles[i]))
          {
            m_freeRectangles.RemoveAt(j);
            --j;
          }
        }
      }
    }

    private static bool IsContainedIn(PxRectangleM a, PxRectangleM b)
    {
      return a.X >= b.X && a.Y >= b.Y
        && a.X + a.Width <= b.X + b.Width
        && a.Y + a.Height <= b.Y + b.Height;
    }
  }
}

