using System;
using System.Collections.Generic;

using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.PubSub;

namespace TF2WindowsInterface
{

    public class TwitchInstance
    {
        public readonly string TwitchUsername;

        // https://dev.twitch.tv/docs/authentication/scopes/
        public static string[] ClientOAuthScopes = new[] {
                "channel:read:redemptions",
                //Get Custom Reward
                //Get Custom Reward Redemption

                //"channel:manage:redemptions",
                //Create Custom Rewards
                //Delete Custom Reward
                //Update Custom Reward
                //Update Redemption Status

                /*
                    analytics:read:extensions 	View analytics data for the Twitch Extensions owned by the authenticated account.
                    analytics:read:games 	View analytics data for the games owned by the authenticated account.
                    bits:read 	View Bits information for a channel.
                    channel:manage:ads 	Manage ads schedule on a channel.
                    channel:read:ads 	Read the ads schedule and details on your channel.
                    channel:manage:broadcast 	Manage a channel’s broadcast configuration, including updating channel configuration and managing stream markers and stream tags.
                    channel:read:charity 	Read charity campaign details and user donations on your channel.
                    channel:edit:commercial 	Run commercials on a channel.
                    channel:read:editors 	View a list of users with the editor role for a channel.
                    channel:manage:extensions 	Manage a channel’s Extension configuration, including activating Extensions.
                    channel:read:goals 	View Creator Goals for a channel.
                    channel:read:guest_star 	Read Guest Star details for your channel.
                    channel:manage:guest_star 	Manage Guest Star for your channel.
                    channel:read:hype_train 	View Hype Train information for a channel.
                    channel:manage:moderators 	Add or remove the moderator role from users in your channel.
                    channel:read:polls 	View a channel’s polls.
                    channel:manage:polls 	Manage a channel’s polls.
                    channel:read:predictions 	View a channel’s Channel Points Predictions.
                    channel:manage:predictions 	Manage of channel’s Channel Points Predictions
                    channel:manage:raids 	Manage a channel raiding another channel.
                    channel:read:redemptions 	View Channel Points custom rewards and their redemptions on a channel.
                    channel:manage:redemptions 	Manage Channel Points custom rewards and their redemptions on a channel.
                    channel:manage:schedule 	Manage a channel’s stream schedule.
                    channel:read:stream_key 	View an authorized user’s stream key.
                    channel:read:subscriptions 	View a list of all subscribers to a channel and check if a user is subscribed to a channel.
                    channel:manage:videos 	Manage a channel’s videos, including deleting videos.
                    channel:read:vips 	Read the list of VIPs in your channel.
                    channel:manage:vips 	Add or remove the VIP role from users in your channel.
                    clips:edit 	Manage Clips for a channel.
                    moderation:read 	View a channel’s moderation data including Moderators, Bans, Timeouts, and Automod settings.
                    moderator:manage:announcements 	Send announcements in channels where you have the moderator role.
                    moderator:manage:automod 	Manage messages held for review by AutoMod in channels where you are a moderator.
                    moderator:read:automod_settings 	View a broadcaster’s AutoMod settings.
                    moderator:manage:automod_settings 	Manage a broadcaster’s AutoMod settings.
                    moderator:manage:banned_users 	Ban and unban users.
                    moderator:read:blocked_terms 	View a broadcaster’s list of blocked terms.
                    moderator:manage:blocked_terms 	Manage a broadcaster’s list of blocked terms.
                    moderator:manage:chat_messages 	Delete chat messages in channels where you have the moderator role
                    moderator:read:chat_settings 	View a broadcaster’s chat room settings.
                    moderator:manage:chat_settings 	Manage a broadcaster’s chat room settings.
                    moderator:read:chatters 	View the chatters in a broadcaster’s chat room.
                    moderator:read:followers 	Read the followers of a broadcaster.
                    moderator:read:guest_star 	Read Guest Star details for channels where you are a Guest Star moderator.
                    moderator:manage:guest_star 	Manage Guest Star for channels where you are a Guest Star moderator.
                    moderator:read:shield_mode 	View a broadcaster’s Shield Mode status.
                    moderator:manage:shield_mode 	Manage a broadcaster’s Shield Mode status.
                    moderator:read:shoutouts 	View a broadcaster’s shoutouts.
                    moderator:manage:shoutouts 	Manage a broadcaster’s shoutouts.
                    user:edit 	Manage a user object.
                    user:edit:follows 	Deprecated. Was previously used for “Create User Follows” and “Delete User Follows.” See Deprecation of Create and Delete Follows API Endpoints.
                    user:read:blocked_users 	View the block list of a user.
                    user:manage:blocked_users 	Manage the block list of a user.
                    user:read:broadcast 	View a user’s broadcasting configuration, including Extension configurations.
                    user:manage:chat_color 	Update the color used for the user’s name in chat.Update User Chat Color
                    user:read:email 	View a user’s email address.
                    user:read:follows 	View the list of channels a user follows.
                    user:read:subscriptions 	View if an authorized user is subscribed to specific channels.
                    user:manage:whispers 	Read whispers that you send and receive, and send whispers on your behalf.
                 */
                /*
                 Chat and PubSub scopes
                    Scope Name 	Type of Access
                    channel:bot 	Allows the client’s bot users access to a channel.
                    channel:moderate 	Perform moderation actions in a channel. The user requesting the scope must be a moderator in the channel.
                    chat:edit 	Send live stream chat messages.
                    chat:read 	View live stream chat messages.
                    user:bot 	Allows client’s bot to act as this user.
                    user:read:chat 	View live stream chat and room messages.
                    whispers:read 	View your whisper messages.
                    whispers:edit 	Send whisper messages.
                 */
                // --- I'm pretty sure the TwitchLib command handler must require chat:read
                "chat:read",
                "chat:edit",
            };

        private TwitchClient client;
        private TwitchPubSub pubsub;

        public static string AuthToken { get; set; } = "";
        public TwitchInstance(string twitchUsername)
        {
            TwitchUsername = twitchUsername;
            string twitchOAuth = AuthToken;
            if (string.IsNullOrEmpty(twitchOAuth))
            {
                // get the value for the twitchOAuth: "access_token"
                TwitchImplicitOAuth oauth = new TwitchImplicitOAuth(
                    //clientName: "TF2SpectatorControl",
                    clientID: "xvco4mzu0kr55ah5gr2xxyefx0kvbc");
                OAuthResult listenResult = oauth.Authorize(ClientOAuthScopes);
                twitchOAuth = listenResult.AccessToken;

                AuthToken = twitchOAuth;
            }

            ConnectionCredentials credentials = new ConnectionCredentials(TwitchUsername, twitchOAuth);

            StartClient(credentials);
        }

        private void StartClient(ConnectionCredentials credentials)
        {
            ClientOptions clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);
            client.Initialize(credentials, channel: TwitchUsername);

            client.OnLog += Client_OnLog;
            client.OnConnected += Client_OnConnected;
            client.OnJoinedChannel += Client_OnJoinedChannel;

            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnWhisperReceived += Client_OnWhisperReceived;
            client.OnNewSubscriber += Client_OnNewSubscriber;

            client.OnChatCommandReceived += Client_OnChatCommandReceived;

            _ = client.Connect();

            pubsub = new TwitchPubSub();
            pubsub.ListenToChannelPoints(TwitchUsername);
            // TODO this is not firing
            pubsub.OnChannelPointsRewardRedeemed += Pubsub_OnChannelPointsRewardRedeemed;
            pubsub.Connect();
            
        }

        private void Pubsub_OnChannelPointsRewardRedeemed(object sender, TwitchLib.PubSub.Events.OnChannelPointsRewardRedeemedArgs e)
        {
            TwitchLib.PubSub.Models.Responses.Messages.Redemption.Redemption redemption = e.RewardRedeemed.Redemption;
            GetRedeemCommand(redemption.Reward.Title)
                ?.Invoke(CleanArgs(redemption.UserInput));
        }

        private void Client_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {
            GetCommand(e.Command.CommandText)
                ?.Invoke(CleanArgs(e.Command.ArgumentsAsString));
        }

        private string CleanArgs(string argumentsAsString)
        {
            return argumentsAsString
                .Replace("\"", "")
                .Replace(';', ',');
        }

        public delegate void ChatCommand(string arguments);
        public Dictionary<string, ChatCommand> ChatCommands
        { get; set; } = new Dictionary<string, ChatCommand>();

        private ChatCommand GetRedeemCommand(string commandText)
        {
            if (commandText == null)
                return null;

            string key = commandText.ToLower();
            if (ChatCommands.ContainsKey(key))
                return ChatCommands[key];

            return null;
        }

        private ChatCommand GetCommand(string commandText)
        {
            if (commandText == null) 
                return null;
            if(commandText == "help")
                return HelpCommand;

            string key = "!" + commandText.ToLower();
            if (ChatCommands.ContainsKey(key))
                return ChatCommands[key];

            return null;
        }
        private void HelpCommand(string arguments)
        {
            string message = "available commands (may or may not take arguments): ";
            foreach(string key in ChatCommands.Keys)
                if(key.StartsWith("!"))
                    message += " " + key;

            client.SendMessage(TwitchUsername, message);
        }

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            //Console.WriteLine($"Connected to {e.AutoJoinChannel}");
            // channel arg takes care of this //client.JoinChannel(TwitchUsername);
        }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            Console.WriteLine("Hey guys! I am a bot connected via TwitchLib!");
            client.SendMessage(e.Channel, "For TF2 Spectator commands, type !help");
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            //Console.WriteLine(e.ChatMessage.Channel+" MESSAGE:"+
            //    e.ChatMessage.BotUsername + "got from " + e.ChatMessage.Username + "message: " + e.ChatMessage.Message);
            //if (e.ChatMessage.Message.Contains("badword"))
            //    client.TimeoutUser(e.ChatMessage.Channel, e.ChatMessage.Username, TimeSpan.FromMinutes(30), "Bad word! 30 minute timeout!");
        }

        private void Client_OnWhisperReceived(object sender, OnWhisperReceivedArgs e)
        {
            //if (e.WhisperMessage.Username == "my_friend")
            //    client.SendWhisper(e.WhisperMessage.Username, "Hey! Whispers are so cool!!");
        }

        private void Client_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            //if (e.Subscriber.SubscriptionPlan == SubscriptionPlan.Prime)
            //    client.SendMessage(e.Channel, $"Welcome {e.Subscriber.DisplayName} to the substers! You just earned 500 points! So kind of you to use your Twitch Prime on this channel!");
            //else
            //    client.SendMessage(e.Channel, $"Welcome {e.Subscriber.DisplayName} to the substers! You just earned 500 points!");
        }
    }

}