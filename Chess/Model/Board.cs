using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Chess.Model
{
    public class Board
    {
        public Piece?[,] Grid { get; private set; }

        public List<(int row, int column)> CurrentPhantoms { get; private set; } = new List<(int, int)>();
        public PieceColor? SuperpositionOwner { get; set; } = null;
        public (int row, int column)? RealPiecePosition { get; set; } = null;

        public int EnPassantRow { get; set; } = -1;
        public int EnPassantColumn { get; set; } = -1;

        public bool WhiteCanCastleKingside { get; set; } = true;
        public bool WhiteCanCastleQueenside { get; set; } = true;
        public bool BlackCanCastleKingside { get; set; } = true;
        public bool BlackCanCastleQueenside { get; set; } = true;

        public Board(int grid)
        {
            Grid = new Piece?[grid, grid];
            InitializeBoard();
        }

        public void InitializeBoard()
        {
            for (int row = 0; row < Grid.GetLength(0); row++)
                for (int column = 0; column < Grid.GetLength(1); column++)
                    Grid[row, column] = null;

            for (int col = 0; col < Grid.GetLength(1); col++)
                Grid[1, col] = new Piece(PieceType.Pawn, PieceColor.Black);

            Grid[0, 0] = new Piece(PieceType.Rook, PieceColor.Black);
            Grid[0, 1] = new Piece(PieceType.Knight, PieceColor.Black);
            Grid[0, 2] = new Piece(PieceType.Bishop, PieceColor.Black);
            Grid[0, 3] = new Piece(PieceType.Queen, PieceColor.Black);
            Grid[0, 4] = new Piece(PieceType.King, PieceColor.Black);
            Grid[0, 5] = new Piece(PieceType.Bishop, PieceColor.Black);
            Grid[0, 6] = new Piece(PieceType.Knight, PieceColor.Black);
            Grid[0, 7] = new Piece(PieceType.Rook, PieceColor.Black);

            for (int col = 0; col < Grid.GetLength(1); col++)
                Grid[6, col] = new Piece(PieceType.Pawn, PieceColor.White);

            Grid[7, 0] = new Piece(PieceType.Rook, PieceColor.White);
            Grid[7, 1] = new Piece(PieceType.Knight, PieceColor.White);
            Grid[7, 2] = new Piece(PieceType.Bishop, PieceColor.White);
            Grid[7, 3] = new Piece(PieceType.Queen, PieceColor.White);
            Grid[7, 4] = new Piece(PieceType.King, PieceColor.White);
            Grid[7, 5] = new Piece(PieceType.Bishop, PieceColor.White);
            Grid[7, 6] = new Piece(PieceType.Knight, PieceColor.White);
            Grid[7, 7] = new Piece(PieceType.Rook, PieceColor.White);

            CurrentPhantoms.Clear();
            SuperpositionOwner = null;
            RealPiecePosition = null;
        }

        public Board Clone()
        {
            var clone = new Board(Grid.GetLength(0));

            for (int row = 0; row < Grid.GetLength(0); row++)
                for (int column = 0; column < Grid.GetLength(1); column++)
                {
                    if (Grid[row, column] != null)
                        clone.Grid[row, column] = Grid[row, column]!.Clone();
                    else clone.Grid[row, column] = null;
                }

            clone.CurrentPhantoms = new List<(int, int)>(CurrentPhantoms);
            clone.SuperpositionOwner = SuperpositionOwner;
            clone.RealPiecePosition = RealPiecePosition;

            clone.EnPassantRow = EnPassantRow;
            clone.EnPassantColumn = EnPassantColumn;
            clone.WhiteCanCastleKingside = WhiteCanCastleKingside;
            clone.WhiteCanCastleQueenside = WhiteCanCastleQueenside;
            clone.BlackCanCastleKingside = BlackCanCastleKingside;
            clone.BlackCanCastleQueenside = BlackCanCastleQueenside;

            return clone;
        }

        public List<Move> GetValidMoves(int row, int column, PieceColor pieceColor)
        {
            var piece = Grid[row, column];
            if (piece == null || piece.Color != pieceColor || piece.State == PieceState.Phantom)
                return new List<Move>();

            var moves = piece.Type switch
            {
                PieceType.Pawn => GetPawnMoves(row, column, pieceColor),
                PieceType.Knight => GetKnightMoves(row, column, pieceColor),
                PieceType.Bishop => GetBishopMoves(row, column, pieceColor),
                PieceType.Rook => GetRookMoves(row, column, pieceColor),
                PieceType.Queen => GetQueenMoves(row, column, pieceColor),
                PieceType.King => GetKingMoves(row, column, pieceColor),
                _ => new List<Move>()
            };

            return moves;
        }

        public void ClearSuperposition()
        {
            if (RealPiecePosition.HasValue)
            {
                var (row, col) = RealPiecePosition.Value;
                if (Grid[row, col]?.State == PieceState.Phantom)
                    Grid[row, col]!.State = PieceState.Real;
            }

            foreach (var (row, col) in CurrentPhantoms)
            {
                if (RealPiecePosition.HasValue &&
                    row == RealPiecePosition.Value.row &&
                    col == RealPiecePosition.Value.column)
                    continue;

                if (Grid[row, col]?.State == PieceState.Phantom)
                    Grid[row, col] = null;
            }

            CurrentPhantoms = new List<(int, int)>();
        }

        public void CreateSuperposition(Move move, List<Move> nonContactMoves, PieceColor playerColor)
        {
            if (nonContactMoves.Count <= 1)
                return;

            var piece = Grid[move.ToRow, move.ToColumn];
            piece!.State = PieceState.Phantom;
            piece.HasMoved = true;
            RealPiecePosition = (move.ToRow, move.ToColumn);
            SuperpositionOwner = playerColor;

            foreach (var m in nonContactMoves)
            {
                if (m.ToRow == move.ToRow && m.ToColumn == move.ToColumn)
                    continue;

                var phantom = new Piece(piece.Type, piece.Color)
                {
                    State = PieceState.Phantom,
                    HasMoved = true
                };
                Grid[m.ToRow, m.ToColumn] = phantom;
                CurrentPhantoms.Add((m.ToRow, m.ToColumn));
            }

            CurrentPhantoms.Add((move.ToRow, move.ToColumn));
        }

        public List<Move> GetNonContactMoves(int row, int column, PieceColor pieceColor)
        {
            var piece = Grid[row, column];
            if (piece == null || piece.Color != pieceColor || piece.State == PieceState.Phantom)
                return new List<Move>();

            var allMoves = GetValidMoves(row, column, pieceColor);

            return allMoves.Where(m =>
                m.CapturedPiece == null &&
                m.IsCastling is false &&
                m.IsEnPassant is false &&
                m.IsPromotion is false
            ).ToList();
        }

        public bool IsKingCaptured(PieceColor kingColor)
        {
            for (var row = 0; row < Grid.GetLength(0); row++)
                for (var column = 0; column < Grid.GetLength(1); column++)
                {
                    if (TryGetPiece(row, column, out Piece piece) &&
                        piece!.Type == PieceType.King &&
                        piece!.Color == kingColor &&
                        piece!.State == PieceState.Real)
                        return false;
                }

            return true;
        }

        private List<Move> GetPawnMoves(int row, int column, PieceColor pieceColor)
        {
            var moves = new List<Move>();
            var piece = Grid[row, column];

            var direction = (pieceColor == PieceColor.White) ? -1 : 1;
            var startRow = (pieceColor == PieceColor.White) ? Grid.GetLength(0) - 2 : 1;
            var metamorphosisRow = (pieceColor == PieceColor.White) ? 0 : Grid.GetLength(0) - 1;

            // Движение вперёд
            var newRow = row + direction;
            if (IsInBounds(newRow, column))
            {
                var target = Grid[newRow, column];
                if (target == null || target.State == PieceState.Phantom)
                {
                    if (target == null || target.Color != pieceColor)
                    {
                        if (newRow == metamorphosisRow)
                            moves.Add(new Move(row, column, newRow, column, piece!, target, isPromotion: true));
                        else
                            moves.Add(new Move(row, column, newRow, column, piece!, target));

                        if (row == startRow)
                        {
                            var newRow2 = row + direction * 2;
                            if (IsInBounds(newRow2, column))
                            {
                                var target2 = Grid[newRow2, column];
                                if ((target2 == null || target2.State == PieceState.Phantom) && (target2 == null || target2.Color != pieceColor))
                                    moves.Add(new Move(row, column, newRow2, column, piece!, target2));
                            }
                        }
                    }
                }
            }

            // Взятие
            var dCols = new int[] { -1, 1 };
            for (var i = 0; i < 2; i++)
            {
                var newCol = column + dCols[i];
                if (IsInBounds(newRow, newCol))
                {
                    var target = Grid[newRow, newCol];
                    if (target != null && target.Color != pieceColor)
                    {
                        if (newRow == metamorphosisRow)
                            moves.Add(new Move(row, column, newRow, newCol, piece!, target, isPromotion: true));
                        else moves.Add(new Move(row, column, newRow, newCol, piece!, target));
                    }
                }
            }

            // Взятие на проходе
            for (var i = 0; i < 2; i++)
            {
                var newCol = column + dCols[i];
                if (EnPassantRow == newRow && EnPassantColumn == newCol)
                    moves.Add(new Move(row, column, newRow, newCol, piece!, isEnPassant: true));
            }

            return moves;
        }

        private List<Move> GetKnightMoves(int row, int column, PieceColor pieceColor)
        {
            var dRows = new int[] { -2, -2, -1, -1, 1, 1, 2, 2 };
            var dCols = new int[] { -1, 1, -2, 2, -2, 2, -1, 1 };
            return GetJumpMoves(row, column, pieceColor, dRows, dCols);
        }

        private List<Move> GetBishopMoves(int row, int column, PieceColor pieceColor)
        {
            var dRows = new int[] { -1, -1, 1, 1 };
            var dCols = new int[] { -1, 1, -1, 1 };
            return GetSlidingMoves(row, column, pieceColor, dRows, dCols);
        }

        private List<Move> GetRookMoves(int row, int column, PieceColor pieceColor)
        {
            var dRows = new int[] { -1, 1, 0, 0 };
            var dCols = new int[] { 0, 0, -1, 1 };
            return GetSlidingMoves(row, column, pieceColor, dRows, dCols);
        }

        private List<Move> GetQueenMoves(int row, int column, PieceColor pieceColor)
        {
            var moves = GetBishopMoves(row, column, pieceColor);
            moves.AddRange(GetRookMoves(row, column, pieceColor));
            return moves;
        }

        private List<Move> GetKingMoves(int row, int column, PieceColor pieceColor)
        {
            var moves = new List<Move>();
            var piece = Grid[row, column];

            var dRows = new int[] { -1, -1, -1, 0, 0, 1, 1, 1 };
            var dCols = new int[] { -1, 0, 1, -1, 1, -1, 0, 1 };

            for (var i = 0; i < dRows.Length; i++)
            {
                var newRow = row + dRows[i];
                var newCol = column + dCols[i];
                if (!IsInBounds(newRow, newCol)) continue;

                var target = Grid[newRow, newCol];
                if (target == null || target.State == PieceState.Phantom)
                    moves.Add(new Move(row, column, newRow, newCol, piece!, target));
                else if (target.Color != pieceColor)
                    moves.Add(new Move(row, column, newRow, newCol, piece!, target));
            }

            // Рокировка
            bool canCastleKingside = pieceColor == PieceColor.White ? WhiteCanCastleKingside : BlackCanCastleKingside;
            bool canCastleQueenside = pieceColor == PieceColor.White ? WhiteCanCastleQueenside : BlackCanCastleQueenside;
            int rookRow = pieceColor == PieceColor.White ? Grid.GetLength(0) - 1 : 0;

            if (canCastleKingside && Grid[rookRow, 7]?.Type == PieceType.Rook && Grid[rookRow, 7]?.Color == pieceColor)
            {
                bool pathClear = true;
                for (int c = column + 1; c < 7; c++)
                {
                    var t = Grid[rookRow, c];
                    if (t != null && t.State == PieceState.Real && t.Color == pieceColor)
                    { pathClear = false; break; }
                }
                if (pathClear)
                    moves.Add(new Move(row, column, rookRow, 6, piece!, isCastling: true));
            }

            if (canCastleQueenside && Grid[rookRow, 0]?.Type == PieceType.Rook && Grid[rookRow, 0]?.Color == pieceColor)
            {
                bool pathClear = true;
                for (int c = column - 1; c > 0; c--)
                {
                    var t = Grid[rookRow, c];
                    if (t != null && t.State == PieceState.Real && t.Color == pieceColor)
                    { pathClear = false; break; }
                }
                if (pathClear)
                    moves.Add(new Move(row, column, rookRow, 2, piece!, isCastling: true));
            }

            return moves;
        }

        private List<Move> GetSlidingMoves(int row, int col, PieceColor color, int[] dRows, int[] dCols)
        {
            var moves = new List<Move>();
            var piece = Grid[row, col];

            for (var dir = 0; dir < dRows.Length; dir++)
            {
                for (var step = 1; step < Grid.GetLength(0); step++)
                {
                    var newRow = row + dRows[dir] * step;
                    var newCol = col + dCols[dir] * step;

                    if (IsInBounds(newRow, newCol) is false)
                        break;

                    var target = Grid[newRow, newCol];

                    if (target == null)
                        moves.Add(new Move(row, col, newRow, newCol, piece!));
                    else if (target.State == PieceState.Phantom)
                        moves.Add(new Move(row, col, newRow, newCol, piece!, target));
                    else if (target.Color != color)
                    {
                        moves.Add(new Move(row, col, newRow, newCol, piece!, target));
                        break;
                    }
                    else break;
                }
            }

            return moves;
        }

        private List<Move> GetJumpMoves(int row, int column, PieceColor pieceColor, int[] dRows, int[] dCols)
        {
            var moves = new List<Move>();
            var piece = Grid[row, column];

            for (var i = 0; i < dRows.Length; i++)
            {
                var newRow = row + dRows[i];
                var newCol = column + dCols[i];

                if (IsInBounds(newRow, newCol) is false)
                    continue;

                var target = Grid[newRow, newCol];
                if (target == null || target.State == PieceState.Phantom)
                    moves.Add(new Move(row, column, newRow, newCol, piece!, target));
                else if (target.Color != pieceColor)
                    moves.Add(new Move(row, column, newRow, newCol, piece!, target));
            }

            return moves;
        }

        public Move ResolveMove(Move desiredMove)
        {
            var piece = Grid[desiredMove.FromRow, desiredMove.FromColumn];
            if (piece == null) return desiredMove;

            // Рокировка
            if (desiredMove.IsCastling)
            {
                int kingRow = desiredMove.FromRow;
                int kingCol = desiredMove.FromColumn;
                int direction = desiredMove.ToColumn == 6 ? 1 : -1;
                int kingEndCol = desiredMove.ToColumn == 6 ? 6 : 2;
                int rookCol = desiredMove.ToColumn == 6 ? 7 : 0;

                int startCol = Math.Min(kingCol, rookCol);
                int endCol = Math.Max(kingCol, rookCol);

                for (int c = startCol; c <= endCol; c++)
                {
                    if (c == kingCol || c == rookCol) continue;

                    var target = Grid[kingRow, c];
                    bool isRealInSuperposition = target != null &&
                                  SuperpositionOwner == target.Color &&
                                  RealPiecePosition.HasValue &&
                                  RealPiecePosition.Value.row == kingRow &&
                                  RealPiecePosition.Value.column == c;
                    bool isBlocking = target != null && (target.State == PieceState.Real || isRealInSuperposition);

                    if (isBlocking)
                        return new Move(desiredMove.FromRow, desiredMove.FromColumn,
                            desiredMove.FromRow, desiredMove.FromColumn, piece);
                }

                return desiredMove;
            }

            // Пешка
            if (piece.Type == PieceType.Pawn && desiredMove.FromColumn == desiredMove.ToColumn)
            {
                int direction = piece.Color == PieceColor.White ? -1 : 1;
                int currRow = desiredMove.FromRow + direction;
                int currCol = desiredMove.FromColumn;

                while (true)
                {
                    if (!IsInBounds(currRow, currCol)) break;

                    var target = Grid[currRow, currCol];
                    bool isRealInSuperposition = target != null &&
                                  SuperpositionOwner == target.Color &&
                                  RealPiecePosition.HasValue &&
                                  RealPiecePosition.Value.row == currRow &&
                                  RealPiecePosition.Value.column == currCol;
                    bool isBlocking = target != null && (target.State == PieceState.Real || isRealInSuperposition);

                    if (isBlocking)
                        return new Move(desiredMove.FromRow, desiredMove.FromColumn,
                            currRow - direction, currCol, piece);

                    if (currRow == desiredMove.ToRow) break;
                    currRow += direction;
                }

                return desiredMove;
            }

            // Скользящие фигуры
            if (piece.Type != PieceType.Bishop && piece.Type != PieceType.Rook && piece.Type != PieceType.Queen)
                return desiredMove;

            int dRow = Math.Sign(desiredMove.ToRow - desiredMove.FromRow);
            int dCol = Math.Sign(desiredMove.ToColumn - desiredMove.FromColumn);
            int currentRow = desiredMove.FromRow + dRow;
            int currentCol = desiredMove.FromColumn + dCol;

            while (true)
            {
                if (!IsInBounds(currentRow, currentCol)) break;

                var target = Grid[currentRow, currentCol];
                bool isRealInSuperposition = target != null &&
                              SuperpositionOwner == target.Color &&
                              RealPiecePosition.HasValue &&
                              RealPiecePosition.Value.row == currentRow &&
                              RealPiecePosition.Value.column == currentCol;
                bool isBlocking = target != null && (target.State == PieceState.Real || isRealInSuperposition);

                if (isBlocking)
                {
                    if (target!.Color != piece.Color)
                    {
                        if (isRealInSuperposition)
                            return new Move(desiredMove.FromRow, desiredMove.FromColumn,
                                currentRow - dRow, currentCol - dCol, piece);
                        else
                            return new Move(desiredMove.FromRow, desiredMove.FromColumn,
                                currentRow, currentCol, piece, target);
                    }
                    else
                        return new Move(desiredMove.FromRow, desiredMove.FromColumn,
                            currentRow - dRow, currentCol - dCol, piece);
                }

                if (currentRow == desiredMove.ToRow && currentCol == desiredMove.ToColumn) break;
                currentRow += dRow;
                currentCol += dCol;
            }

            var finalTarget = Grid[desiredMove.ToRow, desiredMove.ToColumn];
            bool isFinalRealInSuperposition = finalTarget != null &&
                                               SuperpositionOwner == finalTarget.Color &&
                                               RealPiecePosition.HasValue &&
                                               RealPiecePosition.Value.row == desiredMove.ToRow &&
                                               RealPiecePosition.Value.column == desiredMove.ToColumn;
            bool isFinalBlocking = finalTarget != null && (finalTarget.State == PieceState.Real || isFinalRealInSuperposition);

            if (isFinalBlocking)
            {
                if (finalTarget!.Color != piece.Color)
                {
                    if (isFinalRealInSuperposition)
                        return new Move(desiredMove.FromRow, desiredMove.FromColumn,
                            desiredMove.ToRow - dRow, desiredMove.ToColumn - dCol, piece);
                    else
                        return new Move(desiredMove.FromRow, desiredMove.FromColumn,
                            desiredMove.ToRow, desiredMove.ToColumn, piece, finalTarget);
                }
                else
                    return new Move(desiredMove.FromRow, desiredMove.FromColumn,
                        desiredMove.ToRow - dRow, desiredMove.ToColumn - dCol, piece);
            }

            return desiredMove;
        }

        private bool IsInBounds(int row, int col)
        {
            return row >= 0 && row < Grid.GetLength(0) &&
                   col >= 0 && col < Grid.GetLength(1);
        }

        public void MakeMove(Move move)
        {
            EnPassantRow = -1;
            EnPassantColumn = -1;

            if (move.IsPromotion)
            {
                Grid[move.ToRow, move.ToColumn] = new Piece(PieceType.Queen, move.MovedPiece.Color);
                Grid[move.FromRow, move.FromColumn] = null;
                return;
            }

            if (move.IsEnPassant)
            {
                Grid[move.ToRow, move.ToColumn] = Grid[move.FromRow, move.FromColumn];
                Grid[move.FromRow, move.FromColumn] = null;
                Grid[move.FromRow, move.ToColumn] = null;
                return;
            }

            if (move.IsCastling)
            {
                Grid[move.ToRow, move.ToColumn] = Grid[move.FromRow, move.FromColumn];
                Grid[move.FromRow, move.FromColumn] = null;

                if (move.ToColumn == 6)
                {
                    Grid[move.ToRow, 5] = Grid[move.ToRow, 7];
                    Grid[move.ToRow, 7] = null;
                }
                else
                {
                    Grid[move.ToRow, 3] = Grid[move.ToRow, 0];
                    Grid[move.ToRow, 0] = null;
                }

                return;
            }

            if (move.FromRow == move.ToRow && move.FromColumn == move.ToColumn)
            {
                UpdateBoardData(move);
                return;
            }

            Grid[move.ToRow, move.ToColumn] = Grid[move.FromRow, move.FromColumn];
            Grid[move.FromRow, move.FromColumn] = null;

            if (move.CapturedPiece != null && move.CapturedPiece.State == PieceState.Phantom)
                CurrentPhantoms.Remove((move.ToRow, move.ToColumn));

            UpdateBoardData(move);
        }

        private void UpdateBoardData(Move move)
        {
            if (move.MovedPiece.Type == PieceType.Pawn && Math.Abs(move.ToRow - move.FromRow) == 2)
            {
                EnPassantRow = (move.FromRow + move.ToRow) / 2;
                EnPassantColumn = move.FromColumn;
            }

            if (move.MovedPiece.Type == PieceType.King)
            {
                if (move.MovedPiece.Color == PieceColor.White)
                {
                    WhiteCanCastleKingside = false;
                    WhiteCanCastleQueenside = false;
                }
                else
                {
                    BlackCanCastleKingside = false;
                    BlackCanCastleQueenside = false;
                }
            }

            if (move.MovedPiece.Type == PieceType.Rook)
            {
                if (move.FromRow == Grid.GetLength(0) - 1 && move.FromColumn == 0)
                    WhiteCanCastleQueenside = false;
                if (move.FromRow == Grid.GetLength(0) - 1 && move.FromColumn == Grid.GetLength(1) - 1)
                    WhiteCanCastleKingside = false;
                if (move.FromRow == 0 && move.FromColumn == 0)
                    BlackCanCastleQueenside = false;
                if (move.FromRow == 0 && move.FromColumn == Grid.GetLength(1) - 1)
                    BlackCanCastleKingside = false;
            }

            move.MovedPiece.HasMoved = true;
        }

        public bool TryGetPiece(int row, int column, out Piece? piece)
        {
            if (IsInBounds(row, column) && Grid[row, column] != null)
            {
                piece = Grid[row, column];
                return true;
            }

            piece = null;
            return false;
        }

        public string Serialize()
        {
            var data = new List<List<string?>>();
            for (int row = 0; row < Grid.GetLength(0); row++)
            {
                var rowData = new List<string?>();
                for (int col = 0; col < Grid.GetLength(1); col++)
                {
                    var piece = Grid[row, col];
                    if (piece == null) rowData.Add(null);
                    else rowData.Add($"{(int)piece.Type},{(int)piece.Color},{(int)piece.State},{(piece.HasMoved ? 1 : 0)}");
                }
                data.Add(rowData);
            }

            var state = new
            {
                grid = data,
                enPassantRow = EnPassantRow,
                enPassantCol = EnPassantColumn,
                whiteKingside = WhiteCanCastleKingside,
                whiteQueenside = WhiteCanCastleQueenside,
                blackKingside = BlackCanCastleKingside,
                blackQueenside = BlackCanCastleQueenside,
                currentPhantoms = CurrentPhantoms.Select(p => new { p.row, p.column }).ToList(),
                superpositionOwner = SuperpositionOwner == null ? null : ((int)SuperpositionOwner.Value).ToString(),
                realPieceRow = RealPiecePosition?.row ?? -1,
                realPieceCol = RealPiecePosition?.column ?? -1
            };

            return JsonSerializer.Serialize(state);
        }

        public void Deserialize(string json)
        {
            var state = JsonSerializer.Deserialize<JsonElement>(json);

            // Очистка
            for (int r = 0; r < Grid.GetLength(0); r++)
                for (int c = 0; c < Grid.GetLength(1); c++)
                    Grid[r, c] = null;

            CurrentPhantoms.Clear();

            // Восстановление фигур
            var grid = state.GetProperty("grid");
            for (int row = 0; row < grid.GetArrayLength(); row++)
            {
                var rowData = grid[row];
                for (int col = 0; col < rowData.GetArrayLength(); col++)
                {
                    var cell = rowData[col];
                    if (cell.ValueKind == JsonValueKind.Null) continue;

                    var parts = cell.GetString()!.Split(',');
                    var type = (PieceType)int.Parse(parts[0]);
                    var color = (PieceColor)int.Parse(parts[1]);
                    var pieceState = (PieceState)int.Parse(parts[2]);
                    var hasMoved = int.Parse(parts[3]) == 1;

                    Grid[row, col] = new Piece(type, color)
                    {
                        State = pieceState,
                        HasMoved = hasMoved
                    };

                    if (pieceState == PieceState.Phantom)
                        CurrentPhantoms.Add((row, col));
                }
            }

            EnPassantRow = state.GetProperty("enPassantRow").GetInt32();
            EnPassantColumn = state.GetProperty("enPassantCol").GetInt32();
            WhiteCanCastleKingside = state.GetProperty("whiteKingside").GetBoolean();
            WhiteCanCastleQueenside = state.GetProperty("whiteQueenside").GetBoolean();
            BlackCanCastleKingside = state.GetProperty("blackKingside").GetBoolean();
            BlackCanCastleQueenside = state.GetProperty("blackQueenside").GetBoolean();

            var owner = state.GetProperty("superpositionOwner");
            SuperpositionOwner = owner.ValueKind == JsonValueKind.Null ? null : (PieceColor)int.Parse(owner.GetString()!);

            int rpRow = state.GetProperty("realPieceRow").GetInt32();
            int rpCol = state.GetProperty("realPieceCol").GetInt32();
            RealPiecePosition = rpRow >= 0 ? (rpRow, rpCol) : null;
        }
    }
}