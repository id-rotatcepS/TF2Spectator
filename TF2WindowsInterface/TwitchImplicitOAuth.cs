using System;
using System.Diagnostics;

namespace TF2WindowsInterface
{
    public class TwitchImplicitOAuth
    {
        private static readonly string TwitchOAuthPrefix = "https://id.twitch.tv/oauth2/authorize";

        //private string TwitchClientName;

        private string TwitchClientID;

        public TwitchImplicitOAuth(
            //string clientName,
            string clientID)
        {
            //TwitchClientName = clientName;
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