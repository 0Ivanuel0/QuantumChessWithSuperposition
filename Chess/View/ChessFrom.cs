using Chess.AI;
using Chess.Model;
using SuperpositionChess.Controller;
using SuperpositionChess.Network;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;

namespace SuperpositionChess.View
{
    public class ChessForm : Form
    {
        private GameController _controller;

        private Button[,] _boardButtons;
        private Button _newGameButton;
        private Button _backToMenuButton;
        private Button _confirmButton;
        private Button _cancelButton;
        private Button _toggleSuperpositionButton;
        private bool _showSuperposition = true;
        private Button _flipBoardButton;

        private Label _titleLabel;

        private int? _selectedRow;
        private int? _selectedCol;
        private int? _hoveredRow;
        private int? _hoveredCol;

        private List<Move> _currentValidMoves;
        private List<Move> _hoverValidMoves;
        private Move? _lastMove;

        private BotDifficulty? _botDifficulty;
        private QuantumBot? _bot;
        private PieceColor _myColor = PieceColor.White;
        private Move? _myLastMove;

        // Сеть
        private NetworkGameServer? _server;
        private NetworkGameClient? _client;
        private bool _isHost;
        private bool _isNetworkGame;
        private string _roomKey = "";
        private bool _isWhitePerspective = true;
        private bool _isWhitePlayer;

        private const int BoardSize = 8;
        private int SquareSize;
        private int BoardOffsetX;
        private int BoardOffsetY;
        private const int BorderThickness = 35;
        private const int CornerRadius = 40;

        private readonly Color LightSquare = Color.FromArgb(240, 217, 181);
        private readonly Color DarkSquare = Color.FromArgb(181, 136, 99);
        private readonly Color SelectedLight = Color.FromArgb(170, 210, 140);
        private readonly Color SelectedDark = Color.FromArgb(130, 165, 95);
        private readonly Color ValidMoveLight = Color.FromArgb(190, 225, 160);
        private readonly Color ValidMoveDark = Color.FromArgb(145, 180, 110);
        private readonly Color LastMoveLight = Color.FromArgb(255, 235, 150);
        private readonly Color LastMoveDark = Color.FromArgb(200, 175, 80);
        private readonly Color PhantomLight = Color.FromArgb(200, 200, 200);
        private readonly Color PhantomDark = Color.FromArgb(140, 140, 140);
        private readonly Color HoverLight = Color.FromArgb(215, 200, 155);
        private readonly Color HoverDark = Color.FromArgb(160, 115, 80);

        // ==================== КОНСТРУКТОРЫ ====================

        // Локальная игра
        public ChessForm()
        {
            _controller = new GameController();
            _currentValidMoves = new List<Move>();
            _hoverValidMoves = new List<Move>();
            _myColor = PieceColor.White;
            _isWhitePerspective = true;

            SetupFullScreen();
            CreateTitleLabel();
            CreateBoard();
            CreateButtons();

            _controller.OnGameStateChanged += UpdateBoard;
            _controller.OnGameOver += ShowGameOver;
            _controller.OnError += ShowError;
            _controller.StartNewGame();

            this.Resize += ChessForm_Resize;
            UpdateSizes();
        }

        // Игра с ботом
        public ChessForm(BotDifficulty difficulty)
        {
            _botDifficulty = difficulty;
            _controller = new GameController();
            _currentValidMoves = new List<Move>();
            _hoverValidMoves = new List<Move>();
            _myColor = PieceColor.White;
            _isWhitePerspective = true;

            SetupFullScreen();
            CreateTitleLabel();
            CreateBoard();
            CreateButtons();

            _controller.OnGameStateChanged += OnBotGameStateChanged;
            _controller.OnGameOver += ShowGameOver;
            _controller.OnError += ShowError;
            _controller.StartNewGame();

            _bot = new QuantumBot(_controller, PieceColor.Black, difficulty);

            this.Resize += ChessForm_Resize;
            UpdateSizes();
        }

        // Сетевая игра — ХОСТ (принимает готовый сервер)
        public ChessForm(NetworkGameServer server, string roomKey)
        {
            _isHost = true;
            _isNetworkGame = true;
            _myColor = PieceColor.White;
            _server = server;
            _roomKey = roomKey;
            _isWhitePlayer = true;
            _isWhitePerspective = true; // стартовая перспектива = твоя сторона

            _controller = new GameController();
            _currentValidMoves = new List<Move>();
            _hoverValidMoves = new List<Move>();

            SetupFullScreen();
            CreateTitleLabel();
            CreateBoard();
            CreateButtons();

            _server.OnMoveReceived += OnNetworkMoveReceived;
            _server.OnError += OnNetworkError;

            _controller.OnGameStateChanged += OnNetworkGameStateChanged;
            _controller.OnGameOver += ShowGameOver;
            _controller.OnError += ShowError;
            _controller.StartNewGame();

            this.Resize += ChessForm_Resize;
            UpdateSizes();
        }

        // Сетевая игра — КЛИЕНТ (подключается и получает клиент)
        public ChessForm(string host, int port, string roomKey)
        {
            _isHost = false;
            _isNetworkGame = true;
            _myColor = PieceColor.Black;
            _roomKey = roomKey;
            _isWhitePerspective = false;

            _controller = new GameController();
            _currentValidMoves = new List<Move>();
            _hoverValidMoves = new List<Move>();

            SetupFullScreen();
            CreateTitleLabel();
            CreateBoard();
            CreateButtons();

            _client = new NetworkGameClient();
            _client.OnConnected += () =>
            {
                this.Invoke(() => UpdateTitle());
            };
            _client.OnMoveReceived += OnNetworkMoveReceived;
            _client.OnError += (err) =>
            {
                this.Invoke(() =>
                {
                    MessageBox.Show(err, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.Close();
                });
            };

            _controller.OnGameStateChanged += OnNetworkGameStateChanged;
            _controller.OnGameOver += ShowGameOver;
            _controller.OnError += ShowError;
            _controller.StartNewGame();

            this.Shown += async (s, e) =>
            {
                await ConnectClient(host, port, roomKey);
            };

            this.Resize += ChessForm_Resize;
            UpdateSizes();
        }
        private async Task ConnectClient(string host, int port, string roomKey)
        {
            var connected = await _client!.ConnectAsync(host, port, roomKey);
            if (!connected)
            {
                this.Invoke(() => this.Close());
            }
        }

        // ==================== СЕТЕВЫЕ ОБРАБОТЧИКИ ====================

        private void OnNetworkMoveReceived(NetworkMoveData moveData)
        {
            this.Invoke(() =>
            {
                // Противник сделал ход — применяем на нашей доске
                _controller.TryMakeMove(moveData.FromRow, moveData.FromCol, moveData.ToRow, moveData.ToCol);
                _controller.ConfirmMove();
                _lastMove = _controller.LastMove;
                UpdateBoard();
            });
        }

        private void OnNetworkError(string error)
        {
            this.Invoke(() =>
            {
                MessageBox.Show(error, "Сетевая ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            });
        }

        private void OnNetworkGameStateChanged()
        {
            UpdateBoard();

            // После подтверждения СВОЕГО хода — отправляем противнику
            if (!_controller.IsPendingConfirmation && !_controller.IsGameOver && _isNetworkGame)
            {
                var lastMove = _controller.LastMove;
                if (lastMove != null && _controller.CurrentPlayer != _myColor)
                {
                    // Только что подтвердили свой ход (CurrentPlayer уже переключился на противника)
                    if (_isHost)
                        _ = _server!.SendMoveAsync(lastMove.FromRow, lastMove.FromColumn, lastMove.ToRow, lastMove.ToColumn);
                    else
                        _ = _client!.SendMoveAsync(lastMove.FromRow, lastMove.FromColumn, lastMove.ToRow, lastMove.ToColumn);
                }
            }
        }

        // ==================== БОТ ====================

        private void OnBotGameStateChanged()
        {
            UpdateBoard();

            if (!_controller.IsPendingConfirmation && !_controller.IsGameOver && _bot != null)
            {
                if (_controller.LastMove != null && _controller.CurrentPlayer == PieceColor.White)
                    _lastMove = _controller.LastMove;

                UpdateBoard();

                if (_controller.CurrentPlayer == PieceColor.Black)
                {
                    Task.Delay(800).ContinueWith(_ => this.Invoke(() =>
                    {
                        if (!_controller.IsPendingConfirmation && !_controller.IsGameOver)
                            _bot.MakeMove();
                    }));
                }
            }
        }

        // ==================== ОТОБРАЖЕНИЕ ====================

        private int ToDisplayRow(int realRow)
        {
            return _isWhitePerspective ? realRow : BoardSize - 1 - realRow;
        }

        private int ToRealRow(int displayRow)
        {
            return _isWhitePerspective ? displayRow : BoardSize - 1 - displayRow;
        }

        private void SetupFullScreen()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.Bounds = Screen.PrimaryScreen!.Bounds;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) this.Close();
                else if (e.KeyCode == Keys.F11)
                {
                    if (this.FormBorderStyle == FormBorderStyle.None)
                    {
                        this.WindowState = FormWindowState.Normal;
                        this.FormBorderStyle = FormBorderStyle.Sizable;
                    }
                    else
                    {
                        this.WindowState = FormWindowState.Maximized;
                        this.FormBorderStyle = FormBorderStyle.None;
                        this.Bounds = Screen.PrimaryScreen!.Bounds;
                    }
                    UpdateSizes();
                    RepositionControls();
                    this.Invalidate();
                }
            };
            this.BackColor = Color.FromArgb(25, 25, 25);
            this.Paint += ChessForm_Paint;
        }

        private void CreateTitleLabel()
        {
            _titleLabel = new Label
            {
                Text = "ХОД БЕЛЫХ",
                Font = new Font("Segoe UI", 48, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                BackColor = Color.Transparent
            };
            this.Controls.Add(_titleLabel);
        }

        private void UpdateTitle()
        {
            if (_titleLabel == null) return;

            string turn;
            Color titleColor;

            if (_isNetworkGame)
            {
                if (_controller.IsGameOver)
                {
                    turn = _controller.GameResult;
                    titleColor = Color.FromArgb(255, 60, 60);
                }
                else
                {
                    string role = _isHost ? $"ХОСТ ({_roomKey})" : $"ГОСТЬ ({_roomKey})";
                    string action = _controller.CurrentPlayer == _myColor ? "ВАШ ХОД" : "ХОД ПРОТИВНИКА";
                    turn = $"{role} — {action}";
                    titleColor = _controller.CurrentPlayer == _myColor ? Color.White : Color.FromArgb(180, 180, 180);
                }

                _titleLabel.Text = turn;
                _titleLabel.ForeColor = titleColor;
                return;
            }

            if (_bot != null)
                turn = _controller.CurrentPlayer == PieceColor.White ? "ВАШ ХОД" : "ХОД БОТА";
            else
                turn = _controller.CurrentPlayer == PieceColor.White ? "ХОД БЕЛЫХ" : "ХОД ЧЁРНЫХ";

            if (_controller.IsGameOver)
            {
                turn = _controller.GameResult;
                titleColor = Color.FromArgb(255, 60, 60);
            }
            else
            {
                titleColor = _controller.CurrentPlayer == PieceColor.White
                    ? Color.White
                    : Color.FromArgb(180, 180, 180);
            }

            _titleLabel.Text = turn;
            _titleLabel.ForeColor = titleColor;
        }

        // ==================== РАЗМЕРЫ И ПОЗИЦИИ ====================

        private void ChessForm_Resize(object? sender, EventArgs e)
        {
            UpdateSizes();
            RepositionControls();
            this.Invalidate();
        }

        private void UpdateSizes()
        {
            int topMargin = 180;
            int bottomMargin = 150;

            int maxBoardWidth = this.ClientSize.Width - 250;
            int maxBoardHeight = this.ClientSize.Height - topMargin - bottomMargin;

            SquareSize = Math.Min(maxBoardWidth, maxBoardHeight) / BoardSize;

            BoardOffsetX = (this.ClientSize.Width - (SquareSize * BoardSize)) / 2;
            BoardOffsetY = topMargin + (maxBoardHeight - SquareSize * BoardSize) / 2;
        }

        private void RepositionControls()
        {
            if (_boardButtons == null) return;

            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    if (_boardButtons[row, col] != null)
                    {
                        _boardButtons[row, col].Size = new Size(SquareSize, SquareSize);
                        _boardButtons[row, col].Location = new Point(
                            BoardOffsetX + col * SquareSize,
                            BoardOffsetY + row * SquareSize
                        );
                        _boardButtons[row, col].Font = new Font("Segoe UI", SquareSize * 0.4f, FontStyle.Regular);
                    }
                }
            }

            if (_titleLabel != null)
            {
                _titleLabel.Size = new Size(this.ClientSize.Width - 40, 80);
                _titleLabel.Location = new Point(20, 25);
                _titleLabel.Font = new Font("Segoe UI", Math.Max(30, SquareSize * 0.55f), FontStyle.Bold);
            }

            int buttonWidth = 180;
            int buttonHeight = 45;
            int spacing = 15;

            int rightButtonsX = this.ClientSize.Width - buttonWidth - 30;
            int rightButtonsTotalHeight = buttonHeight * 4 + spacing * 3;
            int rightButtonsStartY = BoardOffsetY + (SquareSize * BoardSize - rightButtonsTotalHeight) / 2;

            // 1. Перспектива (сверху)
            if (_flipBoardButton != null)
            {
                _flipBoardButton.Size = new Size(buttonWidth, buttonHeight);
                _flipBoardButton.Location = new Point(rightButtonsX, rightButtonsStartY);
            }
            // 2. Суперпозиция
            if (_toggleSuperpositionButton != null)
            {
                _toggleSuperpositionButton.Size = new Size(buttonWidth, buttonHeight);
                _toggleSuperpositionButton.Location = new Point(rightButtonsX, rightButtonsStartY + buttonHeight + spacing);
            }
            // 3. Новая игра
            if (_newGameButton != null)
            {
                _newGameButton.Size = new Size(buttonWidth, buttonHeight);
                _newGameButton.Location = new Point(rightButtonsX, rightButtonsStartY + (buttonHeight + spacing) * 2);
            }
            // 4. Выйти (снизу)
            if (_backToMenuButton != null)
            {
                _backToMenuButton.Size = new Size(buttonWidth, buttonHeight);
                _backToMenuButton.Location = new Point(rightButtonsX, rightButtonsStartY + (buttonHeight + spacing) * 3);
            }

            int bottomButtonWidth = 200;
            int bottomButtonHeight = 50;
            int totalBottomWidth = bottomButtonWidth * 2 + 20;
            int bottomButtonsX = (this.ClientSize.Width - totalBottomWidth) / 2;
            int bottomButtonsY = this.ClientSize.Height - bottomButtonHeight - 30;

            if (_confirmButton != null)
            {
                _confirmButton.Size = new Size(bottomButtonWidth, bottomButtonHeight);
                _confirmButton.Location = new Point(bottomButtonsX, bottomButtonsY);
            }
            if (_cancelButton != null)
            {
                _cancelButton.Size = new Size(bottomButtonWidth, bottomButtonHeight);
                _cancelButton.Location = new Point(bottomButtonsX + bottomButtonWidth + 20, bottomButtonsY);
            }

        }

        private Color GetBorderColor()
        {
            if (_controller != null && _controller.IsGameOver)
                return Color.FromArgb(220, 50, 50);

            return _controller?.CurrentPlayer == PieceColor.White
                ? Color.FromArgb(230, 230, 230)
                : Color.FromArgb(30, 30, 30);
        }

        private void ChessForm_Paint(object? sender, PaintEventArgs e)
        {
            MenuForm.DrawCheckerboardBackground(e.Graphics, this.ClientSize);

            int boardX = BoardOffsetX - BorderThickness;
            int boardY = BoardOffsetY - BorderThickness;
            int boardWidth = BoardSize * SquareSize + BorderThickness * 2;
            int boardHeight = BoardSize * SquareSize + BorderThickness * 2;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath shadowPath = CreateRoundedRectangle(boardX + 8, boardY + 8, boardWidth, boardHeight, CornerRadius))
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                e.Graphics.FillPath(shadowBrush, shadowPath);

            using (GraphicsPath path = CreateRoundedRectangle(boardX, boardY, boardWidth, boardHeight, CornerRadius))
            using (SolidBrush borderBrush = new SolidBrush(GetBorderColor()))
                e.Graphics.FillPath(borderBrush, path);

            using (GraphicsPath outlinePath = CreateRoundedRectangle(boardX + 2, boardY + 2, boardWidth - 4, boardHeight - 4, CornerRadius - 2))
            using (Pen outlinePen = new Pen(Color.FromArgb(
                Math.Max(0, GetBorderColor().R - 50),
                Math.Max(0, GetBorderColor().G - 50),
                Math.Max(0, GetBorderColor().B - 50)), 3))
                e.Graphics.DrawPath(outlinePen, outlinePath);
        }

        private GraphicsPath CreateRoundedRectangle(int x, int y, int width, int height, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(x, y, radius * 2, radius * 2, 180, 90);
            path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
            path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ==================== КНОПКИ ====================

        private Button CreateStyledButton(string text)
        {
            Button button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.Transparent;
            button.FlatAppearance.MouseDownBackColor = Color.Transparent;
            button.BackColor = Color.Transparent;

            bool isHovered = false;
            bool isPressed = false;

            button.MouseEnter += (s, e) => { isHovered = true; button.Invalidate(); };
            button.MouseLeave += (s, e) => { isHovered = false; isPressed = false; button.Invalidate(); };
            button.MouseDown += (s, e) => { isPressed = true; button.Invalidate(); };
            button.MouseUp += (s, e) => { isPressed = false; button.Invalidate(); };

            button.Paint += (s, e) =>
            {
                Button btn = (Button)s!;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.Clear(Color.Transparent);

                Color bgColor = isPressed ? Color.FromArgb(90, 90, 90) :
                                isHovered ? Color.FromArgb(70, 70, 70) :
                                Color.FromArgb(50, 50, 50);
                Color borderColor = isHovered ? Color.FromArgb(150, 150, 150) : Color.FromArgb(100, 100, 100);
                int radius = btn.Height / 5;

                using (GraphicsPath path = CreateRoundedRectangle(0, 0, btn.Width - 1, btn.Height - 1, radius))
                using (SolidBrush brush = new SolidBrush(bgColor))
                    e.Graphics.FillPath(brush, path);

                using (GraphicsPath path = CreateRoundedRectangle(0, 0, btn.Width - 2, btn.Height - 2, radius))
                using (Pen pen = new Pen(borderColor, 2))
                    e.Graphics.DrawPath(pen, path);

                TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font,
                    new Rectangle(0, 0, btn.Width, btn.Height),
                    isHovered ? Color.White : Color.FromArgb(220, 220, 220),
                    Color.Transparent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            return button;
        }

        private void CreateButtons()
        {
            _confirmButton = CreateStyledButton("Подтвердить");
            _confirmButton.Enabled = false;
            _confirmButton.Click += (s, e) =>
            {
                _controller.ConfirmMove();
                _lastMove = _controller.LastMove;
                ClearSelection();
                this.Invalidate();
            };
            this.Controls.Add(_confirmButton);

            _cancelButton = CreateStyledButton("Отменить");
            _cancelButton.Enabled = false;
            _cancelButton.Click += (s, e) =>
            {
                _controller.CancelMove();
                _lastMove = null;
                ClearSelection();
                this.Invalidate();
            };
            this.Controls.Add(_cancelButton);

            _newGameButton = CreateStyledButton("Новая игра");
            _newGameButton.Click += (s, e) =>
            {
                _controller.StartNewGame();
                _lastMove = null;
                ClearSelection();
                this.Invalidate();
            };
            this.Controls.Add(_newGameButton);

            _backToMenuButton = CreateStyledButton("Выйти");
            _backToMenuButton.Click += (s, e) =>
            {
                _server?.Stop();
                _client?.Disconnect();
                this.Close();
            };
            this.Controls.Add(_backToMenuButton);

            _toggleSuperpositionButton = CreateStyledButton("Суперпозиция");
            _toggleSuperpositionButton.Click += (s, e) =>
            {
                _showSuperposition = !_showSuperposition;
                UpdateAllButtons();
            };
            _toggleSuperpositionButton.Paint += ToggleSuperpositionButton_Paint;
            this.Controls.Add(_toggleSuperpositionButton);

            _flipBoardButton = CreateStyledButton("🔄");
            _flipBoardButton.Click += (s, e) =>
            {
                _isWhitePerspective = !_isWhitePerspective;
                UpdateAllButtons();
                this.Invalidate();
            };
            _flipBoardButton.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            this.Controls.Add(_flipBoardButton);
        }

        private void ToggleSuperpositionButton_Paint(object? sender, PaintEventArgs e)
        {
            Button btn = (Button)sender!;
            if (!_showSuperposition)
            {
                using (Pen pen = new Pen(Color.FromArgb(200, 255, 80, 80), 3))
                    e.Graphics.DrawLine(pen, 0, 0, btn.Width, btn.Height);
            }
        }

        // ==================== ДОСКА ====================

        private void CreateBoard()
        {
            _boardButtons = new Button[BoardSize, BoardSize];
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    Button button = new Button
                    {
                        FlatStyle = FlatStyle.Flat,
                        Tag = (row, col),
                        TextAlign = ContentAlignment.MiddleCenter,
                        Cursor = Cursors.Hand
                    };
                    button.FlatAppearance.BorderSize = 0;
                    button.FlatAppearance.MouseOverBackColor = Color.Transparent;
                    button.FlatAppearance.MouseDownBackColor = Color.Transparent;
                    button.MouseEnter += Button_MouseEnter;
                    button.MouseLeave += Button_MouseLeave;
                    button.Click += BoardButton_Click;
                    button.Paint += Button_Paint;

                    _boardButtons[row, col] = button;
                    this.Controls.Add(button);
                }
            }
        }

        // ==================== ОТРИСОВКА КЛЕТОК ====================

        private void Button_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button button) return;

            var (displayRow, displayCol) = ((int, int))button.Tag;
            int realRow = ToRealRow(displayRow);
            bool isLight = (realRow + displayCol) % 2 == 0;

            Color baseColor = isLight ? LightSquare : DarkSquare;

            bool isSelected = _selectedRow == realRow && _selectedCol == displayCol;
            bool isHovered = _hoveredRow == displayRow && _hoveredCol == displayCol;

            bool isValidMoveFromSelection = _currentValidMoves.Any(m => m.ToRow == realRow && m.ToColumn == displayCol);
            bool isValidMoveFromHover = _hoverValidMoves.Any(m => m.ToRow == realRow && m.ToColumn == displayCol);

            bool isMySuperposition = _controller.Board.SuperpositionOwner == _controller.CurrentPlayer;
            bool isPending = _controller.IsPendingConfirmation;
            bool hasActiveSuperposition = _controller.Board.CurrentPhantoms.Count > 0;

            bool isLastMoveFrom = _lastMove != null &&
                _lastMove.FromRow == realRow && _lastMove.FromColumn == displayCol;
            bool isLastMoveTo = _lastMove != null &&
                _lastMove.ToRow == realRow && _lastMove.ToColumn == displayCol;

            if (!isPending && hasActiveSuperposition)
                isLastMoveTo = false;

            Piece? pieceOnCell = _controller.Board.Grid[realRow, displayCol];
            bool isPhantom = pieceOnCell != null && pieceOnCell.State == PieceState.Phantom;

            bool isRealPiece = isPhantom && _controller.Board.RealPiecePosition.HasValue &&
                _controller.Board.RealPiecePosition.Value.row == realRow &&
                _controller.Board.RealPiecePosition.Value.column == displayCol;

            if (isRealPiece && isPending && isMySuperposition)
                isPhantom = false;

            bool hideThisPhantom = isPhantom && !_showSuperposition && pieceOnCell!.Color == _myColor && !isRealPiece;

            if (_bot != null && pieceOnCell != null && pieceOnCell.Color != _myColor)
                hideThisPhantom = false;

            if (hideThisPhantom)
            {
                isPhantom = false;
                pieceOnCell = null;
            }

            var finalColor = baseColor;

            bool isMyLastMoveFrom = false;
            bool isMyLastMoveTo = false;

            if ((_bot != null || _isNetworkGame) && _myLastMove != null)
            {
                isMyLastMoveFrom = _myLastMove.FromRow == realRow && _myLastMove.FromColumn == displayCol;
                isMyLastMoveTo = _myLastMove.ToRow == realRow && _myLastMove.ToColumn == displayCol;
            }

            if (isSelected)
                finalColor = isLight ? SelectedLight : SelectedDark;
            else if (isValidMoveFromHover || (isValidMoveFromSelection && isHovered))
                finalColor = isLight ? ValidMoveLight : ValidMoveDark;
            else if (isValidMoveFromSelection)
                finalColor = isLight ? ValidMoveLight : ValidMoveDark;
            else if (isMyLastMoveFrom || isMyLastMoveTo)
                finalColor = isLight ? LastMoveLight : LastMoveDark;
            else if (isLastMoveFrom || isLastMoveTo)
                finalColor = isLight ? LastMoveLight : LastMoveDark;
            else if (isPhantom)
                finalColor = isLight ? PhantomLight : PhantomDark;
            else if (isHovered && _selectedRow == null)
            {
                Piece? hoveredPiece = _controller.Board.Grid[realRow, displayCol];
                if (hoveredPiece != null && hoveredPiece.Color == _controller.CurrentPlayer && hoveredPiece.State == PieceState.Real)
                    finalColor = isLight ? HoverLight : HoverDark;
            }

            int cellRadius = CornerRadius;
            bool isTopLeft = (displayRow == 0 && displayCol == 0);
            bool isTopRight = (displayRow == 0 && displayCol == BoardSize - 1);
            bool isBottomLeft = (displayRow == BoardSize - 1 && displayCol == 0);
            bool isBottomRight = (displayRow == BoardSize - 1 && displayCol == BoardSize - 1);

            if (isTopLeft || isTopRight || isBottomLeft || isBottomRight)
            {
                Color borderColor = GetBorderColor();
                using (SolidBrush borderBrush = new SolidBrush(borderColor))
                    e.Graphics.FillRectangle(borderBrush, 0, 0, SquareSize, SquareSize);

                GraphicsPath cellPath = new GraphicsPath();

                if (isTopLeft)
                {
                    cellPath.AddArc(0, 0, cellRadius * 2, cellRadius * 2, 180, 90);
                    cellPath.AddLine(cellRadius, 0, SquareSize, 0);
                    cellPath.AddLine(SquareSize, 0, SquareSize, SquareSize);
                    cellPath.AddLine(SquareSize, SquareSize, 0, SquareSize);
                    cellPath.AddLine(0, SquareSize, 0, cellRadius);
                }
                else if (isTopRight)
                {
                    cellPath.AddArc(SquareSize - cellRadius * 2, 0, cellRadius * 2, cellRadius * 2, 270, 90);
                    cellPath.AddLine(SquareSize, cellRadius, SquareSize, SquareSize);
                    cellPath.AddLine(SquareSize, SquareSize, 0, SquareSize);
                    cellPath.AddLine(0, SquareSize, 0, 0);
                    cellPath.AddLine(0, 0, SquareSize - cellRadius, 0);
                }
                else if (isBottomLeft)
                {
                    cellPath.AddArc(0, SquareSize - cellRadius * 2, cellRadius * 2, cellRadius * 2, 90, 90);
                    cellPath.AddLine(cellRadius, SquareSize, SquareSize, SquareSize);
                    cellPath.AddLine(SquareSize, SquareSize, SquareSize, 0);
                    cellPath.AddLine(SquareSize, 0, 0, 0);
                    cellPath.AddLine(0, 0, 0, SquareSize - cellRadius);
                }
                else
                {
                    cellPath.AddArc(SquareSize - cellRadius * 2, SquareSize - cellRadius * 2, cellRadius * 2, cellRadius * 2, 0, 90);
                    cellPath.AddLine(SquareSize, SquareSize - cellRadius, SquareSize, 0);
                    cellPath.AddLine(SquareSize, 0, 0, 0);
                    cellPath.AddLine(0, 0, 0, SquareSize);
                    cellPath.AddLine(0, SquareSize, SquareSize - cellRadius, SquareSize);
                }

                cellPath.CloseFigure();

                using (SolidBrush brush = new SolidBrush(finalColor))
                    e.Graphics.FillPath(brush, cellPath);

                using (Pen borderPen = new Pen(Color.FromArgb(
                    Math.Max(0, finalColor.R - 25),
                    Math.Max(0, finalColor.G - 25),
                    Math.Max(0, finalColor.B - 25)), 1))
                    e.Graphics.DrawPath(borderPen, cellPath);

                cellPath.Dispose();
            }
            else
            {
                using (SolidBrush brush = new SolidBrush(finalColor))
                    e.Graphics.FillRectangle(brush, 0, 0, SquareSize, SquareSize);

                using (Pen borderPen = new Pen(Color.FromArgb(
                    Math.Max(0, finalColor.R - 25),
                    Math.Max(0, finalColor.G - 25),
                    Math.Max(0, finalColor.B - 25)), 1))
                    e.Graphics.DrawRectangle(borderPen, 0, 0, SquareSize - 1, SquareSize - 1);
            }

            bool showGhostPiece = (_selectedRow.HasValue && isHovered && isValidMoveFromSelection) ||
                                  (_hoveredRow.HasValue && isHovered && isValidMoveFromHover && _selectedRow == null);

            if (showGhostPiece)
            {
                int sourceRow = _selectedRow ?? ToRealRow(_hoveredRow!.Value);
                int sourceCol = _selectedCol ?? _hoveredCol!.Value;

                Piece? sourcePiece = _controller.Board.Grid[sourceRow, sourceCol];
                if (sourcePiece != null)
                {
                    string symbol = sourcePiece.GetUnicodeSymbol();
                    using (Font font = new Font("Segoe UI", SquareSize * 0.4f, FontStyle.Regular))
                    using (SolidBrush ghostBrush = new SolidBrush(Color.FromArgb(80,
                        sourcePiece.Color == PieceColor.White ? Color.White : Color.Black)))
                    {
                        SizeF textSize = e.Graphics.MeasureString(symbol, font);
                        float x = (SquareSize - textSize.Width) / 2;
                        float y = (SquareSize - textSize.Height) / 2;
                        e.Graphics.DrawString(symbol, font, ghostBrush, x, y);
                    }
                }
            }

            if (pieceOnCell != null && !showGhostPiece)
            {
                string symbol = pieceOnCell.GetUnicodeSymbol();
                using (Font font = new Font("Segoe UI", SquareSize * 0.4f, FontStyle.Regular))
                {
                    bool isBright = !isPhantom || isMyLastMoveFrom || isMyLastMoveTo || (isRealPiece && isPending && isMySuperposition);
                    int alpha = isBright ? 255 : 120;

                    Color pieceColor = pieceOnCell.Color == PieceColor.White
                        ? Color.FromArgb(alpha, 255, 255, 255)
                        : Color.FromArgb(alpha, 0, 0, 0);

                    using (SolidBrush pieceBrush = new SolidBrush(pieceColor))
                    {
                        SizeF textSize = e.Graphics.MeasureString(symbol, font);
                        float x = (SquareSize - textSize.Width) / 2;
                        float y = (SquareSize - textSize.Height) / 2;
                        e.Graphics.DrawString(symbol, font, pieceBrush, x, y);
                    }
                }
            }
        }

        // ==================== МЫШЬ ====================

        private void Button_MouseEnter(object? sender, EventArgs e)
        {
            if (sender is not Button button) return;

            var (displayRow, displayCol) = ((int, int))button.Tag;
            int realRow = ToRealRow(displayRow);

            _hoveredRow = displayRow;
            _hoveredCol = displayCol;

            if (_selectedRow == null)
            {
                if (_controller.TryGetPiece(realRow, displayCol, out Piece? piece) &&
                    piece.Color == _controller.CurrentPlayer &&
                    piece.State == PieceState.Real &&
                    !_controller.IsPendingConfirmation &&
                    _controller.CurrentPlayer == _myColor)
                {
                    _hoverValidMoves = _controller.Board.GetValidMoves(realRow, displayCol, piece.Color);
                }
                else if (_controller.IsPendingConfirmation &&
                         _controller.TryGetPiece(realRow, displayCol, out Piece? enemyPiece) &&
                         enemyPiece.Color != _controller.CurrentPlayer)
                {
                    _hoverValidMoves = _controller.Board.GetValidMoves(realRow, displayCol, enemyPiece.Color);
                }
                else
                {
                    _hoverValidMoves.Clear();
                }
            }

            UpdateAllButtons();
        }

        private void Button_MouseLeave(object? sender, EventArgs e)
        {
            _hoveredRow = null;
            _hoveredCol = null;
            _hoverValidMoves.Clear();
            UpdateAllButtons();
        }

        private void BoardButton_Click(object? sender, EventArgs e)
        {
            if (_controller.IsGameOver) return;

            // В сетевой игре можно ходить только когда свой ход
            if (_isNetworkGame && _controller.CurrentPlayer != _myColor) return;
            if (_isNetworkGame && _controller.IsPendingConfirmation) return;

            if (sender is not Button button) return;

            var (displayRow, displayCol) = ((int, int))button.Tag;
            int realRow = ToRealRow(displayRow);
            int realCol = displayCol;

            if (_selectedRow == null)
            {
                if (_controller.TryGetPiece(realRow, realCol, out Piece? piece) &&
                    piece.Color == _controller.CurrentPlayer &&
                    piece.State == PieceState.Real &&
                    !_controller.IsPendingConfirmation)
                {
                    _selectedRow = realRow;
                    _selectedCol = realCol;
                    _currentValidMoves = _controller.Board.GetValidMoves(realRow, realCol, piece.Color);
                    _hoverValidMoves.Clear();
                    UpdateAllButtons();
                }
                return;
            }

            if (_selectedRow == realRow && _selectedCol == realCol)
            {
                ClearSelection();
                return;
            }

            if (_currentValidMoves.Any(m => m.ToRow == realRow && m.ToColumn == realCol))
            {
                _controller.TryMakeMove(_selectedRow.Value, _selectedCol.Value, realRow, realCol);
                _myLastMove = _controller.LastMove;
                _lastMove = _controller.LastMove;
                ClearSelection();
                this.Invalidate();
                return;
            }

            if (_controller.TryGetPiece(realRow, realCol, out Piece? newPiece) &&
                newPiece.Color == _controller.CurrentPlayer &&
                newPiece.State == PieceState.Real &&
                !_controller.IsPendingConfirmation)
            {
                _selectedRow = realRow;
                _selectedCol = realCol;
                _currentValidMoves = _controller.Board.GetValidMoves(realRow, realCol, newPiece.Color);
                _hoverValidMoves.Clear();
                UpdateAllButtons();
                return;
            }

            ClearSelection();
        }

        private void ClearSelection()
        {
            _selectedRow = null;
            _selectedCol = null;
            _currentValidMoves.Clear();
            _hoverValidMoves.Clear();
            UpdateAllButtons();
        }

        private void UpdateAllButtons()
        {
            for (int row = 0; row < BoardSize; row++)
                for (int col = 0; col < BoardSize; col++)
                    _boardButtons[row, col].Invalidate();

            UpdateTitle();
        }

        private void UpdateBoard()
        {
            _confirmButton.Enabled = _controller.IsPendingConfirmation;
            _cancelButton.Enabled = _controller.IsPendingConfirmation;
            _toggleSuperpositionButton.Invalidate();

            if (_bot == null && !_isNetworkGame)
            {
                _myColor = _controller.CurrentPlayer;
                _isWhitePerspective = _controller.CurrentPlayer == PieceColor.White;
            }

            UpdateAllButtons();
            this.Invalidate();
        }

        private void ShowGameOver(string message)
        {
            UpdateTitle();
            this.Invalidate();
            MessageBox.Show(message, "Игра окончена", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _server?.Stop();
            _client?.Disconnect();
            base.OnFormClosing(e);
        }
    }
}