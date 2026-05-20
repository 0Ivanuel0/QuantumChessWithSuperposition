using Chess.Model;
using SuperpositionChess.Controller;

namespace Chess.AI
{
    public enum BotDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    public class QuantumBot
    {
        private GameController _controller;
        private PieceColor _botColor;
        private BotDifficulty _difficulty;
        private Random _random = new Random();

        public QuantumBot(GameController controller, PieceColor botColor, BotDifficulty difficulty)
        {
            _controller = controller;
            _botColor = botColor;
            _difficulty = difficulty;
        }

        public void MakeMove()
        {
            if (_controller.CurrentPlayer != _botColor || _controller.IsGameOver || _controller.IsPendingConfirmation)
                return;

            var allMoves = GetAllPossibleMoves();

            if (allMoves.Count == 0) return;

            Move? bestMove = null;
            int fromRow = 0, fromCol = 0;

            switch (_difficulty)
            {
                case BotDifficulty.Easy:
                    (fromRow, fromCol, bestMove) = GetEasyMove(allMoves);
                    break;
                case BotDifficulty.Medium:
                    (fromRow, fromCol, bestMove) = GetMediumMove(allMoves);
                    break;
                case BotDifficulty.Hard:
                    (fromRow, fromCol, bestMove) = GetHardMove(allMoves);
                    break;
            }

            if (bestMove == null) return;

            _controller.TryMakeMove(fromRow, fromCol, bestMove.ToRow, bestMove.ToColumn);
            _controller.ConfirmMove();
        }

        private List<(int fromRow, int fromCol, Move move)> GetAllPossibleMoves()
        {
            var moves = new List<(int, int, Move)>();

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if (_controller.TryGetPiece(row, col, out var piece) &&
                        piece!.Color == _botColor &&
                        piece.State == PieceState.Real)
                    {
                        var validMoves = _controller.Board.GetValidMoves(row, col, _botColor);
                        foreach (var move in validMoves)
                            moves.Add((row, col, move));
                    }
                }
            }

            return moves;
        }

        // Лёгкий: совсем случайный, даже не проверяет что ход безопасный
        private (int, int, Move) GetEasyMove(List<(int fromRow, int fromCol, Move move)> moves)
        {
            // 30% шанс сделать плохой ход (случайный)
            var chosen = moves[_random.Next(moves.Count)];
            return chosen;
        }

        // Средний: атакует фантомов (может попасть в реальную), создаёт суперпозицию, берёт короля
        private (int, int, Move) GetMediumMove(List<(int fromRow, int fromCol, Move move)> moves)
        {
            var scoredMoves = moves.Select(m => (m.fromRow, m.fromCol, m.move, score: ScoreMoveMedium(m.move))).ToList();

            // Добавляем случайность ±20%
            for (int i = 0; i < scoredMoves.Count; i++)
            {
                var m = scoredMoves[i];
                m.score += (float)(_random.NextDouble() * 0.4 - 0.2) * m.score;
                scoredMoves[i] = m;
            }

            var best = scoredMoves.OrderByDescending(m => m.score).First();
            return (best.fromRow, best.fromCol, best.move);
        }

        private float ScoreMoveMedium(Move move)
        {
            float score = 0;

            if (move.CapturedPiece != null)
            {
                score += GetPieceValue(move.CapturedPiece) * 0.5f;
            }

            if (move.IsPromotion)
                score += 5;

            // Добавляем много случайности (±50%)
            score += (float)(_random.NextDouble() - 0.5) * 4;

            return Math.Max(0, score);
        }

        // Сложный: просчитывает вероятности, оценивает позиции врага
        private (int, int, Move) GetHardMove(List<(int fromRow, int fromCol, Move move)> moves)
        {
            var scoredMoves = moves.Select(m => (m.fromRow, m.fromCol, m.move, score: ScoreMoveHard(m.move))).ToList();

            var best = scoredMoves.OrderByDescending(m => m.score).First();
            return (best.fromRow, best.fromCol, best.move);
        }

        private float ScoreMoveHard(Move move)
        {
            float score = ScoreMoveMedium(move);

            // Оценка позиции после хода (глубина 1)
            var boardClone = _controller.Board.Clone();
            boardClone.ClearSuperposition();
            var resolvedMove = boardClone.ResolveMove(move);
            boardClone.MakeMove(resolvedMove);

            // Проверяем, не подставили ли короля
            bool kingSafe = true;
            var myKing = FindKingInBoard(boardClone, _botColor);
            if (myKing.HasValue)
            {
                kingSafe = !IsCellAttackedInBoard(boardClone, myKing.Value.row, myKing.Value.col, _botColor);
            }

            if (!kingSafe)
                score -= 50;

            // Бонус за атаку вражеского короля
            var enemyKing = FindKingInBoard(boardClone, _botColor == PieceColor.White ? PieceColor.Black : PieceColor.White);
            if (enemyKing.HasValue)
            {
                if (IsCellAttackedInBoard(boardClone, enemyKing.Value.row, enemyKing.Value.col,
                    _botColor == PieceColor.White ? PieceColor.Black : PieceColor.White))
                    score += 20; // Шах!
            }

            return score;
        }

        private float GetPieceValue(Piece piece)
        {
            return piece.Type switch
            {
                PieceType.Pawn => 1,
                PieceType.Knight => 3,
                PieceType.Bishop => 3,
                PieceType.Rook => 5,
                PieceType.Queen => 9,
                PieceType.King => 100,
                _ => 0
            };
        }

        private (int row, int col)? FindEnemyKing()
        {
            var enemyColor = _botColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
            return FindKingInBoard(_controller.Board, enemyColor);
        }

        private (int row, int col)? FindKingInBoard(Board board, PieceColor color)
        {
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                {
                    if (board.TryGetPiece(row, col, out var piece) &&
                        piece!.Type == PieceType.King &&
                        piece.Color == color &&
                        piece.State == PieceState.Real)
                        return (row, col);
                }
            return null;
        }

        private bool IsCellAttackedInBoard(Board board, int row, int col, PieceColor defenderColor)
        {
            var attackerColor = defenderColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    if (board.TryGetPiece(r, c, out var piece) &&
                        piece!.Color == attackerColor &&
                        piece.State == PieceState.Real)
                    {
                        var moves = board.GetValidMoves(r, c, attackerColor);
                        if (moves.Any(m => m.ToRow == row && m.ToColumn == col))
                            return true;
                    }
                }
            return false;
        }

        private float Distance(int r1, int c1, int r2, int c2)
        {
            return Math.Abs(r1 - r2) + Math.Abs(c1 - c2);
        }
    }
}