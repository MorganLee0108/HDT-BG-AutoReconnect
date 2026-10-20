using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BgAutoReconnect.GameLog;
using BgAutoReconnect.Network;
using BgAutoReconnect.Overlay;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Core = Hearthstone_Deck_Tracker.API.Core;

namespace BgAutoReconnect
{
    public class ReconnectOverlay : UserControl, IDisposable
    {
        private const string LogSearchPattern = "Hearthstone.log";
        private const int DisconnectTimeoutSeconds = 20;
        private const int FailedFeedbackMs = 1500;
        private const double EdgeMargin = 16;
        private const double OffsetLeft = 65;
        private const double OffsetTop = 65;
        private const string TextReconnect = "재접속";
        private const string TextDisconnected = "연결 끊는 중…";
        private const string TextFailed = "실패";
        private const string TextNeedAdmin = "(관리자 권한 필요)";
        private const double WidthNormal = 130;
        private const double WidthAdminHint = 200;
        private const double ToggleGap = 6;
        private const double ToggleWidth = 112;

        // Auto reconnect re-arms only after the recruit phase has been observed for this long,
        // so the combat flag flickering while the client replays state after a reconnect can't loop.
        private const double AutoRearmRecruitSeconds = 5;

        private static readonly Brush TrackOffBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x4F, 0x55));
        private static readonly Brush TrackOnBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0xA5, 0x5D));
        private static readonly Brush StateOffBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xA6));

        private readonly ConnectionBreaker _breaker = new ConnectionBreaker();
        private readonly Border _reconnectButton;
        private readonly TextBlock _reconnectText;
        private readonly Brush _originalBrush;
        private readonly Border _autoToggle;
        private readonly Border _toggleTrack;
        private readonly Ellipse _toggleThumb;
        private readonly TextBlock _toggleStateText;
        private readonly Brush _toggleOriginalBrush;
        private readonly Timer _disconnectTimer;
        private readonly Timer _failedFeedbackTimer;

        private LogWatcher _logWatcher;
        private bool _logWatcherInitialized;
        private DateTime _lastGameStartTime;
        private DateTime _trackedGameStartTime = DateTime.MinValue;
        private bool _hadServerAddress;
        private readonly SizeChangedEventHandler _overlaySizeChangedHandler;

        // Not persisted on purpose: every HDT start (and every overlay re-enable) begins with auto OFF.
        private bool _autoEnabled;
        private bool _wasInCombat;
        private bool _autoArmed = true;
        private DateTime _recruitSeenSince = DateTime.MinValue;
        private DateTime _autoFiredGameStart = DateTime.MinValue;
        private int _autoFiredTurn = -1;

        public string RemoteAddr { get; private set; }
        public ushort RemotePort { get; private set; }

        public ReconnectOverlay()
        {
            Height = 36;

            _reconnectText = new TextBlock
            {
                Text = TextReconnect,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _reconnectButton = new Border
            {
                Width = WidthNormal,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x17)),
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(Color.FromRgb(0x23, 0x27, 0x2a)),
                Cursor = Cursors.Hand,
                Child = _reconnectText
            };

            _originalBrush = _reconnectButton.Background;
            _reconnectButton.MouseEnter += ReconnectButton_MouseEnter;
            _reconnectButton.MouseLeave += ReconnectButton_MouseLeave;
            _reconnectButton.MouseLeftButtonDown += ReconnectButton_Click;

            _toggleThumb = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = Brushes.White,
                Margin = new Thickness(2),
                VerticalAlignment = VerticalAlignment.Center
            };
            _toggleTrack = new Border
            {
                Width = 36,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                VerticalAlignment = VerticalAlignment.Center,
                Child = _toggleThumb
            };
            _toggleStateText = new TextBlock
            {
                Width = 28,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var toggleContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            toggleContent.Children.Add(new TextBlock
            {
                Text = "자동",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            toggleContent.Children.Add(_toggleTrack);
            toggleContent.Children.Add(_toggleStateText);

            _autoToggle = new Border
            {
                Width = ToggleWidth,
                Margin = new Thickness(ToggleGap, 0, 0, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x17)),
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(Color.FromRgb(0x23, 0x27, 0x2a)),
                Cursor = Cursors.Hand,
                ToolTip = "ON: 전투가 시작되면 자동으로 재접속\nOFF: 버튼을 누를 때만 재접속",
                Child = toggleContent
            };
            _toggleOriginalBrush = _autoToggle.Background;
            _autoToggle.MouseEnter += (_, __) => _autoToggle.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x38));
            _autoToggle.MouseLeave += (_, __) => _autoToggle.Background = _toggleOriginalBrush;
            _autoToggle.MouseLeftButtonDown += AutoToggle_Click;
            UpdateToggleVisual();

            var root = new StackPanel { Orientation = Orientation.Horizontal };
            root.Children.Add(_reconnectButton);
            root.Children.Add(_autoToggle);
            Content = root;
            Width = WidthNormal + ToggleGap + ToggleWidth;
            Visibility = Visibility.Collapsed;

            _disconnectTimer = new Timer(DisconnectedTimeout);
            _failedFeedbackTimer = new Timer(FailedFeedbackTimeout);
            OverlayRegistration.RegisterClickable(_reconnectButton);
            OverlayRegistration.RegisterClickable(_autoToggle);

            _overlaySizeChangedHandler = (_, __) => UpdatePosition();
            Core.OverlayCanvas.SizeChanged += _overlaySizeChangedHandler;
            UpdatePosition();
        }

        public void Dispose()
        {
            _disconnectTimer.Dispose();
            _failedFeedbackTimer.Dispose();
            _logWatcher?.Stop();
            Core.OverlayCanvas.SizeChanged -= _overlaySizeChangedHandler;
            OverlayRegistration.UnregisterClickable(_reconnectButton);
            OverlayRegistration.UnregisterClickable(_autoToggle);
        }

        internal void OnServerAddressUpdated(string addr, ushort port)
        {
            lock (this)
            {
                var wasDisconnected = _breaker.Status == ConnectionBreaker.ConnectionStatus.Disconnected;
                var isReconnect = wasDisconnected && _hadServerAddress;

                RemoteAddr = addr;
                RemotePort = port;
                _hadServerAddress = true;

                if (isReconnect)
                {
                    Log.Info($"Game server reconnected at {addr}:{port}");
                    RestoreConnectedState();
                }
            }
        }

        private void UpdatePosition()
        {
            var canvas = Core.OverlayCanvas;
            var width = canvas.ActualWidth > 0 ? canvas.ActualWidth : canvas.Width;
            var height = canvas.ActualHeight > 0 ? canvas.ActualHeight : canvas.Height;
            if (width <= 0 || height <= 0)
                return;

            Canvas.SetLeft(this, width - Width - EdgeMargin - OffsetLeft);
            Canvas.SetTop(this, height - Height - EdgeMargin - OffsetTop);
        }

        public void OnUpdate()
        {
            var game = Core.Game;
            TrackCurrentGame(game);

            var showInBgMatch = game != null
                                && game.IsBattlegroundsMatch
                                && game.IsRunning
                                && !game.IsInMenu;

            Visibility = showInBgMatch ? Visibility.Visible : Visibility.Collapsed;
            if (showInBgMatch)
            {
                UpdatePosition();
                UpdateAdminState();
            }

            UpdateAutoReconnect(game, showInBgMatch);

            if (_breaker.Status == ConnectionBreaker.ConnectionStatus.Disconnected)
            {
                if (IsInMainOrBgMenu())
                {
                    Log.Info("Can't reconnect to the game");
                    RestoreConnectedState();
                    return;
                }

                if (IsGameRestarted() || IsCurrentGameEnded())
                {
                    RestoreConnectedState();
                }
            }

            if (game != null && game.IsRunning && !_logWatcherInitialized)
            {
                StartLogWatcher();
            }

            if (game != null && !game.IsRunning && _logWatcherInitialized)
            {
                StopLogWatcher();
            }
        }

        private void UpdateAutoReconnect(Hearthstone_Deck_Tracker.Hearthstone.GameV2 game, bool inBgMatch)
        {
            var inCombat = inBgMatch && game.IsBattlegroundsCombatPhase;

            if (!inCombat)
            {
                if (_recruitSeenSince == DateTime.MinValue)
                    _recruitSeenSince = DateTime.Now;
                if (!_autoArmed && inBgMatch && (DateTime.Now - _recruitSeenSince).TotalSeconds >= AutoRearmRecruitSeconds)
                    _autoArmed = true;
            }
            else
            {
                _recruitSeenSince = DateTime.MinValue;
            }

            var combatStarted = inCombat && !_wasInCombat;
            _wasInCombat = inCombat;
            if (!combatStarted || !_autoEnabled || !_autoArmed)
                return;

            var gameStart = game.CurrentGameStats?.StartTime ?? DateTime.MinValue;
            var turn = game.GetTurnNumber();
            if (gameStart == _autoFiredGameStart && turn == _autoFiredTurn)
                return;

            _autoFiredGameStart = gameStart;
            _autoFiredTurn = turn;
            _autoArmed = false;

            if (!CanReconnect())
            {
                Log.Info($"Auto reconnect skipped at combat start (turn {turn}): not ready");
                return;
            }

            Log.Info($"Auto reconnect at combat start (turn {turn})");
            TryReconnect();
        }

        private void TrackCurrentGame(Hearthstone_Deck_Tracker.Hearthstone.GameV2 game)
        {
            var stats = game?.CurrentGameStats;
            if (stats == null || stats.StartTime <= _trackedGameStartTime)
                return;

            _trackedGameStartTime = stats.StartTime;
            OnNewGameStarted();
        }

        private void OnNewGameStarted()
        {
            lock (this)
            {
                RemoteAddr = "";
                RemotePort = 0;
                _hadServerAddress = false;
            }

            _wasInCombat = false;
            _autoArmed = true;
            RestoreConnectedState();
            RestartLogWatcher();
        }

        private void ReconnectButton_Click(object sender, MouseButtonEventArgs e)
        {
            if (!CanReconnect())
                return;

            TryReconnect();
        }

        private void TryReconnect()
        {
            _lastGameStartTime = Core.Game.CurrentGameStats?.StartTime ?? DateTime.MinValue;

            lock (this)
            {
                if (_breaker.Disconnect(RemoteAddr, RemotePort) == 0)
                {
                    SetButtonText(TextDisconnected);
                    _disconnectTimer.Change(DisconnectTimeoutSeconds * 1000, Timeout.Infinite);
                }
                else
                {
                    ShowFailedFeedback();
                }
            }
        }

        private void AutoToggle_Click(object sender, MouseButtonEventArgs e)
        {
            _autoEnabled = !_autoEnabled;
            Log.Info("Auto reconnect " + (_autoEnabled ? "ON" : "OFF"));
            UpdateToggleVisual();
        }

        private void UpdateToggleVisual()
        {
            _toggleTrack.Background = _autoEnabled ? TrackOnBrush : TrackOffBrush;
            _toggleThumb.HorizontalAlignment = _autoEnabled ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            _toggleStateText.Text = _autoEnabled ? "ON" : "OFF";
            _toggleStateText.Foreground = _autoEnabled ? Brushes.White : StateOffBrush;
        }

        private void ReconnectButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (CanReconnect())
                _reconnectButton.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x38));
        }

        private void ReconnectButton_MouseLeave(object sender, MouseEventArgs e)
        {
            _reconnectButton.Background = _originalBrush;
        }

        private bool CanReconnect()
        {
            if (!Utils.IsElevated())
                return false;

            var game = Core.Game;
            return _breaker.Status == ConnectionBreaker.ConnectionStatus.Connected
                   && game != null
                   && game.IsBattlegroundsMatch
                   && game.IsRunning
                   && !game.IsInMenu
                   && !IsCurrentGameEnded();
        }

        private void UpdateAdminState()
        {
            if (!Utils.IsElevated())
            {
                _reconnectButton.Width = WidthAdminHint;
                Width = WidthAdminHint + ToggleGap + ToggleWidth;
                _reconnectText.FontSize = 11;
                SetButtonText(TextNeedAdmin);
                _reconnectButton.Cursor = Cursors.Arrow;
                _reconnectButton.Opacity = 0.85;
                return;
            }

            _reconnectButton.Width = WidthNormal;
            Width = WidthNormal + ToggleGap + ToggleWidth;
            _reconnectText.FontSize = 14;
            _reconnectButton.Opacity = CanReconnect() ? 1 : 0.55;
            _reconnectButton.Cursor = CanReconnect() ? Cursors.Hand : Cursors.Arrow;

            if (_breaker.Status == ConnectionBreaker.ConnectionStatus.Connected)
                SetButtonText(TextReconnect);
        }

        private bool IsGameRestarted()
        {
            var stats = Core.Game?.CurrentGameStats;
            return Core.Game != null
                   && Core.Game.CurrentGameMode != GameMode.None
                   && stats != null
                   && stats.StartTime > _lastGameStartTime;
        }

        private static bool IsInMainOrBgMenu()
        {
            var mode = Core.Game?.CurrentMode;
            return mode == Mode.HUB || mode == Mode.BACON;
        }

        private bool IsCurrentGameEnded()
        {
            var stats = Core.Game?.CurrentGameStats;
            return stats != null
                   && stats.StartTime == _trackedGameStartTime
                   && stats.EndTime > stats.StartTime;
        }

        private void RestoreConnectedState()
        {
            _breaker.MarkConnected();
            _disconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
            Dispatcher.Invoke(UpdateAdminState);
        }

        private void DisconnectedTimeout(object state)
        {
            RestoreConnectedState();
        }

        private void ShowFailedFeedback()
        {
            SetButtonText(TextFailed);
            _failedFeedbackTimer.Change(FailedFeedbackMs, Timeout.Infinite);
        }

        private void FailedFeedbackTimeout(object state)
        {
            Dispatcher.Invoke(UpdateAdminState);
        }

        private void SetButtonText(string text)
        {
            if (Dispatcher.CheckAccess())
                _reconnectText.Text = text;
            else
                Dispatcher.Invoke(() => _reconnectText.Text = text);
        }

        private void StartLogWatcher()
        {
            try
            {
                var logPath = FindLatestLogFile();
                _logWatcher = new LogWatcher(this, logPath);
                _logWatcher.Start();
                _logWatcherInitialized = true;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to start log watcher: " + ex);
            }
        }

        private void StopLogWatcher()
        {
            _logWatcher?.Stop();
            _logWatcher = null;
            _logWatcherInitialized = false;
        }

        private void RestartLogWatcher()
        {
            StopLogWatcher();
            if (Core.Game?.IsRunning == true)
                StartLogWatcher();
        }

        private static string FindLatestLogFile()
        {
            var logDirectory = System.IO.Path.Combine(
                Config.Instance.HearthstoneDirectory,
                Config.Instance.HearthstoneLogsDirectoryName);

            var latestDir = new DirectoryInfo(logDirectory)
                .GetDirectories()
                .OrderByDescending(x => x.CreationTime)
                .First();

            var logFile = latestDir.GetFiles(LogSearchPattern).First();
            Log.Info("Using log file: " + logFile.FullName);
            return logFile.FullName;
        }
    }
}
