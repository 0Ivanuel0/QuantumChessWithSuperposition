namespace Chess.Model
{
    public class Move
    {
        public int FromRow {  get; set; }
        public int FromColumn { get; set; }
        public int ToRow { get; set; }
        public int ToColumn { get; set; }

        public Piece MovedPiece { get; set; }
        public Piece? CapturedPiece { get; set; }

        public bool IsCastling { get; set; }
        public bool IsEnPassant { get; set; }
        public bool IsPromotion { get; set; }

        public Move(int fromRow, int fromColumn, int toRow, int toColumn,
            Piece movedPiece, Piece? capturedPiece = null,
            bool isCastling = false, bool isEnPassant = false, bool isPromotion = false)
        {
            FromRow = fromRow;
            FromColumn = fromColumn;
            ToRow = toRow;
            ToColumn = toColumn;

            MovedPiece = movedPiece;
            CapturedPiece = capturedPiece;

            IsCastling = isCastling;
            IsEnPassant = isEnPassant;
            IsPromotion = isPromotion;
        }
    }
}
