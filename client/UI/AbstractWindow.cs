using System;
using System.Collections.Generic;
using UnityEngine;

// AbstractWindow shamelessly copied from RemoteTech

namespace ksp_ris.UI
{
	public abstract class AbstractWindow
	{
		public Rect Position;
		public String Title { get; set; }
		public String Tooltip { get; set; }
		public bool Enabled = false;
		public static GUIStyle Frame = new GUIStyle(HighLogic.Skin.window);
		public const double TooltipDelay = 0.5;
		private double mLastTime;
		private double mTooltipTimer;
		private readonly Guid mGuid;
		public static Dictionary<Guid, AbstractWindow> Windows = new Dictionary<Guid, AbstractWindow>();
		public static GUIStyle headingStyle, readyBtnStyle, busyBtnStyle, successBtnStyle, failureBtnStyle;
		/// <summary>The initial width of this window</summary>
		public float mInitialWidth;
		/// <summary>The initial height of this window</summary>
		public float mInitialHeight;
		/// <summary>Callback trigger for the change in the position</summary>
		public Action onPositionChanged = delegate { };
		public Rect backupPosition;
		public enum ButtonState { READY, BUSY, SUCCESS, FAILURE };

		public class AsyncButton
		{
			public ButtonState state = ButtonState.READY;
			private string text;
			public Server.CancelDelegate canceller = null;
			public AsyncButton(string text)
			{
			        this.text = text;
			}
			public void AsyncStart(Server.CancelDelegate canceller)
			{
				if (state == ButtonState.BUSY)
					return;
				this.canceller = canceller;
				state = ButtonState.BUSY;
			}
			public void AsyncFinish(bool result)
			{
				state = result ? ButtonState.SUCCESS : ButtonState.FAILURE;
				canceller = null;
			}
			public void Cancel()
			{
				if (canceller != null)
					canceller.Invoke();
			}
			public void Reset()
			{
				state = ButtonState.READY;
			}
			public bool render()
			{
				GUIStyle style = busyBtnStyle;
				switch (state) {
				case ButtonState.READY:
					style = readyBtnStyle;
					break;
				case ButtonState.BUSY:
					style = busyBtnStyle;
					break;
				case ButtonState.SUCCESS:
					style = successBtnStyle;
					break;
				case ButtonState.FAILURE:
					style = failureBtnStyle;
					break;
				}
				return GUILayout.Button(text, style, GUILayout.ExpandWidth(false));
			}
		}

		static AbstractWindow()
		{
			Frame.padding = new RectOffset(5, 5, 5, 5);
			headingStyle = new GUIStyle(HighLogic.Skin.label)
			{
				fontStyle = FontStyle.Bold,
				fontSize = 14,
			};
			readyBtnStyle = new GUIStyle(HighLogic.Skin.button)
			{
				fontStyle = FontStyle.Bold,
				fontSize = 12,
			};
			busyBtnStyle = new GUIStyle(readyBtnStyle);
			busyBtnStyle.normal.textColor = Color.yellow;
			busyBtnStyle.hover.textColor = Color.yellow;
			busyBtnStyle.active.textColor = Color.yellow;
			successBtnStyle = new GUIStyle(readyBtnStyle);
			successBtnStyle.normal.textColor = Color.green;
			successBtnStyle.hover.textColor = Color.green;
			successBtnStyle.active.textColor = Color.green;
			failureBtnStyle = new GUIStyle(readyBtnStyle);
			failureBtnStyle.normal.textColor = Color.red;
			failureBtnStyle.hover.textColor = Color.red;
			failureBtnStyle.active.textColor = Color.red;
		}

		public AbstractWindow(Guid id, String title, Rect position)
		{
			mGuid = id;
			Title = title;
			Position = position;
			backupPosition = position;
			mInitialHeight = position.height + 15;
			mInitialWidth = position.width + 15;

			GameEvents.onHideUI.Add(OnHideUI);
			GameEvents.onShowUI.Add(OnShowUI);
		}

		public Rect RequestPosition() { return Position; }

		public virtual void Show()
		{
			if (Enabled)
				return;

			if (Windows.ContainsKey(mGuid))
			{
				Windows[mGuid].Hide();
			}
			Windows[mGuid] = this;
			Enabled = true;
		}

		private void OnHideUI()
		{
			Enabled = false;
		}

		private void OnShowUI()
		{
			Enabled = true;
		}

		public virtual void Hide()
		{
			removeWindowCtrlLock();
			Windows.Remove(mGuid);
			Enabled = false;
			GameEvents.onHideUI.Remove(OnHideUI);
			GameEvents.onShowUI.Remove(OnShowUI);
		}

		private void WindowPre(int uid)
		{
			try {
				Window(uid);
			} catch (Exception e) {
				Logging.LogException(e);
			}
		}

		public virtual void Window(int uid)
		{
			if (Title != null)
			{
				GUI.DragWindow(new Rect(0, 0, Single.MaxValue, 20));
			}
			Tooltip = GUI.tooltip;
		}

		public virtual void Draw()
		{
			if (!Enabled) return;
			if (Event.current.type == EventType.Layout)
			{
				Position.width = 0;
				Position.height = 0;
			}

			Position = GUILayout.Window(mGuid.GetHashCode(), Position, WindowPre, Title, Title == null ? Frame : HighLogic.Skin.window);
			
			if (Title != null)
			{
				if (GUI.Button(new Rect(Position.x + Position.width - 18, Position.y + 2, 16, 16), ""))
				{
					Hide();
				}
			}
			if (Event.current.type == EventType.Repaint)
			{
				if (Tooltip != "")
				{
					if (mTooltipTimer > TooltipDelay)
					{
						var pop = GUI.skin.box.alignment;
						var width = GUI.skin.box.CalcSize(new GUIContent(Tooltip)).x;
						GUI.skin.box.alignment = TextAnchor.MiddleLeft;
						GUI.Box(new Rect(Position.x, Position.y + Position.height + 10, width, 28), Tooltip);
						GUI.skin.box.alignment = pop;
					}
					else
					{
						mTooltipTimer += Time.time - mLastTime;
						mLastTime = Time.time;
					}
				}
				else
				{
					mTooltipTimer = 0.0;
				}
				mLastTime = Time.time;

				// Position of the window changed?
				if (!backupPosition.Equals(Position))
				{
					// trigger the onPositionChanged callbacks
					onPositionChanged.Invoke();
					backupPosition = Position;
				}

				// Set ship control lock if one ris input is in focus
				if (GUI.GetNameOfFocusedControl().StartsWith("kspris_"))
				{
					setWindowCtrlLock();
				}
				else
				{
					removeWindowCtrlLock();
				}
			}
		}

		/// <summary>
		/// Set a input lock to keep typing to this window
		/// </summary>
		public void setWindowCtrlLock()
		{
			// only if we are enabled and the controllock is not set
			if (Enabled && InputLockManager.GetControlLock("RISLockControlForWindows") == ControlTypes.None)
			{
				InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS, "RISLockControlForWindows");
				InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS, "RISLockControlCamForWindows");
			}
		}


		/// <summary>
		/// Remove the input lock
		/// </summary>
		public void removeWindowCtrlLock()
		{
			// only if the controllock is set
			if (InputLockManager.GetControlLock("KPULockControlForWindows") != ControlTypes.None)
			{
				InputLockManager.RemoveControlLock("KPULockControlForWindows");
				InputLockManager.RemoveControlLock("KPULockControlCamForWindows");
			}
		}

		/// <summary>
		/// Toggle the window
		/// </summary>
		public void toggleWindow()
		{
			if (this.Enabled)
			{
				this.Hide();
			}
			else
			{
				this.Show();
			}
		}
	}
}
