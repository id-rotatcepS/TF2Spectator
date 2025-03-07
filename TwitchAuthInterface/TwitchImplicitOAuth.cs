using System;
using System.Diagnostics;

namespace TwitchAuthInterface
{
    /// <summary>
    /// Authorize a Twitch client ID using Implicit OAuth to get an Auth Token
    /// https://dev.twitch.tv/docs/authentication/getting-tokens-oauth/#implicit-grant-flow
    /// </summary>
    public class TwitchImplicitOAuth
    {
        // https://dev.twitch.tv/docs/authentication/scopes/
        #region authorization scope names
        /// <summary>
        /// channel:read:redemptions 	View Channel Points custom rewards and their redemptions on a channel.
        /// </summary>
        public const string ReadRewardRedemptions = "channel:read:redemptions";
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
        /// <summary>
        /// chat:read 	View live stream chat messages.
        /// </summary>
        public const string ChatRead = "chat:read";
        /// <summary>
        /// chat:edit 	Send live stream chat messages.
        /// </summary>
        public const string ChatSend = "chat:edit";
        #endregion authorization scope names

        private static readonly string TwitchOAuthPrefix = "https://id.twitch.tv/oauth2/authorize";

        private readonly string TwitchClientID;

        public TwitchImplicitOAuth(
            string clientID)
        {
            TwitchClientID = clientID;
        }

        public string RedirectUriPrefix { get; set; } = "http://localhost:3000/oauthResponse/";


        /// <summary>
        /// Launch twitch URL to request authorization, and receive the result
        /// </summary>
        /// <param name="clientOAuthScopes">https://dev.twitch.tv/docs/authentication/scopes/</param>
        /// <returns></returns>
        public OAuthResult Authorize(string[] clientOAuthScopes)
        {
            // generate random statevalue for protection from XSS
            string statevalue = new Random(DateTime.Now.Millisecond).Next().ToString();

            string TwitchOAuthLink = GetTwitchOAuthLink(statevalue, RedirectUriPrefix, clientOAuthScopes);
            OAuthRedirectListener responseListener = new OAuthRedirectListener(RedirectUriPrefix);

            _ = Process.Start(TwitchOAuthLink);
            OAuthResult listenResult = responseListener.ReceiveAuthTokenFromRedirect(statevalue);
            return listenResult;
        }

        private string GetTwitchOAuthLink(string statevalue, string redirectUriPrefix, params string[] scopes)
        {
            // space-separated list of scopes
            string scopestring = string.Join(" ", scopes);
            //"&scope=channel%3Amanage%3Apolls+channel%3Aread%3Apolls" +
            // escape for url
            scopestring = scopestring.Replace(":", "%3A").Replace(" ", "+");

            string twitchOAuthLink = TwitchOAuthPrefix +
                "?response_type=token" +
                "&client_id=" + TwitchClientID +
                "&redirect_uri=" + redirectUriPrefix +
                "&scope=" + scopestring;

            // twitch doesn't appear to support response_mode to change the result into a query or form_post
            // https://auth0.com/docs/authenticate/protocols/oauth#how-response-mode-works
            // so instead we have to force the result to run javascript to resubmit itself with the fragment as a query.
            //twitchOAuthLink += "&response_mode=query";
            //twitchOAuthLink += "&response_mode=form_post";

            if (!string.IsNullOrEmpty(statevalue))
                twitchOAuthLink += "&state=" + statevalue;

            return twitchOAuthLink;
        }
    }

}