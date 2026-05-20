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
        public bool IsPendingConfirmation { get; private set; } = false;

        private Board? _boardBeforeMove;
        private Move? _pendingMove;
        private List<Move>? _pendingNonContactMoves;
        private int _pendingFromRow, _pendingFromCol, _pendingToRow, _pendingToColumn;

        public event Action? OnGameStateChanged;
        public event Action<string>? OnGameOver;
        public event Action<string>? OnError;

        public GameController(int gridSize = 8)
        {
            Board = new Board(gridSize);
            CurrentPlayer = PieceColor.White;
            IsGameOver = false;
        }

        public void StartNewGame()
        {
            Board = new Board(Board.Grid.GetLength(0));
            CurrentPlayer = PieceColor.White;
            IsGameOver = false;
            GameResult = "";
            IsPendingConfirmation = false;
            _boardBeforeMove = null;
            _pendingMove = null;
            _pendingNonContactMoves = null;

            OnGameStateChanged?.Invoke();
        }

        public bool TryMakeMove(int fromRow, int fromColumn, int toRow, int toColumn)
        {
            if (IsGameOver)
            {
                OnError?.Invoke("Игра Окончена");
                return false;
            }

            if (IsPendingConfirmation)
            {
                OnError?.Invoke("Подтвердите или отмените текущий ход");
                return false;
            }

            if (!Board.TryGetPiece(fromRow, fromColumn, out Piece? piece) || piece.Color != CurrentPlayer)
            {
                OnError?.Invoke("Выберите свою фигуру!");
                return false;
            }

            Move? selectedMove = null;
            foreach (var move in GetValidMoves(fromRow, fromColumn))
                if (move.ToRow == toRow && move.ToColumn == toColumn)
                {
                    selectedMove = move;
                    break;
                }

            if (selectedMove == null)
            {
                OnError?.Invoke("Эта фигура не может ходить!");
                return false;
            }

            // Сохраняем состояние до хода
            _boardBeforeMove = Board.Clone();
            _pendingFromRow = fromRow;
            _pendingFromCol = fromColumn;
            _pendingToRow = toRow;
            _pendingToColumn = toColumn;

            // Показываем ход БЕЗ очистки суперпозиции противника
            _pendingNonContactMoves = Board.GetNonContactMoves(fromRow, fromColumn, CurrentPlayer);
            _pendingMove = selectedMove;
            Board.MakeMove(selectedMove);

            // Создаём суперпозицию
            if (_pendingNonContactMoves.Any(m => m.ToRow == toRow && m.ToColumn == toColumn))
                Board.CreateSuperposition(selectedMove, _pendingNonContactMoves, CurrentPlayer);

            IsPendingConfirmation = true;
            LastMove = selectedMove;
            OnGameStateChanged?.Invoke();
            return true;
        }

        public void ConfirmMove()
        {
            if (!IsPendingConfirmation || _pendingMove == null || _boardBeforeMove == null) return;

            // Откатываем ход и суперпозицию
            Board = _boardBeforeMove.Clone();

            // Заново: очистка, резолв, выполнение
            Board.ClearSuperposition();
            var resolvedMove = Board.ResolveMove(_pendingMove);
            Board.MakeMove(resolvedMove);

            // Суперпозиция только если не было столкновения
            if (resolvedMove.ToRow == _pendingToRow && resolvedMove.ToColumn == _pendingToColumn &&
                _pendingNonContactMoves != null &&
                _pendingNonContactMoves.Any(m => m.ToRow == resolvedMove.ToRow && m.ToColumn == resolvedMove.ToColumn))
                Board.CreateSuperposition(resolvedMove, _pendingNonContactMoves, CurrentPlayer);

            LastMove = resolvedMove;
            IsPendingConfirmation = false;

            var opponent = CurrentPlayer == PieceColor.White ? PieceColor.Black : PieceColor.White;
            if (Board.IsKingCaptured(opponent))
            {
                IsGameOver = true;
                GameResult = $"Победили {CurrentPlayer}!";
                OnGameOver?.Invoke(GameResult);
                OnGameStateChanged?.Invoke();
                return;
            }

            SwitchPlayer();
            _boardBeforeMove = null;
            _pendingMove = null;
            _pendingNonContactMoves = null;
            OnGameStateChanged?.Invoke();
        }

        public void CancelMove()
        {
            if (!IsPendingConfirmation || _boardBeforeMove == null) return;

            Board = _boardBeforeMove;
            IsPendingConfirmation = false;
            LastMove = null;
            _pendingMove = null;
            _pendingNonContactMoves = null;
            _boardBeforeMove = null;

            OnGameStateChanged?.Invoke();
        }

        public List<Move> GetValidMoves(int row, int column)
        {
            return Board.GetValidMoves(row, column, CurrentPlayer);
        }

        private void SwitchPlayer()
        {
            CurrentPlayer = CurrentPlayer == PieceColor.White ? PieceColor.Black : PieceColor.White;
        }

        public bool TryGetPiece(int row, int column, out Piece? piece)
        {
            return Board.TryGetPiece(row, column, out piece);
        }
    }
}