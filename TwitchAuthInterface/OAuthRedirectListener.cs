using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;

namespace TwitchAuthInterface
{
    /// <summary>
    /// Connect your oauth request redirect URL and get the resulting authtoken.
    /// based on example at https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener?view=netcore-3.1
    /// </summary>
    public class OAuthRedirectListener
    {
        public string[] Prefixes { get; }

        /// <summary>
        /// Configure prefixes that it will 
        /// </summary>
        /// <param name="prefixes">URI prefixes are required, for example "http://contoso.com:8080/index/" or "http://localhost:8686/oauthResponse/"</param>
        /// <exception cref="NotSupportedException"><see cref="HttpListener.IsSupported"/></exception>
        /// <exception cref="ArgumentException">no prefixes</exception>
        public OAuthRedirectListener(params string[] prefixes)
        {
            if (!HttpListener.IsSupported)
                throw new NotSupportedException("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");

            // URI prefixes are required,
            // for example "http://contoso.com:8080/index/".
            if (prefixes == null || prefixes.Length == 0)
                throw new ArgumentException(nameof(prefixes));

            Prefixes = prefixes;
        }

        /// <summary>
        /// Listens for an oauth authorization response on the prefix.
        /// </summary>
        /// <returns></returns>
        public OAuthResult ReceiveAuthTokenFromRedirect(string stateValue)
        {
            // Create a listener.
            HttpListener listener = new HttpListener();
            // Add the prefixes.
            foreach (string s in Prefixes)
                listener.Prefixes.Add(s);

            listener.Start();
            try
            {
                // Note: The GetContext method blocks while waiting for a request.
                HttpListenerContext context = listener.GetContext();
                context = ResubmitWithFragmentAsQuery(listener, context);

                OAuthResult result = Parse(context, stateValue);

                Respond(context, result);

                return result;
            }
            finally
            {
                try
                {
                    listener.Stop();
                }
                catch (ObjectDisposedException)
                {
                    // ok
                }
            }
        }

        private HttpListenerContext ResubmitWithFragmentAsQuery(HttpListener listener, HttpListenerContext context)
        {
            if (context.Request.QueryString.Count == 0)
            {
                // Obtain a response object.
                HttpListenerResponse response = context.Response;

                // Construct a response that loads with hash as the query.
                string responseString =
                    "<HTML>" +
                    "<head><script>" +
                    "window.location.href = " +
                    "window.location.href.substring(0, window.location.href.indexOf(window.location.hash))" +
                    "+'?'+window.location.hash.substring(1);" +
                    "</script></head>" +
                    "<BODY> Registering " +
                    "</BODY></HTML>";

                WriteResponse(response, responseString);

                return listener.GetContext();
            }
            else
            {
                return context;
            }
        }

        private static void WriteResponse(HttpListenerResponse response, string responseString)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;

            Stream output = response.OutputStream;
            try
            {
                output.Write(buffer, 0, buffer.Length);
            }
            finally
            {
                // You must close the output stream.
                output.Close();
            }
        }

        private static OAuthResult Parse(HttpListenerContext context, string stateValue)
        {
            // actual response using default response_mode:
            // http://localhost:3000/oauthResponse/#access_token=i8n88ff6bvhtrv0jndy5eclyct4cnn&scope=channel%3Aread%3Aredemptions+chat%3Aread&state=700010673&token_type=bearer

            HttpListenerRequest request = context.Request;

            return ParseResponse(stateValue, request.QueryString);
        }

        private static OAuthResult ParseResponse(string stateValue, NameValueCollection keyValuePairs)
        {
            string accessToken = keyValuePairs["access_token"];
            if (accessToken == null)
            {
                return ParseErrorResponse(keyValuePairs);
            }

            //string scope = keyValuePairs["scope"];

            string state = keyValuePairs["state"];
            if (!string.IsNullOrEmpty(stateValue) && stateValue != state)
                return new OAuthErrorResult("Security", "state value did not match, possible XSS attack");

            //string token_type = keyValuePairs["token_type"];

            return new OAuthResult(accessToken);
        }

        private static OAuthResult ParseErrorResponse(NameValueCollection keyValuePairs)
        {
            string error = keyValuePairs["error"];
            string desc = keyValuePairs["error_description"];
            //keyValuePairs["state"];
            return new OAuthErrorResult(error, desc);
        }

        private static void Respond(HttpListenerContext context, OAuthResult result)
        {
            // Obtain a response object.
            HttpListenerResponse response = context.Response;

            // Construct a response.
            string responseString = result.IsError
                ? "<HTML><BODY> Twitch linkage failed.<br/>" +
                "<dl><dt>" + result.Error + "</dt><dd>" + result.ErrorDescription + "</dd></dl><br/>" +
                "Try again?</BODY></HTML>"
                : "<HTML><BODY> Twitch linkage Authorized</BODY></HTML>";

            WriteResponse(response, responseString);
        }
    }

    public class OAuthResult
    {
        public OAuthResult(string accessToken)
        {
            AccessToken = accessToken;
        }

        /// <summary>
        /// the oauth2 redirect fragment "access_token" value.
        /// </summary>
        public virtual string AccessToken { get; }

        public bool IsError { get; protected set; } = false;
        public string Error { get; protected set; } = string.Empty;
        public string ErrorDescription { get; protected set; } = string.Empty;
    }
    internal class OAuthErrorResult : OAuthResult
    {
        public OAuthErrorResult(string error, string errorDescription)
            : base(string.Empty)
        {
            IsError = true;
            Error = error;
            ErrorDescription = errorDescription;
        }
        public override string AccessToken => throw new NotImplementedException("Access Token not received");
    }


}