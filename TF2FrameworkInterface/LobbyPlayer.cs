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

        private bool isMuted;
        public bool IsMuted
        {
            get => isMuted;
            set
            {
                isMuted = value;
                NotifyProperty(nameof(TextIcon));
            }
        }

        private bool isBanned;
        public bool IsBanned
        {
            get => isBanned;
            set
            {
                isBanned = value;
                NotifyProperty(nameof(TextIcon));
            }
        }
        private bool isUserBanned;
        public bool IsUserBanned
        {
            get => isUserBanned;
            set
            {
                isUserBanned = value;
                NotifyProperty(nameof(TextIcon));
            }
        }
        private bool isFriend;
        public bool IsFriend { 
            get => isFriend;
            set  {
                isFriend = value; 
                NotifyProperty(nameof(TextIcon));
            }
        }
        private bool isMe;
        public bool IsMe
        {
            get => isMe;
            set
            {
                isMe = value;
                NotifyProperty(nameof(TextIcon));
            }
        }

        public string TextIcon
            => (IsMuted ? "🔇" : " ")
            + (IsUserBanned ? "👎"
            : IsBanned ? "☠"
            : IsMe ? "👉"
            : IsFriend ? "💚"
            : " ");

        public string TextTime => StatusConnectedSeconds == 0
            ? ""
            : new System.TimeSpan(0, 0, StatusConnectedSeconds).TotalMinutes.ToString("N2");
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
            NotifyProperty(nameof(HaveStatus));
            NotifyProperty(nameof(HaveKickInfo));
            NotifyProperty(nameof(StatusName));
            NotifyProperty(nameof(StatusConnectedSeconds));
            NotifyProperty(nameof(StatusPing));
            NotifyProperty(nameof(StatusState));
        }

        private void NotifyProperty(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void Update(TF2DebugLobby updatedLobby)
        {
            lobbyInfo.Team = updatedLobby.Team;
            NotifyProperty(nameof(IsRED));
            NotifyProperty(nameof(IsBLU));
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
        public string StatusState => StatusInfo?.UserState
            ?? "";
    }
}
