using Microsoft.Extensions.Logging;
using SimpleExpressionEvaluator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchAuthInterface;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Models.Responses.Messages.Redemption;

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
        private TwitchPubSub Pubsub;

        private readonly User TwitchUser;

        /// <summary>
        /// Only affiliate and partner can do redeems.
        /// (BroadcasterType is "", not "affiliate" or "partner")
        /// </summary>
        public bool HasRedeems => !string.IsNullOrEmpty(TwitchUser?.BroadcasterType);

        public static string AuthToken { get; set; } = "";

        public TwitchInstance(string twitchUsername)
        {
            TwitchUsername = twitchUsername;
            AuthToken = GetAuthToken();

            ConnectionCredentials credentials = new ConnectionCredentials(TwitchUsername, AuthToken);

            TwitchUser = GetTwitchChannelInfo();

            StartClient(credentials);
        }

        private string GetAuthToken()
        {
            if (!string.IsNullOrEmpty(AuthToken))
                return AuthToken;

            // get the value for the twitchOAuth: "access_token"
            TwitchImplicitOAuth oauth = new TwitchImplicitOAuth(
                //clientName: "TF2SpectatorControl",
                clientID: ClientID);
            // includes redeems in scope even if it's a non-affiliated account
            OAuthResult authResult = oauth.Authorize(ClientOAuthScopes);
            
            // Throws exception if it was an error result:
            return authResult.AccessToken;
        }

        private User GetTwitchChannelInfo()
        {
            TwitchAPI twitchAPI = new TwitchAPI();
            twitchAPI.Settings.ClientId = ClientID;
            twitchAPI.Settings.AccessToken = AuthToken;

            Task<GetUsersResponse> usersTask = twitchAPI.Helix.Users
                .GetUsersAsync(logins: new List<string>(new string[] {
                    TwitchUsername
                }));

            usersTask.Wait();

            GetUsersResponse getUsersResponse = usersTask.Result;
            return getUsersResponse.Users.First();
        }

        private void StartClient(ConnectionCredentials credentials)
        {
            StartPubSubWhenNeeded();

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

        public void Dispose()
        {
            Client.Disconnect();
        }

        private void StartPubSubWhenNeeded()
        {
            // PubSub is only being used for Redemptions, and redemptions are only valid for affiliate/partner accounts.
            // (The Internet implies we'll get a BADAUTH for attempting this with default accounts)
            if (!HasRedeems)
                return;

            using (ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole()))
            {
                ILogger<TwitchPubSub> logger = factory.CreateLogger<TwitchPubSub>();
                Pubsub = new TwitchPubSub(logger);
            }
            //Pubsub.OnLog += Client_OnLog;

            Pubsub.OnListenResponse += Pubsub_OnListenResponse;
            Pubsub.ListenToChannelPoints(channelTwitchId: TwitchUser.Id);

            Pubsub.OnChannelPointsRewardRedeemed += Pubsub_OnChannelPointsRewardRedeemed;

            // odd pubsub format... connect, within 15 seconds of connection send topics.
            Pubsub.OnPubSubServiceClosed += Pubsub_OnPubSubServiceClosed;
            Pubsub.OnPubSubServiceError += Pubsub_OnPubSubServiceError;
            Pubsub.OnPubSubServiceConnected += (sender, e) =>
            {
                (sender as TwitchPubSub).SendTopics(AuthToken);
            };
            Pubsub.Connect();
        }

        private void Pubsub_OnPubSubServiceClosed(object sender, EventArgs e)
        {
            Console.WriteLine("pubsub CLOSED"); // no special event
        }

        private void Pubsub_OnPubSubServiceError(object sender, TwitchLib.PubSub.Events.OnPubSubServiceErrorArgs e)
        {
            Console.WriteLine("pubsub ERROR " + e.Exception?.ToString());
        }

        private void Pubsub_OnListenResponse(object sender, TwitchLib.PubSub.Events.OnListenResponseArgs e)
        {
            Console.WriteLine("pubsub heard " + (e.Successful ? "succeeded" : "failed") + ":" + e.Response?.Error + ":" + e.Topic);
        }

        private void Pubsub_OnChannelPointsRewardRedeemed(object sender, TwitchLib.PubSub.Events.OnChannelPointsRewardRedeemedArgs e)
        {
            Redemption redemption = e.RewardRedeemed.Redemption;
            ChatCommandDetails commandDetails = GetRedeemCommandByNameOrID(redemption);
            string userName = redemption.User.DisplayName;
            string userInput = redemption.UserInput;
            //the redemption.Id is not a message id to reply to. Using it causes no message at all
            string messageID = null;

            // redemption.Reward.Cost
            commandDetails
                ?.InvokeCommand(
                    userName,
                    userInput,
                    messageID);
        }

        private ChatCommandDetails GetRedeemCommandByNameOrID(Redemption redemption)
        {
            return GetRedeemCommand(redemption.Reward.Title)
                // if the title no longer works, maybe they set up the ID as an alias?
                ?? GetRedeemCommand(redemption.Reward.Id);
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
                message = "\"!help commandname\" for more info on these commands: ";
                foreach (string key in ChatCommands.Keys)
                    if (key.StartsWith("!"))
                        message += " " + GetMatchingCommandName(key);
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
            ((TwitchClient)sender).SendMessage(e.Channel, "For TF2 Spectator commands, type !help");
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string msg = e.ChatMessage.Message;

            // Math in chat feature
            string response = GetMathAnswer(msg);
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

        // only try messages that have at least two numbers and between them something mathish other than a decimal point.
        private readonly Regex mathRegex = new Regex("\\d.*[-+/*^\\p{IsMathematicalOperators}\\p{Sm}].*\\d");
        private readonly ExpressionEvaluator mathDoer = new ExpressionEvaluator();
        private string GetMathAnswer(string msg)
        {
            //consider stripping before/after text one might enter, like "what is () = ?"

            if (!mathRegex.IsMatch(msg))
                return null;

            try
            {
                string mathAnswer = mathDoer.Evaluate(msg).ToString();
                if (mathAnswer == null)
                    return null;

                return (msg + " = " + mathAnswer);
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message);
                return null;
            }
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