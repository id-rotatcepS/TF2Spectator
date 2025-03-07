using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TwitchAuthInterface;

using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

namespace TF2SpectatorWin
{
    public class TwitchInstance : IDisposable
    {
        private const string ClientID = "xvco4mzu0kr55ah5gr2xxyefx0kvbc";

        public readonly string TwitchUsername;

        // https://dev.twitch.tv/docs/authentication/scopes/
        public string[] ClientOAuthScopes =>
            new[]
            {
                TwitchImplicitOAuth.ReadRewardRedemptions,
                // --- I'm pretty sure the TwitchLib command handler must require chat:read
                TwitchImplicitOAuth.ChatRead,
                TwitchImplicitOAuth.ChatSend,
            };

        private TwitchClient Client;
        private EventSubWebsocketClient _EventSubClient;
        private ILoggerFactory _ESLoggerFactory;
        private readonly TwitchAPI _TwitchAPI;

        private readonly User TwitchUser;

        /// <summary>
        /// Only affiliate and partner can do redeems.
        /// (BroadcasterType is "", not "affiliate" or "partner")
        /// </summary>
        public bool HasRedeems => !string.IsNullOrEmpty(TwitchUser?.BroadcasterType);

        ///// <summary>
        ///// Seconds that were Remaining when the AuthToken was verified at instantiation (or zero).
        ///// </summary>
        //public int AuthorizedSecondsRemaining { get; private set; }

        public static string AuthToken { get; set; } = "";

        public TwitchInstance(string twitchUsername)
        {
            TwitchUsername = twitchUsername;

            _TwitchAPI = new TwitchAPI();
            _TwitchAPI.Settings.ClientId = ClientID;

            //FUTURE we're supposed to validate the auth token every hour per twitch docs, or risk app shutdown.
            // but first the Validate call has to be a real validate call... TwitchLib update needed?
            EnsureValidAuthTokenFromUser();

            TwitchUser = GetTwitchChannelInfo();

            StartClient(new ConnectionCredentials(TwitchUsername, AuthToken));
        }

        private void EnsureValidAuthTokenFromUser()
        {
            AuthToken = GetInitialAuthToken();
            _TwitchAPI.Settings.AccessToken = AuthToken;

            while (IsInvalidAccessToken())
            {
                // blank value to reflect the failure if next attempt throws an exception.
                AuthToken = string.Empty;

                AuthToken = GetNewAuthorizedToken();
                _TwitchAPI.Settings.AccessToken = AuthToken;
            }
        }

        private string GetInitialAuthToken()
        {
            if (!string.IsNullOrEmpty(AuthToken))
                return AuthToken;

            return GetNewAuthorizedToken();
        }

        private string GetNewAuthorizedToken()
        {
            // get the value for the twitchOAuth: "access_token"
            TwitchImplicitOAuth oauth = new TwitchImplicitOAuth(
                //clientName: "TF2SpectatorControl",
                clientID: ClientID);
            // includes redeems in scope even if it's a non-affiliated account
            OAuthResult authResult = oauth.Authorize(ClientOAuthScopes);

            // Throws exception if it was an error result:
            return authResult.AccessToken;
        }

        //// one day in seconds
        //private readonly int MinimumRemainingAuthorizedSeconds = 60 * 60 * 24;
        private bool IsInvalidAccessToken()
        {
            // TODO twitchlib update needed?
            // For some reason the validate call times out.
            // so we use a substitute that throws exception when things are bad.
            try
            {
                _ = GetTwitchChannelInfo();
            }
            catch (Exception)
            {
                return true;
            }

            //Task<TwitchLib.Api.Auth.ValidateAccessTokenResponse> validation
            //    = _TwitchAPI.Auth.ValidateAccessTokenAsync();

            //AuthorizedSecondsRemaining = 0;
            //if (!validation.Wait(DefaultTimeout))
            //    throw new TwitchTimeoutException("Checking Access Token");
            //if (validation.Result == null)
            //    return true;

            //AuthorizedSecondsRemaining = validation.Result.ExpiresIn;
            //var days = AuthorizedSecondsRemaining / 60.0 / 60.0 / 24.0; // TODO delete test

            //// don't risk the authorization expiring during this stream.
            //if (AuthorizedSecondsRemaining < MinimumRemainingAuthorizedSeconds)
            //    return true;

            return false;
        }

        private User GetTwitchChannelInfo()
        {
            Task<GetUsersResponse> usersTask = _TwitchAPI.Helix.Users
                .GetUsersAsync(logins: new List<string>(new string[] {
                    TwitchUsername
                }));

            if (!usersTask.Wait(DefaultTimeout))
                throw new TwitchTimeoutException("Getting user info");

            GetUsersResponse getUsersResponse = usersTask.Result;
            return getUsersResponse.Users.First();
        }

        private void StartClient(ConnectionCredentials credentials)
        {
            StartEventSubWhenNeeded();

            ClientOptions clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            Client = new TwitchClient(customClient);
            Client.Initialize(credentials, channel: TwitchUsername);

            Client.OnLog += Client_OnLog;
            Client.OnSendReceiveData += Client_OnSendReceiveData;
            Client.OnUnaccountedFor += Client_OnUnaccountedFor;
            Client.OnConnected += Client_OnConnected;
            Client.OnJoinedChannel += Client_OnJoinedChannel;

            Client.OnMessageReceived += Client_OnMessageReceived;
            Client.OnWhisperReceived += Client_OnWhisperReceived;
            Client.OnNewSubscriber += Client_OnNewSubscriber;

            Client.OnChatCommandReceived += Client_OnChatCommandReceived;

            _ = Client.Connect();
        }

        private TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
        public void Dispose()
        {
            Client.Disconnect();
            //FUTURE synchronizing disconnect so that disposing the loggerfactory dispose doesn't conflict - could probably do it as an event instead.
            _ = _EventSubClient?.DisconnectAsync().Wait(DefaultTimeout);
            _EventSubClient = null;
            _ESLoggerFactory?.Dispose();
            _ESLoggerFactory = null;
        }

        private void StartEventSubWhenNeeded()
        {
            // EventSub is only being used for Redemptions, and redemptions are only valid for affiliate/partner accounts.
            // (The Internet implies we'll get a BADAUTH for attempting this with default accounts)
            if (!HasRedeems)
                return;

            _ESLoggerFactory?.Dispose(); // just in case.
            _ESLoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

            _EventSubClient = new EventSubWebsocketClient(_ESLoggerFactory);

            // sub format: connect, post-connection send topics.
            _EventSubClient.WebsocketConnected += EventSub_WebsocketConnected;
            _EventSubClient.WebsocketDisconnected += EventSub_WebsocketDisconnected;
            _EventSubClient.ErrorOccurred += EventSub_ErrorOccurred;

            // events for subscriptions we will be registering:
            _EventSubClient.ChannelPointsCustomRewardRedemptionAdd += EventSub_ChannelPointsCustomRewardRedemptionAdd;
            //  EventSubClient.ChannelPointsCustomRewardRedemptionUpdate // update that status=fullfilled or cancelled.

            //FUTURE pass reconnect URL from Twitch?
            _ = _EventSubClient.ConnectAsync();
            //FUTURE use EventSubClient.ReconnectAsync somewhere?
            //      EventSubClient.WebsocketReconnected +=
        }

        private async Task EventSub_WebsocketConnected(object sender, WebsocketConnectedArgs args)
        {
            //_logger.LogInformation
            Console.WriteLine($"Websocket {_EventSubClient.SessionId} connected!");

            if (args.IsRequestedReconnect)
                return;

            EventSub es = _TwitchAPI.Helix.EventSub;

            await SubscribeToChannelPointsCustomRewardRedemptionAdd(es);
        }

        private async Task SubscribeToChannelPointsCustomRewardRedemptionAdd(EventSub es)
        {
            //TwitchLib.Api.Helix.Models.EventSub.CreateEventSubSubscriptionResponse subscriptionResponse
            _ = await es.CreateEventSubSubscriptionAsync(
                // https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types/
                //Channel Points Custom Reward Redemption Add 	
                //channel.channel_points_custom_reward_redemption.add 	1 	
                //A viewer has redeemed a custom channel points reward on the specified channel.
                type: "channel.channel_points_custom_reward_redemption.add", version: "1",
                //Subscription-specific parameters.
                //Pass in the broadcaster user ID for the channel you want to receive channel points custom reward redemption notifications for.
                //You can optionally pass in a reward id to only receive notifications for a specific reward.
                condition: new Dictionary<string, string>()
                {
                    ["broadcaster_user_id"] = TwitchUser.Id,
                },
                EventSubTransportMethod.Websocket, websocketSessionId: _EventSubClient.SessionId);
        }

        private async Task EventSub_WebsocketDisconnected(object sender, EventArgs args)
        {
            //_logger.LogError
            Console.WriteLine($"Websocket eventsub {_EventSubClient.SessionId} disconnected!");

            int delay = 1000;
            while (!await _EventSubClient.ReconnectAsync())
            {
                //_logger.LogError
                Console.WriteLine("Websocket reconnect failed!");
                await Task.Delay(delay);

                // "... You should implement a ... reconnect strategy with exponential backoff"
                delay = 2 * delay;
                if (delay > 1000 * 30)
                    delay = 1000;
            }
        }

        private Task EventSub_ErrorOccurred(object sender, ErrorOccuredArgs args)
        {
            Console.WriteLine("eventsub ERROR " + args.Exception?.ToString());
            //TODO better task answer?
            return Task.CompletedTask;
        }

        private Task EventSub_ChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs e)
        {
            ChannelPointsCustomRewardRedemption redemption = e.Notification.Payload.Event;
            ChatCommandDetails commandDetails = GetRedeemCommandByNameOrID(redemption);
            string userName = redemption.UserName;
            string userInput = redemption.UserInput;
            //the redemption.Id is not a message id to reply to. Using it causes no message at all
            string messageID = null;

            // redemption.Reward.Cost
            commandDetails
                ?.InvokeCommand(
                    userName,
                    userInput,
                    messageID);

            //TODO better task answer? consider adding simple Async version of "InvokeCommand" above
            return Task.CompletedTask;
        }

        private ChatCommandDetails GetRedeemCommandByNameOrID(ChannelPointsCustomRewardRedemption redemption)
        {
            ChatCommandDetails byID = GetRedeemCommand(redemption.Reward.Id);
            if (byID != null)
                return byID;

            ChatCommandDetails byTitle = GetRedeemCommand(redemption.Reward.Title);

            AddAutomaticIdAlias(redemption.Reward.Id, byTitle);

            return byTitle;
        }

        public delegate void CommandRedeemId(ChatCommandDetails commandDetail, string id);
        public event CommandRedeemId OnCommandRedeemedWithoutIdAlias;
        private void AddAutomaticIdAlias(string id, ChatCommandDetails byTitle)
        {
            if (byTitle == null)
                return;

            OnCommandRedeemedWithoutIdAlias?.Invoke(byTitle, id);
        }

        private void Client_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {
            ChatCommand chatCommand = e.Command;
            ChatCommandDetails commandDetails = GetChatCommand(chatCommand.CommandText);
            string userName = chatCommand.ChatMessage.DisplayName;
            string userInput = chatCommand.ArgumentsAsString;
            string messageID = chatCommand.ChatMessage.Id;
            commandDetails
                ?.InvokeCommand(
                    userName,
                    userInput,
                    messageID);
        }

        public void AddCommand(ChatCommandDetails chatCommandDetails)
        {
            AddCommand(chatCommandDetails.Command, chatCommandDetails);
        }

        public void AddCommand(string alias, ChatCommandDetails chatCommandDetails)
        {
            if (ChatCommands == null)
                ChatCommands = new Dictionary<string, ChatCommandDetails>();

            ChatCommands.Add(alias.ToLower(), chatCommandDetails);
        }

        public void AddAlias(string alias, string commandName)
        {
            if (HasCommand(commandName))
            {
                ChatCommandDetails com = ChatCommands[commandName.ToLower()];
                AddCommand(alias, com);
                //TODO add alias to com.Aliases
            }
        }

        private Dictionary<string, ChatCommandDetails> ChatCommands
        { get; set; } = new Dictionary<string, ChatCommandDetails>();

        public string ConnectMessage { get; internal set; }

        private ChatCommandDetails GetRedeemCommand(string commandText)
        {
            if (HasCommand(commandText))
                return ChatCommands[commandText.ToLower()];

            return null;
        }

        public bool HasCommand(string key)
        {
            if (key == null) return false;
            return ChatCommands.ContainsKey(key.ToLower());
        }

        private ChatCommandDetails GetChatCommand(string commandText)
        {
            if (commandText == null)
                return null;

            if (commandText == "help" || commandText == "commands")
                return new ChatCommandDetails("!help", HelpCommand, "this help command. \"!help commandName\" for help on that command");

            string key = "!" + commandText.ToLower();
            if (HasCommand(key))
                return ChatCommands[key];

            return null;
        }

        private void HelpCommand(string userDisplayName, string arguments, string messageID)
        {
            string message;
            if (string.IsNullOrEmpty(arguments))
            {
                StringBuilder commandList = new StringBuilder();
                foreach (string key in ChatCommands.Keys)
                    if (key.StartsWith("!"))
                        _ = commandList.Append(" ").Append(GetMatchingCommandName(key));
                if (commandList.Length == 0)
                    return;

                message = "\"!help commandname\" for more info on these commands: "
                    + commandList.ToString();
            }
            else
            {
                if (arguments.StartsWith("!"))
                    arguments = arguments.Substring(1);
                ChatCommandDetails com = GetChatCommand(arguments.Trim());
                if (com == null)
                    return;

                string help = com.Help;
                if (string.IsNullOrEmpty(help))
                    help = "?";

                string messagef;
                if (!("!" + arguments.ToLower()).Contains(com.Command.ToLower()))
                    messagef = "(alias for {0}): {1}";
                else
                    messagef = "{0}: {1}";
                message = string.Format(messagef, com.Command, help);
            }

            SendReplyWithWrapping(messageID, message);
        }

        private string GetMatchingCommandName(string key)
        {
            string name = ChatCommands[key].Command;
            if (key.ToLower() != name.ToLower())
                name = ChatCommands[key].Aliases.FirstOrDefault((n) => n.ToLower() == key.ToLower()) ?? key;

            return name;
        }

        // per an error I got...
        public readonly int TwitchMaxMessageLength = 500;
        /// <summary>
        /// Sends one or more messages, accounting for max allowed message length.
        /// </summary>
        /// <param name="message"></param>
        public void SendMessageWithWrapping(string message)
        {
            SendMessageOrReplyWithWrapping(messageID: null, message);
        }
        private void SendMessageOrReplyWithWrapping(string messageID, string message)
        {
            string[] messages = SplitMessage(message);
            foreach (string msg in messages)
                if (Client == null || !Client.JoinedChannels.Any())
                    Console.WriteLine(msg);
                else
                {
                    if (string.IsNullOrEmpty(messageID))
                    {
                        Client.SendMessage(TwitchUsername, msg);
                        // only reply to FIRST wrapped message.
                        messageID = null;
                    }
                    else
                        Client.SendReply(TwitchUsername, messageID, msg);
                }
        }
        public void SendReplyWithWrapping(string messageID, string message)
        {
            SendMessageOrReplyWithWrapping(messageID, message);
        }

        private string[] SplitMessage(string message)
        {
            List<string> messages = new List<string>();
            while (message.Length > TwitchMaxMessageLength)
            {
                int len = GetNextBreakLength(message);

                messages.Add(message.Substring(0, len).TrimEnd());
                message = message.Substring(len).TrimStart();
            }
            messages.Add(message);
            return messages.ToArray();
        }

        private int GetNextBreakLength(string message)
        {
            int idx = message.Substring(0, TwitchMaxMessageLength)
                .LastIndexOfAny(new[] { ' ', '-', '\t', '\n', '\r' });
            return idx >= 0
                ? idx + 1
                : TwitchMaxMessageLength;
        }

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private void Client_OnSendReceiveData(object sender, OnSendReceiveDataArgs e)
        {
            //Received - @badge-info=subscriber/1;badges=broadcaster/1,subscriber/0;color=#2E8B57;custom-reward-id=cbabba18-d1ec-44ca-9e30-59303812a600;display-name=id_rotatcepS;emotes=;first-msg=0;flags=;id=17374374-de7f-4f82-bb54-c61c7a5ed19f;mod=0;returning-chatter=0;room-id=491942603;subscriber=1;tmi-sent-ts=1703397616900;turbo=0;user-id=491942603;user-type= :id_rotatceps!id_rotatceps@id_rotatceps.tmi.twitch.tv PRIVMSG #id_rotatceps :sdf

            Console.WriteLine($"{e.Direction.ToString()} - {e.Data}");
        }

        private void Client_OnUnaccountedFor(object sender, OnUnaccountedForArgs e)
        {

            Console.WriteLine($"{e.BotUsername} - {e.RawIRC}");
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            //Console.WriteLine($"Connected to {e.AutoJoinChannel}");
            // channel arg takes care of this //client.JoinChannel(TwitchUsername);
        }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            Console.WriteLine("Hey guys! I am a bot connected via TwitchLib!");
            ((TwitchClient)sender).SendMessage(e.Channel, ConnectMessage);
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string msg = e.ChatMessage.Message;

            // Math in chat feature
            string response = mathChat.GetMathAnswer(msg);
            if (response != null)
            {
                SendReplyWithWrapping(e.ChatMessage.Id, response);
                return;
            }

            Console.WriteLine(e.ChatMessage.Channel + " MESSAGE:" +
                e.ChatMessage.BotUsername + "got from " + e.ChatMessage.Username + "message: " + e.ChatMessage.Message
                + " reward:" + e.ChatMessage.CustomRewardId);
            //if (e.ChatMessage.Message.Contains("badword"))
            //    client.TimeoutUser(e.ChatMessage.Channel, e.ChatMessage.Username, TimeSpan.FromMinutes(30), "Bad word! 30 minute timeout!");
        }

        private MathChat mathChat = new MathChat();

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