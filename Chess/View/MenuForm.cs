using System.Drawing.Drawing2D;
using Chess.AI;
using SuperpositionChess.Network;

namespace SuperpositionChess.View
{
    public class MenuForm : Form
    {
        private Panel botDifficultyPanel;
        private Panel infoPanel;
        private Panel networkCreatePanel;
        private Panel networkJoinPanel;
        private string joinKey = "";
        private NetworkGameServer? pendingServer;
        private string pendingKey = "";
        private bool dragging;
        private Point dragStart;

        public MenuForm()
        {
            Text = "Quantum Chess";
            Size = new Size(600, 750);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(30, 30, 30);
            Paint += MenuFormPaint;
            Load += (s, e) => CenterUi();
            Resize += (s, e) => CenterUi();

            CreateMenuButtons();
            CreateBotDifficultyPanel();
            CreateInfoPanel();
            CreateNetworkCreatePanel();
            CreateNetworkJoinPanel();

            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) { dragging = true; dragStart = e.Location; }
            };
            MouseMove += (s, e) =>
            {
                if (dragging)
                    Location = new Point(Location.X + e.X - dragStart.X, Location.Y + e.Y - dragStart.Y);
            };
            MouseUp += (s, e) => dragging = false;
        }

        private void CenterUi()
        {
            var buttonWidth = 300;
            var buttonHeight = 60;
            var spacing = 20;
            var startX = (ClientSize.Width - buttonWidth) / 2;
            var startY = 220;

            Controls[0].Location = new Point((ClientSize.Width - 500) / 2, 40);
            Controls[1].Location = new Point((ClientSize.Width - 500) / 2, 90);
            Controls[2].Location = new Point(startX, startY);
            Controls[3].Location = new Point(startX, startY + buttonHeight + spacing);
            Controls[4].Location = new Point(startX, startY + (buttonHeight + spacing) * 2);
            Controls[5].Location = new Point(startX, startY + (buttonHeight + spacing) * 3);
            Controls[6].Location = new Point(startX, startY + (buttonHeight + spacing) * 4);
            Controls[7].Location = new Point(startX, startY + (buttonHeight + spacing) * 5);

            botDifficultyPanel.Location = new Point((ClientSize.Width - botDifficultyPanel.Width) / 2,
                Math.Max(140, (ClientSize.Height - botDifficultyPanel.Height) / 2));
            infoPanel.Location = new Point((ClientSize.Width - infoPanel.Width) / 2,
                (ClientSize.Height - infoPanel.Height) / 2);
            networkCreatePanel.Location = new Point((ClientSize.Width - networkCreatePanel.Width) / 2,
                (ClientSize.Height - networkCreatePanel.Height) / 2);
            networkJoinPanel.Location = new Point((ClientSize.Width - networkJoinPanel.Width) / 2,
                Math.Max(140, (ClientSize.Height - networkJoinPanel.Height) / 2));
        }

        private void MenuFormPaint(object? sender, PaintEventArgs e) => DrawCheckerboardBackground(e.Graphics, ClientSize);

        public static void DrawCheckerboardBackground(Graphics g, Size size)
        {
            var cellSize = 200;
            var lightGray = Color.FromArgb(55, 55, 55);
            var darkGray = Color.FromArgb(40, 40, 40);

            for (var row = 0; row < size.Height / cellSize + 1; row++)
            {
                for (var col = 0; col < size.Width / cellSize + 1; col++)
                {
                    var color = (row + col) % 2 == 0 ? lightGray : darkGray;
                    using var brush = new SolidBrush(color);
                    g.FillRectangle(brush, col * cellSize, row * cellSize, cellSize, cellSize);
                }
            }
        }

        private void CreateMenuButtons()
        {
            var buttonWidth = 300;
            var buttonHeight = 60;
            var startX = (ClientSize.Width - buttonWidth) / 2;
            var startY = 220;
            var spacing = 20;

            var titleLabel = new Label
            {
                Text = "♔ QUANTUM CHESS ♚",
                Font = new Font("Segoe UI", 30, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(500, 50),
                Location = new Point((ClientSize.Width - 500) / 2, 40)
            };
            Controls.Add(titleLabel);

            var subtitleLabel = new Label
            {
                Text = "With Superposition",
                Font = new Font("Segoe UI", 16, FontStyle.Italic),
                ForeColor = Color.FromArgb(180, 180, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(500, 30),
                Location = new Point((ClientSize.Width - 500) / 2, 90)
            };
            Controls.Add(subtitleLabel);

            var localButton = CreateMenuButton("♙ Локально", startX, startY, buttonWidth, buttonHeight);
            localButton.Click += (s, e) => OpenChessForm(() => new ChessForm());
            Controls.Add(localButton);

            var botButton = CreateMenuButton("♖ Компьютер", startX, startY + buttonHeight + spacing, buttonWidth, buttonHeight);
            botButton.Click += (s, e) => { botDifficultyPanel.Visible = true; botDifficultyPanel.BringToFront(); };
            Controls.Add(botButton);

            var createButton = CreateMenuButton("♗ Создать игру", startX, startY + (buttonHeight + spacing) * 2, buttonWidth, buttonHeight);
            createButton.Click += (s, e) =>
            {
                pendingKey = RoomKeyGenerator.GenerateKey();
                pendingServer = new NetworkGameServer(5555, pendingKey);
                networkCreatePanel.Controls["keyLabel"]!.Text = pendingKey;
                networkCreatePanel.Controls["statusLabel"]!.Text = "Ожидание противника...";
                networkCreatePanel.Visible = true;
                networkCreatePanel.BringToFront();

                pendingServer.OnClientConnected += () =>
                {
                    Invoke(() =>
                    {
                        networkCreatePanel.Controls["statusLabel"]!.Text = "Противник подключился!";
                        networkCreatePanel.Controls["statusLabel"]!.ForeColor = Color.FromArgb(100, 255, 100);
                        Task.Delay(500).ContinueWith(_ => Invoke(() =>
                        {
                            Hide();
                            var chessForm = new ChessForm(pendingServer, pendingKey);
                            chessForm.FormClosed += (s, e) => Show();
                            chessForm.Show();
                            networkCreatePanel.Visible = false;
                        }));
                    });
                };
                _ = pendingServer.StartAsync();
            };
            Controls.Add(createButton);

            var joinButton = CreateMenuButton("♘ Подключиться", startX, startY + (buttonHeight + spacing) * 3, buttonWidth, buttonHeight);
            joinButton.Click += (s, e) =>
            {
                joinKey = "";
                networkJoinPanel.Controls["keyInput"]!.Text = "";
                networkJoinPanel.Visible = true;
                networkJoinPanel.BringToFront();
            };
            Controls.Add(joinButton);

            var infoButton = CreateMenuButton("♕ Инфо", startX, startY + (buttonHeight + spacing) * 4, buttonWidth, buttonHeight);
            infoButton.Click += (s, e) => { infoPanel.Visible = true; infoPanel.BringToFront(); };
            Controls.Add(infoButton);

            var exitButton = CreateMenuButton("✕ Выйти", startX, startY + (buttonHeight + spacing) * 5, buttonWidth, buttonHeight);
            exitButton.Click += (s, e) => Application.Exit();
            Controls.Add(exitButton);
        }

        private void OpenChessForm(Func<ChessForm> factory)
        {
            Hide();
            var chessForm = factory();
            chessForm.FormClosed += (s, e) => Show();
            chessForm.Show();
        }

        private void CreateNetworkCreatePanel()
        {
            networkCreatePanel = new Panel
            {
                Size = new Size(400, 250),
                BackColor = Color.FromArgb(35, 35, 35),
                Visible = false
            };
            networkCreatePanel.Paint += (s, e) =>
            {
                e.Graphics.Clear(Color.FromArgb(35, 35, 35));
                using var pen = new Pen(Color.FromArgb(100, 200, 100), 3);
                e.Graphics.DrawRectangle(pen, 1, 1, networkCreatePanel.Width - 3, networkCreatePanel.Height - 3);
            };

            var panelW = networkCreatePanel.Width;
            var titleLabel = new Label
            {
                Text = "СОЗДАНИЕ ИГРЫ",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 20, 35),
                Location = new Point(10, 15)
            };
            networkCreatePanel.Controls.Add(titleLabel);

            var keyCaptionLabel = new Label
            {
                Text = "Ключ комнаты:",
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(180, 180, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 40, 25),
                Location = new Point(20, 60)
            };
            networkCreatePanel.Controls.Add(keyCaptionLabel);

            var keyLabel = new Label
            {
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(300, 40),
                Location = new Point((panelW - 300) / 2, 90),
                Name = "keyLabel",
                BackColor = Color.FromArgb(50, 50, 50)
            };
            networkCreatePanel.Controls.Add(keyLabel);

            var statusLabel = new Label
            {
                Text = "Ожидание противника...",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.FromArgb(255, 200, 50),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 40, 25),
                Location = new Point(20, 145),
                Name = "statusLabel"
            };
            networkCreatePanel.Controls.Add(statusLabel);

            var backButton = new Button
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
            backButton.Click += (s, e) => networkCreatePanel.Visible = false;
            networkCreatePanel.Controls.Add(backButton);

            Controls.Add(networkCreatePanel);
        }

        private void CreateNetworkJoinPanel()
        {
            networkJoinPanel = new Panel
            {
                Size = new Size(400, 300),
                BackColor = Color.FromArgb(35, 35, 35),
                Visible = false
            };
            networkJoinPanel.Paint += (s, e) =>
            {
                e.Graphics.Clear(Color.FromArgb(35, 35, 35));
                using var pen = new Pen(Color.FromArgb(200, 150, 50), 3);
                e.Graphics.DrawRectangle(pen, 1, 1, networkJoinPanel.Width - 3, networkJoinPanel.Height - 3);
            };

            var panelW = networkJoinPanel.Width;
            var titleLabel = new Label
            {
                Text = "ПОДКЛЮЧЕНИЕ К ИГРЕ",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 20, 35),
                Location = new Point(10, 15)
            };
            networkJoinPanel.Controls.Add(titleLabel);

            var keyCaptionLabel = new Label
            {
                Text = "Введите ключ комнаты:",
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(180, 180, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 40, 25),
                Location = new Point(20, 60)
            };
            networkJoinPanel.Controls.Add(keyCaptionLabel);

            var keyInput = new TextBox
            {
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
            networkJoinPanel.Controls.Add(keyInput);

            var ipCaptionLabel = new Label
            {
                Text = "IP адрес:",
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(180, 180, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panelW - 40, 25),
                Location = new Point(20, 145)
            };
            networkJoinPanel.Controls.Add(ipCaptionLabel);

            var ipTextBox = new TextBox
            {
                Text = "127.0.0.1",
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 50),
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(panelW - 40, 30),
                Location = new Point(20, 170),
                Name = "ipInput"
            };
            networkJoinPanel.Controls.Add(ipTextBox);

            var buttonWidth = 130;
            var buttonsTotalWidth = buttonWidth * 2 + 20;
            var buttonsX = (panelW - buttonsTotalWidth) / 2;
            var buttonsY = 220;

            var connectButton = new Button
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
                var ip = ipTextBox.Text.Trim();
                Hide();
                var chessForm = new ChessForm(ip, 5555, keyInput.Text.Trim());
                chessForm.FormClosed += (s, e) => Show();
                chessForm.Show();
                networkJoinPanel.Visible = false;
            };
            networkJoinPanel.Controls.Add(connectButton);

            var cancelButton = new Button
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
            cancelButton.Click += (s, e) => { keyInput.Text = ""; networkJoinPanel.Visible = false; };
            networkJoinPanel.Controls.Add(cancelButton);

            Controls.Add(networkJoinPanel);
        }

        private void CreateBotDifficultyPanel()
        {
            botDifficultyPanel = new Panel
            {
                Size = new Size(350, 410),
                BackColor = Color.FromArgb(35, 35, 35),
                Visible = false
            };
            botDifficultyPanel.Paint += (s, e) =>
            {
                e.Graphics.Clear(Color.FromArgb(35, 35, 35));
                using var pen = new Pen(Color.FromArgb(180, 180, 180), 3);
                e.Graphics.DrawRectangle(pen, 1, 1, botDifficultyPanel.Width - 3, botDifficultyPanel.Height - 3);
            };

            var titleLabel = new Label
            {
                Text = "Выберите сложность",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(300, 40),
                Location = new Point(25, 20)
            };
            botDifficultyPanel.Controls.Add(titleLabel);

            var difficulties = new[] { "Лёгкий", "Средний", "Сложный", "Читер" };
            var difficultyValues = new[] { BotDifficulty.Easy, BotDifficulty.Medium, BotDifficulty.Hard, BotDifficulty.Cheater };
            var difficultyColors = new[]
            {
                Color.FromArgb(100, 200, 100), Color.FromArgb(255, 200, 50),
                Color.FromArgb(255, 80, 80), Color.FromArgb(200, 50, 200)
            };

            for (var i = 0; i < 4; i++)
            {
                var difficulty = difficultyValues[i];
                var diffButton = new Button
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
                diffButton.Click += (s, e) =>
                {
                    Hide();
                    var chessForm = new ChessForm(difficulty);
                    chessForm.FormClosed += (s, e) => Show();
                    chessForm.Show();
                    botDifficultyPanel.Visible = false;
                };
                botDifficultyPanel.Controls.Add(diffButton);
            }

            var backButton = new Button
            {
                Text = "Назад",
                Size = new Size(100, 35),
                Location = new Point(125, 340),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 200),
                BackColor = Color.FromArgb(50, 50, 50),
                Cursor = Cursors.Hand
            };
            backButton.FlatAppearance.BorderSize = 1;
            backButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            backButton.Click += (s, e) => botDifficultyPanel.Visible = false;
            botDifficultyPanel.Controls.Add(backButton);

            Controls.Add(botDifficultyPanel);
        }

        private void CreateInfoPanel()
        {
            infoPanel = new Panel
            {
                Size = new Size(520, 480),
                BackColor = Color.FromArgb(35, 35, 35),
                Visible = false
            };
            infoPanel.Paint += (s, e) =>
            {
                e.Graphics.Clear(Color.FromArgb(35, 35, 35));
                using var pen = new Pen(Color.FromArgb(180, 180, 180), 3);
                e.Graphics.DrawRectangle(pen, 1, 1, infoPanel.Width - 3, infoPanel.Height - 3);
            };

            var headerPanel = new Panel
            {
                Size = new Size(514, 45),
                Location = new Point(3, 3),
                BackColor = Color.FromArgb(50, 50, 50)
            };
            headerPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(90, 90, 90), 2);
                e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            var headerLabel = new Label
            {
                Text = "♔ ПРАВИЛА ИГРЫ ♚",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            headerPanel.Controls.Add(headerLabel);
            infoPanel.Controls.Add(headerPanel);

            var rules = @"
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
Кнопка справа от доски позволяет скрыть своих мнимых фигур для удобства обзора.

ПОДТВЕРЖДЕНИЕ ХОДА
Перед завершением хода вы видите его предпросмотр. Нажмите «Подтвердить» для завершения или «Отменить» для возврата доски.

ИГРА С КОМПЬЮТЕРОМ
Три уровня сложности: Лёгкий, Средний, Сложный. Бот не знает, где настоящая фигура среди фантомов.
";

            var scrollPanel = new NoScrollPanel
            {
                Location = new Point(15, 40),
                Size = new Size(490, 390),
                AutoScroll = true,
                BackColor = Color.Transparent
            };

            var rulesLabel = new Label
            {
                Text = rules,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(200, 200, 200),
                BackColor = Color.Transparent,
                Location = new Point(0, 0),
                AutoSize = true,
                MaximumSize = new Size(470, 0)
            };
            scrollPanel.Controls.Add(rulesLabel);
            infoPanel.Controls.Add(scrollPanel);

            var closeButton = new Button
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
            closeButton.Click += (s, e) => infoPanel.Visible = false;
            infoPanel.Controls.Add(closeButton);

            Controls.Add(infoPanel);
        }

        private Button CreateMenuButton(string text, int x, int y, int width, int height)
        {
            var button = new Button
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
                var cp = base.CreateParams;
                cp.Style &= ~0x200000;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int wmNcCalcSize = 0x83;
            if (m.Msg == wmNcCalcSize) return;
            base.WndProc(ref m);
        }
    }
}