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
                //Console.WriteLine("Listening...");

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

        private static OAuthResult Parse(HttpListenerContext context, string stateValue)
        {
            // actual response using default response_mode:
            // http://localhost:3000/oauthResponse/#access_token=i8n88ff6bvhtrv0jndy5eclyct4cnn&scope=channel%3Aread%3Aredemptions+chat%3Aread&state=700010673&token_type=bearer

            HttpListenerRequest request = context.Request;

            //if (request.HttpMethod == "POST")
            //{
            //    string text;
            //    using (var reader = new StreamReader(request.InputStream,
            //                                         request.ContentEncoding))
            //    {
            //        text = reader.ReadToEnd();
            //    }

            //    NameValueCollection keyValuePairs = GetAmpersandKeyValues(text);

            //    return ParseResponse(stateValue, keyValuePairs);
            //}

            return ParseResponse(stateValue, request.QueryString);

            //// this doesn't work because #fragment isn't sent to the server by the web browser.
            //Uri oauthRedirectUrl = request.Url;
            //bool haveToken =
            //    !string.IsNullOrEmpty(oauthRedirectUrl.Fragment)
            //    && oauthRedirectUrl.Fragment.StartsWith("#access_token=");
            //if (haveToken)
            //{
            //    /* http://localhost:3000/
            //        #access_token=73d0f8mkabpbmjp921asv2jaidwxn
            //        &scope=channel%3Amanage%3Apolls+channel%3Aread%3Apolls
            //        &state=c3ab8aa609ea11e793ae92361f002671
            //        &token_type=bearer */
            //    int fragmentStartLength = "#".Length;
            //    return ParseResponse(stateValue, oauthRedirectUrl.Fragment.Substring(fragmentStartLength));
            //}
            //else
            //{
            //    /* http://localhost:3000/
            //        ?error=access_denied
            //        &error_description=The+user+denied+you+access
            //        &state=c3ab8aa609ea11e793ae92361f002671 */
            //    NameValueCollection keyValuePairs = request.QueryString;

            //    return ParseErrorResponse(keyValuePairs);
            //}
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

                return listener.GetContext();
            }
            else
            {
                return context;
            }
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

        //private static NameValueCollection GetAmpersandKeyValues(string v)
        //{
        //    NameValueCollection result = new NameValueCollection();
        //    foreach (string item in v.Split('&'))
        //    {
        //        string[] p = item.Split('=');
        //        if (p.Length > 1)
        //            result.Add(p[0], p[1]);
        //    }
        //    return result;
        //}

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

        //// based on example at https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.tcplistener?redirectedfrom=MSDN&view=netcore-3.1
        //public static void doIt()
        //{
        //    TcpListener server = null;
        //    try
        //    {
        //        // Set the TcpListener on port 13000.
        //        int port = 13000;
        //        IPAddress localAddr = IPAddress.Parse("127.0.0.1");

        //        // TcpListener server = new TcpListener(port);
        //        server = new TcpListener(localAddr, port);

        //        // Start listening for client requests.
        //        server.Start();

        //        // Buffer for reading data
        //        byte[] bytes = new byte[256];
        //        string data = null;

        //        // Enter the listening loop.
        //        while (true)
        //        {
        //            //Console.Write("Waiting for a connection... ");

        //            // Perform a blocking call to accept requests.
        //            // You could also use server.AcceptSocket() here.
        //            using (System.Net.Sockets.TcpClient client = server.AcceptTcpClient())
        //            {
        //                Console.WriteLine("Connected!");

        //                data = null;

        //                // Get a stream object for reading and writing
        //                NetworkStream stream = client.GetStream();

        //                int i;

        //                // Loop to receive all the data sent by the client.
        //                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
        //                {
        //                    // Translate data bytes to a ASCII string.
        //                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
        //                    Console.WriteLine("Received: {0}", data);

        //                    // Process the data sent by the client.
        //                    data = data.ToUpper();

        //                    byte[] msg = System.Text.Encoding.ASCII.GetBytes(data);

        //                    // Send back a response.
        //                    stream.Write(msg, 0, msg.Length);
        //                    Console.WriteLine("Sent: {0}", data);
        //                }
        //            }
        //        }
        //    }
        //    catch (SocketException e)
        //    {
        //        Console.WriteLine("SocketException: {0}", e);
        //    }
        //    finally
        //    {
        //        server.Stop();
        //    }

        //    //Console.WriteLine("\nHit enter to continue...");
        //    //Console.Read();
        //}
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