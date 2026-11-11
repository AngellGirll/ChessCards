using System;
using System.Collections.Generic;

namespace ChessCards
{
    public sealed class GoPosition
    {
        public const int Size = 9;
        public const int PointCount = Size * Size;
        public const double Komi = 7.5;
        static readonly int[][] Neighbors = MakeNeighbors();

        readonly HashSet<string> seen;
        byte[] stones;
        public Side? LastPass { get; private set; }
        public int LastPoint { get; private set; } = -1;
        public int Occupied { get; private set; }

        public GoPosition()
        {
            stones = new byte[PointCount];
            seen = new HashSet<string> { Key(stones) };
        }

        GoPosition(GoPosition source)
        {
            stones = (byte[])source.stones.Clone();
            seen = new HashSet<string>(source.seen);
            LastPass = source.LastPass;
            LastPoint = source.LastPoint;
            Occupied = source.Occupied;
        }

        public GoPosition Clone() { return new GoPosition(this); }
        public Side? StoneAt(int point)
        {
            if (stones[point] == 0) return null;
            return stones[point] == 1 ? Side.White : Side.Black;
        }

        public bool CanPlace(Side side, int point)
        {
            return TryPosition(side, point, out _, out _, out _);
        }

        public bool Place(Side side, int point, out int captured)
        {
            if (!TryPosition(side, point, out byte[] next, out captured, out _)) return false;
            stones = next;
            seen.Add(Key(stones));
            Occupied += 1 - captured;
            LastPass = null;
            LastPoint = point;
            return true;
        }

        public bool Pass(Side side)
        {
            bool finished = LastPass == ChessPosition.Other(side);
            LastPass = side;
            return finished;
        }

        public int Area(Side side)
        {
            byte color = Color(side);
            int result = 0;
            bool[] visited = new bool[PointCount];
            var region = new List<int>(PointCount);
            for (int point = 0; point < PointCount; point++)
            {
                if (stones[point] == color) result++;
                if (stones[point] != 0 || visited[point]) continue;
                region.Clear();
                region.Add(point);
                visited[point] = true;
                int borders = 0;
                for (int i = 0; i < region.Count; i++)
                {
                    foreach (int neighbor in Neighbors[region[i]])
                    {
                        byte stone = stones[neighbor];
                        if (stone != 0) borders |= 1 << stone;
                        else if (!visited[neighbor])
                        {
                            visited[neighbor] = true;
                            region.Add(neighbor);
                        }
                    }
                }
                if (borders == (1 << color)) result += region.Count;
            }
            return result;
        }

        public int StoneBalance(Side side)
        {
            byte color = Color(side);
            int balance = 0;
            foreach (byte stone in stones)
            {
                if (stone != 0) balance += stone == color ? 1 : -1;
            }
            return balance;
        }

        internal int ChoosePoint(Side side, Random random)
        {
            if (LastPass == ChessPosition.Other(side) && Occupied >= 12)
            {
                double difference = Area(Side.Black) - Area(Side.White) - Komi;
                if (side == Side.Black ? difference > 0 : difference < 0) return -1;
            }

            int best = -1;
            double bestScore = double.NegativeInfinity;
            byte color = Color(side);
            for (int point = 0; point < PointCount; point++)
            {
                if (!TryPosition(side, point, out _, out int captures, out int liberties)) continue;
                int friendly = 0, enemy = 0;
                foreach (int neighbor in Neighbors[point])
                {
                    if (stones[neighbor] == color) friendly++;
                    else if (stones[neighbor] != 0) enemy++;
                }
                if (friendly == Neighbors[point].Length && captures == 0) continue;
                int x = point % Size, y = point / Size;
                int edge = Math.Min(Math.Min(x, Size - 1 - x), Math.Min(y, Size - 1 - y));
                double score = 18 * captures + Math.Min(liberties, 5) * 2 + enemy * 2
                    + Math.Min(friendly, 2) + Math.Min(edge, 2) - (liberties == 1 ? 13 : 0)
                    + random.NextDouble() * 2;
                if (score <= bestScore) continue;
                best = point;
                bestScore = score;
            }
            return Occupied >= 24 && bestScore < 0 ? -1 : best;
        }

        bool TryPosition(Side side, int point, out byte[] next, out int captured, out int liberties)
        {
            next = null;
            captured = liberties = 0;
            if (point < 0 || point >= PointCount || stones[point] != 0) return false;
            byte color = Color(side);
            byte enemy = Color(ChessPosition.Other(side));
            next = (byte[])stones.Clone();
            next[point] = color;
            var group = new List<int>(PointCount);
            foreach (int neighbor in Neighbors[point])
            {
                if (next[neighbor] != enemy || GroupLiberties(next, neighbor, group) != 0) continue;
                foreach (int member in group) next[member] = 0;
                captured += group.Count;
            }
            liberties = GroupLiberties(next, point, group);
            return liberties > 0 && !seen.Contains(Key(next));
        }

        static int GroupLiberties(byte[] board, int start, List<int> group)
        {
            group.Clear();
            group.Add(start);
            bool[] visited = new bool[PointCount];
            visited[start] = true;
            int liberties = 0;
            byte color = board[start];
            for (int i = 0; i < group.Count; i++)
            {
                foreach (int neighbor in Neighbors[group[i]])
                {
                    if (visited[neighbor]) continue;
                    visited[neighbor] = true;
                    if (board[neighbor] == 0) liberties++;
                    else if (board[neighbor] == color) group.Add(neighbor);
                }
            }
            return liberties;
        }

        static byte Color(Side side) { return side == Side.White ? (byte)1 : (byte)2; }
        static string Key(byte[] board)
        {
            var key = new char[PointCount];
            for (int i = 0; i < PointCount; i++) key[i] = (char)board[i];
            return new string(key);
        }
        static int[][] MakeNeighbors()
        {
            var result = new int[PointCount][];
            for (int i = 0; i < PointCount; i++)
            {
                var adjacent = new List<int>(4);
                if (i % Size > 0) adjacent.Add(i - 1);
                if (i % Size < Size - 1) adjacent.Add(i + 1);
                if (i / Size > 0) adjacent.Add(i - Size);
                if (i / Size < Size - 1) adjacent.Add(i + Size);
                result[i] = adjacent.ToArray();
            }
            return result;
        }
    }
}
