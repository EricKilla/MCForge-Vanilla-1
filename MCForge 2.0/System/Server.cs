﻿/*
Copyright 2011 MCForge
Dual-licensed under the Educational Community License, Version 2.0 and
the GNU General Public License, Version 3 (the "Licenses"); you may
not use this file except in compliance with the Licenses. You may
obtain a copy of the Licenses at
http://www.opensource.org/licenses/ecl2.php
http://www.gnu.org/licenses/gpl-3.0.html
Unless required by applicable law or agreed to in writing,
software distributed under the Licenses are distributed on an "AS IS"
BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
or implied. See the Licenses for the specific language governing
permissions and limitations under the Licenses.
*/
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Timers;
using System.IO;
using MCForge.Entity;
using MCForge.World;
using MCForge.Interface;
using MCForge.Interface.Command;
using MCForge.API.PlayerEvent;

namespace MCForge.Core
{
    public static class Server
    {
        /// <summary>
        /// Get whether the server is currently shutting down
        /// </summary>
        public static bool shuttingDown;
        /// <summary>
        /// Get whether the server is currently fully started or not
        /// </summary>
        public static bool Started = false;

        private static System.Timers.Timer UpdateTimer;
		private static int HeartbeatInterval = 300;
		private static int HeartbeatIntervalCurrent = 0;
		private static int GroupsaveInterval = 3000;
		private static int GroupsaveIntervalCurrent = 0;

        internal static List<Player> Connections = new List<Player>();
        /// <summary>
        /// Get the current list of online players, note that if your doing a foreach on this always add .ToArray() to the end, it solves a LOT of issues
        /// </summary>
        public static List<Player> Players = new List<Player>();
        /// <summary>
        /// Get the current list of banned ip addresses, note that if your doing a foreach on this (or any other public list) you should always add .ToArray() to the end so that you avoid errors!
        /// </summary>
        public static List<string> BannedIP = new List<string>();
        /// <summary>
        /// The list of MCForge developers.
        /// </summary>
        public static readonly List<string> devs = new List<string>(new string[] { "EricKilla", "Merlin33069", "Snowl", "gamezgalaxy", "Gamemakergm", "cazzar", "hirsty", "givo", "jasonbay13", "Alem_Zupa", "7imekeeper", "Shade2010", "TheMusiKid", "Nerketur" });
        /// <summary>
        /// List of players that agreed to the rules
        /// </summary>
        public static List<string> agreed = new List<string>();

        public static List<string> jokermessages = new List<string>();
        /// <summary>
        /// The main level of the server, where players spawn when they first join
        /// </summary>
        public static Level Mainlevel;
        /// <summary>
        /// Determines if the chat moderation is enabled
        /// </summary>
        public static bool moderation;
        //Voting
        /// <summary>
        /// Is the server in voting mode?
        /// </summary>
        public static bool voting;
        /// <summary>
        /// Is it a kickvote?
        /// </summary>
        public static bool kickvote;
        /// <summary>
        /// Amount of yes votes.
        /// </summary>
        public static int YesVotes;
        /// <summary>
        /// Amount of no votes.
        /// </summary>
        public static int NoVotes;
        /// <summary>
        /// The player who's getting, if it's /votekick
        /// </summary>
        public static Player kicker;

        /// <summary>
        /// The server's default color.
        /// </summary>
        public static string DefaultColor = Colors.yellow;

        /// <summary>
        /// The minecraft.net URL of the server
        /// </summary>
        /// 
        public static string URL = "";

		/// <summary>
		/// This delegate is used when a command or plugin needs to call a method after a certain amount of time
		/// </summary>
		/// <param name="dataPass">This delegate passes the object that was passed to it back to the method that is to be invoked</param>
		/// <returns>this delegate returns an updated object for the datapass</returns>
		public delegate object TimedMethodDelegate(object dataPass);
		static List<TimedMethod> TimedMethodList = new List<TimedMethod>();

        internal static void Init()
        {
			//TODO load the level if it exists
            Mainlevel = Level.CreateLevel(new Point3(256, 256, 64), Level.LevelTypes.Flat);
            UpdateTimer = new System.Timers.Timer(100);
            UpdateTimer.Elapsed += delegate { Update(); };
            UpdateTimer.Start();

            Groups.PlayerGroup.InitDefaultGroups();

			LoadAllDlls.Init();

            Heartbeat.sendHeartbeat();

            CmdReloadCmds reload = new CmdReloadCmds();
            reload.Initialize();

			CreateDirectories();

			StartListening();
			Started = true;
			Log("[Important]: Server Started.", ConsoleColor.Black, ConsoleColor.White);
        }

        static void Update()
        {
			HeartbeatIntervalCurrent++;
			GroupsaveIntervalCurrent++;

            Player.GlobalUpdate();

			if (HeartbeatIntervalCurrent >= HeartbeatInterval) { Heartbeat.sendHeartbeat(); HeartbeatIntervalCurrent = 0; }
			if (GroupsaveIntervalCurrent >= GroupsaveInterval) { foreach (Groups.PlayerGroup g in Groups.PlayerGroup.groups) { g.SaveGroup(); } GroupsaveIntervalCurrent = 0; }

			foreach (TimedMethod TM in TimedMethodList.ToArray())
			{
				TM.time--;
				if (TM.time <= 0)
				{
					TM.PassBack = TM.MethodToInvoke.Invoke(TM.PassBack);
					if (TM.repeat == 0) TimedMethodList.Remove(TM);
					TM.repeat--;
					TM.time = TM.consistentTime;
				}
			}
        }

		static void CreateDirectories()
		{
			if (!Directory.Exists("text")) { Directory.CreateDirectory("text"); Log("Created text directory...", ConsoleColor.White, ConsoleColor.Black); }
			if (!File.Exists("text/badwords.txt")) { File.Create("text/badwords.txt").Close(); Log("[File] Created badwords.txt", ConsoleColor.White, ConsoleColor.Black); }
			if (!File.Exists("text/replacementwords.txt")) { File.Create("text/replacementwords.txt").Close(); Log("[File] Created replacementwords.txt", ConsoleColor.White, ConsoleColor.Black); }
			if (!File.Exists("text/agreed.txt")) { File.Create("text/agreed.txt").Close(); Log("[File] Created agreed.txt", ConsoleColor.White, ConsoleColor.Black); }
			if (!File.Exists("text/hacksmessages.txt")) { File.Create("text/hacksmessages.txt").Close(); Log("[File] Created hacksmessages.txt", ConsoleColor.White, ConsoleColor.Black); }
			if (!File.Exists("text/news.txt")) { File.Create("text/news.txt").Close(); Log("[File] Created news.txt", ConsoleColor.White, ConsoleColor.Black); }
			if (!File.Exists("text/jokermessages.txt"))
			{
				File.Create("text/jokermessages.txt").Close();
				Log("[File] Created jokermessages.txt", ConsoleColor.White, ConsoleColor.Black);
				string text = "I am a pony" + Environment.NewLine + "Rainbow Dash <3" + Environment.NewLine + "I like trains!";
				File.WriteAllText("text/jokermessages.txt", text);
				Log("[File] Added default messages to jokermessages.txt", ConsoleColor.White, ConsoleColor.Black);
			}
			try
			{
				string[] lines = File.ReadAllLines("text/agreed.txt");
				foreach (string pl in lines) { agreed.Add(pl); }
			}
			catch { Log("[Error] Error reading agreed players!", ConsoleColor.Red, ConsoleColor.Black); }
		}

		/// <summary>
		/// Add a method to be called in a specified time for a specified number of repetitions
		/// </summary>
		/// <param name="d">The delegate representing the method to be called</param>
		/// <param name="time">The amount of milliseconds you want to wait to call this method, and to wait inbetween each calling if it repeats (note that this is rounded to 10ths of a second)</param>
		/// <param name="repeat">the number of times to repeat this call</param>
		/// <param name="PassBack">the object to pass back to the method that is called.</param>
		public static void AddTimedMethod(TimedMethodDelegate d, int time, int repeat, object PassBack)
		{
			TimedMethod TMS = new TimedMethod(d, time, repeat, PassBack);
			TimedMethodList.Add(TMS);
		}

        #region Socket Stuff
        private static TcpListener listener;
        private static void StartListening()
        {
        startretry:
            try
            {
                listener = new TcpListener(System.Net.IPAddress.Any, ServerSettings.port);
                listener.Start();
                IAsyncResult ar = listener.BeginAcceptTcpClient(new AsyncCallback(AcceptCallback), listener);
            }
            catch (SocketException E)
            {
                Server.Log(E);
            }
            catch (Exception E)
            {
                Server.Log(E);
                goto startretry;
            }
        }
        private static void AcceptCallback(IAsyncResult ar)
        {
            TcpListener listener2 = (TcpListener)ar.AsyncState;
            try
            {
                TcpClient clientSocket = listener2.EndAcceptTcpClient(ar);
                new Player(clientSocket);
            }
            catch { }
            if (!shuttingDown)
            {
                listener.BeginAcceptTcpClient(new AsyncCallback(AcceptCallback), listener);
            }
        }
        #endregion
        #region Log Stuff
        /// <summary>
        /// Write A message to the Console and the GuiLog using default (white on black) colors.
        /// </summary>
        /// <param name="message">The message to show</param>
        public static void Log(string message)
        {
            Log(message, ConsoleColor.White, ConsoleColor.Black);
        }
        /// <summary>
        /// Write an error to the Console and the GuiLog using Red on black colors
        /// </summary>
        /// <param name="E">The error exception to write.</param>
        public static void Log(Exception E)
        {
            Log("[ERROR]: ", ConsoleColor.Red, ConsoleColor.Black);
            Log(E.Message, ConsoleColor.Red, ConsoleColor.Black);
            Log(E.StackTrace, ConsoleColor.Red, ConsoleColor.Black);
        }
        /// <summary>
        /// Write a message to the console and GuiLog using a specified TextColor and BackGround Color
        /// </summary>
        /// <param name="message">The Message to show</param>
        /// <param name="TextColor">The color of the text to show</param>
        /// <param name="BackgroundColor">The color behind the text.</param>
        public static void Log(string message, ConsoleColor TextColor, ConsoleColor BackgroundColor)
        {
            Console.ForegroundColor = TextColor;
            Console.BackgroundColor = BackgroundColor;
            Console.WriteLine(message.PadRight(Console.WindowWidth - 1));
            Console.ResetColor();
        }
        #endregion

		class TimedMethod
		{
			public TimedMethodDelegate MethodToInvoke;
			public int consistentTime;
			public int time;
			public int repeat;
			public object PassBack;

			public TimedMethod(TimedMethodDelegate a, int b, int c, object d)
			{
				MethodToInvoke = a;
				consistentTime = b / 100;
				time = b / 100;
				repeat = c;
				PassBack = d;
			}
		}
    }
}
