namespace Chess.Model
{
    public enum PieceType
    {
        Pawn,
        Knight,
        Bishop,
        Rook,
        Queen,
        King,
    }

    public enum PieceState
    {
        Real,
        Phantom,
    }

    public enum PieceColor
    {
        White,
        Black,
    }

    public class Piece
    {
        public PieceType Type { get; set; }
        public PieceState State { get; set; }
        public PieceColor Color { get; set; }
        public bool HasMoved { get; set; }

        public Piece(PieceType type, PieceColor color)
        {
            Type = type;
            State = PieceState.Real;
            Color = color;
            HasMoved = false;
        }

        public char GetSymbol()
        {
            var symbol = Type switch
            {
                PieceType.Pawn => 'P',
                PieceType.Knight => 'N',
                PieceType.Bishop => 'B',
                PieceType.Rook => 'R',
                PieceType.Queen => 'Q',
                PieceType.King => 'K',
                _ => '?',
            };

            if (Color == PieceColor.White)
                return symbol;
            else return char.ToLower(symbol);
        }

        public string GetUnicodeSymbol()
        {
            if (Color == PieceColor.White)
                return Type switch
                {
                    PieceType.King => "♔",
                    PieceType.Queen => "♕",
                    PieceType.Rook => "♖",
                    PieceType.Bishop => "♗",
                    PieceType.Knight => "♘",
                    PieceType.Pawn => "♙",
                    _ => "?"
                };
            else
                return Type switch
                {
                    PieceType.King => "♚",
                    PieceType.Queen => "♛",
                    PieceType.Rook => "♜",
                    PieceType.Bishop => "♝",
                    PieceType.Knight => "♞",
                    PieceType.Pawn => "♟",
                    _ => "?"
                };
        }

        public Piece Clone()
        {
            var clone = new Piece(this.Type, this.Color);
            clone.State = this.State;
            clone.HasMoved = this.HasMoved;
            return clone;
        }
    }
}
