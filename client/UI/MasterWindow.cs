using System;
using System.Collections.Generic;
using UnityEngine;

namespace ksp_ris.UI
{
	public class MasterWindow : AbstractWindow
	{
	        private ksp_ris.Server server;
	        AsyncButton listBtn, joinBtn, leaveBtn, refreshBtn;
	        Vector2 listScroll, plistScroll, ptableScroll;
	        string selectedGame;
	        string yourName = "";

		public MasterWindow(ksp_ris.Server s) : base(new Guid("2104b836-35ce-403d-926d-b0e0e0b98d1a"),
							     "Race Into Space",
							     new Rect(100, 100, 515, 320))
		{
		        server = s;
		        listBtn = new AsyncButton("List Games");
		        joinBtn = new AsyncButton("Join Game");
		        leaveBtn = new AsyncButton("Leave Game");
		        refreshBtn = new AsyncButton("Refresh");
			listScroll = new Vector2();
		        plistScroll = new Vector2();
		}

		private void SelectServer()
		{
			GUILayout.BeginHorizontal();
			try {
				GUILayout.Label("Server:", headingStyle);
				server.host = GUILayout.TextField(server.host, GUILayout.Width(200));
				string serverPort = GUILayout.TextField(server.port.ToString(), GUILayout.Width(80));
				UInt16.TryParse(serverPort, out server.port);
			} finally {
				GUILayout.EndHorizontal();
			}
		}

		private void GameList()
		{
			if (server.gameList == null)
				return;
			foreach (KeyValuePair<string,GameListEntry> kvp in server.gameList) {
				GUIStyle keyStyle = new GUIStyle(headingStyle), valueStyle = new GUIStyle(HighLogic.Skin.label);
				if (kvp.Key == selectedGame) {
					keyStyle.fontStyle = FontStyle.BoldAndItalic;
					valueStyle.fontStyle = FontStyle.Italic;
				}
				GUILayout.BeginHorizontal(new GUIStyle(HighLogic.Skin.box));
				try {
					if (GUILayout.Button(kvp.Key, keyStyle, GUILayout.Width(320)))
						selectedGame = kvp.Key;
					if (GUILayout.Button(kvp.Value.mindate.ToString(), valueStyle))
						selectedGame = kvp.Key;
				} finally {
					GUILayout.EndHorizontal();
				}
		        }
		}

		private void PlayerList()
		{
			GUILayout.Label("Players in game " + selectedGame, headingStyle);
		        GameListEntry gle = server.gameList[selectedGame];
		        foreach (string player in gle.players) {
		                GUILayout.Label(player);
		        }
		}

		private void JoinButton()
		{
		        GUILayout.Label("Your name: ", headingStyle);
		        yourName = GUILayout.TextField(yourName, GUILayout.Width(160));
			if (joinBtn.render()) {
				switch (joinBtn.state) {
				case ButtonState.READY:
				case ButtonState.FAILURE:
					if (refreshBtn.state == ButtonState.BUSY)
						refreshBtn.Cancel();
					else
						refreshBtn.Reset();
					joinBtn.AsyncStart(server.JoinGame(selectedGame, yourName, joinBtn.AsyncFinish));
					break;
				case ButtonState.BUSY:
					Logging.Log("Cancelling JoinGame");
					joinBtn.Cancel();
				        break;
				case ButtonState.SUCCESS:
					Logging.LogWarningFormat("Join button should not be visible in SUCCESS state");
					break;
				default:
					Logging.LogFormat("Discarding old joinBtn state {0}", joinBtn.state);
					joinBtn.Reset();
					break;
				}
			}
		}

		private void SelectGame()
		{
			GUILayout.Label("Not in a game; join one.", headingStyle);
			if (listBtn.render()) {
				switch (listBtn.state) {
				case ButtonState.READY:
				case ButtonState.SUCCESS:
				case ButtonState.FAILURE:
					selectedGame = null;
					listBtn.AsyncStart(server.ListGames(listBtn.AsyncFinish));
					break;
				case ButtonState.BUSY:
					Logging.Log("Cancelling ListGames");
					listBtn.Cancel();
				        break;
				default:
					Logging.LogFormat("Discarding old listBtn state {0}", listBtn.state);
					listBtn.Reset();
					break;
				}
			}
			listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Width(495), GUILayout.Height(160));
			try {
				GameList();
			} finally {
				GUILayout.EndScrollView();
			}
			if (server.gameList != null && selectedGame != null && server.gameList.ContainsKey(selectedGame)) {
				plistScroll = GUILayout.BeginScrollView(plistScroll, GUILayout.Width(495), GUILayout.Height(160));
				try {
					PlayerList();
				} finally {
					GUILayout.EndScrollView();
				}
				GUILayout.BeginHorizontal();
				try {
					JoinButton();
				} finally {
					GUILayout.EndHorizontal();
				}
			}
		}

		private void RefreshButton()
		{
			if (refreshBtn.render()) {
				switch (refreshBtn.state) {
				case ButtonState.READY:
				case ButtonState.SUCCESS:
				case ButtonState.FAILURE:
					refreshBtn.AsyncStart(server.ReadGame(refreshBtn.AsyncFinish));
					break;
				case ButtonState.BUSY:
					Logging.Log("Cancelling ReadGame");
					refreshBtn.Cancel();
				        break;
				default:
					Logging.LogFormat("Discarding old refreshBtn state {0}", refreshBtn.state);
					refreshBtn.Reset();
					break;
				}
			}
		}

		private void PlayerTable()
		{
		        foreach (KeyValuePair<string,Player> kvp in server.game.players) {
		                GUILayout.BeginHorizontal();
		                try {
		                        GUILayout.Label(kvp.Key, GUILayout.Width(160));
		                        GUILayout.Label(kvp.Value.date.ToString(), GUILayout.Width(80));
		                        if (kvp.Value.leader)
		                                GUILayout.Label("leader");
		                } finally {
		                        GUILayout.EndHorizontal();
		                }
		        }
		}

		private void ShowGame()
		{
			if (server.ourName == null) {
				/* can't happen */
				server.inGame = null;
				GUILayout.Label("We have no name!  So, we aren't in the game.", headingStyle);
				return;
			}
		        if (server.game == null) {
		                if (refreshBtn.state != ButtonState.BUSY && refreshBtn.state != ButtonState.FAILURE)
					refreshBtn.AsyncStart(server.ReadGame(refreshBtn.AsyncFinish));
				GUILayout.Label("Connecting to server...", headingStyle);
				RefreshButton();
				return;
		        }
			GUILayout.Label(String.Format("In game {0} as player {1}", server.game, server.ourName), headingStyle);
			GUILayout.Label(String.Format("Min. Date: {0}", server.game.mindate.ToString()));
			RefreshButton();
			GUILayout.Label("Players:", headingStyle);
			ptableScroll = GUILayout.BeginScrollView(ptableScroll, GUILayout.Width(495), GUILayout.Height(160));
			try {
				PlayerTable();
			} finally {
				GUILayout.EndScrollView();
			}
		}

		public override void Window(int id)
		{
			GUILayout.BeginVertical(GUILayout.Width(495));
			try {
				SelectServer();
				if (server.inGame == null)
					SelectGame();
				else
					ShowGame();
			} finally {
				GUILayout.EndVertical();
				base.Window(id);
			}
		}
	}
}

