using System.ComponentModel;
using System.Linq;

namespace TF2FrameworkInterface
{
    public class LobbyPlayer : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void ViewNotification(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private TF2DebugLobby lobbyInfo;
        private StatusCommandLogOutput status;
        public LobbyPlayer(TF2DebugLobby l, StatusCommandLogOutput status)
        {
            this.lobbyInfo = l;
            this.status = status;
        }

        /// <summary>
        /// Externally set Lobby-level flag
        /// </summary>
        public bool IsKicking { get; set; } = false;
        /// <summary>
        /// Externally set Lobby-level flag
        /// </summary>
        public bool IsMarking { get; set; } = false;

        private bool isMuted;
        public bool IsMuted
        {
            get => isMuted;
            set
            {
                isMuted = value;
                ViewNotification(nameof(TextIcon));
            }
        }

        private bool isBanned;
        public bool IsBanned
        {
            get => isBanned;
            set
            {
                isBanned = value;
                ViewNotification(nameof(TextIcon));
            }
        }
        private bool isUserBanned;
        public bool IsUserBanned
        {
            get => isUserBanned;
            set
            {
                isUserBanned = value;
                ViewNotification(nameof(TextIcon));
            }
        }
        private bool isFriend;
        public bool IsFriend
        {
            get => isFriend;
            set
            {
                isFriend = value;
                ViewNotification(nameof(TextIcon));
            }
        }
        private bool isMe;
        public bool IsMe
        {
            get => isMe;
            set
            {
                isMe = value;
                ViewNotification(nameof(TextIcon));
            }
        }

        public string TextIcon
            => (IsMuted ? "🔇" : " ")
            + (IsUserBanned ? "👎"
            : IsBanned ? "☠"
            : IsMe ? "👉"
            : IsFriend ? "💚"
            : " ");

        public string TextTime
            => IsMissing ? "--"
            : StatusConnectedSeconds == 0
            ? ""
            : new System.TimeSpan(0, 0, StatusConnectedSeconds).TotalMinutes.ToString("N0");
        //ToString(@"hh\:mm\:ss");

        private TF2Status _lastGoodStatus;

        private TF2Status StatusInfo
        {
            get
            {
                TF2Status liveStatus = status.LogStatus
                    .FirstOrDefault(s => s.SteamUniqueID == lobbyInfo.SteamUniqueID);

                if (liveStatus != null && liveStatus.IsDifferent(_lastGoodStatus))
                {
                    _lastGoodStatus = liveStatus;
                    NotifyStatusChanged();
                }

                return _lastGoodStatus;
            }
        }

        public bool IsRED => lobbyInfo.Team == TF2DebugLobby.TF_GC_TEAM_DEFENDERS;
        public bool IsBLU => lobbyInfo.Team == TF2DebugLobby.TF_GC_TEAM_INVADERS;

        public string SteamID => lobbyInfo.SteamUniqueID;

        private void NotifyStatusChanged()
        {
            ViewNotification(nameof(HaveStatus));
            ViewNotification(nameof(HaveKickInfo));
            ViewNotification(nameof(StatusName));
            ViewNotification(nameof(StatusConnectedSeconds));
            ViewNotification(nameof(TextTime));
            ViewNotification(nameof(StatusPing));
            ViewNotification(nameof(StatusState));
        }

        internal void Update(TF2DebugLobby updatedLobby)
        {
            lobbyInfo.Team = updatedLobby.Team;
            ViewNotification(nameof(IsRED));
            ViewNotification(nameof(IsBLU));
            //lobbyInfo.MemberNumber
            //lobbyInfo.PlayerType
        }

        public bool HaveStatus => StatusInfo != null;
        public bool HaveKickInfo => StatusInfo?.GameUserID != null;

        public string StatusName => StatusInfo?.UserName
            ?? " -❓" + lobbyInfo.SteamUniqueID + "❔- ";
        public int StatusConnectedSeconds => StatusInfo?.ConnectedSeconds
            ?? 0;
        public int StatusPing => StatusInfo?.Ping
            ?? 0;
        /// <summary>
        /// invalid, challenging, connecting, spawning, active
        /// </summary>
        public string StatusState 
            => IsMissing ? "--"
            : StatusInfo?.UserState
            ?? "";

        public bool IsMissing => RemoveCounter > 0;

        private int removeCounter;
        /// <summary>
        /// track how many times we tried to remove this player 
        /// - they can accidentally show as removed for a while, 
        /// also useful so they stick around a little while for marking after another player kicks them.
        /// </summary>
        public int RemoveCounter
        {
            get => removeCounter;
            internal set
            {
                removeCounter = value;
                ViewNotification(nameof(TextTime));
                ViewNotification(nameof(StatusState));
                ViewNotification(nameof(IsMissing));
            }
        }
    }
}
