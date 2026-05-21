using Chess.Model;

namespace SuperpositionChess.Controller
{
    public class GameController
    {
        public Board Board;
        public PieceColor CurrentPlayer { get; private set; }
        public bool IsGameOver;
        public string GameResult { get; private set; } = "";
        public Move? LastMove { get; private set; }
        public bool IsPendingConfirmation { get; private set; }

        private Board? boardBeforeMove;
        private Move? pendingMove;
        private List<Move>? pendingNonContactMoves;
        private int pendingFromRow, pendingFromCol, pendingToRow, pendingToColumn;

        public event Action? OnGameStateChanged;
        public event Action<string>? OnGameOver;
        public event Action<string>? OnError;

        public GameController(int gridSize = 8)
        {
            Board = new Board(gridSize);
            CurrentPlayer = PieceColor.White;
        }

        public void StartNewGame()
        {
            Board = new Board(Board.Grid.GetLength(0));
            CurrentPlayer = PieceColor.White;
            IsGameOver = false;
            GameResult = "";
            IsPendingConfirmation = false;
            boardBeforeMove = null;
            pendingMove = null;
            pendingNonContactMoves = null;
            OnGameStateChanged?.Invoke();
        }

        public bool TryMakeMove(int fromRow, int fromColumn, int toRow, int toColumn)
        {
            if (IsGameOver) { OnError?.Invoke("Игра окончена"); return false; }
            if (IsPendingConfirmation) { OnError?.Invoke("Подтвердите или отмените текущий ход"); return false; }

            if (!Board.TryGetPiece(fromRow, fromColumn, out var piece) || piece.Color != CurrentPlayer)
            {
                OnError?.Invoke("Выберите свою фигуру!");
                return false;
            }

            Move? selectedMove = null;
            foreach (var move in GetValidMoves(fromRow, fromColumn))
            {
                if (move.ToRow == toRow && move.ToColumn == toColumn) { selectedMove = move; break; }
            }

            if (selectedMove == null) { OnError?.Invoke("Эта фигура не может ходить!"); return false; }

            boardBeforeMove = Board.Clone();
            pendingFromRow = fromRow;
            pendingFromCol = fromColumn;
            pendingToRow = toRow;
            pendingToColumn = toColumn;

            pendingNonContactMoves = Board.GetNonContactMoves(fromRow, fromColumn, CurrentPlayer);
            pendingMove = selectedMove;
            Board.MakeMove(selectedMove);

            if (pendingNonContactMoves.Any(m => m.ToRow == toRow && m.ToColumn == toColumn))
                Board.CreateSuperposition(selectedMove, pendingNonContactMoves, CurrentPlayer);

            IsPendingConfirmation = true;
            LastMove = selectedMove;
            OnGameStateChanged?.Invoke();
            return true;
        }

        public void ConfirmMove()
        {
            if (!IsPendingConfirmation || pendingMove == null || boardBeforeMove == null) return;

            Board = boardBeforeMove.Clone();
            Board.ClearSuperposition();
            var resolvedMove = Board.ResolveMove(pendingMove);
            Board.MakeMove(resolvedMove);

            if (resolvedMove.ToRow == pendingToRow && resolvedMove.ToColumn == pendingToColumn &&
                pendingNonContactMoves != null &&
                pendingNonContactMoves.Any(m => m.ToRow == resolvedMove.ToRow && m.ToColumn == resolvedMove.ToColumn))
                Board.CreateSuperposition(resolvedMove, pendingNonContactMoves, CurrentPlayer);

            LastMove = resolvedMove;
            IsPendingConfirmation = false;

            var opponent = CurrentPlayer == PieceColor.White ? PieceColor.Black : PieceColor.White;
            if (Board.IsKingCaptured(opponent))
            {
                IsGameOver = true;
                GameResult = CurrentPlayer == PieceColor.White ? "БЕЛЫЕ ПОБЕДИЛИ!" : "ЧЁРНЫЕ ПОБЕДИЛИ!";
                OnGameOver?.Invoke(GameResult);
                OnGameStateChanged?.Invoke();
                return;
            }

            SwitchPlayer();
            boardBeforeMove = null;
            pendingMove = null;
            pendingNonContactMoves = null;
            OnGameStateChanged?.Invoke();
        }

        public void CancelMove()
        {
            if (!IsPendingConfirmation || boardBeforeMove == null) return;

            Board = boardBeforeMove;
            IsPendingConfirmation = false;
            LastMove = null;
            pendingMove = null;
            pendingNonContactMoves = null;
            boardBeforeMove = null;
            OnGameStateChanged?.Invoke();
        }

        public List<Move> GetValidMoves(int row, int column) =>
            Board.GetValidMoves(row, column, CurrentPlayer);

        private void SwitchPlayer() =>
            CurrentPlayer = CurrentPlayer == PieceColor.White ? PieceColor.Black : PieceColor.White;

        public bool TryGetPiece(int row, int column, out Piece? piece) =>
            Board.TryGetPiece(row, column, out piece);
    }
}