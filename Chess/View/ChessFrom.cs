using Chess.AI;
using Chess.Model;
using SuperpositionChess.Controller;
using SuperpositionChess.Network;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;

namespace SuperpositionChess.View
{
    public class ChessForm : Form
    {
        // ==================== ПОЛЯ ====================
        private GameController controller;
        private Button[,] boardButtons;
        private Button newGameButton, backToMenuButton, confirmButton, cancelButton;
        private Button toggleSuperpositionButton, flipBoardButton;
        private bool showSuperposition = true;
        private Label titleLabel;
        private int? selectedRow, selectedCol, hoveredRow, hoveredCol;
        private List<Move> currentValidMoves, hoverValidMoves;
        private Move? lastMove, myLastMove;
        private QuantumBot? bot;
        private PieceColor myColor = PieceColor.White;
        private bool iWon;
        private NetworkGameServer? server;
        private NetworkGameClient? client;
        private bool isHost, isNetworkGame;
        private string roomKey = "";
        private bool isWhitePerspective = true;

        // ==================== КОНСТАНТЫ И ЦВЕТА ====================
        private const int BoardSize = 8, BorderThickness = 35, CornerRadius = 40;
        private int squareSize, boardOffsetX, boardOffsetY;

        private readonly Color lightSquare = Color.FromArgb(240, 217, 181);
        private readonly Color darkSquare = Color.FromArgb(181, 136, 99);
        private readonly Color selectedLight = Color.FromArgb(170, 210, 140);
        private readonly Color selectedDark = Color.FromArgb(130, 165, 95);
        private readonly Color validMoveLight = Color.FromArgb(190, 225, 160);
        private readonly Color validMoveDark = Color.FromArgb(145, 180, 110);
        private readonly Color lastMoveLight = Color.FromArgb(255, 235, 150);
        private readonly Color lastMoveDark = Color.FromArgb(200, 175, 80);
        private readonly Color phantomLight = Color.FromArgb(200, 200, 200);
        private readonly Color phantomDark = Color.FromArgb(140, 140, 140);
        private readonly Color hoverLight = Color.FromArgb(215, 200, 155);
        private readonly Color hoverDark = Color.FromArgb(160, 115, 80);

        // ==================== КОНСТРУКТОРЫ ====================
        public ChessForm()
        {
            InitCommon(PieceColor.White, true);
            controller!.OnGameStateChanged += UpdateBoard;
            FinishInit();
        }

        public ChessForm(BotDifficulty difficulty)
        {
            InitCommon(PieceColor.White, true);
            controller!.OnGameStateChanged += OnBotGameStateChanged;
            bot = new QuantumBot(controller, PieceColor.Black, difficulty);
            FinishInit();
        }

        public ChessForm(string host, int port, string key)
        {
            isHost = false;
            isNetworkGame = true;
            myColor = PieceColor.Black;
            roomKey = key;
            isWhitePerspective = false;
            InitCommon(PieceColor.Black, false);
            client = new NetworkGameClient();
            client.OnConnected += () => Invoke(UpdateTitle);
            client.OnMoveReceived += OnNetworkMoveReceived;
            client.OnError += err => Invoke(() =>
            {
                MessageBox.Show(err, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
            });
            controller!.OnGameStateChanged += OnNetworkGameStateChanged;
            Shown += async (s, e) =>
            {
                if (!await client.ConnectAsync(host, port, key))
                    Invoke(Close);
            };
            FinishInit();
        }

        public ChessForm(NetworkGameServer srv, string key)
        {
            isHost = true;
            isNetworkGame = true;
            server = srv;
            roomKey = key;
            InitCommon(PieceColor.White, true);
            server.OnMoveReceived += OnNetworkMoveReceived;
            server.OnError += OnNetworkError;
            controller!.OnGameStateChanged += OnNetworkGameStateChanged;
            FinishInit();
        }

        private void InitCommon(PieceColor color, bool perspective)
        {
            controller = new GameController();
            currentValidMoves = new List<Move>();
            hoverValidMoves = new List<Move>();
            myColor = color;
            isWhitePerspective = perspective;

            SetupFullScreen();
            CreateTitleLabel();
            CreateBoard();
            CreateButtons();

            controller.OnGameOver += ShowGameOver;
            controller.OnError += ShowError;
            controller.StartNewGame();
            Resize += (s, e) =>
            {
                UpdateSizes();
                RepositionControls();
                Invalidate();
            };
        }

        private void FinishInit()
        {
            UpdateSizes();
            RepositionControls();
        }

        // ==================== СЕТЕВЫЕ ОБРАБОТЧИКИ ====================
        private void OnNetworkMoveReceived(NetworkMoveData data)
        {
            Invoke(() =>
            {
                controller.TryMakeMove(data.FromRow, data.FromCol, data.ToRow, data.ToCol);
                controller.ConfirmMove();
                lastMove = controller.LastMove;
                UpdateBoard();
            });
        }

        private void OnNetworkError(string err) =>
            Invoke(() => MessageBox.Show(err, "Сетевая ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning));

        private void OnNetworkGameStateChanged()
        {
            UpdateBoard();
            if (controller.IsPendingConfirmation || controller.IsGameOver || !isNetworkGame)
                return;
            var move = controller.LastMove;
            if (move != null && controller.CurrentPlayer != myColor)
            {
                if (isHost)
                    _ = server!.SendMoveAsync(move.FromRow, move.FromColumn, move.ToRow, move.ToColumn);
                else
                    _ = client!.SendMoveAsync(move.FromRow, move.FromColumn, move.ToRow, move.ToColumn);
            }
        }

        // ==================== БОТ ====================
        private void OnBotGameStateChanged()
        {
            UpdateBoard();
            if (controller.IsPendingConfirmation || controller.IsGameOver || bot == null)
                return;
            if (controller.LastMove != null && controller.CurrentPlayer == PieceColor.White)
                lastMove = controller.LastMove;
            UpdateBoard();
            if (controller.CurrentPlayer == PieceColor.Black)
                Task.Delay(800).ContinueWith(_ => Invoke(() =>
                {
                    if (!controller.IsPendingConfirmation && !controller.IsGameOver)
                        bot.MakeMove();
                }));
        }

        // ==================== ОТОБРАЖЕНИЕ ====================
        private int ToDisplayRow(int r) => isWhitePerspective ? r : BoardSize - 1 - r;
        private int ToRealRow(int d) => isWhitePerspective ? d : BoardSize - 1 - d;

        private void SetupFullScreen()
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            Bounds = Screen.FromControl(Application.OpenForms["MenuForm"] ?? this).Bounds;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    Close();
                else if (e.KeyCode == Keys.F11)
                {
                    if (FormBorderStyle == FormBorderStyle.None)
                    {
                        WindowState = FormWindowState.Normal;
                        FormBorderStyle = FormBorderStyle.Sizable;
                    }
                    else
                    {
                        WindowState = FormWindowState.Maximized;
                        FormBorderStyle = FormBorderStyle.None;
                    }
                    UpdateSizes();
                    RepositionControls();
                    Invalidate();
                }
            };
            BackColor = Color.FromArgb(25, 25, 25);
            Paint += ChessFormPaint;
        }

        private void CreateTitleLabel()
        {
            titleLabel = new Label
            {
                Text = "ХОД БЕЛЫХ",
                Font = new Font("Segoe UI", 48, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                BackColor = Color.Transparent
            };
            Controls.Add(titleLabel);
        }

        private void UpdateTitle()
        {
            if (titleLabel == null)
                return;
            string turn;
            Color color;
            if (isNetworkGame)
                UpdateNetworkTitle(out turn, out color);
            else if (bot != null)
                UpdateBotTitle(out turn, out color);
            else
                UpdateLocalTitle(out turn, out color);
            titleLabel.Text = turn;
            titleLabel.ForeColor = color;
        }

        private void UpdateNetworkTitle(out string turn, out Color color)
        {
            if (controller.IsGameOver)
            {
                turn = iWon ? "ПОБЕДА!" : "ПОРАЖЕНИЕ";
                color = iWon ? Color.FromArgb(100, 255, 100) : Color.FromArgb(255, 60, 60);
            }
            else
            {
                var role = isHost ? $"ХОСТ ({roomKey})" : $"ГОСТЬ ({roomKey})";
                var act = controller.CurrentPlayer == myColor ? "ВАШ ХОД" : "ХОД ПРОТИВНИКА";
                turn = $"{role} — {act}";
                color = controller.CurrentPlayer == myColor ? Color.White : Color.FromArgb(180, 180, 180);
            }
        }

        private void UpdateBotTitle(out string turn, out Color color)
        {
            if (controller.IsGameOver)
            {
                turn = iWon ? "ВЫ ПОБЕДИЛИ!" : "ВЫ ПРОИГРАЛИ";
                color = iWon ? Color.FromArgb(100, 255, 100) : Color.FromArgb(255, 60, 60);
            }
            else
            {
                turn = controller.CurrentPlayer == PieceColor.White ? "ВАШ ХОД" : "ХОД БОТА";
                color = controller.CurrentPlayer == PieceColor.White ? Color.White : Color.FromArgb(180, 180, 180);
            }
        }

        private void UpdateLocalTitle(out string turn, out Color color)
        {
            if (controller.IsGameOver)
            {
                turn = controller.GameResult;
                color = Color.FromArgb(100, 255, 100);
            }
            else
            {
                turn = controller.CurrentPlayer == PieceColor.White ? "ХОД БЕЛЫХ" : "ХОД ЧЁРНЫХ";
                color = controller.CurrentPlayer == PieceColor.White ? Color.White : Color.FromArgb(180, 180, 180);
            }
        }

        // ==================== РАЗМЕРЫ ====================
        private void UpdateSizes()
        {
            var top = 180;
            var bottom = 150;
            var maxW = ClientSize.Width - 250;
            var maxH = ClientSize.Height - top - bottom;
            squareSize = Math.Min(maxW, maxH) / BoardSize;
            boardOffsetX = (ClientSize.Width - squareSize * BoardSize) / 2;
            boardOffsetY = top + (maxH - squareSize * BoardSize) / 2;
        }

        private void RepositionControls()
        {
            if (boardButtons == null)
                return;

            for (var r = 0; r < BoardSize; r++)
                for (var c = 0; c < BoardSize; c++)
                    if (boardButtons[r, c] != null)
                    {
                        boardButtons[r, c].Size = new Size(squareSize, squareSize);
                        boardButtons[r, c].Location = new Point(boardOffsetX + c * squareSize, boardOffsetY + r * squareSize);
                        boardButtons[r, c].Font = new Font("Segoe UI", squareSize * 0.4f);
                    }

            if (titleLabel != null)
            {
                titleLabel.Size = new Size(ClientSize.Width - 40, 80);
                titleLabel.Location = new Point(20, 25);
                titleLabel.Font = new Font("Segoe UI", Math.Max(30, squareSize * 0.55f), FontStyle.Bold);
            }

            var bw = 180;
            var bh = 45;
            var sp = 15;
            var rx = ClientSize.Width - bw - 30;
            var ry = boardOffsetY + (squareSize * BoardSize - (bh * 4 + sp * 3)) / 2;

            if (flipBoardButton != null)
            {
                flipBoardButton.Size = new Size(bw, bh);
                flipBoardButton.Location = new Point(rx, ry);
            }
            if (toggleSuperpositionButton != null)
            {
                toggleSuperpositionButton.Size = new Size(bw, bh);
                toggleSuperpositionButton.Location = new Point(rx, ry + bh + sp);
            }
            if (newGameButton != null)
            {
                newGameButton.Size = new Size(bw, bh);
                newGameButton.Location = new Point(rx, ry + (bh + sp) * 2);
            }
            if (backToMenuButton != null)
            {
                backToMenuButton.Size = new Size(bw, bh);
                backToMenuButton.Location = new Point(rx, ry + (bh + sp) * 3);
            }

            var b2w = 200;
            var b2h = 50;
            var b2x = (ClientSize.Width - (b2w * 2 + 20)) / 2;
            var b2y = ClientSize.Height - b2h - 30;
            if (confirmButton != null)
            {
                confirmButton.Size = new Size(b2w, b2h);
                confirmButton.Location = new Point(b2x, b2y);
            }
            if (cancelButton != null)
            {
                cancelButton.Size = new Size(b2w, b2h);
                cancelButton.Location = new Point(b2x + b2w + 20, b2y);
            }
        }

        private Color GetBorderColor()
        {
            if (controller.IsGameOver)
                return (isNetworkGame || bot != null)
                    ? (iWon ? Color.FromArgb(100, 255, 100) : Color.FromArgb(255, 60, 60))
                    : Color.FromArgb(100, 255, 100);
            return controller.CurrentPlayer == PieceColor.White
                ? Color.FromArgb(230, 230, 230)
                : Color.FromArgb(30, 30, 30);
        }

        private void ChessFormPaint(object? s, PaintEventArgs e)
        {
            MenuForm.DrawCheckerboardBackground(e.Graphics, ClientSize);
            var bx = boardOffsetX - BorderThickness;
            var by = boardOffsetY - BorderThickness;
            var bw = BoardSize * squareSize + BorderThickness * 2;
            var bh = BoardSize * squareSize + BorderThickness * 2;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var shadowPath = RoundedRect(bx + 8, by + 8, bw, bh, CornerRadius);
            using var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
            e.Graphics.FillPath(shadowBrush, shadowPath);

            using var path = RoundedRect(bx, by, bw, bh, CornerRadius);
            using var borderBrush = new SolidBrush(GetBorderColor());
            e.Graphics.FillPath(borderBrush, path);

            var brd = GetBorderColor();
            using var outlinePath = RoundedRect(bx + 2, by + 2, bw - 4, bh - 4, CornerRadius - 2);
            using var outlinePen = new Pen(Color.FromArgb(Math.Max(0, brd.R - 50), Math.Max(0, brd.G - 50), Math.Max(0, brd.B - 50)), 3);
            e.Graphics.DrawPath(outlinePen, outlinePath);
        }

        private static GraphicsPath RoundedRect(int x, int y, int w, int h, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ==================== КНОПКИ ====================
        private void CreateButtons()
        {
            confirmButton = StyledButton("Подтвердить");
            confirmButton.Enabled = false;
            confirmButton.Click += (s, e) =>
            {
                controller.ConfirmMove();
                lastMove = controller.LastMove;
                ClearSelection();
                Invalidate();
            };
            Controls.Add(confirmButton);

            cancelButton = StyledButton("Отменить");
            cancelButton.Enabled = false;
            cancelButton.Click += (s, e) =>
            {
                var prev = lastMove;
                controller.CancelMove();
                lastMove = prev;
                myLastMove = null;
                ClearSelection();
                Invalidate();
            };
            Controls.Add(cancelButton);

            newGameButton = StyledButton("Новая игра");
            newGameButton.Click += (s, e) =>
            {
                controller.StartNewGame();
                lastMove = null;
                myLastMove = null;
                ClearSelection();
                Invalidate();
            };
            Controls.Add(newGameButton);

            backToMenuButton = StyledButton("Выйти");
            backToMenuButton.Click += (s, e) =>
            {
                server?.Stop();
                client?.Disconnect();
                Close();
            };
            Controls.Add(backToMenuButton);

            toggleSuperpositionButton = StyledButton("Суперпозиция");
            toggleSuperpositionButton.Click += (s, e) =>
            {
                showSuperposition = !showSuperposition;
                UpdateAllButtons();
            };
            toggleSuperpositionButton.Paint += (s, e) =>
            {
                if (!showSuperposition)
                    using (var pen = new Pen(Color.FromArgb(200, 255, 80, 80), 3))
                        e.Graphics.DrawLine(pen, 0, 0, toggleSuperpositionButton.Width, toggleSuperpositionButton.Height);
            };
            Controls.Add(toggleSuperpositionButton);

            flipBoardButton = StyledButton("Перспектива");
            flipBoardButton.Click += (s, e) =>
            {
                isWhitePerspective = !isWhitePerspective;
                UpdateAllButtons();
                Invalidate();
            };
            Controls.Add(flipBoardButton);
        }

        private Button StyledButton(string text)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btn.BackColor = Color.Transparent;

            var hovered = false;
            var pressed = false;
            btn.MouseEnter += (s, e) => { hovered = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { hovered = false; pressed = false; btn.Invalidate(); };
            btn.MouseDown += (s, e) => { pressed = true; btn.Invalidate(); };
            btn.MouseUp += (s, e) => { pressed = false; btn.Invalidate(); };

            btn.Paint += (s, e) =>
            {
                var b = (Button)s!;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Color.Transparent);

                var bg = pressed ? Color.FromArgb(90, 90, 90)
                    : hovered ? Color.FromArgb(70, 70, 70)
                    : Color.FromArgb(50, 50, 50);
                var bc = hovered ? Color.FromArgb(150, 150, 150) : Color.FromArgb(100, 100, 100);
                var rad = b.Height / 5;

                using var path = RoundedRect(0, 0, b.Width - 1, b.Height - 1, rad);
                using var brush = new SolidBrush(bg);
                e.Graphics.FillPath(brush, path);

                using var path2 = RoundedRect(0, 0, b.Width - 2, b.Height - 2, rad);
                using var pen = new Pen(bc, 2);
                e.Graphics.DrawPath(pen, path2);

                TextRenderer.DrawText(e.Graphics, b.Text, b.Font,
                    new Rectangle(0, 0, b.Width, b.Height),
                    hovered ? Color.White : Color.FromArgb(220, 220, 220),
                    Color.Transparent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            return btn;
        }

        // ==================== ДОСКА ====================
        private void CreateBoard()
        {
            boardButtons = new Button[BoardSize, BoardSize];
            for (var r = 0; r < BoardSize; r++)
                for (var c = 0; c < BoardSize; c++)
                {
                    var btn = new Button
                    {
                        FlatStyle = FlatStyle.Flat,
                        Tag = (r, c),
                        TextAlign = ContentAlignment.MiddleCenter,
                        Cursor = Cursors.Hand
                    };
                    btn.FlatAppearance.BorderSize = 0;
                    btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
                    btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
                    btn.MouseEnter += ButtonMouseEnter;
                    btn.MouseLeave += ButtonMouseLeave;
                    btn.Click += BoardButtonClick;
                    btn.Paint += ButtonPaint;
                    boardButtons[r, c] = btn;
                    Controls.Add(btn);
                }
        }

        private void ButtonPaint(object? s, PaintEventArgs e)
        {
            if (s is not Button btn)
                return;

            var (dr, dc) = ((int, int))btn.Tag;
            var realRow = ToRealRow(dr);
            var isLight = (realRow + dc) % 2 == 0;
            var baseColor = isLight ? lightSquare : darkSquare;

            var selected = selectedRow == realRow && selectedCol == dc;
            var hovered = hoveredRow == dr && hoveredCol == dc;
            var validSel = currentValidMoves.Any(m => m.ToRow == realRow && m.ToColumn == dc);
            var validHov = hoverValidMoves.Any(m => m.ToRow == realRow && m.ToColumn == dc);
            var pending = controller.IsPendingConfirmation;
            var hasSuper = controller.Board.CurrentPhantoms.Count > 0;
            var mySuper = controller.Board.SuperpositionOwner == controller.CurrentPlayer;
            var lastFrom = lastMove != null && lastMove.FromRow == realRow && lastMove.FromColumn == dc;
            var lastTo = lastMove != null && lastMove.ToRow == realRow && lastMove.ToColumn == dc;

            if (!pending && hasSuper)
                lastTo = false;

            var pieceOnCell = controller.Board.Grid[realRow, dc];
            var isPhantom = pieceOnCell != null && pieceOnCell.State == PieceState.Phantom;
            var isRealPiece = isPhantom && controller.Board.RealPiecePosition is var (rpRow, rpCol) && rpRow == realRow && rpCol == dc;

            if (isRealPiece && pending && mySuper)
                isPhantom = false;

            var hideThis = isPhantom && !showSuperposition && pieceOnCell!.Color == myColor && !isRealPiece;
            if (bot != null && pieceOnCell != null && pieceOnCell.Color != myColor)
                hideThis = false;
            if (hideThis)
            {
                isPhantom = false;
                pieceOnCell = null;
            }

            var myLastFrom = false;
            var myLastTo = false;
            if ((bot != null || isNetworkGame) && myLastMove != null)
            {
                myLastFrom = myLastMove.FromRow == realRow && myLastMove.FromColumn == dc;
                myLastTo = myLastMove.ToRow == realRow && myLastMove.ToColumn == dc;
            }

            var finalColor = baseColor;

            if (selected)
                finalColor = isLight ? selectedLight : selectedDark;
            else if (validHov || (validSel && hovered))
                finalColor = isLight ? validMoveLight : validMoveDark;
            else if (validSel)
                finalColor = isLight ? validMoveLight : validMoveDark;
            else if (myLastFrom || myLastTo)
                finalColor = isLight ? lastMoveLight : lastMoveDark;
            else if (lastFrom || lastTo)
                finalColor = isLight ? lastMoveLight : lastMoveDark;
            else if (isPhantom)
                finalColor = isLight ? phantomLight : phantomDark;
            else if (hovered && selectedRow == null)
            {
                var hp = controller.Board.Grid[realRow, dc];
                if (hp != null && hp.Color == controller.CurrentPlayer && hp.State == PieceState.Real)
                    finalColor = isLight ? hoverLight : hoverDark;
            }

            var topLeft = dr == 0 && dc == 0;
            var topRight = dr == 0 && dc == BoardSize - 1;
            var bottomLeft = dr == BoardSize - 1 && dc == 0;
            var bottomRight = dr == BoardSize - 1 && dc == BoardSize - 1;

            if (topLeft || topRight || bottomLeft || bottomRight)
            {
                var borderColor = GetBorderColor();
                using var bBrush = new SolidBrush(borderColor);
                e.Graphics.FillRectangle(bBrush, 0, 0, squareSize, squareSize);

                var cellPath = new GraphicsPath();
                if (topLeft) { cellPath.AddArc(0, 0, CornerRadius * 2, CornerRadius * 2, 180, 90); cellPath.AddLine(CornerRadius, 0, squareSize, 0); cellPath.AddLine(squareSize, 0, squareSize, squareSize); cellPath.AddLine(squareSize, squareSize, 0, squareSize); cellPath.AddLine(0, squareSize, 0, CornerRadius); }
                else if (topRight) { cellPath.AddArc(squareSize - CornerRadius * 2, 0, CornerRadius * 2, CornerRadius * 2, 270, 90); cellPath.AddLine(squareSize, CornerRadius, squareSize, squareSize); cellPath.AddLine(squareSize, squareSize, 0, squareSize); cellPath.AddLine(0, squareSize, 0, 0); cellPath.AddLine(0, 0, squareSize - CornerRadius, 0); }
                else if (bottomLeft) { cellPath.AddArc(0, squareSize - CornerRadius * 2, CornerRadius * 2, CornerRadius * 2, 90, 90); cellPath.AddLine(CornerRadius, squareSize, squareSize, squareSize); cellPath.AddLine(squareSize, squareSize, squareSize, 0); cellPath.AddLine(squareSize, 0, 0, 0); cellPath.AddLine(0, 0, 0, squareSize - CornerRadius); }
                else { cellPath.AddArc(squareSize - CornerRadius * 2, squareSize - CornerRadius * 2, CornerRadius * 2, CornerRadius * 2, 0, 90); cellPath.AddLine(squareSize, squareSize - CornerRadius, squareSize, 0); cellPath.AddLine(squareSize, 0, 0, 0); cellPath.AddLine(0, 0, 0, squareSize); cellPath.AddLine(0, squareSize, squareSize - CornerRadius, squareSize); }
                cellPath.CloseFigure();

                using var fBrush = new SolidBrush(finalColor);
                e.Graphics.FillPath(fBrush, cellPath);
                using var fPen = new Pen(Color.FromArgb(Math.Max(0, finalColor.R - 25), Math.Max(0, finalColor.G - 25), Math.Max(0, finalColor.B - 25)), 1);
                e.Graphics.DrawPath(fPen, cellPath);
                cellPath.Dispose();
            }
            else
            {
                using var fBrush = new SolidBrush(finalColor);
                e.Graphics.FillRectangle(fBrush, 0, 0, squareSize, squareSize);
                using var fPen = new Pen(Color.FromArgb(Math.Max(0, finalColor.R - 25), Math.Max(0, finalColor.G - 25), Math.Max(0, finalColor.B - 25)), 1);
                e.Graphics.DrawRectangle(fPen, 0, 0, squareSize - 1, squareSize - 1);
            }

            var showGhost = (selectedRow.HasValue && hovered && validSel) || (hoveredRow.HasValue && hovered && validHov && selectedRow == null);
            if (showGhost)
            {
                var srcRow = selectedRow ?? ToRealRow(hoveredRow!.Value);
                var srcCol = selectedCol ?? hoveredCol!.Value;
                var srcPiece = controller.Board.Grid[srcRow, srcCol];
                if (srcPiece != null)
                {
                    var sym = srcPiece.GetUnicodeSymbol();
                    using var font = new Font("Segoe UI", squareSize * 0.4f);
                    using var gBrush = new SolidBrush(Color.FromArgb(80, srcPiece.Color == PieceColor.White ? Color.White : Color.Black));
                    var ts = e.Graphics.MeasureString(sym, font);
                    e.Graphics.DrawString(sym, font, gBrush, (squareSize - ts.Width) / 2, (squareSize - ts.Height) / 2);
                }
            }

            if (pieceOnCell != null && !showGhost)
            {
                var sym = pieceOnCell.GetUnicodeSymbol();
                using var font = new Font("Segoe UI", squareSize * 0.4f);
                var bright = !isPhantom || myLastFrom || myLastTo || (isRealPiece && pending && mySuper);
                var alpha = bright ? 255 : 120;
                var pColor = pieceOnCell.Color == PieceColor.White ? Color.FromArgb(alpha, 255, 255, 255) : Color.FromArgb(alpha, 0, 0, 0);
                using var pBrush = new SolidBrush(pColor);
                var ts = e.Graphics.MeasureString(sym, font);
                e.Graphics.DrawString(sym, font, pBrush, (squareSize - ts.Width) / 2, (squareSize - ts.Height) / 2);
            }
        }

        private void ButtonMouseEnter(object? s, EventArgs e)
        {
            if (s is not Button btn)
                return;
            var (dr, dc) = ((int, int))btn.Tag;
            var rr = ToRealRow(dr);
            hoveredRow = dr;
            hoveredCol = dc;

            if (selectedRow == null)
            {
                if (controller.TryGetPiece(rr, dc, out var p) && p.Color == controller.CurrentPlayer && p.State == PieceState.Real && !controller.IsPendingConfirmation && controller.CurrentPlayer == myColor)
                    hoverValidMoves = controller.Board.GetValidMoves(rr, dc, p.Color);
                else if (controller.IsPendingConfirmation && controller.TryGetPiece(rr, dc, out var ep) && ep.Color != controller.CurrentPlayer)
                    hoverValidMoves = controller.Board.GetValidMoves(rr, dc, ep.Color);
                else
                    hoverValidMoves.Clear();
            }
            UpdateAllButtons();
        }

        private void ButtonMouseLeave(object? s, EventArgs e)
        {
            hoveredRow = null;
            hoveredCol = null;
            hoverValidMoves.Clear();
            UpdateAllButtons();
        }

        private void BoardButtonClick(object? s, EventArgs e)
        {
            if (controller.IsGameOver)
                return;
            if (isNetworkGame && controller.CurrentPlayer != myColor)
                return;
            if (isNetworkGame && controller.IsPendingConfirmation)
                return;
            if (s is not Button btn)
                return;

            var (dr, dc) = ((int, int))btn.Tag;
            var rr = ToRealRow(dr);

            if (selectedRow == null)
            {
                if (controller.TryGetPiece(rr, dc, out var p) && p.Color == controller.CurrentPlayer && p.State == PieceState.Real && !controller.IsPendingConfirmation)
                {
                    selectedRow = rr;
                    selectedCol = dc;
                    currentValidMoves = controller.Board.GetValidMoves(rr, dc, p.Color);
                    hoverValidMoves.Clear();
                    UpdateAllButtons();
                }
                return;
            }

            if (selectedRow == rr && selectedCol == dc)
            {
                ClearSelection();
                return;
            }

            if (currentValidMoves.Any(m => m.ToRow == rr && m.ToColumn == dc))
            {
                controller.TryMakeMove(selectedRow.Value, selectedCol.Value, rr, dc);
                myLastMove = controller.LastMove;
                lastMove = controller.LastMove;
                ClearSelection();
                Invalidate();
                return;
            }

            if (controller.TryGetPiece(rr, dc, out var np) && np.Color == controller.CurrentPlayer && np.State == PieceState.Real && !controller.IsPendingConfirmation)
            {
                selectedRow = rr;
                selectedCol = dc;
                currentValidMoves = controller.Board.GetValidMoves(rr, dc, np.Color);
                hoverValidMoves.Clear();
                UpdateAllButtons();
                return;
            }

            ClearSelection();
        }

        private void ClearSelection()
        {
            selectedRow = null;
            selectedCol = null;
            currentValidMoves.Clear();
            hoverValidMoves.Clear();
            UpdateAllButtons();
        }

        private void UpdateAllButtons()
        {
            for (var r = 0; r < BoardSize; r++)
                for (var c = 0; c < BoardSize; c++)
                    boardButtons[r, c].Invalidate();
            UpdateTitle();
        }

        private void UpdateBoard()
        {
            confirmButton.Enabled = controller.IsPendingConfirmation;
            cancelButton.Enabled = controller.IsPendingConfirmation;
            toggleSuperpositionButton.Invalidate();

            if (bot == null && !isNetworkGame)
            {
                myColor = controller.CurrentPlayer;
                isWhitePerspective = controller.CurrentPlayer == PieceColor.White;
            }

            if (controller.IsGameOver)
            {
                if (isNetworkGame)
                {
                    var whiteWon = controller.GameResult.Contains("БЕЛЫЕ");
                    iWon = (myColor == PieceColor.White && whiteWon) || (myColor == PieceColor.Black && !whiteWon);
                }
                else if (bot != null)
                    iWon = controller.GameResult.Contains("БЕЛЫЕ");
            }

            UpdateAllButtons();
            Invalidate();
        }

        private void ShowGameOver(string msg)
        {
            UpdateTitle();
            Invalidate();
            MessageBox.Show(msg, "Игра окончена", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowError(string msg) =>
            MessageBox.Show(msg, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            server?.Stop();
            client?.Disconnect();
            base.OnFormClosing(e);
        }
    }
}