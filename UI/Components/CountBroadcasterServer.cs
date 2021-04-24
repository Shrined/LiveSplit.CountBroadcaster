﻿using Newtonsoft.Json;
using System;
using System.Net;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LiveSplit.Components;

namespace LiveSplit.UI.Components
{
    static class CountBroadcasterServer
    {

        private const int port = 43928;

        private static readonly HttpListener Listener = new HttpListener { Prefixes = { $"http://localhost:{port}/" } };

        private static volatile bool _keepGoing = true;

        private static Task _mainLoop;

        private static CountBroadcaster count;

        public static void StartWebServer(CountBroadcaster countBroadcaster)
        {
            if (_mainLoop != null && !_mainLoop.IsCompleted) return; //Already started
            count = countBroadcaster;
            _mainLoop = MainLoop();
        }

        /// <summary>
        /// Call this to stop the web server. It will not kill any requests currently being processed.
        /// </summary>
        public static void StopWebServer()
        {
            _keepGoing = false;
            lock (Listener)
            {
                //Use a lock so we don't kill a request that's currently being processed
                Listener.Stop();
            }
            try
            {
                _mainLoop.Wait();
            }
            catch { }
        }

        /// <summary>
        /// The main loop to handle requests into the HttpListener
        /// </summary>
        /// <returns></returns>
        private static async Task MainLoop()
        {
            Listener.Start();
            while (_keepGoing)
            {
                try
                {
                    //GetContextAsync() returns when a new request come in
                    var context = await Listener.GetContextAsync();
                    lock (Listener)
                    {
                        if (_keepGoing) ProcessRequest(context);
                    }
                }
                catch (Exception e)
                {
                    if (e is HttpListenerException) return; //this gets thrown when the listener is stopped
                    //TODO: Log the exception
                }
            }
        }

        /// <summary>
        /// Handle an incoming request
        /// </summary>
        /// <param name="context">The context of the incoming request</param>
        private static void ProcessRequest(HttpListenerContext context)
        {
            using (var response = context.Response)
            {
                try
                {
                    var handled = false;
                    switch (context.Request.Url.AbsolutePath)
                    {
                        //This is where we do different things depending on the URL
                        //TODO: Add cases for each URL we want to respond to
                        case "/getTotalAttempts":
                            switch (context.Request.HttpMethod)
                            {
                                case "GET":
                                    //Get the current settings
                                    response.ContentType = "application/json";

                                    //This is what we want to send back
                                    var responseBody = JsonConvert.SerializeObject(count.getNumOfAttemps());

                                    //Write it to the response stream
                                    var buffer = Encoding.UTF8.GetBytes(responseBody);
                                    response.ContentLength64 = buffer.Length;
                                    response.OutputStream.Write(buffer, 0, buffer.Length);
                                    handled = true;
                                    break;
                            }
                            break;
                    }
                    if (!handled)
                    {
                        response.StatusCode = 404;
                    }
                }
                catch (Exception e)
                {
                    //Return the exception details the client - you may or may not want to do this
                    response.StatusCode = 500;
                    response.ContentType = "application/json";
                    var buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(e));
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);

                    //TODO: Log the exception
                }
            }
        }

    }
}