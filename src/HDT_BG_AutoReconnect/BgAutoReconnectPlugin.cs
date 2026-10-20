using System;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace BgAutoReconnect
{
    public class BgAutoReconnectPlugin : IPlugin
    {
        private MenuItem _menuItem;
        private ReconnectOverlay _overlay;

        public string Name => "전장 자동 재접속 (BG AutoReconnect)";

        public string Description =>
            "전장 경기 중에 재접속 버튼을 보여줍니다. 게임 서버 연결을 잠깐 끊었다 다시 붙어서 전투 애니메이션을 건너뜁니다.\n\n" +
            "버튼 옆 '자동' 스위치를 ON으로 두면 전투가 시작되는 순간 자동으로 재접속합니다. OFF일 때는 버튼을 직접 누를 때만 작동하며, HDT를 켤 때마다 OFF로 시작합니다.\n\n" +
            "※ HDT를 관리자 권한으로 실행해야 작동합니다.\n\n" +
            "GitHub: https://github.com/MorganLee0108/HDT-BG-AutoReconnect";

        public string ButtonText => "정보";

        public string Author => "MorganLee0108";

        public Version Version => new Version(1, 0, 0);

        public MenuItem MenuItem => _menuItem;

        public void OnLoad()
        {
            _menuItem = new MenuItem
            {
                Header = "전장 자동 재접속",
                IsCheckable = true
            };
            _menuItem.Checked += OnMenuChecked;
            _menuItem.Unchecked += OnMenuUnchecked;
            _menuItem.IsChecked = true;
        }

        public void OnUnload()
        {
            if (_menuItem != null)
            {
                _menuItem.Checked -= OnMenuChecked;
                _menuItem.Unchecked -= OnMenuUnchecked;
                _menuItem.IsChecked = false;
            }
        }

        public void OnButtonPress()
        {
        }

        public void OnUpdate()
        {
            _overlay?.OnUpdate();
        }

        private void OnMenuChecked(object sender, EventArgs e)
        {
            if (_overlay != null)
                return;

            _overlay = new ReconnectOverlay();
            Core.OverlayCanvas.Children.Add(_overlay);
            Log.Info("BG AutoReconnect overlay enabled");
        }

        private void OnMenuUnchecked(object sender, EventArgs e)
        {
            if (_overlay == null)
                return;

            Core.OverlayCanvas.Children.Remove(_overlay);
            _overlay.Dispose();
            _overlay = null;
            Log.Info("BG AutoReconnect overlay disabled");
        }
    }
}
