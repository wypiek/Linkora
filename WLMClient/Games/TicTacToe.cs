using System;

namespace WLMClient.Games
{
    /// <summary>
    /// The rules of tic-tac-toe, with no knowledge of who is playing or how moves travel. Both
    /// clients run their own copy and only accept a move their copy agrees is legal, so the two
    /// boards cannot drift apart without one of them noticing.
    /// </summary>
    class TicTacToe
    {
        public const int Empty = 0;
        public const int X = 1;
        public const int O = 2;

        public const int CellCount = 9;

        private static readonly int[][] Lines =
        {
            new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
            new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
            new[] { 0, 4, 8 }, new[] { 2, 4, 6 }
        };

        private readonly int[] cells = new int[CellCount];

        /// <summary>Whose move it is. X always opens.</summary>
        public int Turn { get; private set; }

        /// <summary>X or O once somebody has three in a row, otherwise Empty.</summary>
        public int Winner { get; private set; }

        /// <summary>The three cells that won, or null.</summary>
        public int[] WinningLine { get; private set; }

        public bool IsDraw { get; private set; }

        public bool IsOver
        {
            get { return Winner != Empty || IsDraw; }
        }

        public TicTacToe()
        {
            Turn = X;
        }

        public int this[int cell]
        {
            get { return cells[cell]; }
        }

        /// <summary>Whether the player to move may take this cell.</summary>
        public bool CanPlay(int cell)
        {
            return !IsOver && cell >= 0 && cell < CellCount && cells[cell] == Empty;
        }

        /// <summary>Puts the current player's mark in a cell. Returns false for an illegal move.</summary>
        public bool Play(int cell)
        {
            if (!CanPlay(cell))
            {
                return false;
            }

            cells[cell] = Turn;

            foreach (int[] line in Lines)
            {
                if (cells[line[0]] == Turn && cells[line[1]] == Turn && cells[line[2]] == Turn)
                {
                    Winner = Turn;
                    WinningLine = line;

                    return true;
                }
            }

            IsDraw = Array.IndexOf(cells, Empty) < 0;

            Turn = Turn == X ? O : X;

            return true;
        }

        public static string MarkText(int mark)
        {
            return mark == X ? "X" : mark == O ? "O" : "";
        }
    }
}
