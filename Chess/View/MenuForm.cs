using System.Drawing.Drawing2D;
using Chess.AI;
using SuperpositionChess.Network;

namespace SuperpositionChess.View
{
    public class MenuForm : Form
    {
        private Panel _botDifficultyPanel;
        private Panel _infoPanel;

        private Panel _networkCreatePanel;
        private Panel _networkJoinPanel;
        private string _joinKey = "";
        private NetworkGameServer? _pendingServer;
        private string _pendingKey = "";

        public MenuForm()
        {
            this.Text = "Quantum Chess";
            this.Size = new Size(600, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.Paint += MenuForm_Paint;
            this.Load += (s, e) => CenterUI();
            this.Resize += (s, e) => CenterUI();

            CreateMenuButtons();
            CreateBotDifficultyPanel();
            CreateInfoPanel();
            CreateNetworkCreatePanel();
            CreateNetworkJoinPanel();
        }

        private void CenterUI()
        {
            int buttonWidth = 300;
            int buttonHeight = 60;
            int spacing = 20;

            int startX = (ClientSize.Width - buttonWidth) / 2;
            int startY = 220;

            Controls[0].Location = new Point((ClientSize.Width - 500) / 2, 40);
            Controls[1].Location = new Point((ClientSize.Width - 500) / 2, 90);
            Controls[2].Location = new Point(startX, startY);
            Controls[3].Location = new Point(startX, startY + buttonHeight + spacing);
            Controls[4].Location = new Point(startX, startY + (buttonHeight + spacing) * 2);
            Controls[5].Location = new Point(startX, startY + (buttonHeight + spacing) * 3);
            Controls[6].Location = new Point(startX, startY + (buttonHeight + spacing) * 4);
            Controls[7].Location = new Point(startX, startY + (buttonHeight + spacing) * 5);

            _botDifficultyPanel.Location = new Point(
                (ClientSize.Width - _botDifficultyPanel.Width) / 2,
                (ClientSize.Height - _botDifficultyPanel.Height) / 2
            );
            _infoPanel.Location = new Point(
                (ClientSize.Width - _infoPanel.Width) / 2,
                (ClientSize.Height - _infoPanel.Height) / 2
            );
            _networkCreatePanel.Location = new Point(
                (ClientSize.Width - _networkCreatePanel.Width) / 2,
                (ClientSize.Height - _networkCreatePanel.Height) / 2
            );
            _networkJoinPanel.Location = new Point(
                (ClientSize.Width - _networkJoinPanel.Width) / 2,
                Math.Max(140, (ClientSize.Height - _networkJoinPanel.Height) / 2)
            );
        }

        private void MenuForm_Paint(object? sender, PaintEventArgs e)
        {
            DrawCheckerboardBackground(e.Graphics, this.ClientSize);
        }

        public static void DrawCheckerboardBackground(Graphics g, Size size)
        {
            int cellSize = 200;
            Color lightGray = Color.FromArgb(55, 55, 55);
            Color darkGray = Color.FromArgb(40, 40, 40);

            for (int row = 0; row < size.Height / cellSize + 1; row++)
            {
                for (int col = 0; col < size.Width / cellSize + 1; col++)
                {
                    Color color = (row + col) % 2 == 0 ? lightGray : darkGray;
                    using (SolidBrush brush = new SolidBrush(color))
                    {
                        g.FillRectangle(brush, col * cellSize, row * cellSize, cellSize, cellSize);
                    }
                }
            }
        }

        private void CreateMenuButtons()
        {
            int buttonWidth = 300;
            int buttonHeight = 60;
            int startX = (this.ClientSize.Width - buttonWidth) / 2;
            int startY = 220;
            int spacing = 20;

            Label titleLabel = new Label
            {
                Text = "♔ QUANTUM CHESS ♚",
                Font = new Font("Segoe UI", 30, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(500, 50),
                Location = new Point((this.ClientSize.Width - 500) / 2, 40)
            };
            this.Controls.Add(titleLabel);

            Label subtitleLabel = new Label
            {
                Text = "With Superposition",
                Font = new Font("Segoe UI", 16, FontStyle.Italic),
                ForeColor = Color.FromArgb(180, 180, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(500, 30),
                Location = new Point((this.ClientSize.Width - 500) / 2, 90)
            };
            this.Controls.Add(subtitleLabel);

            Button localButton = CreateMenuButton("♙ Локально", startX, startY, buttonWidth, buttonHeight);
            localButton.Click += (s, e) =>
            {
                this.Hide();
                ChessForm chessForm = new ChessForm();
                chessForm.FormClosed += (s, e) => this.Show();
                chessForm.Show();
            };
            this.Controls.Add(localButton);

            Button botButton = CreateMenuButton("♖ Компьютер", startX, startY + buttonHeight + spacing, buttonWidth, buttonHeight);
            botButton.Click += (s, e) =>
            {
                _botDifficultyPanel.Visible = true;
                _botDifficultyPanel.BringToFront();
            };
            this.Controls.Add(botButton);

            // Кнопка "Создать игру" — ИЗМЕНЁННАЯ
            Button createButton = CreateMenuButton("♗ Создать игру", startX, startY + (buttonHeight + spacing) * 2, buttonWidth, buttonHeight);
            createButton.Click += (s, e) =>
            {
                _pendingKey = RoomKeyGenerator.GenerateKey();
                _pendingServer = new NetworkGameServer(5555, _pendingKey);

                _networkCreatePanel.Controls["keyLabel"].Text = _pendingKey;
                _networkCreatePanel.Controls["statusLabel"].Text = "Ожидание противника...";
                _networkCreatePanel.Visible = true;
                _networkCreatePanel.BringToFront();

                _pendingServer.OnClientConnected += () =>
                {
                    this.Invoke(() =>
                    {
                        _networkCreatePanel.Controls["statusLabel"].Text = "Противник подключился!";
                        _networkCreatePanel.Controls["statusLabel"].ForeColor = Color.FromArgb(100, 255, 100);
                        Task.Delay(500).ContinueWith(_ => this.Invoke(() =>
                        {
                            this.Hide();
                            var chessForm = new ChessForm(_pendingServer, _pendingKey);
                            chessForm.FormClosed += (s, e) => this.Show();
                            chessForm.Show();
                            _networkCreatePanel.Visible = false;
                        }));
                    });
                };

                _ = _pendingServer.StartAsync();
            };
            this.Controls.Add(createButton);

            Button joinButton = CreateMenuButton("♘ Подключиться", startX, startY + (buttonHeight + spacing) * 3, buttonWidth, buttonHeight);
            joinButton.Click += (s, e) =>
            {
                _joinKey = "";
                _networkJoinPanel.Controls["keyInput"].Text = "";
                _networkJoinPanel.Visible = true;
                _networkJoinPanel.BringToFront();
            };
            this.Controls.Add(joinButton);

            Button infoButton = CreateMenuButton("♕ Инфо", startX, startY + (buttonHeight + spacing) * 4, buttonWidth, buttonHeight);
            infoButton.Click += (s, e) =>
            {
                _infoPanel.Visible = true;
                _infoPanel.BringToFront();
            };
            this.Controls.Add(infoButton);

            Button exitButton = CreateMenuButton("✕ Выйти", startX, startY + (buttonHeight + spacing) * 5, buttonWidth, buttonHeight);
            exitButton.Click += (s, e) => Application.Exit();
            this.Controls.Add(exitButton);
        }

        private void CreateNetworkCreatePanel()
        {
            _networkCreatePanel = new Panel
            {
                Size = new Size(400, 250),
                BackColor = Color.FromArgb(35, 35, 35),
                Visible = false,
                Name = "createPanel"
            };
            _networkCreatePanel.Paint += (s, e) =>
            {
                e.Graphics.Clear(Color.FromArgb(35, 35, 35));
                using (Pen pen = new Pen(Color.FromArgb(100, 200, 100), 3))
                {
                    e.Graphics.DrawRectangle(pen, 1, 1, _networkCreatePanel.Width - 3, _networkCreatePanel.Height - 3);
                }
            };

            int panelW = _networkCreatePanel.Width;
            int panelH = _networkCreatePanel.Height;
            int centerX = panelW / 2;

            Label titleLabel = new Label
            {
                Text = "СОЗДАНИЕ ИГРЫ",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 20, 35),
                Location = new Point(10, 15)
            };
            _networkCreatePanel.Controls.Add(titleLabel);

            Label keyCaptionLabel = new Label
            {
                Text = "Ключ комнаты:",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 40, 25),
                Location = new Point(20, 60)
            };
            _networkCreatePanel.Controls.Add(keyCaptionLabel);

            Label keyLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(300, 40),
                Location = new Point((panelW - 300) / 2, 90),
                Name = "keyLabel",
                BackColor = Color.FromArgb(50, 50, 50)
            };
            _networkCreatePanel.Controls.Add(keyLabel);

            Label statusLabel = new Label
            {
                Text = "Ожидание противника...",
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                ForeColor = Color.FromArgb(255, 200, 50),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 40, 25),
                Location = new Point(20, 145),
                Name = "statusLabel"
            };
            _networkCreatePanel.Controls.Add(statusLabel);

            Button backButton = new Button
            {
                Text = "Отмена",
                Size = new Size(120, 35),
                Location = new Point((panelW - 120) / 2, 190),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 50),
                Cursor = Cursors.Hand
            };
            backButton.FlatAppearance.BorderSize = 1;
            backButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            backButton.Click += (s, e) => _networkCreatePanel.Visible = false;
            _networkCreatePanel.Controls.Add(backButton);

            this.Controls.Add(_networkCreatePanel);
        }

        private void CreateNetworkJoinPanel()
        {
            _networkJoinPanel = new Panel
            {
                Size = new Size(400, 300),
                BackColor = Color.FromArgb(35, 35, 35),
                Visible = false,
                Name = "joinPanel"
            };
            _networkJoinPanel.Paint += (s, e) =>
            {
                e.Graphics.Clear(Color.FromArgb(35, 35, 35));
                using (Pen pen = new Pen(Color.FromArgb(200, 150, 50), 3))
                {
                    e.Graphics.DrawRectangle(pen, 1, 1, _networkJoinPanel.Width - 3, _networkJoinPanel.Height - 3);
                }
            };

            int panelW = _networkJoinPanel.Width;

            Label titleLabel = new Label
            {
                Text = "ПОДКЛЮЧЕНИЕ К ИГРЕ",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 20, 35),
                Location = new Point(10, 15)
            };
            _networkJoinPanel.Controls.Add(titleLabel);

            Label keyCaptionLabel = new Label
            {
                Text = "Введите ключ комнаты:",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 40, 25),
                Location = new Point(20, 60)
            };
            _networkJoinPanel.Controls.Add(keyCaptionLabel);

            TextBox keyInput = new TextBox
            {
                Text = "",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 50),
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(panelW - 40, 40),
                Location = new Point(20, 90),
                Name = "keyInput",
                TextAlign = HorizontalAlignment.Center,
                MaxLength = 6
            };
            _networkJoinPanel.Controls.Add(keyInput);

            Label ipCaptionLabel = new Label
            {
                Text = "IP адрес:",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 40, 25),
                Location = new Point(20, 145)
            };
            _networkJoinPanel.Controls.Add(ipCaptionLabel);

            TextBox ipTextBox = new TextBox
            {
                Text = "127.0.0.1",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 50),
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(panelW - 40, 30),
                Location = new Point(20, 170),
                Name = "ipInput"
            };
            _networkJoinPanel.Controls.Add(ipTextBox);

            // Кнопки в ряд
            int buttonWidth = 130;
            int buttonsTotalWidth = buttonWidth * 2 + 20;
            int buttonsX = (panelW - buttonsTotalWidth) / 2;
            int buttonsY = 220;

            Button connectButton = new Button
            {
                Text = "Подключиться",
                Size = new Size(buttonWidth, 35),
                Location = new Point(buttonsX, buttonsY),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(60, 60, 60),
                Cursor = Cursors.Hand
            };
            connectButton.FlatAppearance.BorderSize = 1;
            connectButton.FlatAppearance.BorderColor = Color.FromArgb(100, 200, 100);
            connectButton.Click += (s, e) =>
            {
                if (keyInput.Text.Trim().Length != 6)
                {
                    MessageBox.Show("Ключ должен содержать 6 цифр!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string ip = ipTextBox.Text.Trim();
                this.Hide();
                ChessForm chessForm = new ChessForm(ip, 5555, keyInput.Text.Trim());
                chessForm.FormClosed += (s, e) => this.Show();
                chessForm.Show();
                _networkJoinPanel.Visible = false;
            };
            _networkJoinPanel.Controls.Add(connectButton);

            Button cancelButton = new Button
            {
                Text = "Назад",
                Size = new Size(buttonWidth, 35),
                Location = new Point(buttonsX + buttonWidth + 20, buttonsY),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 200),
                BackColor = Color.FromArgb(50, 50, 50),
                Cursor = Cursors.Hand
            };
            cancelButton.FlatAppearance.BorderSize = 1;
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            cancelButton.Click += (s, e) =>
            {
                keyInput.Text = "";
                _networkJoinPanel.Visible = false;
            };
            _networkJoinPanel.Controls.Add(cancelButton);

            this.Controls.Add(_networkJoinPanel);
        }

        private void CreateBotDifficultyPanel()
        {
            _botDifficultyPanel = new Panel
            {
                Size = new Size(350, 350),
                Location = new Point((this.ClientSize.Width - 350) / 2, 180),
                BackColor = Color.FromArgb(35, 35, 35),
                Visible = false
            };

            _botDifficultyPanel.Paint += (s, e) =>
            {
                e.Graphics.Clear(Color.FromArgb(35, 35, 35));
                using (Pen pen = new Pen(Color.FromArgb(180, 180, 180), 3))
                {
                    e.Graphics.DrawRectangle(pen, 1, 1, _botDifficultyPanel.Width - 3, _botDifficultyPanel.Height - 3);
                }
            };

            Label titleLabel = new Label
            {
                Text = "Выберите сложность",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(300, 40),
                Location = new Point(25, 20)
            };
            _botDifficultyPanel.Controls.Add(titleLabel);

            string[] difficulties = { "Лёгкий", "Средний", "Сложный" };
            BotDifficulty[] difficultyValues = { BotDifficulty.Easy, BotDifficulty.Medium, BotDifficulty.Hard };
            Color[] difficultyColors = { Color.FromArgb(100, 200, 100), Color.FromArgb(255, 200, 50), Color.FromArgb(255, 80, 80) };

            for (int i = 0; i < 3; i++)
            {
                Button diffButton = new Button
                {
                    Text = difficulties[i],
                    Size = new Size(280, 50),
                    Location = new Point(35, 80 + i * 60),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 14, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(60, 60, 60),
                    Cursor = Cursors.Hand
                };
                diffButton.FlatAppearance.BorderSize = 1;
                diffButton.FlatAppearance.BorderColor = difficultyColors[i];
                diffButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);

                var difficulty = difficultyValues[i];
                diffButton.Click += (s, e) =>
                {
                    this.Hide();
                    ChessForm chessForm = new ChessForm(difficulty);
                    chessForm.FormClosed += (s, e) => this.Show();
                    chessForm.Show();
                    _botDifficultyPanel.Visible = false;
                };

                _botDifficultyPanel.Controls.Add(diffButton);
            }

            Button backButton = new Button
            {
                Text = "Назад",
                Size = new Size(100, 35),
                Location = new Point(125, 300),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 200),
                BackColor = Color.FromArgb(50, 50, 50),
                Cursor = Cursors.Hand
            };
            backButton.FlatAppearance.BorderSize = 1;
            backButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            backButton.Click += (s, e) => _botDifficultyPanel.Visible = false;
            _botDifficultyPanel.Controls.Add(backButton);

            this.Controls.Add(_botDifficultyPanel);
        }

        private void CreateInfoPanel()
        {
            _infoPanel = new Panel
            {
                Size = new Size(520, 480),
                Location = new Point((this.ClientSize.Width - 520) / 2, 140),
                BackColor = Color.FromArgb(35, 35, 35),
                Visible = false
            };

            _infoPanel.Paint += (s, e) =>
            {
                e.Graphics.Clear(Color.FromArgb(35, 35, 35));
                using (Pen pen = new Pen(Color.FromArgb(180, 180, 180), 3))
                {
                    e.Graphics.DrawRectangle(pen, 1, 1, _infoPanel.Width - 3, _infoPanel.Height - 3);
                }
            };

            Panel headerPanel = new Panel
            {
                Size = new Size(514, 45),
                Location = new Point(3, 3),
                BackColor = Color.FromArgb(50, 50, 50)
            };

            headerPanel.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(90, 90, 90), 2))
                {
                    e.Graphics.DrawLine(
                        pen,
                        0,
                        headerPanel.Height - 1,
                        headerPanel.Width,
                        headerPanel.Height - 1
                    );
                }
            };

            Label headerLabel = new Label
            {
                Text = "♔ ПРАВИЛА ИГРЫ ♚",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };

            headerPanel.Controls.Add(headerLabel);
            _infoPanel.Controls.Add(headerPanel);

            string rules = @"
ОБЩИЕ ПРАВИЛА
Все стандартные правила шахмат сохраняются: ходы фигур, рокировка, взятие на проходе, превращение пешки.

СУПЕРПОЗИЦИЯ
Когда фигура ходит на пустую клетку без взаимодействия с другими фигурами, она входит в состояние суперпозиции. На всех остальных клетках, куда фигура могла бы пойти без взаимодействия, появляются её мнимые копии (фантомы). Противник не знает, какая из копий настоящая.

СВОЙСТВА МНИМЫХ ФИГУР
• Мнимые фигуры выглядят полупрозрачными (серый фон)
• Они НЕ блокируют движение противника — можно проходить сквозь
• Их можно рубить, но они просто исчезают
• Настоящая фигура при этом остаётся на месте
• Ходить мнимыми фигурами нельзя

ЖИЗНЬ СУПЕРПОЗИЦИИ
Суперпозиция живёт один ход противника. После его хода все мнимые копии исчезают, остаётся только настоящая фигура.

ШАХ, МАТ И ПОБЕДА
• Король может ходить под шах
• Нет обязательной защиты от шаха
• Победа — только взятие настоящего (реального) короля
• Если съеден мнимый король — игра продолжается

СТОЛКНОВЕНИЕ С РЕАЛЬНОЙ ФИГУРОЙ
При ходе сквозь мнимые фигуры, если на пути встречается реальная фигура врага, ваша фигура останавливается перед ней. Это не взятие.

КНОПКА СУПЕРПОЗИЦИИ
Кнопка справа от доски позволяет скрыть своих мнимых фигур для удобства обзора. Реальная фигура всегда видна.

ПОДТВЕРЖДЕНИЕ ХОДА
Перед завершением хода вы видите его предпросмотр. Нажмите «Подтвердить» для завершения или «Отменить» для возврата доски.

ИГРА С КОМПЬЮТЕРОМ
Три уровня сложности: Лёгкий, Средний, Сложный. Бот не знает, где настоящая фигура среди фантомов.
";

            NoScrollPanel scrollPanel = new NoScrollPanel
            {
                Location = new Point(15, 40),
                Size = new Size(490, 390),
                AutoScroll = true,
                BackColor = Color.Transparent
            };

            Label rulesLabel = new Label
            {
                Text = rules,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 200, 200),
                BackColor = Color.Transparent,
                Location = new Point(0, 0),
                AutoSize = true,
                MaximumSize = new Size(470, 0)
            };

            scrollPanel.Controls.Add(rulesLabel);
            _infoPanel.Controls.Add(scrollPanel);

            // Кнопка закрыть
            Button closeButton = new Button
            {
                Text = "Закрыть",
                Size = new Size(120, 35),
                Location = new Point(200, 438),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(55, 55, 55),
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 1;
            closeButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 70);
            closeButton.Click += (s, e) => _infoPanel.Visible = false;
            _infoPanel.Controls.Add(closeButton);

            this.Controls.Add(_infoPanel);
        }

        private Button CreateMenuButton(string text, int x, int y, int width, int height)
        {
            Button button = new Button
            {
                Text = text,
                Size = new Size(width, height),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(60, 60, 60),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 2;
            button.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(100, 100, 100);

            return button;
        }
    }

    public class NoScrollPanel : Panel
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style &= ~0x200000; // WS_VSCROLL
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCCALCSIZE = 0x83;
            if (m.Msg == WM_NCCALCSIZE)
                return;

            base.WndProc(ref m);
        }
    }

}