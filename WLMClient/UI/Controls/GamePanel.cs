using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

using WLMClient.Games;
using WLMClient.Locale;
using WLMData.Data.Packets;
using WLMData.Enums;

namespace WLMClient.UI.Controls
{
    enum GamePanelState
    {
        /// <summary>This side asked and is waiting for an answer.</summary>
        Inviting,

        /// <summary>The contact asked and this side has not answered.</summary>
        Invited,

        Playing,

        /// <summary>Somebody won or the board filled up.</summary>
        Finished,

        /// <summary>Declined, abandoned or unreachable; nothing more will arrive.</summary>
        Closed
    }

    /// <summary>
    /// The game shown beside a conversation: the invitation, the board, and the result. It speaks
    /// to one contact, and both clients check every move against their own copy of the rules.
    /// Whoever sends the invitation plays X and moves first.
    /// </summary>
    class GamePanel : Border
    {
        public const double PanelWidth = 240;

        private const double CellSize = 62;

        private readonly string contactId;
        private readonly Func<string> contactName;

        private GamePanelState state;
        private TicTacToe game;
        private int myMark;

        /// <summary>The language key for why the game closed, filled in with the contact's name.</summary>
        private string closedReason;

        private readonly StackPanel content;

        /// <summary>Raised when the panel should be taken off the window.</summary>
        public event Action CloseRequested;

        public GameKind Kind { get; private set; }

        /// <summary>Whether a game is being set up or played, as opposed to over.</summary>
        public bool IsActive
        {
            get { return state == GamePanelState.Inviting || state == GamePanelState.Invited || state == GamePanelState.Playing; }
        }

        public GamePanel(string contactId, Func<string> contactName, GameKind kind)
        {
            this.contactId = contactId;
            this.contactName = contactName;

            Kind = kind;
            state = GamePanelState.Closed;

            content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10, 18, 10, 10)
            };

            Child = content;
            Background = Brushes.Transparent;
        }

        /// <summary>The name of a game as the interface shows it.</summary>
        public static string GameName(GameKind kind)
        {
            switch (kind)
            {
                case GameKind.ticTacToe:
                    return Language.Get("games.tictactoe");

                default:
                    return kind.ToString();
            }
        }

        #region Signals

        /// <summary>Asks the contact to play.</summary>
        public void Invite()
        {
            game = null;
            state = GamePanelState.Inviting;

            Send(GameSignalType.invite, 0);

            Render();
        }

        /// <summary>The contact asked to play.</summary>
        public void ReceiveInvite()
        {
            if (state == GamePanelState.Inviting)
            {
                // Both asked at once, so both want to play. Each side settles who opens the same
                // way, by comparing the two names, and no answer is needed.
                bool iOpen = string.CompareOrdinal(Personal.USER_INFO.id.Trim(), contactId.Trim()) < 0;

                Start(iOpen ? TicTacToe.X : TicTacToe.O);

                return;
            }

            game = null;
            state = GamePanelState.Invited;

            Render();
        }

        /// <summary>Handles anything but an invitation.</summary>
        public void Receive(GameSignal signal)
        {
            switch ((GameSignalType)signal.signal)
            {
                case GameSignalType.accept:
                    if (state == GamePanelState.Inviting)
                    {
                        Start(TicTacToe.X);
                    }

                    break;

                case GameSignalType.decline:
                    if (state == GamePanelState.Inviting)
                    {
                        Close("game.declined");
                    }
                    else if (state == GamePanelState.Invited)
                    {
                        Close("game.withdrawn");
                    }

                    break;

                case GameSignalType.move:
                    // Only the contact's own turn, and only a move this side's rules allow.
                    if (state == GamePanelState.Playing && game.Turn != myMark && game.Play(signal.value))
                    {
                        AfterMove();
                    }

                    break;

                case GameSignalType.leave:
                    if (state != GamePanelState.Closed)
                    {
                        Close("game.left");
                    }

                    break;

                case GameSignalType.unavailable:
                    if (state == GamePanelState.Inviting)
                    {
                        Close("game.unavailable");
                    }

                    break;
            }
        }

        /// <summary>The contact went offline; nothing they had going can finish.</summary>
        public void ContactSignedOut()
        {
            if (state != GamePanelState.Closed)
            {
                Close("game.signedout");
            }
        }

        /// <summary>
        /// Tells the contact this side is done with the game, in whatever way fits where it got to.
        /// </summary>
        public void Quit()
        {
            switch (state)
            {
                case GamePanelState.Inviting:
                case GamePanelState.Invited:
                    Send(GameSignalType.decline, 0);
                    break;

                case GamePanelState.Playing:
                case GamePanelState.Finished:
                    Send(GameSignalType.leave, 0);
                    break;
            }

            state = GamePanelState.Closed;
            closedReason = null;
        }

        private void Accept()
        {
            if (state != GamePanelState.Invited)
            {
                return;
            }

            Send(GameSignalType.accept, 0);

            Start(TicTacToe.O);
        }

        private void Start(int mark)
        {
            game = new TicTacToe();
            myMark = mark;
            state = GamePanelState.Playing;

            Render();
        }

        private void PlayCell(int cell)
        {
            if (state != GamePanelState.Playing || game.Turn != myMark || !game.Play(cell))
            {
                return;
            }

            Send(GameSignalType.move, cell);

            AfterMove();
        }

        private void AfterMove()
        {
            if (game.IsOver)
            {
                state = GamePanelState.Finished;
            }

            Render();
        }

        private void Close(string reason)
        {
            state = GamePanelState.Closed;
            closedReason = reason;

            Render();
        }

        private void Send(GameSignalType type, int value)
        {
            Network.Client.SendGameSignal(contactId, type, Kind, value);
        }

        private void RequestClose()
        {
            Action handler = CloseRequested;

            if (handler != null)
            {
                handler();
            }
        }

        #endregion

        #region Drawing

        /// <summary>Repaints in the current theme.</summary>
        public void ApplyTheme()
        {
            Render();
        }

        /// <summary>Rewrites the text in the current language.</summary>
        public void ApplyLanguage()
        {
            Render();
        }

        /// <summary>Rebuilds the panel for the state it is in. Nine cells and a few lines of text.</summary>
        private void Render()
        {
            content.Children.Clear();

            string name = contactName();

            switch (state)
            {
                case GamePanelState.Inviting:
                    AddHeadline(Language.Format("game.waiting", name));
                    AddActions(CreateButton(Language.Get("game.cancel"), false, () =>
                    {
                        Quit();
                        RequestClose();
                    }));
                    break;

                case GamePanelState.Invited:
                    AddHeadline(Language.Format("game.invited", name, GameName(Kind)));
                    AddActions(
                        CreateButton(Language.Get("game.accept"), true, Accept),
                        CreateButton(Language.Get("game.decline"), false, () =>
                        {
                            Quit();
                            RequestClose();
                        }));
                    break;

                case GamePanelState.Playing:
                    AddHeadline(game.Turn == myMark
                        ? Language.Get("game.yourturn")
                        : Language.Format("game.theirturn", name));
                    AddDetail(Language.Format("game.youplay", TicTacToe.MarkText(myMark)));
                    AddBoard();
                    AddActions(CreateButton(Language.Get("game.leave"), false, () =>
                    {
                        Quit();
                        RequestClose();
                    }));
                    break;

                case GamePanelState.Finished:
                    AddHeadline(game.IsDraw
                        ? Language.Get("game.draw")
                        : game.Winner == myMark
                            ? Language.Get("game.won")
                            : Language.Format("game.lost", name));
                    AddDetail(Language.Format("game.youplay", TicTacToe.MarkText(myMark)));
                    AddBoard();
                    AddActions(
                        CreateButton(Language.Get("game.playagain"), true, Invite),
                        CreateButton(Language.Get("game.close"), false, () =>
                        {
                            Quit();
                            RequestClose();
                        }));
                    break;

                case GamePanelState.Closed:
                    if (closedReason != null)
                    {
                        AddHeadline(Language.Format(closedReason, name));
                    }

                    if (game != null)
                    {
                        AddBoard();
                    }

                    AddActions(
                        CreateButton(Language.Get("game.playagain"), true, Invite),
                        CreateButton(Language.Get("game.close"), false, RequestClose));
                    break;
            }
        }

        private void AddHeadline(string text)
        {
            content.Children.Add(new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                Foreground = Config.Theme.TextPrimary,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                MaxWidth = PanelWidth - 30,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        private void AddDetail(string text)
        {
            content.Children.Add(new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = Config.Theme.TextSecondary,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        private void AddBoard()
        {
            Grid board = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 6)
            };

            for (int i = 0; i < 3; i++)
            {
                board.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                board.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            for (int cell = 0; cell < TicTacToe.CellCount; cell++)
            {
                Border cellBorder = CreateCell(cell);

                Grid.SetColumn(cellBorder, cell % 3);
                Grid.SetRow(cellBorder, cell / 3);

                board.Children.Add(cellBorder);
            }

            content.Children.Add(board);
        }

        private Border CreateCell(int cell)
        {
            int mark = game[cell];

            bool winning = game.WinningLine != null && Array.IndexOf(game.WinningLine, cell) >= 0;
            bool playable = state == GamePanelState.Playing && game.Turn == myMark && game.CanPlay(cell);

            IBrush background = winning ? Config.Theme.GameHighlight : Config.Theme.GameCell;

            Border border = new Border
            {
                Width = CellSize,
                Height = CellSize,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(1),
                BorderBrush = Config.Theme.GameCellBorder,
                Background = background,
                Cursor = new Cursor(playable ? StandardCursorType.Hand : StandardCursorType.Arrow),
                Child = new TextBlock
                {
                    Text = TicTacToe.MarkText(mark),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 34,
                    FontWeight = FontWeight.Bold,
                    Foreground = mark == TicTacToe.X ? Config.Theme.GameMarkX : Config.Theme.GameMarkO,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            if (playable)
            {
                border.PointerEntered += (sender, e) => border.Background = Config.Theme.GameCellHover;
                border.PointerExited += (sender, e) => border.Background = background;
                border.PointerReleased += (sender, e) => PlayCell(cell);
            }

            return border;
        }

        private void AddActions(params Button[] buttons)
        {
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 6,
                Margin = new Thickness(0, 10, 0, 0)
            };

            foreach (Button button in buttons)
            {
                row.Children.Add(button);
            }

            content.Children.Add(row);
        }

        private static Button CreateButton(string text, bool strong, Action onClick)
        {
            Button button = new Button
            {
                Content = text,
                MinWidth = 80,
                Height = 26,
                Padding = new Thickness(10, 0),
                Background = strong ? Config.Theme.ButtonStrongBackground : Config.Theme.ButtonBackground,
                Foreground = strong ? Config.Theme.ButtonStrongForeground : Config.Theme.TextPrimary,
                BorderBrush = Config.Theme.Separator,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            button.Click += (sender, e) => onClick();

            return button;
        }

        #endregion
    }
}
