using System;
using System.Collections.Generic;
using UnityEngine;

namespace ksp_ris.UI
{
	public class MasterWindow : AbstractWindow
	{
	        private ksp_ris.Server server;
	        AsyncButton listBtn;
	        Vector2 listScroll;
	        Vector2 plistScroll;
	        string selectedGame;

		public MasterWindow(ksp_ris.Server s) : base(new Guid("2104b836-35ce-403d-926d-b0e0e0b98d1a"),
							     "Race Into Space",
							     new Rect(100, 100, 515, 320))
		{
		        server = s;
		        listBtn = new AsyncButton("List Games");
			listScroll = new Vector2();
		        plistScroll = new Vector2();
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

		public override void Window(int id)
		{
		        GUILayout.BeginVertical();
		        try {
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
				}
			} finally {
				GUILayout.EndVertical();
				base.Window(id);
			}
		}
	}
}

