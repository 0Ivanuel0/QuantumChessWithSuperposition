using Chess.Model;
using SuperpositionChess.Controller;

namespace Chess.AI
{
    public enum BotDifficulty
    {
        Easy,
        Medium,
        Hard,
        Cheater
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
                case BotDifficulty.Cheater:
                    (fromRow, fromCol, bestMove) = GetCheaterMove(allMoves);
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

        // ==================== ЛЁГКИЙ ====================
        private (int, int, Move) GetEasyMove(List<(int fromRow, int fromCol, Move move)> moves)
        {
            // Приоритет: взятие короля > взятие фигуры > случайный
            var kingCapture = moves.FirstOrDefault(m => m.move.CapturedPiece?.Type == PieceType.King);
            if (kingCapture != default) return kingCapture;

            var captures = moves.Where(m => m.move.CapturedPiece != null).ToList();
            if (captures.Count > 0 && _random.NextDouble() < 0.5)
                return captures[_random.Next(captures.Count)];

            return moves[_random.Next(moves.Count)];
        }

        // ==================== СРЕДНИЙ ====================
        private (int, int, Move) GetMediumMove(List<(int fromRow, int fromCol, Move move)> moves)
        {
            var scoredMoves = new List<(int fromRow, int fromCol, Move move, float score)>();
            foreach (var m in moves)
                scoredMoves.Add((m.fromRow, m.fromCol, m.move, ScoreMoveMedium(m.move)));

            for (int i = 0; i < scoredMoves.Count; i++)
            {
                var m = scoredMoves[i];
                m.score += (float)(_random.NextDouble() * 0.3 - 0.15) * Math.Abs(m.score);
                scoredMoves[i] = m;
            }

            var best = scoredMoves.OrderByDescending(m => m.score).First();
            return (best.fromRow, best.fromCol, best.move);
        }

        private float ScoreMoveMedium(Move move)
        {
            float score = 0;

            // Взятие
            if (move.CapturedPiece != null)
            {
                if (move.CapturedPiece.Type == PieceType.King)
                    return 10000;
                score += GetPieceValue(move.CapturedPiece) * 2f;
            }

            // Суперпозиция
            var nonContact = _controller.Board.GetNonContactMoves(move.FromRow, move.FromColumn, _botColor);
            if (nonContact.Any(m => m.ToRow == move.ToRow && m.ToColumn == move.ToColumn))
                score += nonContact.Count * 0.5f;

            // Продвижение пешки
            if (move.MovedPiece.Type == PieceType.Pawn)
                score += (7 - move.ToRow) * 0.2f;

            // Центр доски
            float centerDist = Math.Abs(3.5f - move.ToColumn);
            score += (3.5f - centerDist) * 0.1f;

            // Ближе к вражескому королю
            var enemyKing = FindEnemyKing();
            if (enemyKing.HasValue)
            {
                float oldDist = Distance(move.FromRow, move.FromColumn, enemyKing.Value.row, enemyKing.Value.col);
                float newDist = Distance(move.ToRow, move.ToColumn, enemyKing.Value.row, enemyKing.Value.col);
                score += (oldDist - newDist) * 0.15f;
            }

            if (move.IsPromotion) score += 8;
            if (move.IsCastling) score += 2;

            return Math.Max(0, score);
        }

        // ==================== СЛОЖНЫЙ ====================
        private (int, int, Move) GetHardMove(List<(int fromRow, int fromCol, Move move)> moves)
        {
            var scoredMoves = new List<(int fromRow, int fromCol, Move move, float score)>();
            foreach (var m in moves)
                scoredMoves.Add((m.fromRow, m.fromCol, m.move, ScoreMoveHard(m.move)));

            for (int i = 0; i < scoredMoves.Count; i++)
            {
                var m = scoredMoves[i];
                m.score += (float)(_random.NextDouble() * 0.2 - 0.1) * Math.Abs(m.score);
                scoredMoves[i] = m;
            }

            var best = scoredMoves.OrderByDescending(m => m.score).First();
            return (best.fromRow, best.fromCol, best.move);
        }

        private float ScoreMoveHard(Move move)
        {
            float score = ScoreMoveMedium(move);

            // Симуляция хода
            var boardClone = _controller.Board.Clone();
            boardClone.ClearSuperposition();
            var resolvedMove = boardClone.ResolveMove(move);
            boardClone.MakeMove(resolvedMove);

            // Не подставили ли короля
            var myKing = FindKingInBoard(boardClone, _botColor);
            if (myKing.HasValue)
            {
                if (IsCellAttackedInBoard(boardClone, myKing.Value.row, myKing.Value.col, _botColor))
                    score -= 100;
            }

            // Атакуем ли вражеского короля
            var enemyKing = FindKingInBoard(boardClone, OpponentColor());
            if (enemyKing.HasValue)
            {
                if (IsCellAttackedInBoard(boardClone, enemyKing.Value.row, enemyKing.Value.col, OpponentColor()))
                    score += 30;

                // Ограничиваем подвижность вражеского короля
                int enemyMoves = boardClone.GetValidMoves(enemyKing.Value.row, enemyKing.Value.col, OpponentColor()).Count;
                score += (8 - enemyMoves) * 2;
            }

            // Оцениваем ответные угрозы
            score += EvaluateOpponentThreats(boardClone);

            // Развитие в начале игры
            if (!move.MovedPiece.HasMoved && move.MovedPiece.Type == PieceType.Knight)
                score += 1;
            if (!move.MovedPiece.HasMoved && move.MovedPiece.Type == PieceType.Bishop)
                score += 1.5f;

            return score;
        }

        private float EvaluateOpponentThreats(Board board)
        {
            float threats = 0;
            var oppColor = OpponentColor();

            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (board.TryGetPiece(r, c, out var piece) &&
                        piece!.Color == oppColor &&
                        piece.State == PieceState.Real)
                    {
                        var oppMoves = board.GetValidMoves(r, c, oppColor);
                        foreach (var m in oppMoves)
                        {
                            if (m.CapturedPiece != null)
                                threats += GetPieceValue(m.CapturedPiece) * 0.3f;
                        }
                    }
                }
            }

            return -threats;
        }

        // ==================== ЧИТЕР ====================
        private (int, int, Move) GetCheaterMove(List<(int fromRow, int fromCol, Move move)> moves)
        {
            var scoredMoves = new List<(int fromRow, int fromCol, Move move, float score)>();
            foreach (var m in moves)
                scoredMoves.Add((m.fromRow, m.fromCol, m.move, ScoreCheaterMove(m.move)));

            var best = scoredMoves.OrderByDescending(m => m.score).First();
            return (best.fromRow, best.fromCol, best.move);
        }

        private float ScoreCheaterMove(Move move)
        {
            float score = ScoreMoveHard(move);

            // Читер точно знает, где реальная фигура среди фантомов
            // Он никогда не бьёт фантомов (если это не единственный ход)
            if (move.CapturedPiece != null && move.CapturedPiece.State == PieceState.Phantom)
            {
                bool isRealPhantom = _controller.Board.RealPiecePosition.HasValue &&
                    _controller.Board.RealPiecePosition.Value.row == move.ToRow &&
                    _controller.Board.RealPiecePosition.Value.column == move.ToColumn;

                if (isRealPhantom)
                    score += GetPieceValue(move.CapturedPiece) * 3; // Знает что это реальная — супер-приоритет
                else
                    score -= GetPieceValue(move.CapturedPiece) * 0.5f; // Знает что пустышка — не хочет бить
            }

            // Читер знает, где реальный король врага
            var realKing = FindRealEnemyKing();
            if (realKing.HasValue)
            {
                float dist = Distance(move.ToRow, move.ToColumn, realKing.Value.row, realKing.Value.col);
                score += (8 - dist) * 2; // Максимально приближается к реальному королю
            }

            // Читер избегает суперпозиций врага (знает где реальные фигуры)
            if (_controller.Board.SuperpositionOwner == OpponentColor())
            {
                // Не ходит на клетки, где может быть реальная фигура врага
                if (_controller.Board.RealPiecePosition.HasValue &&
                    _controller.Board.RealPiecePosition.Value.row == move.ToRow &&
                    _controller.Board.RealPiecePosition.Value.column == move.ToColumn)
                {
                    score -= 50; // Сильно избегает столкновения с реальной фигурой
                }
            }

            return score;
        }

        private (int row, int col)? FindRealEnemyKing()
        {
            var oppColor = OpponentColor();
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (_controller.Board.TryGetPiece(r, c, out var piece) &&
                        piece!.Type == PieceType.King &&
                        piece.Color == oppColor &&
                        piece.State == PieceState.Real)
                        return (r, c);
                }
            }

            // Если король в суперпозиции — читер знает где он
            if (_controller.Board.SuperpositionOwner == oppColor &&
                _controller.Board.RealPiecePosition.HasValue)
            {
                var (r, c) = _controller.Board.RealPiecePosition.Value;
                var piece = _controller.Board.Grid[r, c];
                if (piece?.Type == PieceType.King)
                    return (r, c);
            }

            return null;
        }

        // ==================== ВСПОМОГАТЕЛЬНЫЕ ====================
        private PieceColor OpponentColor() => _botColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

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
            return FindKingInBoard(_controller.Board, OpponentColor());
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

        private float Distance(int r1, int c1, int r2, int c2) => Math.Abs(r1 - r2) + Math.Abs(c1 - c2);
    }
}