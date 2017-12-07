﻿using UnityEngine;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System;
using Newtonsoft.Json;

public class CompetitiveServer : MonoBehaviour
{
    KMBombInfo bombInfo;
    KMGameCommands gameCommands;
    string modules;
    string solvableModules;
    string solvedModules;
    string bombState;
    public Settings ModSettings;

    Thread workerThread;
    Worker workerObject;
    Queue<Action> actions;

    public class Settings
    {
        public int Port = 8085;
    }

    public class BombInfoResponse
    {
        public string Time;
        public int Strikes;
        public List<string> Modules;
        public List<string> SolvableModules;
        public List<string> SolvedModules;
        public string BombState;

        public static BombInfoResponse GetResponse(KMBombInfo bombInfo, string bombState)
        {
            BombInfoResponse response = new BombInfoResponse();

            response.Time = bombInfo.GetFormattedTime();
            response.Strikes = bombInfo.GetStrikes();
            response.Modules = bombInfo.GetModuleNames();
            response.SolvableModules = bombInfo.GetSolvableModuleNames();
            response.SolvedModules = bombInfo.GetSolvedModuleNames();
            response.BombState = bombState;

            return response;
        }
    }

    void Awake()
    {
        ModSettings = JsonConvert.DeserializeObject<Settings>(GetComponent<KMModSettings>().Settings);
        actions = new Queue<Action>();
        bombInfo = GetComponent<KMBombInfo>();
        bombInfo.OnBombExploded += OnBombExplodes;
        bombInfo.OnBombSolved += OnBombDefused;
        gameCommands = GetComponent<KMGameCommands>();
        GetComponent<KMGameInfo>().OnStateChange += OnGameStateChange;
        bombState = "NA";
        // Create the thread object. This does not start the thread.
        workerObject = new Worker(this);
        workerThread = new Thread(workerObject.DoWork);
        // Start the worker thread.
        workerThread.Start(this);
    }

    protected void OnGameStateChange(KMGameInfo.State state)
    {
        if(state == KMGameInfo.State.Gameplay)
        {
            bombState = "Active";
        }
        else if(bombState == "Active")
        {
            bombState = "NA";
        }
    }

    void Update()
    {
        if (actions.Count > 0)
        {
            Action action = actions.Dequeue();
            action();
        }
    }

    void OnDestroy()
    {
        workerThread.Abort();
        workerObject.Stop();
    }

    // This example requires the System and System.Net namespaces.
    public void SimpleListenerExample(HttpListener listener)
    {
        while (true)
        {
            // Note: The GetContext method blocks while waiting for a request. 
            HttpListenerContext context = listener.GetContext();
            HttpListenerRequest request = context.Request;
            // Obtain a response object.
            HttpListenerResponse response = context.Response;
            response.AddHeader("Access-Control-Allow-Origin", "*");
            // Construct a response.
            string responseString = "";

            if (request.Url.OriginalString.Contains("bombInfo"))
            {
                responseString = GetBombInfo();
            }

            if (request.Url.OriginalString.Contains("startMission"))
            {
                string missionId = request.QueryString.Get("missionId");
                string seed = request.QueryString.Get("seed");
                responseString = StartMission(missionId, seed);
            }

            if (request.Url.OriginalString.Contains("causeStrike"))
            {
                string reason = request.QueryString.Get("reason");
                responseString = CauseStrike(reason);
            }

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            output.Close();
        }
    }

    protected string StartMission(string missionId, string seed)
    {
        actions.Enqueue(delegate () { gameCommands.StartMission(missionId, seed); });

        return missionId + " " + seed;
    }

    protected string CauseStrike(string reason)
    {
        actions.Enqueue(delegate () { gameCommands.CauseStrike(reason); });

        return reason;
    }

    protected string GetBombInfo()
    {
        string responseString = JsonConvert.SerializeObject(BombInfoResponse.GetResponse(bombInfo, bombState));

        return responseString;
    }

    protected void OnBombExplodes()
    {
        bombState = "Exploded";
    }

    protected void OnBombDefused()
    {
        bombState = "Defused";
    }

    protected string GetListAsHTML(List<string> list)
    {
        string listString = "";

        foreach (string s in list)
        {
            listString += s + ", ";
        }

        return listString;
    }

    public class Worker
    {
        CompetitiveServer service;
        HttpListener listener;

        public Worker(CompetitiveServer s)
        {
            service = s;
        }

        // This method will be called when the thread is started. 
        public void DoWork()
        {
            // Create a listener.
            listener = new HttpListener();
            // Add the prefixes.
            foreach (string s in new string[] { "http://*:" + service.ModSettings.Port + "/" })
            {
                listener.Prefixes.Add(s);
            }
            listener.Start();

            service.SimpleListenerExample(listener);
        }

        public void Stop()
        {
            listener.Stop();
        }
    }
}