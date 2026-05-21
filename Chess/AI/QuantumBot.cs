using Chess.Model;
using SuperpositionChess.Controller;

namespace Chess.AI
{
    public enum BotDifficulty { Easy, Medium, Hard, Cheater }

    public class QuantumBot
    {
        private GameController controller;
        private PieceColor botColor;
        private BotDifficulty difficulty;
        private Random random = new();

        public QuantumBot(GameController controller, PieceColor botColor, BotDifficulty difficulty)
        {
            this.controller = controller;
            this.botColor = botColor;
            this.difficulty = difficulty;
        }

        public void MakeMove()
        {
            if (controller.CurrentPlayer != botColor || controller.IsGameOver || controller.IsPendingConfirmation)
                return;

            var allMoves = GetAllPossibleMoves();
            if (allMoves.Count == 0)
                return;

            var (fromRow, fromCol, bestMove) = difficulty switch
            {
                BotDifficulty.Easy => GetEasyMove(allMoves),
                BotDifficulty.Medium => GetMediumMove(allMoves),
                BotDifficulty.Hard => GetHardMove(allMoves),
                BotDifficulty.Cheater => GetCheaterMove(allMoves),
                _ => (0, 0, null)
            };

            if (bestMove == null)
                return;

            controller.TryMakeMove(fromRow, fromCol, bestMove.ToRow, bestMove.ToColumn);
            controller.ConfirmMove();
        }

        private List<(int fromRow, int fromCol, Move move)> GetAllPossibleMoves()
        {
            var moves = new List<(int fromRow, int fromCol, Move move)>();
            for (var row = 0; row < 8; row++)
                for (var col = 0; col < 8; col++)
                    if (controller.TryGetPiece(row, col, out var piece) && piece!.Color == botColor && piece.State == PieceState.Real)
                        foreach (var move in controller.Board.GetValidMoves(row, col, botColor))
                            moves.Add((row, col, move));
            return moves;
        }

        // ==================== ЛЁГКИЙ ====================
        private (int, int, Move) GetEasyMove(List<(int fromRow, int fromCol, Move move)> moves)
        {
            var kingCapture = moves.FirstOrDefault(m => m.move.CapturedPiece?.Type == PieceType.King);
            if (kingCapture != default)
                return kingCapture;

            var captures = moves.Where(m => m.move.CapturedPiece != null).ToList();
            if (captures.Count > 0 && random.NextDouble() < 0.5)
                return captures[random.Next(captures.Count)];

            return moves[random.Next(moves.Count)];
        }

        // ==================== СРЕДНИЙ ====================
        private (int, int, Move) GetMediumMove(List<(int fromRow, int fromCol, Move move)> moves)
        {
            var scored = moves.Select(m => (m.fromRow, m.fromCol, m.move, score: ScoreMoveMedium(m.move))).ToList();
            for (var i = 0; i < scored.Count; i++)
            {
                var s = scored[i];
                s.score += (float)(random.NextDouble() * 0.3 - 0.15) * Math.Abs(s.score);
                scored[i] = s;
            }
            var best = scored.OrderByDescending(m => m.score).First();
            return (best.fromRow, best.fromCol, best.move);
        }

        private float ScoreMoveMedium(Move move)
        {
            if (move.CapturedPiece?.Type == PieceType.King)
                return 10000;

            var score = 0f;
            if (move.CapturedPiece != null)
                score += GetPieceValue(move.CapturedPiece) * 2f;

            var nonContact = controller.Board.GetNonContactMoves(move.FromRow, move.FromColumn, botColor);
            if (nonContact.Any(m => m.ToRow == move.ToRow && m.ToColumn == move.ToColumn))
                score += nonContact.Count * 0.5f;

            if (move.MovedPiece.Type == PieceType.Pawn)
                score += (7 - move.ToRow) * 0.2f;

            var centerDist = Math.Abs(3.5f - move.ToColumn);
            score += (3.5f - centerDist) * 0.1f;

            var enemyKing = FindEnemyKing();
            if (enemyKing.HasValue)
            {
                var oldDist = Distance(move.FromRow, move.FromColumn, enemyKing.Value.row, enemyKing.Value.col);
                var newDist = Distance(move.ToRow, move.ToColumn, enemyKing.Value.row, enemyKing.Value.col);
                score += (oldDist - newDist) * 0.15f;
            }

            if (move.IsPromotion) score += 8;
            if (move.IsCastling) score += 2;

            return Math.Max(0, score);
        }

        // ==================== СЛОЖНЫЙ ====================
        private (int, int, Move) GetHardMove(List<(int fromRow, int fromCol, Move move)> moves)
        {
            var scored = moves.Select(m => (m.fromRow, m.fromCol, m.move, score: ScoreMoveHard(m.move))).ToList();
            for (var i = 0; i < scored.Count; i++)
            {
                var s = scored[i];
                s.score += (float)(random.NextDouble() * 0.2 - 0.1) * Math.Abs(s.score);
                scored[i] = s;
            }
            var best = scored.OrderByDescending(m => m.score).First();
            return (best.fromRow, best.fromCol, best.move);
        }

        private float ScoreMoveHard(Move move)
        {
            var score = ScoreMoveMedium(move);
            var boardClone = controller.Board.Clone();
            boardClone.ClearSuperposition();
            var resolved = boardClone.ResolveMove(move);
            boardClone.MakeMove(resolved);

            var myKing = FindKingInBoard(boardClone, botColor);
            if (myKing.HasValue && IsCellAttackedInBoard(boardClone, myKing.Value.row, myKing.Value.col, botColor))
                score -= 100;

            var enemyKing = FindKingInBoard(boardClone, OpponentColor());
            if (enemyKing.HasValue)
            {
                if (IsCellAttackedInBoard(boardClone, enemyKing.Value.row, enemyKing.Value.col, OpponentColor()))
                    score += 30;
                var enemyMoves = boardClone.GetValidMoves(enemyKing.Value.row, enemyKing.Value.col, OpponentColor()).Count;
                score += (8 - enemyMoves) * 2;
            }

            score += EvaluateOpponentThreats(boardClone);

            if (!move.MovedPiece.HasMoved && move.MovedPiece.Type == PieceType.Knight) score += 1;
            if (!move.MovedPiece.HasMoved && move.MovedPiece.Type == PieceType.Bishop) score += 1.5f;

            return score;
        }

        private float EvaluateOpponentThreats(Board board)
        {
            var threats = 0f;
            var oppColor = OpponentColor();
            for (var r = 0; r < 8; r++)
                for (var c = 0; c < 8; c++)
                    if (board.TryGetPiece(r, c, out var piece) && piece!.Color == oppColor && piece.State == PieceState.Real)
                        foreach (var m in board.GetValidMoves(r, c, oppColor))
                            if (m.CapturedPiece != null)
                                threats += GetPieceValue(m.CapturedPiece) * 0.3f;
            return -threats;
        }

        // ==================== ЧИТЕР ====================
        private (int, int, Move) GetCheaterMove(List<(int fromRow, int fromCol, Move move)> moves)
        {
            var scored = moves.Select(m => (m.fromRow, m.fromCol, m.move, score: ScoreCheaterMove(m.move))).ToList();
            var best = scored.OrderByDescending(m => m.score).First();
            return (best.fromRow, best.fromCol, best.move);
        }

        private float ScoreCheaterMove(Move move)
        {
            var score = ScoreMoveHard(move);

            if (move.CapturedPiece?.State == PieceState.Phantom)
            {
                var isReal = controller.Board.RealPiecePosition is var (rpRow, rpCol)
                    && rpRow == move.ToRow && rpCol == move.ToColumn;
                score += isReal ? GetPieceValue(move.CapturedPiece) * 3 : -GetPieceValue(move.CapturedPiece) * 0.5f;
            }

            var realKing = FindRealEnemyKing();
            if (realKing.HasValue)
                score += (8 - Distance(move.ToRow, move.ToColumn, realKing.Value.row, realKing.Value.col)) * 2;

            if (controller.Board.SuperpositionOwner == OpponentColor()
                && controller.Board.RealPiecePosition is var (rpRow2, rpCol2)
                && rpRow2 == move.ToRow && rpCol2 == move.ToColumn)
                score -= 50;

            return score;
        }

        private (int row, int col)? FindRealEnemyKing()
        {
            var oppColor = OpponentColor();
            for (var r = 0; r < 8; r++)
                for (var c = 0; c < 8; c++)
                    if (controller.Board.TryGetPiece(r, c, out var piece)
                        && piece!.Type == PieceType.King
                        && piece.Color == oppColor
                        && piece.State == PieceState.Real)
                        return (r, c);

            if (controller.Board.SuperpositionOwner == oppColor && controller.Board.RealPiecePosition is var (rr, rc))
            {
                var piece = controller.Board.Grid[rr, rc];
                if (piece?.Type == PieceType.King)
                    return (rr, rc);
            }
            return null;
        }

        // ==================== ВСПОМОГАТЕЛЬНЫЕ ====================
        private PieceColor OpponentColor() => botColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

        private static float GetPieceValue(Piece piece) => piece.Type switch
        {
            PieceType.Pawn => 1,
            PieceType.Knight => 3,
            PieceType.Bishop => 3,
            PieceType.Rook => 5,
            PieceType.Queen => 9,
            PieceType.King => 100,
            _ => 0
        };

        private (int row, int col)? FindEnemyKing() => FindKingInBoard(controller.Board, OpponentColor());

        private static (int row, int col)? FindKingInBoard(Board board, PieceColor color)
        {
            for (var r = 0; r < 8; r++)
                for (var c = 0; c < 8; c++)
                    if (board.TryGetPiece(r, c, out var piece)
                        && piece!.Type == PieceType.King
                        && piece.Color == color
                        && piece.State == PieceState.Real)
                        return (r, c);
            return null;
        }

        private static bool IsCellAttackedInBoard(Board board, int row, int col, PieceColor defenderColor)
        {
            var attackerColor = defenderColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
            for (var r = 0; r < 8; r++)
                for (var c = 0; c < 8; c++)
                    if (board.TryGetPiece(r, c, out var piece)
                        && piece!.Color == attackerColor
                        && piece.State == PieceState.Real
                        && board.GetValidMoves(r, c, attackerColor).Any(m => m.ToRow == row && m.ToColumn == col))
                        return true;
            return false;
        }

        private static float Distance(int r1, int c1, int r2, int c2) => Math.Abs(r1 - r2) + Math.Abs(c1 - c2);
    }
}