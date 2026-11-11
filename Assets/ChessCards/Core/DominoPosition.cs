using System;
using System.Collections.Generic;

namespace ChessCards
{
    public readonly struct DominoTile
    {
        public readonly int Left;
        public readonly int Right;
        public DominoTile(int left, int right) { Left = left; Right = right; }
        public int Pips { get { return Left + Right; } }
        public bool Matches(int value) { return Left == value || Right == value; }
        public DominoTile Flipped() { return new DominoTile(Right, Left); }
        public override string ToString() { return Left + "–" + Right; }
    }

    public sealed class DominoPosition
    {
        readonly List<DominoTile>[] racks = { new List<DominoTile>(), new List<DominoTile>() };
        readonly List<DominoTile> stock = new List<DominoTile>();
        readonly List<DominoTile> chain = new List<DominoTile>();

        public IReadOnlyList<DominoTile> Chain { get { return chain; } }
        public int StockCount { get { return stock.Count; } }
        public int LeftEnd { get { return chain.Count == 0 ? -1 : chain[0].Left; } }
        public int RightEnd { get { return chain.Count == 0 ? -1 : chain[chain.Count - 1].Right; } }

        public DominoPosition(Random random)
        {
            for (int left = 0; left <= 6; left++)
                for (int right = left; right <= 6; right++)
                    stock.Add(new DominoTile(left, right));
            for (int i = stock.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                DominoTile tile = stock[i];
                stock[i] = stock[j];
                stock[j] = tile;
            }
            for (int i = 0; i < 7; i++)
            {
                TakeFromStock(Side.White);
                TakeFromStock(Side.Black);
            }
        }

        DominoPosition(DominoPosition source)
        {
            racks[0].AddRange(source.racks[0]);
            racks[1].AddRange(source.racks[1]);
            stock.AddRange(source.stock);
            chain.AddRange(source.chain);
        }

        public DominoPosition Clone() { return new DominoPosition(this); }
        public IReadOnlyList<DominoTile> Rack(Side side) { return racks[(int)side]; }
        public int PipTotal(Side side)
        {
            int total = 0;
            foreach (var tile in Rack(side)) total += tile.Pips;
            return total;
        }

        public bool CanPlace(Side side, int index, bool left)
        {
            var rack = racks[(int)side];
            return index >= 0 && index < rack.Count &&
                (chain.Count == 0 || rack[index].Matches(left ? LeftEnd : RightEnd));
        }

        public bool HasPlacement(Side side)
        {
            foreach (var tile in Rack(side))
            {
                if (chain.Count == 0 || tile.Matches(LeftEnd) || tile.Matches(RightEnd)) return true;
            }
            return false;
        }

        public bool Place(Side side, int index, bool left, out DominoTile placed)
        {
            placed = default;
            if (!CanPlace(side, index, left)) return false;
            var rack = racks[(int)side];
            placed = rack[index];
            if (chain.Count == 0) chain.Add(placed);
            else if (left)
            {
                if (placed.Right != LeftEnd) placed = placed.Flipped();
                chain.Insert(0, placed);
            }
            else
            {
                if (placed.Left != RightEnd) placed = placed.Flipped();
                chain.Add(placed);
            }
            rack.RemoveAt(index);
            return true;
        }

        public bool Draw(Side side)
        {
            if (stock.Count == 0 || HasPlacement(side)) return false;
            TakeFromStock(side);
            return true;
        }

        void TakeFromStock(Side side)
        {
            int last = stock.Count - 1;
            racks[(int)side].Add(stock[last]);
            stock.RemoveAt(last);
        }
    }
}
