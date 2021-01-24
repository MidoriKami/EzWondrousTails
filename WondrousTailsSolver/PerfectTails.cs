using System;
using System.Collections.Generic;
using System.Linq;

namespace WondrousTailsSolver
{
    public sealed class PerfectTails
    {
        public const int TotalStickers = 16;
        public const int StickersPerLane = 4;

        private readonly Dictionary<int, long[]> PossibleBoards = new Dictionary<int, long[]>();
        private readonly Dictionary<int, double[]> SampleProbs = new Dictionary<int, double[]>();

        public PerfectTails()
        {
            CalculateBoards(0, 0, 0, 0, 0);
            CalculateSamples();
        }

        public double[] Solve(bool[] cells)
        {
            var mask = CellsToMask(cells);

            if (PossibleBoards.TryGetValue(mask, out var counts))
            {
                var divisor = (double)counts[0];
                var probs = counts.Skip(1).Select(c => Math.Round(c / divisor, 4)).ToArray();

                return probs;
            }
            else
            {
                return new double[] { -1, -1, -1 };
            }
        }

        public double[] GetSample(int stickersPlaced)
        {
            if (SampleProbs.TryGetValue(stickersPlaced, out var probs))
                return probs;
            return null;
        }

        private long[] CalculateBoards(int mask, int numStickers, int numRows, int numCols, int numDiags)
        {
            if (PossibleBoards.TryGetValue(mask, out var result))
                return result;

            if (numStickers > 9)
            {
                return new long[] { 0, 0, 0, 0 };
            }

            if (numStickers == 9)
            {
                var lines = numRows + numCols + numDiags;
                return PossibleBoards[mask] = new long[] {
                    1,
                    lines >= 1 ? 1 : 0,
                    lines >= 2 ? 1 : 0,
                    lines >= 3 ? 1 : 0
                };
            }

            result = PossibleBoards[mask] = new long[] { 0, 0, 0, 0 };

            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++)
                {
                    if (MaskHasBit(mask, r, c))
                        continue;

                    var nMask = SetMaskBit(mask, r, c);
                    var nRows = MaskHasRow(nMask, r) ? 1 : 0;
                    var nCols = MaskHasCol(nMask, c) ? 1 : 0;
                    var nDiag1 = MaskHasDiag1(nMask) ? 1 : 0;
                    var nDiag2 = MaskHasDiag2(nMask) ? 1 : 0;
                    var nResult = CalculateBoards(nMask, numStickers + 1, numRows + nRows, numCols + nCols, numDiags + nDiag1 + nDiag2);

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
            Random random = new Random();
            for (int stickersPlaced = 1; stickersPlaced <= 7; stickersPlaced++)
            {
                var samples = new List<double[]>();
                for (int i = 0; i < 50; i++)
                {
                    var sampleState = new bool[16];
                    foreach (var sampleIndex in Enumerable.Range(0, 16).OrderBy(v => random.Next()).Take(stickersPlaced))
                        sampleState[sampleIndex] = true;
                    samples.Add(Solve(sampleState));
                }
                var avgSolution = new double[]
                {
                    Math.Round(samples.Average(s => s[0]), 4),
                    Math.Round(samples.Average(s => s[1]), 4),
                    Math.Round(samples.Average(s => s[2]), 4),
                };
                SampleProbs[stickersPlaced] = avgSolution;
            }
        }

        private int CellsToMask(bool[] cells)
        {
            var mask = 0;
            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++)
                {
                    if (cells[r * 4 + c])
                        mask = SetMaskBit(mask, r, c);
                }
            }
            return mask;
        }

        private int GetMaskBit(int r, int c) => 1 << (4 * r + c);

        private int SetMaskBit(int mask, int r, int c) => mask | GetMaskBit(r, c);

        private bool MaskHasBit(int mask, int r, int c) => (mask & GetMaskBit(r, c)) == GetMaskBit(r, c);

        private bool MaskHasRow(int mask, int r) => Enumerable.Range(0, 4).All(c => MaskHasBit(mask, r, c));

        private bool MaskHasCol(int mask, int c) => Enumerable.Range(0, 4).All(r => MaskHasBit(mask, r, c));

        private bool MaskHasDiag1(int mask) => Enumerable.Range(0, 4).All(i => MaskHasBit(mask, i, i));

        private bool MaskHasDiag2(int mask) => Enumerable.Range(0, 4).All(i => MaskHasBit(mask, i, 3 - i));
    }
}
