using System;
using System.Collections.Generic;
using System.Linq;

namespace WondrousTailsSolver
{
    /// <summary>
    /// Minigame solver.
    /// </summary>
    internal sealed partial class PerfectTails
    {
        private static readonly Random Random = new();
        private readonly Dictionary<int, long[]> possibleBoards = new();
        private readonly Dictionary<int, double[]> sampleProbs = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="PerfectTails"/> class.
        /// </summary>
        public PerfectTails()
        {
            this.CalculateBoards(0, 0, 0, 0, 0);
            this.CalculateSamples();
        }

        /// <summary>
        /// Gets the error response.
        /// </summary>
        public static double[] Error { get; } = new double[] { -1, -1, -1 };

        /// <summary>
        /// Solve the board.
        /// </summary>
        /// <param name="cells">Current board state.</param>
        /// <returns>Array of probabilities.</returns>
        public double[] Solve(bool[] cells)
        {
            var counts = this.Values(cells);

            if (counts == null)
                return Error;

            var divisor = (double)counts[0];
            var probs = counts.Skip(1).Select(c => Math.Round(c / divisor, 4)).ToArray();

            return probs;
        }

        /// <summary>
        /// Get the average of all the potential solutions.
        /// </summary>
        /// <param name="stickersPlaced">Number of stickers placed.</param>
        /// <returns>Sampled probabilities.</returns>
        public double[] GetSample(int stickersPlaced)
        {
            if (this.sampleProbs.TryGetValue(stickersPlaced, out var probs))
                return probs;

            return Error;
        }

        private long[]? Values(bool[] cells)
        {
            var mask = CellsToMask(cells);

            if (this.possibleBoards.TryGetValue(mask, out var counts))
                return counts;

            return null;
        }

        private long[] CalculateBoards(int mask, int numStickers, int numRows, int numCols, int numDiags)
        {
            if (this.possibleBoards.TryGetValue(mask, out var result))
                return result;

            if (numStickers == 9)
            {
                var lines = numRows + numCols + numDiags;
                return this.possibleBoards[mask] = new long[]
                {
                    1,
                    lines >= 1 ? 1 : 0,
                    lines >= 2 ? 1 : 0,
                    lines >= 3 ? 1 : 0,
                };
            }

            if (numStickers > 9)
            {
                return this.possibleBoards[mask] = new long[] { 0, 0, 0, 0 };
            }

            result = this.possibleBoards[mask] = new long[] { 0, 0, 0, 0 };

            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++)
                {
                    if (MaskHasBit(mask, r, c))
                        continue;

                    var nMask = SetMaskBit(mask, r, c);
                    var nRows = MaskHasRow(nMask, r) ? 1 : 0;
                    var nCols = MaskHasCol(nMask, c) ? 1 : 0;
                    var nDiag1 = MaskHasDiag1(nMask) && r == c ? 1 : 0;
                    var nDiag2 = MaskHasDiag2(nMask) && r == 3 - c ? 1 : 0;
                    var nResult = this.CalculateBoards(nMask, numStickers + 1, numRows + nRows, numCols + nCols, numDiags + nDiag1 + nDiag2);

                    for (var i = 0; i < 4; i++)
                    {
                        result[i] += nResult[i];
                    }
                }
            }

            return result;
        }

        private void CalculateSamples()
        {
            for (int stickersPlaced = 1; stickersPlaced <= 7; stickersPlaced++)
            {
                var samples = new List<double[]>();
                for (int i = 0; i < 500; i++)
                {
                    var sampleState = new bool[16];
                    var sampleIndexes = Enumerable.Range(0, 16)
                        .OrderBy(v => Random.Next())
                        .Take(stickersPlaced);

                    foreach (var sampleIndex in sampleIndexes)
                        sampleState[sampleIndex] = true;

                    samples.Add(this.Solve(sampleState));
                }

                this.sampleProbs[stickersPlaced] = new double[]
                {
                    Math.Round(samples.Average(s => s[0]), 4),
                    Math.Round(samples.Average(s => s[1]), 4),
                    Math.Round(samples.Average(s => s[2]), 4),
                };
            }
        }
    }

    /// <summary>
    /// Static calculations.
    /// </summary>
    internal sealed partial class PerfectTails
    {
        private static int CellsToMask(bool[] cells)
        {
            var mask = 0;
            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++)
                {
                    if (cells[(r * 4) + c])
                        mask = SetMaskBit(mask, r, c);
                }
            }

            return mask;
        }

        private static int GetMaskBit(int r, int c)
            => 1 << ((4 * r) + c);

        private static int SetMaskBit(int mask, int r, int c)
            => mask | GetMaskBit(r, c);

        private static bool MaskHasBit(int mask, int r, int c)
            => (mask & GetMaskBit(r, c)) == GetMaskBit(r, c);

        private static bool MaskHasRow(int mask, int r)
            => Enumerable.Range(0, 4).All(c => MaskHasBit(mask, r, c));

        private static bool MaskHasCol(int mask, int c)
            => Enumerable.Range(0, 4).All(r => MaskHasBit(mask, r, c));

        private static bool MaskHasDiag1(int mask)
            => Enumerable.Range(0, 4).All(i => MaskHasBit(mask, i, i));

        private static bool MaskHasDiag2(int mask)
            => Enumerable.Range(0, 4).All(i => MaskHasBit(mask, i, 3 - i));
    }
}
