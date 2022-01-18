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

// Implements different bin packer algorithms that use the GUILLOTINE data structure.
// Based on the Public Domain GuillotineBinPack.cpp source by Jukka Jylänki
// https://github.com/juj/RectangleBinPack/
//
// Ported to C# from
// - GuillotineBinPack.h    Revision: 1ec9730eba5ddf9a26dd3cabd0e72191a9942108
// - GuillotineBinPack.cpp  Revision: 1ec9730eba5ddf9a26dd3cabd0e72191a9942108
//

using MB.Base.MathEx.Pixel;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TexturePacker.RectangleBinPack.BinPack
{
  /// <summary>
  /// MaxRectsBinPack implements the MAXRECTS data structure and different bin packing algorithms that use this structure.
  /// </summary>
  public sealed class GuillotineBinPack
  {
    /// <summary>
    /// Specifies the different choice heuristics that can be used when deciding which of the free subrectangles
    /// to place the to-be-packed rectangle into.
    /// </summary>
    public enum FreeRectChoiceHeuristic
    {
      RectBestAreaFit, ///< -BAF
			RectBestShortSideFit, ///< -BSSF
			RectBestLongSideFit, ///< -BLSF
			RectWorstAreaFit, ///< -WAF
			RectWorstShortSideFit, ///< -WSSF
			RectWorstLongSideFit ///< -WLSF
		};

    /// <summary>
    /// Specifies the different choice heuristics that can be used when the packer needs to decide whether to
    /// subdivide the remaining free space in horizontal or vertical direction.
    /// </summary>
    public enum GuillotineSplitHeuristic
    {
      SplitShorterLeftoverAxis, ///< -SLAS
			SplitLongerLeftoverAxis, ///< -LLAS
			SplitMinimizeArea, ///< -MINAS, Try to make a single big rectangle at the expense of making the other small.
			SplitMaximizeArea, ///< -MAXAS, Try to make both remaining rectangles as even-sized as possible.
			SplitShorterAxis, ///< -SAS
			SplitLongerAxis ///< -LAS
		};

    private int m_binWidth;
    private int m_binHeight;

    /// <summary>
    /// Stores a list of all the rectangles that we have packed so far. This is used only to compute the Occupancy ratio,
    /// so if you want to have the packer consume less memory, this can be removed.
    /// </summary>
    private List<PxRectangleM> m_usedRectangles = new List<PxRectangleM>();

    /// <summary>
    /// Stores a list of rectangles that represents the free area of the bin. This rectangles in this list are disjoint.
    /// </summary>
    private List<PxRectangleM> m_freeRectangles = new List<PxRectangleM>();

#if DEBUG
    /// Used to track that the packer produces proper packings.
    private DisjointRectCollection m_disjointRects = new DisjointRectCollection();
#endif


    /// <summary>
    /// The initial bin size will be (0,0). Call Init to set the bin size.
    /// </summary>
    public GuillotineBinPack()
    {
    }

    /// <summary>
    /// Initializes a new bin of the given size.
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    public GuillotineBinPack(int width, int height)
    {
      Init(width, height);
    }

    /// <summary>
    /// (Re)initializes the packer to an empty bin of width x height units. Call whenever
    /// you need to restart with a new bin.
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    public void Init(int width, int height)
    {
      m_binWidth = width;
      m_binHeight = height;

#if DEBUG
      m_disjointRects.Clear();
#endif

      // Clear any memory of previously packed rectangles.
      m_usedRectangles.Clear();

      // We start with a single big free rectangle that spans the whole bin.
      m_freeRectangles.Clear();
      m_freeRectangles.Add(new PxRectangleM(0, 0, width, height));
    }


    /// Inserts a list of rectangles into the bin.
    /// @param rects The list of rectangles to add. This list will be destroyed in the packing process.
    /// @param merge If true, performs Rectangle Merge operations during the packing process.
    /// @param rectChoice The free rectangle choice heuristic rule to use.
    /// @param splitMethod The free rectangle split heuristic rule to use.
    public void Insert(List<PxSize2D> rects, bool merge, FreeRectChoiceHeuristic rectChoice, GuillotineSplitHeuristic splitMethod)
    {
      if (rects == null)
        throw new ArgumentNullException(nameof(rects));

      // Remember variables about the best packing choice we have made so far during the iteration process.
      int bestFreeRect = 0;
      int bestRect = 0;
      bool bestFlipped = false;

      // Pack rectangles one at a time until we have cleared the rects array of all rectangles.
      // rects will get destroyed in the process.
      while (rects.Count > 0)
      {
        // Stores the penalty score of the best rectangle placement - bigger=worse, smaller=better.
        int bestScore = int.MaxValue;

        for (int i = 0; i < m_freeRectangles.Count; ++i)
        {
          for (int j = 0; j < rects.Count; ++j)
          {
            // If this rectangle is a perfect match, we pick it instantly.
            if (rects[j].Width == m_freeRectangles[i].Width && rects[j].Height == m_freeRectangles[i].Height)
            {
              bestFreeRect = i;
              bestRect = j;
              bestFlipped = false;
              bestScore = int.MinValue;
              i = m_freeRectangles.Count; // Force a jump out of the outer loop as well - we got an instant fit.
              break;
            }
            // If flipping this rectangle is a perfect match, pick that then.
            else if (rects[j].Height == m_freeRectangles[i].Width && rects[j].Width == m_freeRectangles[i].Height)
            {
              bestFreeRect = i;
              bestRect = j;
              bestFlipped = true;
              bestScore = int.MinValue;
              i = m_freeRectangles.Count; // Force a jump out of the outer loop as well - we got an instant fit.
              break;
            }
            // Try if we can fit the rectangle upright.
            else if (rects[j].Width <= m_freeRectangles[i].Width && rects[j].Height <= m_freeRectangles[i].Height)
            {
              int score = ScoreByHeuristic(rects[j].Width, rects[j].Height, m_freeRectangles[i], rectChoice);
              if (score < bestScore)
              {
                bestFreeRect = i;
                bestRect = j;
                bestFlipped = false;
                bestScore = score;
              }
            }
            // If not, then perhaps flipping sideways will make it fit?
            else if (rects[j].Height <= m_freeRectangles[i].Width && rects[j].Width <= m_freeRectangles[i].Height)
            {
              int score = ScoreByHeuristic(rects[j].Height, rects[j].Width, m_freeRectangles[i], rectChoice);
              if (score < bestScore)
              {
                bestFreeRect = i;
                bestRect = j;
                bestFlipped = true;
                bestScore = score;
              }
            }
          }
        }

        // If we didn't manage to find any rectangle to pack, abort.
        if (bestScore == int.MaxValue)
          return;

        // Otherwise, we're good to go and do the actual packing.
        var newNode = new PxRectangleM();
        newNode.X = m_freeRectangles[bestFreeRect].X;
        newNode.Y = m_freeRectangles[bestFreeRect].Y;
        newNode.Width = rects[bestRect].Width;
        newNode.Height = rects[bestRect].Height;

        if (bestFlipped)
        {
          int tmp = newNode.Width;
          newNode.Width = newNode.Height;
          newNode.Height = tmp;
        }

        // Remove the free space we lost in the bin.
        SplitFreeRectByHeuristic(m_freeRectangles[bestFreeRect], newNode, splitMethod);
        m_freeRectangles.RemoveAt(bestFreeRect);

        // Remove the rectangle we just packed from the input list.
        rects.RemoveAt(bestRect);

        // Perform a Rectangle Merge step if desired.
        if (merge)
          MergeFreeList();

        // Remember the new used rectangle.
        m_usedRectangles.Add(newNode);

#if DEBUG
        // Check that we're really producing correct packings here.
        Debug.Assert(m_disjointRects.Add(newNode) == true);
#endif
      }
    }

    /// @return True if r fits inside freeRect (possibly rotated).
    private static bool Fits(PxSize2D r, PxRectangleM freeRect)
    {
      return (r.Width <= freeRect.Width && r.Height <= freeRect.Height) || (r.Height <= freeRect.Width && r.Width <= freeRect.Height);
    }

    /// @return True if r fits perfectly inside freeRect, i.e. the leftover area is 0.
    private static bool FitsPerfectly(PxSize2D r, PxRectangleM freeRect)
    {
      return (r.Width == freeRect.Width && r.Height == freeRect.Height) || (r.Height == freeRect.Width && r.Width == freeRect.Height);
    }



    /// <summary>
    /// Inserts a single rectangle into the bin. The packer might rotate the rectangle, in which case the returned
    /// struct will have the width and height values swapped.
    /// @param merge If true, performs free Rectangle Merge procedure after packing the new rectangle. This procedure
    ///		tries to defragment the list of disjoint free rectangles to improve packing performance, but also takes up
    ///		some extra time.
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="merge"></param>
    /// <param name="rectChoice">The free rectangle choice heuristic rule to use</param>
    /// <param name="splitMethod">The free rectangle split heuristic rule to use</param>
    /// <returns></returns>
    public PxRectangleM Insert(int width, int height, bool merge, FreeRectChoiceHeuristic rectChoice, GuillotineSplitHeuristic splitMethod)
    {
      // Find where to put the new rectangle.
      int freeNodeIndex = 0;
      var newRect = FindPositionForNewNode(width, height, rectChoice, ref freeNodeIndex);

      // Abort if we didn't have enough space in the bin.
      if (newRect.Height == 0)
        return newRect;

      // Remove the space that was just consumed by the new rectangle.
      SplitFreeRectByHeuristic(m_freeRectangles[freeNodeIndex], newRect, splitMethod);
      m_freeRectangles.RemoveAt(freeNodeIndex);

      // Perform a Rectangle Merge step if desired.
      if (merge)
        MergeFreeList();

      // Remember the new used rectangle.
      m_usedRectangles.Add(newRect);

#if DEBUG
      // Check that we're really producing correct packings here.
      Debug.Assert(m_disjointRects.Add(newRect) == true);
#endif

      return newRect;
    }

    // Implements GUILLOTINE-MAXFITTING, an experimental heuristic that's really cool but didn't quite work in practice.
    //	void InsertMaxFitting(std::vector<RectSize> &rects, std::vector<Rect> &dst, bool merge,
    //		FreeRectChoiceHeuristic rectChoice, GuillotineSplitHeuristic splitMethod);

    /// <summary>
    /// Computes the ratio of used/total surface area. 0.00 means no space is yet used, 1.00 means the whole bin is used.
    /// </summary>
    /// <returns></returns>
    public float Occupancy()
    {
      ///\todo The occupancy rate could be cached/tracked incrementally instead
      ///      of looping through the list of packed rectangles here.
      ulong usedSurfaceArea = 0u;
      for (int i = 0; i < m_usedRectangles.Count; ++i)
        usedSurfaceArea += ((ulong)m_usedRectangles[i].Width) * ((ulong)m_usedRectangles[i].Height);

      return (float)usedSurfaceArea / (m_binWidth * m_binHeight);
    }

    /// Returns the internal list of disjoint rectangles that track the free area of the bin. You may alter this vector
    /// any way desired, as long as the end result still is a list of disjoint rectangles.
    List<PxRectangleM> GetFreeRectangles()
    {
      return m_freeRectangles;
    }

    /// Returns the list of packed rectangles. You may alter this vector at will, for example, you can move a Rect from
    /// this list to the Free Rectangles list to free up space on-the-fly, but notice that this causes fragmentation.
    List<PxRectangleM> GetUsedRectangles()
    {
      return m_usedRectangles;
    }

    /// <summary>
    /// Returns the heuristic score value for placing a rectangle of size width*height into freeRect. Does not try to rotate.
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="freeRect"></param>
    /// <param name="rectChoice"></param>
    /// <returns></returns>
    private static int ScoreByHeuristic(int width, int height, PxRectangleM freeRect, FreeRectChoiceHeuristic rectChoice)
    {
      switch (rectChoice)
      {
        case FreeRectChoiceHeuristic.RectBestAreaFit:
          return ScoreBestAreaFit(width, height, freeRect);
        case FreeRectChoiceHeuristic.RectBestShortSideFit:
          return ScoreBestShortSideFit(width, height, freeRect);
        case FreeRectChoiceHeuristic.RectBestLongSideFit:
          return ScoreBestLongSideFit(width, height, freeRect);
        case FreeRectChoiceHeuristic.RectWorstAreaFit:
          return ScoreWorstAreaFit(width, height, freeRect);
        case FreeRectChoiceHeuristic.RectWorstShortSideFit:
          return ScoreWorstShortSideFit(width, height, freeRect);
        case FreeRectChoiceHeuristic.RectWorstLongSideFit:
          return ScoreWorstLongSideFit(width, height, freeRect);
        default:
          Debug.Fail($"Unsupported FreeRectChoiceHeuristic {rectChoice}");
          return int.MaxValue;
      }
    }
    private static int ScoreBestAreaFit(int width, int height, PxRectangleM freeRect)
    {
      return (freeRect.Width * freeRect.Height) - (width * height);
    }

    private static int ScoreBestShortSideFit(int width, int height, PxRectangleM freeRect)
    {
      int leftoverHoriz = Math.Abs(freeRect.Width - width);
      int leftoverVert = Math.Abs(freeRect.Height - height);
      int leftover = Math.Min(leftoverHoriz, leftoverVert);
      return leftover;
    }

    private static int ScoreBestLongSideFit(int width, int height, PxRectangleM freeRect)
    {
      int leftoverHoriz = Math.Abs(freeRect.Width - width);
      int leftoverVert = Math.Abs(freeRect.Height - height);
      int leftover = Math.Max(leftoverHoriz, leftoverVert);
      return leftover;
    }

    private static int ScoreWorstAreaFit(int width, int height, PxRectangleM freeRect)
    {
      return -ScoreBestAreaFit(width, height, freeRect);
    }

    private static int ScoreWorstShortSideFit(int width, int height, PxRectangleM freeRect)
    {
      return -ScoreBestShortSideFit(width, height, freeRect);
    }

    private static int ScoreWorstLongSideFit(int width, int height, PxRectangleM freeRect)
    {
      return -ScoreBestLongSideFit(width, height, freeRect);
    }

    /// <summary>
    /// Goes through the list of free rectangles and finds the best one to place a rectangle of given size into.
    /// Running time is Theta(|freeRectangles|).
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="rectChoice"></param>
    /// <param name="rNodeIndex">The index of the free rectangle in the freeRectangles array into which the new rect was placed</param>
    /// <returns>A Rect structure that represents the placement of the new rect into the best free rectangle</returns>
    private PxRectangleM FindPositionForNewNode(int width, int height, FreeRectChoiceHeuristic rectChoice, ref int rNodeIndex)
    {
      var bestNode = new PxRectangleM();
      int bestScore = int.MaxValue;

      /// Try each free rectangle to find the best one for placement.
      for (int i = 0; i < m_freeRectangles.Count; ++i)
      {
        // If this is a perfect fit upright, choose it immediately.
        if (width == m_freeRectangles[i].Width && height == m_freeRectangles[i].Height)
        {
          bestNode.X = m_freeRectangles[i].X;
          bestNode.Y = m_freeRectangles[i].Y;
          bestNode.Width = width;
          bestNode.Height = height;
          bestScore = int.MinValue;
          rNodeIndex = i;
#if DEBUG
          Debug.Assert(m_disjointRects.Disjoint(bestNode));
#endif
          break;
        }
        // If this is a perfect fit sideways, choose it.
        else if (height == m_freeRectangles[i].Width && width == m_freeRectangles[i].Height)
        {
          bestNode.X = m_freeRectangles[i].X;
          bestNode.Y = m_freeRectangles[i].Y;
          bestNode.Width = height;
          bestNode.Height = width;
          bestScore = int.MinValue;
          rNodeIndex = i;
#if DEBUG
          Debug.Assert(m_disjointRects.Disjoint(bestNode));
#endif
          break;
        }
        // Does the rectangle fit upright?
        else if (width <= m_freeRectangles[i].Width && height <= m_freeRectangles[i].Height)
        {
          int score = ScoreByHeuristic(width, height, m_freeRectangles[i], rectChoice);

          if (score < bestScore)
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = width;
            bestNode.Height = height;
            bestScore = score;
            rNodeIndex = i;
#if DEBUG
            Debug.Assert(m_disjointRects.Disjoint(bestNode));
#endif
          }
        }
        // Does the rectangle fit sideways?
        else if (height <= m_freeRectangles[i].Width && width <= m_freeRectangles[i].Height)
        {
          int score = ScoreByHeuristic(height, width, m_freeRectangles[i], rectChoice);

          if (score < bestScore)
          {
            bestNode.X = m_freeRectangles[i].X;
            bestNode.Y = m_freeRectangles[i].Y;
            bestNode.Width = height;
            bestNode.Height = width;
            bestScore = score;
            rNodeIndex = i;
#if DEBUG
            Debug.Assert(m_disjointRects.Disjoint(bestNode));
#endif
          }
        }
      }
      return bestNode;
    }


    /// <summary>
    /// Splits the given L-shaped free rectangle into two new free rectangles after placedRect has been placed into it.
    /// Determines the split axis by using the given heuristic.
    /// </summary>
    /// <param name="freeRect"></param>
    /// <param name="placedRect"></param>
    /// <param name="method"></param>
    private void SplitFreeRectByHeuristic(PxRectangleM freeRect, PxRectangleM placedRect, GuillotineSplitHeuristic method)
    {
      // Compute the lengths of the leftover area.
      int w = freeRect.Width - placedRect.Width;
      int h = freeRect.Height - placedRect.Height;

      // Placing placedRect into freeRect results in an L-shaped free area, which must be split into
      // two disjoint rectangles. This can be achieved with by splitting the L-shape using a single line.
      // We have two choices: horizontal or vertical.

      // Use the given heuristic to decide which choice to make.

      bool splitHorizontal;
      switch (method)
      {
        case GuillotineSplitHeuristic.SplitShorterLeftoverAxis:
          // Split along the shorter leftover axis.
          splitHorizontal = (w <= h);
          break;
        case GuillotineSplitHeuristic.SplitLongerLeftoverAxis:
          // Split along the longer leftover axis.
          splitHorizontal = (w > h);
          break;
        case GuillotineSplitHeuristic.SplitMinimizeArea:
          // Maximize the larger area == minimize the smaller area.
          // Tries to make the single bigger rectangle.
          splitHorizontal = (placedRect.Width * h > w * placedRect.Height);
          break;
        case GuillotineSplitHeuristic.SplitMaximizeArea:
          // Maximize the smaller area == minimize the larger area.
          // Tries to make the rectangles more even-sized.
          splitHorizontal = (placedRect.Width * h <= w * placedRect.Height);
          break;
        case GuillotineSplitHeuristic.SplitShorterAxis:
          // Split along the shorter total axis.
          splitHorizontal = (freeRect.Width <= freeRect.Height);
          break;
        case GuillotineSplitHeuristic.SplitLongerAxis:
          // Split along the longer total axis.
          splitHorizontal = (freeRect.Width > freeRect.Height);
          break;
        default:
          splitHorizontal = true;
          Debug.Fail($"Unknown method {method}");
          break;
      }

      // Perform the actual split.
      SplitFreeRectAlongAxis(freeRect, placedRect, splitHorizontal);
    }

    /// <summary>
    // The following functions compute (penalty) score values if a rect of the given size was placed into the
    // given free rectangle. In these score values, smaller is better.

    /// Splits the given L-shaped free rectangle into two new free rectangles along the given fixed split axis.
    /// </summary>
    /// <param name="freeRect"></param>
    /// <param name="placedRect"></param>
    /// <param name="splitHorizontal"></param>
    private void SplitFreeRectAlongAxis(PxRectangleM freeRect, PxRectangleM placedRect, bool splitHorizontal)
    {
      // Form the two new rectangles.
      var bottom = new PxRectangleM();
      bottom.X = freeRect.X;
      bottom.Y = freeRect.Y + placedRect.Height;
      bottom.Height = freeRect.Height - placedRect.Height;

      var right = new PxRectangleM();
      right.X = freeRect.X + placedRect.Width;
      right.Y = freeRect.Y;
      right.Width = freeRect.Width - placedRect.Width;

      if (splitHorizontal)
      {
        bottom.Width = freeRect.Width;
        right.Height = placedRect.Height;
      }
      else // Split vertically
      {
        bottom.Width = placedRect.Width;
        right.Height = freeRect.Height;
      }

      // Add the new rectangles into the free rectangle pool if they weren't degenerate.
      if (bottom.Width > 0 && bottom.Height > 0)
        m_freeRectangles.Add(bottom);
      if (right.Width > 0 && right.Height > 0)
        m_freeRectangles.Add(right);

#if DEBUG
      Debug.Assert(m_disjointRects.Disjoint(bottom));
      Debug.Assert(m_disjointRects.Disjoint(right));
#endif
    }


    /// Performs a Rectangle Merge operation. This procedure looks for adjacent free rectangles and merges them if they
    /// can be represented with a single rectangle. Takes up Theta(|freeRectangles|^2) time.
    private void MergeFreeList()
    {
#if DEBUG
      var test = new DisjointRectCollection();
      for (int i = 0; i < m_freeRectangles.Count; ++i)
      {
        Debug.Assert(test.Add(m_freeRectangles[i]) == true);
      }
#endif

      // Do a Theta(n^2) loop to see if any pair of free rectangles could me merged into one.
      // Note that we miss any opportunities to merge three rectangles into one. (should call this function again to detect that)
      for (int i = 0; i < m_freeRectangles.Count; ++i)
        for (int j = i + 1; j < m_freeRectangles.Count; ++j)
        {
          if (m_freeRectangles[i].Width == m_freeRectangles[j].Width && m_freeRectangles[i].X == m_freeRectangles[j].X)
          {
            if (m_freeRectangles[i].Y == m_freeRectangles[j].Y + m_freeRectangles[j].Height)
            {
              var tmp = m_freeRectangles[i];
              tmp.Y -= m_freeRectangles[j].Height;
              tmp.Height += m_freeRectangles[j].Height;
              m_freeRectangles[i] = tmp;
              m_freeRectangles.RemoveAt(j);
              --j;
            }
            else if (m_freeRectangles[i].Y + m_freeRectangles[i].Height == m_freeRectangles[j].Y)
            {
              var tmp = m_freeRectangles[i];
              tmp.Height += m_freeRectangles[j].Height;
              m_freeRectangles[i] = tmp;
              m_freeRectangles.RemoveAt(j);
              --j;
            }
          }
          else if (m_freeRectangles[i].Height == m_freeRectangles[j].Height && m_freeRectangles[i].Y == m_freeRectangles[j].Y)
          {
            if (m_freeRectangles[i].X == m_freeRectangles[j].X + m_freeRectangles[j].Width)
            {
              var tmp = m_freeRectangles[i];
              tmp.X -= m_freeRectangles[j].Width;
              tmp.Width += m_freeRectangles[j].Width;
              m_freeRectangles[i] = tmp;
              m_freeRectangles.RemoveAt(j);
              --j;
            }
            else if (m_freeRectangles[i].X + m_freeRectangles[i].Width == m_freeRectangles[j].X)
            {
              var tmp = m_freeRectangles[i];
              tmp.Width += m_freeRectangles[j].Width;
              m_freeRectangles[i] = tmp;
              m_freeRectangles.RemoveAt(j);
              --j;
            }
          }
        }

#if DEBUG
      test.Clear();
      for (int i = 0; i < m_freeRectangles.Count; ++i)
      {
        Debug.Assert(test.Add(m_freeRectangles[i]) == true);
      }
#endif
    }
  }
}

