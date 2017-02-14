using System;
using SlimDX;

namespace AcTools.Render.Base.Sprites {
    /// <summary>
    /// Defines, in which area a specific text is rendered
    /// </summary>
    /// <remarks>
    /// <para>The textblock is the area filled with actual characters without any overhang.</para>
    /// <para>Overhangs enlarge the textblock rectangle. I.e. if OverhangLeft is 10, then there are parts of the text that are rendered 10 units left of the actual text block.
    /// However, these parts do not count as real text.
    /// If an overhang is negative, there is no letter, which actually reaches the textblock edge. Thus, the textblock is rendered on a smaller area.</para>
    /// <para>The full rect is the actual rendering space. I.e. the textblock with overhangs.</para>
    /// </remarks>
    /// <example>
    /// <para>Consider the following example. The string "Example Text" has been drawn at position (20, 40).</para>
    /// <img src="../Blocks.jpg" alt="Block structure"/>
    /// <para>The light red block is the text block. This is the layout rectangle. Text blocks containing one line
    /// usually have the same height - the line height. Therefore, text blocks can easily be concatenated without
    /// worrying about layout.</para>
    /// <para>The dark red block is the actual FullRect. This is the rectangle that exactly fits the rendered text.
    /// The difference between text block and full rect is described by the overhangs. If an overhang is positive,
    /// then the full rect is bigger than the textblock (as for the right side). If it is negative, the full rect
    /// is smaller (as for the other sides).</para>
    /// <para>
    /// Here are the actual values for the example:
    /// <ul>
    /// <li>TopLeft: (20, 40)</li>
    /// <li>Size: (449.17, 117.9)</li>
    /// <li>OverhangLeft: -14.48</li>
    /// <li>OverhangRight: 12.30</li>
    /// <li>OverhangTop: -15.06</li>
    /// <li>OverhangBottom: -4.54</li>
    /// </ul>
    /// </para>
    /// </example>
    public class StringMetrics {
        /// <summary>
        /// Top left corner of the textblock.
        /// </summary>
        public Vector2 TopLeft { get; set; }

        /// <summary>
        /// Size of the textblock.
        /// </summary>
        public Vector2 Size { get; set; }

        /// <summary>
        /// Returns the bottom right corner of the textblock
        /// </summary>
        public Vector2 BottomRight => TopLeft + Size;

        /// <summary>
        /// The space that is added to the textblock by overhangs on the left side.
        /// </summary>
        public float OverhangLeft { get; set; }

        /// <summary>
        /// The space that is added to the textblock by overhangs on the right side.
        /// </summary>
        public float OverhangRight { get; set; }

        /// <summary>
        /// The space that is added above the textblock by overhangs.
        /// </summary>
        public float OverhangTop { get; set; }

        /// <summary>
        /// The space that is added below the textblock by overhangs.
        /// </summary>
        public float OverhangBottom { get; set; }

        /// <summary>
        /// The top left corner of the full rect.
        /// </summary>
        public Vector2 FullRectTopLeft => new Vector2(
                TopLeft.X - OverhangLeft * (Size.X < 0 ? -1 : 1),
                TopLeft.Y - OverhangTop * (Size.Y < 0 ? -1 : 1));

        /// <summary>
        /// The size of the full rect.
        /// </summary>
        public Vector2 FullRectSize => new Vector2(
                Size.X + (OverhangLeft + OverhangRight) * (Size.X < 0 ? -1 : 1),
                Size.Y + (OverhangTop + OverhangBottom) * (Size.Y < 0 ? -1 : 1));

        /// <summary>
        /// Merges this instance of StringMetrics with another instance. 
        /// The textblock and overhangs of this instance will be increased to cover both instances.
        /// </summary>
        /// <param name="second">The second StringMetrics instance. This object will not be changed.</param>
        /// <exception cref="System.ArgumentException">Thrown when one instance has flipped axes and the other does not.</exception>
        public void Merge(StringMetrics second) {
            // If current instance has no values yet, take the values of the second instance
            if (Size.X == 0 && Size.Y == 0) {
                TopLeft = second.TopLeft;
                Size = second.Size;
                OverhangLeft = second.OverhangLeft;
                OverhangRight = second.OverhangRight;
                OverhangTop = second.OverhangTop;
                OverhangBottom = second.OverhangBottom;
                return;
            }

            // If second instance is not visible, do nothing
            if (second.FullRectSize.X == 0 && second.FullRectSize.Y == 0) return;

            // Flipped y axis means that positive y points upwards
            // Flipped x axis means that positive x points to the right
            var xAxisFlipped = Size.X < 0;
            var yAxisFlipped = Size.Y < 0;

            // Check, if axes of both instances point in the same direction
            if (Size.X * second.Size.X < 0)
                throw new ArgumentException("The x-axis of the current instance is " +
                        (xAxisFlipped ? "" : "not ") + "flipped. The x-axis of the second instance has to point in the same direction");
            if (Size.Y * second.Size.Y < 0)
                throw new ArgumentException("The y-axis of the current instance is " +
                        (yAxisFlipped ? "" : "not ") + "flipped. The y-axis of the second instance has to point in the same direction");

            // Update flipped info if it cannot be obtained from the current instance
            if (Size.X == 0) xAxisFlipped = second.Size.X < 0;
            if (Size.Y == 0) yAxisFlipped = second.Size.Y < 0;

            // Find the functions to determine the topmost of two values and so on
            Func<float, float, float> findTopMost, findBottomMost;
            Func<float, float, float> findLeftMost, findRightMost;
            if (yAxisFlipped) {
                findTopMost = Math.Max;
                findBottomMost = Math.Min;
            } else {
                findTopMost = Math.Min;
                findBottomMost = Math.Max;
            }

            if (xAxisFlipped) {
                findLeftMost = Math.Max;
                findRightMost = Math.Min;
            } else {
                findLeftMost = Math.Min;
                findRightMost = Math.Max;
            }

            // Find new textblock
            var top = findTopMost(TopLeft.Y, second.TopLeft.Y);
            var bottom = findBottomMost(TopLeft.Y + Size.Y, second.TopLeft.Y + second.Size.Y);
            var left = findLeftMost(TopLeft.X, second.TopLeft.X);
            var right = findRightMost(TopLeft.X + Size.X, second.TopLeft.X + second.Size.X);

            // Find new overhangs
            var topOverhangPos = findTopMost(FullRectTopLeft.Y, second.FullRectTopLeft.Y);
            var bottomOverhangPos = findBottomMost(FullRectTopLeft.Y + FullRectSize.Y, second.FullRectTopLeft.Y + second.FullRectSize.Y);
            var leftOverhangPos = findLeftMost(FullRectTopLeft.X, second.FullRectTopLeft.X);
            var rightOverhangPos = findRightMost(FullRectTopLeft.X + FullRectSize.X, second.FullRectTopLeft.X + second.FullRectSize.X);

            TopLeft = new Vector2(left, top);
            Size = new Vector2(right - left, bottom - top);
            OverhangLeft = (left - leftOverhangPos) * (xAxisFlipped ? -1 : 1);
            OverhangRight = (rightOverhangPos - right) * (xAxisFlipped ? -1 : 1);
            OverhangTop = (top - topOverhangPos) * (yAxisFlipped ? -1 : 1);
            OverhangBottom = (bottomOverhangPos - bottom) * (yAxisFlipped ? -1 : 1);
        }
    }
}
