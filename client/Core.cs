using System;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using KSP.UI.Screens;

namespace ksp_ris
{
	public class YDate : IComparable
	{
		public int year;
		public int day;

		public YDate(double UT)
		{
			int days = (int)(UT / 86400);
			year = 1 + days / 365;
			day = 1 + days % 365;
		}

		public YDate(Hashtable ht)
		{
			this.year = (int)(double)ht["year"];
		        this.day = (int)(double)ht["day"];
		}

		public YDate(ConfigNode node)
		{
			year = int.Parse(node.GetValue("year"));
			day = int.Parse(node.GetValue("day"));
		}

		public int CompareTo(YDate other)
		{
			if (other.year != year)
				return year.CompareTo(other.year);
			return day.CompareTo(other.day);
		}

		public int CompareTo(object obj)
		{
			if (obj == null)
				return 1;
			YDate other = obj as YDate;
			if (other == null)
				throw new ArgumentException("Object is not a YDate");
			return CompareTo(other);
		}

		public override string ToString()
		{
		        return String.Format("y{0:D}d{1:D3}", year, day);
		}
	}

	public class GameListEntry
	{
		public List<string> players;
		public YDate mindate;

		public GameListEntry(Hashtable ht)
		{
			mindate = new YDate(ht["mindate"] as Hashtable);
		        players = new List<string>();
		        foreach (object obj in ht["players"] as ArrayList) {
		                players.Add(obj as string);
		        }
		}
	}

	public class Player
	{
		public YDate date;
		public bool leader;
		public Player(Hashtable ht)
		{
			date = new YDate(ht["date"] as Hashtable);
			leader = (bool)ht["leader"];
	        }
	}

	public class Game
	{
	        public YDate mindate;
	        public Dictionary<string, Player> players;
	        public Game(Hashtable ht)
	        {
	                mindate = new YDate(ht["mindate"] as Hashtable);
	                players = new Dictionary<string, Player>();
	                foreach (DictionaryEntry de in ht["players"] as Hashtable) {
	                        players.Add(de.Key.ToString(), new Player(de.Value as Hashtable));
	                }
	        }
	}

	public class Result
	{
		public YDate date;
		public enum First { FIRST, WAS_LEADER, UNKNOWN, NOT_FIRST };
		public First first;
		public Result(Hashtable ht, string ourName)
		{
			if (ht.ContainsKey(ourName)) {
				Hashtable data = ht[ourName] as Hashtable;
				date = new YDate(data["date"] as Hashtable);
				switch (data["first"] as string) {
				case "first":
					first = First.FIRST;
					break;
				case "was_leader":
					first = First.WAS_LEADER;
					break;
				case "unknown":
					first = First.UNKNOWN;
					break;
				case "not_first":
					first = First.NOT_FIRST;
					break;
				default:
					throw new Exception(String.Format("Bad 'first' value {0}", data["first"] as string));
				}
			} else {
				date = null;
				first = First.UNKNOWN;
			}
		}
	}

	public class Server
	{
		public Dictionary<string,GameListEntry> gameList = null;
		public delegate void CancelDelegate();
		public delegate void ResultCallback(bool ok);
		public ContractMonitor monitor;

		public string inGame = null;
		public string ourName = null;
		public Game game = null;
		/* Error codes used in RIS protocol */
		private const int ENOENT = 2, EEXIST = 17, EINVAL = 22;
		public string host = "127.0.0.1";
		public UInt16 port = 8080;
		private Uri server { get { return new UriBuilder("http", host, port).Uri; } }
		private Uri Page(string path)
		{
			UriBuilder ub = new UriBuilder(server);
			ub.Path = path;
			ub.Query = "json=1";
			return ub.Uri;
		}
		private Uri Page(string path, string query_format, params object[] args)
		{
			UriBuilder ub = new UriBuilder(server);
			ub.Path = path;
			ub.Query = "json=1&" + String.Format(query_format, args);
			return ub.Uri;
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("host", host);
			node.AddValue("port", port);
			if (inGame != null)
				node.AddValue("game", inGame);
			if (ourName != null)
				node.AddValue("player", ourName);
		}

		public void Load(ConfigNode node)
		{
			if (node.HasValue("host"))
				host = node.GetValue("host");
			if (node.HasValue("port"))
				UInt16.TryParse(node.GetValue("port"), out port);
			if (node.HasValue("game"))
				inGame = node.GetValue("game");
			if (node.HasValue("player"))
				ourName = node.GetValue("player");
		}

		private class Error : Exception
		{
			string msg;
			int code;
			public Error(string msg, int code)
			{
				this.msg = msg;
				this.code = code;
			}
			public Error(string msg) : this(msg, 0)
			{
			}
			public override string Message { get {
				return String.Format("Error {0:D}: {1}", code, msg);
			}}
		}

		private void checkError(Hashtable ht)
		{
			if (!ht.Contains("err"))
				return;
			if (ht.Contains("code"))
				throw new Error(ht["err"] as string, (int)ht["code"]);
			throw new Error(ht["err"] as string);
		}

		public CancelDelegate ListGames(ResultCallback cb)
		{
			KerbalMonitor.CountDeadKerbals();
			WebClient client = new WebClient();
			client.DownloadStringCompleted += (object sender, DownloadStringCompletedEventArgs e) => {
				bool result = false;
				try {
					if (e.Cancelled) {
						Logging.Log("ListGames cancelled");
					} else if (e.Error != null) {
						Logging.LogException(e.Error);
					} else {
						string json = e.Result;
						Logging.Log("ListGames: " + json);
						Hashtable ht = MiniJSON.jsonDecode(json) as Hashtable;
						checkError(ht);
						gameList = new Dictionary<string, GameListEntry>();
						foreach (DictionaryEntry de in ht) {
							gameList.Add(de.Key.ToString(), new GameListEntry(de.Value as Hashtable));
						}
						Logging.LogFormat("Listed {0} games", gameList.Count);
						result = true;
					}
				} catch (Exception exc) {
					/* Job failed, but we still have to exit job state */
					Logging.LogException(exc);
				}
				cb.Invoke(result);
			};
			client.DownloadStringAsync(Page("/"));
			return client.CancelAsync;
		}

		public CancelDelegate JoinGame(string game, string name, ResultCallback cb)
		{
			WebClient client = new WebClient();
			client.DownloadStringCompleted += (object sender, DownloadStringCompletedEventArgs e) => {
				bool result = false;
				try {
					if (e.Cancelled) {
						Logging.Log("JoinGame cancelled");
					} else if (e.Error != null) {
						Logging.LogException(e.Error);
					} else {
						string json = e.Result;
						Logging.Log("JoinGame: " + json);
						Hashtable ht = MiniJSON.jsonDecode(json) as Hashtable;
						checkError(ht);
						inGame = game;
						ourName = name;
						this.game = new Game(ht);
						result = true;
					}
				} catch (Exception exc) {
					/* Job failed, but we still have to exit job state */
					Logging.LogException(exc);
				}
				cb.Invoke(result);
			};
			client.DownloadStringAsync(Page("/join", "game={0}&name={1}", game, name));
			return client.CancelAsync;
		}

		public CancelDelegate PartGame(ResultCallback cb)
		{
			WebClient client = new WebClient();
			client.DownloadStringCompleted += (object sender, DownloadStringCompletedEventArgs e) => {
				bool result = false;
				try {
					if (e.Cancelled) {
						Logging.Log("PartGame cancelled");
					} else if (e.Error != null) {
						Logging.LogException(e.Error);
					} else {
						string json = e.Result;
						Logging.Log("PartGame: " + json);
						Hashtable ht = MiniJSON.jsonDecode(json) as Hashtable;
						if (ht.Contains("err")) {
							if (ht.Contains("code") && (int)ht["code"] == ENOENT)
								Logging.Log("Player was already parted");
							else /* real error, let's throw */
								checkError(ht);
						}
						inGame = null;
						ourName = null;
						this.game = null;
						result = true;
					}
				} catch (Exception exc) {
					/* Job failed, but we still have to exit job state */
					Logging.LogException(exc);
				}
				cb.Invoke(result);
			};
			client.DownloadStringAsync(Page("/part", "game={0}&name={1}", inGame, ourName));
			return client.CancelAsync;
		}

		public CancelDelegate ReadGame(ResultCallback cb)
		{
			WebClient client = new WebClient();
			client.DownloadStringCompleted += (object sender, DownloadStringCompletedEventArgs e) => {
				bool result = false;
				try {
					if (e.Cancelled) {
						Logging.Log("ReadGame cancelled");
					} else if (e.Error != null) {
						Logging.LogException(e.Error);
					} else {
						string json = e.Result;
						Logging.Log("ReadGame: " + json);
						Hashtable ht = MiniJSON.jsonDecode(json) as Hashtable;
						checkError(ht);
						game = new Game(ht);
						result = true;
					}
				} catch (Exception exc) {
					/* Job failed, but we still have to exit job state */
					Logging.LogException(exc);
				}
				cb.Invoke(result);
			};
			client.DownloadStringAsync(Page("/game", "name={0}", inGame));
			return client.CancelAsync;
		}

		public CancelDelegate Sync(ResultCallback cb)
		{
			RISMilestone stone = monitor.CheckAll();
			CancelDelegate cd = () => {};
			if (stone != null) {
				cd += Report(stone, (bool ok) => {
					if (ok)
						cd += Sync(cb);
					else
						cb.Invoke(false);
				});
				return cd;
			}
			cd += SyncTail((bool ok) => {
				if (ok) {
					List<RISMilestone> toResolve = monitor.ToResolve();
					if (toResolve.Count > 0)
						cd += Resolve(toResolve, cb);
					else
						cb.Invoke(true);
				} else {
					cb.Invoke(false);
				}
			});
			return cd;
		}
		public CancelDelegate SyncTail(ResultCallback cb)
		{
			WebClient client = new WebClient();
			client.DownloadStringCompleted += (object sender, DownloadStringCompletedEventArgs e) => {
				bool result = false;
				try {
					if (e.Cancelled) {
						Logging.Log("Sync cancelled");
					} else if (e.Error != null) {
						Logging.LogException(e.Error);
					} else {
						string json = e.Result;
						Logging.Log("Sync: " + json);
						Hashtable ht = MiniJSON.jsonDecode(json) as Hashtable;
						checkError(ht);
						game = new Game(ht);
						result = true;
					}
				} catch (Exception exc) {
					/* Job failed, but we still have to exit job state */
					Logging.LogException(exc);
				}
				cb.Invoke(result);
			};
			YDate date = new YDate(Planetarium.GetUniversalTime());
			int kia = KerbalMonitor.CountDeadKerbals();
			client.DownloadStringAsync(Page("/sync", "game={0}&player={1}&year={2:D}&day={3:D}&kia={4:D}", inGame, ourName, date.year, date.day, kia));
			return client.CancelAsync;
		}

		public CancelDelegate Report(RISMilestone stone, ResultCallback cb)
		{
			WebClient client = new WebClient();
			Logging.LogFormat("Reporting {0} completed at {1}", stone.name, stone.completed.ToString());
			client.DownloadStringCompleted += (object sender, DownloadStringCompletedEventArgs e) => {
				bool result = false;
				try {
					if (e.Cancelled) {
						Logging.Log("Report(Sync) cancelled");
					} else if (e.Error != null) {
						Logging.LogException(e.Error);
					} else {
						string json = e.Result;
						Logging.Log("Report: " + json);
						Hashtable ht = MiniJSON.jsonDecode(json) as Hashtable;
						checkError(ht);
						Result r = new Result(ht, ourName);
						if (r.date != null)
							stone.reported = true;
						stone.Resolve(r.first);
						result = true;
					}
				} catch (Exception exc) {
					/* Job failed, but we still have to exit job state */
					Logging.LogException(exc);
				}
				cb.Invoke(result);
			};
			client.DownloadStringAsync(Page("/completed", "game={0}&player={1}&year={2:D}&day={3:D}&contract={4}",
							inGame, ourName, stone.completed.year, stone.completed.day, stone.name));
			return client.CancelAsync;
		}

		public CancelDelegate Resolve(List<RISMilestone> stones, ResultCallback cb)
		{
			WebClient client = new WebClient();
			CancelDelegate cd = () => {};
			RISMilestone stone = stones[0];
			stones.RemoveAt(0);
			Logging.LogFormat("Resolving {0}", stone.name);
			client.DownloadStringCompleted += (object sender, DownloadStringCompletedEventArgs e) => {
				bool result = false;
				try {
					if (e.Cancelled) {
						Logging.Log("Resolve(Sync) cancelled");
					} else if (e.Error != null) {
						Logging.LogException(e.Error);
					} else {
						string json = e.Result;
						Logging.Log("Resolve: " + json);
						Hashtable ht = MiniJSON.jsonDecode(json) as Hashtable;
						checkError(ht);
						Result r = new Result(ht, ourName);
						stone.Resolve(r.first);
						result = true;
					}
				} catch (Exception exc) {
					/* Job failed, but we still have to exit job state */
					Logging.LogException(exc);
				}
				if (result && stones.Count > 0) {
					cd += Resolve(stones, cb);
					return;
				}
				cb.Invoke(result);
			};
			client.DownloadStringAsync(Page("/result", "game={0}&contract={1}", inGame, stone.name));
			cd += client.CancelAsync;
			return cd;
		}
	}

	public class RISMilestone
	{
	        public string name, ccName;
	        public double rewardFunds;
	        public YDate completed = null;
	        public bool reported = false;
	        public Result.First result = Result.First.UNKNOWN;
	        public RISMilestone(ConfigNode cn)
	        {
	                name = cn.GetValue("name");
	                ccName = cn.GetValue("ccName");
	                rewardFunds = double.Parse(cn.GetValue("rewardFunds"));
	        }
	        public void Resolve(Result.First r)
	        {
			if (r == Result.First.UNKNOWN)
				return;
			result = r;
			if (r != Result.First.FIRST)
				return;
			if (Funding.Instance == null) {
				Logging.Log("No Funding.Instance!  Are we not in a career?");
				return;
			}
			Funding.Instance.AddFunds(rewardFunds, TransactionReasons.ContractReward);
			string msg = String.Format("Awarded {0} funds for being first to complete {1}", rewardFunds, name);
			Logging.Log(msg);
			MessageSystem.Instance.AddMessage(new MessageSystem.Message(
						"Race Into Space", msg,
						MessageSystemButton.MessageButtonColor.GREEN,
						MessageSystemButton.ButtonIcons.ACHIEVE));
	        }
	        public void Save(ConfigNode node)
	        {
	                node.AddValue("year", completed.year);
	                node.AddValue("day", completed.day);
	                node.AddValue("reported", reported);
	                node.AddValue("result", (int)result);
	        }
	        private void Reset()
	        {
	                completed = null;
	                reported = false;
	                result = Result.First.UNKNOWN;
	        }
	        public void Load(ConfigNode node)
	        {
			if (node == null) {
				Reset();
				return;
			}
			if (node.HasValue("year") && node.HasValue("day")) {
				completed = new YDate(node);
			}
			if (node.HasValue("reported"))
				reported = bool.Parse(node.GetValue("reported"));
			if (node.HasValue("result"))
				result = (Result.First)int.Parse(node.GetValue("result"));
	        }
	}
	public abstract class KerbalMonitor
	{
		public static int CountDeadKerbals()
		{
			return HighLogic.CurrentGame.CrewRoster.GetKIACrewCount();
		}
	}
	public class ContractMonitor
	{
	        public List <RISMilestone> stones = new List<RISMilestone>();
	        public ContractMonitor()
	        {
			foreach (ConfigNode cn in GameDatabase.Instance.GetConfigNodes("RISMilestone")) {
				RISMilestone stone = new RISMilestone(cn);
				stones.Add(stone);
			}
	        }
		public RISMilestone CheckAll()
		{
			foreach (RISMilestone stone in stones) {
				if (stone.completed == null)
					CheckCompletion(stone);
				if (stone.completed != null && !stone.reported)
					return stone;
			}
			return null;
		}
		public List<RISMilestone> ToResolve()
		{
			List<RISMilestone> list = new List<RISMilestone>();
			foreach (RISMilestone stone in stones) {
				if (stone.completed != null && stone.result == Result.First.UNKNOWN)
					list.Add(stone);
			}
			return list;
		}
		public void CheckCompletion(RISMilestone stone)
		{
			ContractConfigurator.ContractType type = ContractConfigurator.ContractType.GetContractType(stone.ccName);
			if (type == null) {
				Logging.LogWarningFormat("Failed to get CC Type {0} for Milestone {1}", stone.ccName, stone.name);
				return;
			}
			if (type.ActualCompletions() > 0) {
				stone.completed = new YDate(Planetarium.GetUniversalTime());
			}
		}
		public void Save(ConfigNode node)
		{
			foreach (RISMilestone stone in stones) {
				if (stone.completed != null) {
					ConfigNode sn = node.AddNode(stone.name);
					stone.Save(sn);
				}
			}
		}
		public void Load(ConfigNode node)
		{
			foreach (RISMilestone stone in stones) {
				stone.Load(node.GetNode(stone.name));
			}
		}
	}

	[KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
	public class RISCore : MonoBehaviour
	{
		public static RISCore Instance { get; protected set; }
		public ContractMonitor monitor = new ContractMonitor();
		public Server server = new Server();
		private ApplicationLauncherButton button;
		private UI.MasterWindow masterWindow;

		public void Start()
		{
		        if (Instance != null) {
				Destroy(this);
				return;
			}

			Instance = this;
			masterWindow = new ksp_ris.UI.MasterWindow(server);
			if (ScenarioRIS.Instance != null)
				Load(ScenarioRIS.Instance.node);
			server.monitor = monitor;
			Logging.Log("RISCore loaded successfully.");
		}

		protected void Awake()
		{
			try {
				GameEvents.onGUIApplicationLauncherReady.Add(this.OnGuiAppLauncherReady);
			} catch (Exception ex) {
				Logging.LogException(ex);
			}
		}

		public void OnGUI()
		{
			GUI.depth = 0;

			Action windows = delegate { };
			foreach (var window in UI.AbstractWindow.Windows.Values)
				windows += window.Draw;
			windows.Invoke();
		}

		private void OnGuiAppLauncherReady()
		{
			if (HighLogic.CurrentGame.Mode != global::Game.Modes.CAREER)
				return;
			try {
				button = ApplicationLauncher.Instance.AddModApplication(
					masterWindow.Show,
					HideGUI,
					null,
					null,
					null,
					null,
					ApplicationLauncher.AppScenes.ALWAYS,
					GameDatabase.Instance.GetTexture("RIS/Textures/toolbar_icon", false));
				GameEvents.onGameSceneLoadRequested.Add(this.OnSceneChange);
			} catch (Exception ex) {
				Logging.LogException(ex);
			}
		}

		private void HideGUI()
		{
			masterWindow.Hide();
		}

		private void OnSceneChange(GameScenes s)
		{
			if (s != GameScenes.FLIGHT)
				HideGUI();
		}

		public void OnDestroy()
		{
			Instance = null;
			try {
				GameEvents.onGUIApplicationLauncherReady.Remove(this.OnGuiAppLauncherReady);
				if (button != null)
					ApplicationLauncher.Instance.RemoveModApplication(button);
			} catch (Exception ex) {
				Logging.LogException(ex);
			}
		}

		public void Save(ConfigNode node)
		{
			ConfigNode sn = node.AddNode("server");
			server.Save(sn);
			ConfigNode mn = node.AddNode("monitor");
			monitor.Save(mn);
		}

		public void Load(ConfigNode node)
		{
			if (node.HasNode("server"))
				server.Load(node.GetNode("server"));
			if (node.HasNode("monitor"))
				monitor.Load(node.GetNode("monitor"));
		}
	}

	[KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.FLIGHT, GameScenes.EDITOR, GameScenes.SPACECENTER, GameScenes.TRACKSTATION)]
	public class ScenarioRIS : ScenarioModule
	{
		public static ScenarioRIS Instance {get; protected set; }
		public ConfigNode node;

		public override void OnAwake()
		{
			Instance = this;
			base.OnAwake();
		}

		public override void OnSave(ConfigNode node)
		{
			RISCore.Instance.Save(node);
		}

		public override void OnLoad(ConfigNode node)
		{
			this.node = node;
			if (RISCore.Instance != null)
				RISCore.Instance.Load(node);
		}
	}
}
