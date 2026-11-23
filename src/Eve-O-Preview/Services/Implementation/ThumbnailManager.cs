using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services.Interop;
using EveOPreview.UI.Hotkeys;
using EveOPreview.View;
using MediatR;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;

namespace EveOPreview.Services
{
	sealed class ThumbnailManager : IThumbnailManager
	{
		#region Private constants
		private const int WINDOW_POSITION_THRESHOLD_LOW = -10_000;
		private const int WINDOW_POSITION_THRESHOLD_HIGH = 31_000;
		private const int WINDOW_SIZE_THRESHOLD = 10;
		private const int FORCED_REFRESH_CYCLE_THRESHOLD = 2;
		private const int DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY = 2;

		private const string DEFAULT_CLIENT_TITLE = "EVE";
		#endregion

		#region Private fields
		private readonly IMediator _mediator;
		private readonly IProcessMonitor _processMonitor;
		private readonly IWindowManager _windowManager;
		private readonly IGlobalMouseHook _mouseHook;
		private readonly IGlobalKeyboardHook _keyboardHook;
		private readonly IGamepadHook _gamepadHook;
		private readonly IClientWindowHook _clientWindowHook;
		private readonly IThumbnailConfiguration _configuration;
		private readonly DispatcherTimer _thumbnailUpdateTimer;
		private readonly IThumbnailViewFactory _thumbnailViewFactory;
		private readonly Dictionary<IntPtr, IThumbnailView> _thumbnailViews;

		private (IntPtr Handle, string Title) _activeClient;
		private IntPtr _externalApplication;

		private readonly object _locationChangeNotificationSyncRoot;
		private (IntPtr Handle, string Title, string ActiveClient, Point Location, int Delay) _enqueuedLocationChangeNotification;

		private bool _ignoreViewEvents;
		private bool _isHoverEffectActive;

		private int _refreshCycleCount;
		private int _hideThumbnailsDelay;

		private List<HotkeyHandler> _cycleClientHotkeyHandlers = new List<HotkeyHandler>();

		// While a binding capture dialog is open, global mouse / gamepad button
		// events must not trigger client switching.
		private bool _bindingCaptureActive;

		// EVE repositions its own windows shortly after launch, overriding the
		// layout we restore at startup. We re-apply the saved layout a few times
		// over the first seconds so it wins once EVE has settled.
		private DispatcherTimer _layoutReapplyTimer;
		private int _layoutReapplyTicksRemaining;
		private const int LAYOUT_REAPPLY_TICKS = 6;
		#endregion

		public ThumbnailManager(IMediator mediator, IThumbnailConfiguration configuration, IProcessMonitor processMonitor, IWindowManager windowManager, IThumbnailViewFactory factory, IGlobalMouseHook mouseHook, IGlobalKeyboardHook keyboardHook, IGamepadHook gamepadHook, IClientWindowHook clientWindowHook)
		{
			this._mediator = mediator;
			this._processMonitor = processMonitor;
			this._windowManager = windowManager;
			this._mouseHook = mouseHook;
			this._keyboardHook = keyboardHook;
			this._gamepadHook = gamepadHook;
			this._clientWindowHook = clientWindowHook;
			this._configuration = configuration;
			this._thumbnailViewFactory = factory;

			this._mouseHook.MouseButtonPressed += this.GlobalMouseButtonPressed;
			this._keyboardHook.KeyComboPressed += this.GlobalKeyComboPressed;
			this._gamepadHook.GamepadButtonPressed += this.GamepadButtonPressed;
			this._clientWindowHook.WindowMoveResizeEnded += this.ClientWindowMoveResizeEnded;

			this._activeClient = (IntPtr.Zero, ThumbnailManager.DEFAULT_CLIENT_TITLE);

			this.EnableViewEvents();
			this._isHoverEffectActive = false;

			this._refreshCycleCount = 0;
			this._locationChangeNotificationSyncRoot = new object();
			this._enqueuedLocationChangeNotification = (IntPtr.Zero, null, null, Point.Empty, -1);

			this._thumbnailViews = new Dictionary<IntPtr, IThumbnailView>();

			//  DispatcherTimer setup
			this._thumbnailUpdateTimer = new DispatcherTimer();
			this._thumbnailUpdateTimer.Tick += ThumbnailUpdateTimerTick;
			this._thumbnailUpdateTimer.Interval = new TimeSpan(0, 0, 0, 0, configuration.ThumbnailRefreshPeriod);

			this._hideThumbnailsDelay = this._configuration.HideThumbnailsDelay;

			RegisterCycleClientHotkey(this._configuration.CycleGroup1ForwardHotkeys?.Select(x => this._configuration.StringToKey(x)), true, this._configuration.CycleGroup1ClientsOrder);
			RegisterCycleClientHotkey(this._configuration.CycleGroup1BackwardHotkeys?.Select(x => this._configuration.StringToKey(x)), false, this._configuration.CycleGroup1ClientsOrder);

			RegisterCycleClientHotkey(this._configuration.CycleGroup2ForwardHotkeys?.Select(x => this._configuration.StringToKey(x)), true, this._configuration.CycleGroup2ClientsOrder);
			RegisterCycleClientHotkey(this._configuration.CycleGroup2BackwardHotkeys?.Select(x => this._configuration.StringToKey(x)), false, this._configuration.CycleGroup2ClientsOrder);

			RegisterCycleClientHotkey(this._configuration.CycleGroup3ForwardHotkeys?.Select(x => this._configuration.StringToKey(x)), true, this._configuration.CycleGroup3ClientsOrder);
			RegisterCycleClientHotkey(this._configuration.CycleGroup3BackwardHotkeys?.Select(x => this._configuration.StringToKey(x)), false, this._configuration.CycleGroup3ClientsOrder);

			RegisterCycleClientHotkey(this._configuration.CycleGroup4ForwardHotkeys?.Select(x => this._configuration.StringToKey(x)), true, this._configuration.CycleGroup4ClientsOrder);
			RegisterCycleClientHotkey(this._configuration.CycleGroup4BackwardHotkeys?.Select(x => this._configuration.StringToKey(x)), false, this._configuration.CycleGroup4ClientsOrder);

			RegisterCycleClientHotkey(this._configuration.CycleGroup5ForwardHotkeys?.Select(x => this._configuration.StringToKey(x)), true, this._configuration.CycleGroup5ClientsOrder);
			RegisterCycleClientHotkey(this._configuration.CycleGroup5BackwardHotkeys?.Select(x => this._configuration.StringToKey(x)), false, this._configuration.CycleGroup5ClientsOrder);

			RegisterMinimizeAllClientsHotkey(this._configuration.MinimizeAllClientsHotkeys?.Select(x => this._configuration.StringToKey(x)));
		}

		public IThumbnailView GetClientByTitle(string title)
		{
			return _thumbnailViews.FirstOrDefault(x => x.Value.Title == title).Value;
		}

		public IThumbnailView GetClientByPointer(IntPtr ptr)
		{
			return _thumbnailViews.FirstOrDefault(x => x.Key == ptr).Value;
		}

		public IThumbnailView GetActiveClient()
		{
			return GetClientByPointer(this._activeClient.Handle);
		}

		public void SetActive(KeyValuePair<IntPtr, IThumbnailView> newClient)
		{
			this.GetActiveClient()?.ClearBorder();
#if LINUX
			this._windowManager.ActivateWindow(newClient.Key, newClient.Value.Title);
#else
			this._windowManager.ActivateWindow(newClient.Key, this._configuration.WindowsAnimationStyle);
#endif
			this.SwitchActiveClient(newClient.Key, newClient.Value.Title);

			newClient.Value.SetHighlight();
			newClient.Value.Refresh(true);
		}

		public void MinimizeAllClients()
		{
			foreach (var x in _thumbnailViews.Reverse())
			{
				this._windowManager.MinimizeWindow(x.Value.Id, this._configuration.WindowsAnimationStyle, false);
			}
		}
		public void CycleNextClient(bool isForwards, Dictionary<string, int> cycleOrder)
		{
			IOrderedEnumerable<KeyValuePair<string, int>> clientOrder;
			Dictionary<string, int> _cycleOrder = new Dictionary<string, int>(cycleOrder);

			if ( _cycleOrder.Count == 0 ) 
			{
				int order = 0;
				foreach( var x in _thumbnailViews )
				{
					_cycleOrder.Add(x.Value.Title, order++);
				}
			}

			if (isForwards)
			{
				clientOrder = _cycleOrder.OrderBy(x => x.Value);
			}
			else
			{
				clientOrder = _cycleOrder.OrderByDescending(x => x.Value);
			}

			bool setNextClient = false;
			IThumbnailView lastClient = null;

			foreach (var t in clientOrder)
			{
				if (t.Key == _activeClient.Title && t.Key != DEFAULT_CLIENT_TITLE)
				{
					setNextClient = true;
					lastClient = _thumbnailViews.FirstOrDefault(x => x.Value.Title == t.Key).Value;
					continue;
				}

				// cycle through login screens ?
				if (t.Key == _activeClient.Title && t.Key == DEFAULT_CLIENT_TITLE)
				{
					lastClient = _thumbnailViews.FirstOrDefault(x => x.Value.Title == t.Key && x.Value.Id == _activeClient.Handle).Value;
					if (lastClient == null)
					{
						setNextClient = true;
						continue;
					}
					var possibleClients = (isForwards ? _thumbnailViews.OrderBy(x => x.Value.Id.ToInt64()) : _thumbnailViews.OrderByDescending(x => x.Value.Id.ToInt64())).Where(x => x.Value.Title == t.Key && ! x.Value.IsExcludedFromCycleGroup);
					foreach (var pc in possibleClients)
					{
						if ( pc.Value.Id.Equals(lastClient.Id) )
						{
							setNextClient = true;
							continue;
						}

						if (!setNextClient)
						{
							continue;
						}

						// this is the next client (at login screen)
						SetActive(pc);
						return;
					}

					// rolled off top of list - back to first (if any there!)
					// set next client ?
					continue;
				}

				if (!setNextClient)
				{
					continue;
				}

				if (_thumbnailViews.Any(x => x.Value.Title == t.Key && !x.Value.IsExcludedFromCycleGroup))
				{
					var ptr = t.Key.Equals(DEFAULT_CLIENT_TITLE) ? 
						(isForwards ? _thumbnailViews.OrderBy(x => x.Value.Id.ToInt64()) : _thumbnailViews.OrderByDescending(x => x.Value.Id.ToInt64())).FirstOrDefault(x => x.Value.Title == t.Key && ! x.Value.IsExcludedFromCycleGroup)
						: _thumbnailViews.First(x => x.Value.Title == t.Key && !x.Value.IsExcludedFromCycleGroup);
					SetActive(ptr);
					return;
				}
			}

			// we didn't get a next one. just get the first one from the start.
			foreach (var t in clientOrder)
			{
				if (_thumbnailViews.Any(x => x.Value.Title == t.Key && !x.Value.IsExcludedFromCycleGroup))
				{
					var ptr = t.Key.Equals(DEFAULT_CLIENT_TITLE) ?
						(isForwards ? _thumbnailViews.OrderBy(x => x.Value.Id.ToInt64()) : _thumbnailViews.OrderByDescending(x => x.Value.Id.ToInt64())).FirstOrDefault(x => x.Value.Title == t.Key && !x.Value.IsExcludedFromCycleGroup)
						: _thumbnailViews.First(x => x.Value.Title == t.Key && !x.Value.IsExcludedFromCycleGroup);
					SetActive(ptr);
					_activeClient = (ptr.Key, t.Key);
					return;
				}
			}

			// unable to select anything !
			return;
		}

		public void RegisterCycleClientHotkey(IEnumerable<Keys> keys, bool isForwards, Dictionary<string, int> cycleOrder)
		{
			foreach (var hotkey in keys)
			{
				if (hotkey == Keys.None)
				{
					return;
				}

				var newHandler = new HotkeyHandler(default(IntPtr), hotkey);
				newHandler.Pressed += (object s, HandledEventArgs e) =>
				{
					this.CycleNextClient(isForwards, cycleOrder);
					e.Handled = true;
				};

				newHandler.Register();
				this._cycleClientHotkeyHandlers.Add(newHandler);
			}
		}
		public void RegisterMinimizeAllClientsHotkey(IEnumerable<Keys> keys)
		{
			foreach (var hotkey in keys)
			{
				if (hotkey == Keys.None)
				{
					return;
				}

				var newHandler = new HotkeyHandler(default(IntPtr), hotkey);
				newHandler.Pressed += (object s, HandledEventArgs e) =>
				{
					this.MinimizeAllClients();
					e.Handled = true;
				};

				newHandler.Register();
				this._cycleClientHotkeyHandlers.Add(newHandler);
			}
		}

		public void Start()
		{
			this._thumbnailUpdateTimer.Start();
			this._mouseHook.Start();
			this._keyboardHook.Start();
			this._gamepadHook.Start();
			this._clientWindowHook.Start();

			this.RefreshThumbnails();

			this.StartLayoutReapply();
		}

		public void Stop()
		{
			this._thumbnailUpdateTimer.Stop();
			this._mouseHook.Stop();
			this._keyboardHook.Stop();
			this._gamepadHook.Stop();
			this._clientWindowHook.Stop();

			if (this._layoutReapplyTimer != null)
			{
				this._layoutReapplyTimer.Stop();
				this._layoutReapplyTimer.Tick -= this.LayoutReapplyTick;
				this._layoutReapplyTimer = null;
			}
		}

		private void StartLayoutReapply()
		{
			if (!this._configuration.EnableClientLayoutTracking)
			{
				return;
			}

			this._layoutReapplyTicksRemaining = ThumbnailManager.LAYOUT_REAPPLY_TICKS;

			if (this._layoutReapplyTimer == null)
			{
				this._layoutReapplyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
				this._layoutReapplyTimer.Tick += this.LayoutReapplyTick;
			}

			this._layoutReapplyTimer.Start();
		}

		private void LayoutReapplyTick(object sender, EventArgs e)
		{
			foreach (IThumbnailView view in this._thumbnailViews.Values)
			{
				if (view.Title != ThumbnailManager.DEFAULT_CLIENT_TITLE)
				{
					this.ApplyClientLayout(view);
				}
			}

			this._layoutReapplyTicksRemaining--;
			if (this._layoutReapplyTicksRemaining <= 0)
			{
				this._layoutReapplyTimer.Stop();
			}
		}

		private void ThumbnailUpdateTimerTick(object sender, EventArgs e)
		{
			this.UpdateThumbnailsList();
			this.RefreshThumbnails();
		}

		private async void UpdateThumbnailsList()
		{
			this._processMonitor.GetUpdatedProcesses(out ICollection<IProcessInfo> addedProcesses, out ICollection<IProcessInfo> updatedProcesses, out ICollection<IProcessInfo> removedProcesses);

			List<string> viewsAdded = new List<string>();
			List<string> viewsRemoved = new List<string>();

			foreach (IProcessInfo process in addedProcesses)
			{
				Size initialSize = this._configuration.ThumbnailSize;
				if (this._configuration.PerClientThumbnailSize.Any(x => x.Key == process.Title))
				{
					initialSize = this._configuration.PerClientThumbnailSize[process.Title];
				}

				IThumbnailView view = this._thumbnailViewFactory.Create(process.Handle, process.Title, this._configuration.ThumbnailSize);
				view.IsOverlayEnabled = this._configuration.ShowThumbnailOverlays;
				view.IsExcludedFromCycleGroup = false;
				view.SetFrames(this._configuration.ShowThumbnailFrames);
				// Max/Min size limitations should be set AFTER the frames are disabled
				// Otherwise thumbnail window will be unnecessary resized
				view.SetSizeLimitations(this._configuration.ThumbnailMinimumSize, this._configuration.ThumbnailMaximumSize);
				view.SetTopMost(this._configuration.ShowThumbnailsAlwaysOnTop);

				view.ThumbnailLocation = this.IsManageableThumbnail(view)
											? this._configuration.GetThumbnailLocation(view.Title, this._activeClient.Title, view.ThumbnailLocation)
											: this._configuration.LoginThumbnailLocation;

				this._thumbnailViews.Add(view.Id, view);

				view.ThumbnailResized = this.ThumbnailViewResized;
				view.ThumbnailMoved = this.ThumbnailViewMoved;
				view.ThumbnailFocused = this.ThumbnailViewFocused;
				view.ThumbnailLostFocus = this.ThumbnailViewLostFocus;
				view.ThumbnailActivated = this.ThumbnailActivated;
				view.ThumbnailDeactivated = this.ThumbnailDeactivated;

				view.ThumbnailToggleCycleGroup = this.ThumbnailToggleCycleGroup;

				view.ThumbnailAssignKeyboardHotkeyRequested = this.AssignKeyboardHotkey;
				view.ThumbnailAssignMouseButtonRequested = this.AssignMouseButton;
				view.ThumbnailAssignGamepadButtonRequested = this.AssignGamepadButton;
				view.ThumbnailAssignInsertHotkeyRequested = this.AssignInsertHotkey;
				view.ThumbnailClearBindingsRequested = this.ClearClientBindings;

				view.ThumbnailSetWindowSizeRequested = this.AssignClientWindowSize;
				view.ThumbnailSetWindowSizeAllClientsRequested = this.AssignClientWindowSizeAllClients;
				view.ThumbnailClearWindowSizeRequested = this.ClearClientWindowSize;

				view.RegisterHotkey(this._configuration.GetClientHotkey(view.Title));

				// Caption bar must be applied BEFORE the layout: toggling the caption
				// changes the window frame, so applying it after a move would shift the
				// window (title bar creeping off the top of the screen on each launch).
				this.ApplyCaptionBar(view);
				this.ApplyClientLayout(view);
				this.ApplyClientWindowSize(view);

				// TODO Add extension filter here later
				if (view.Title != ThumbnailManager.DEFAULT_CLIENT_TITLE)
				{
					viewsAdded.Add(view.Title);
				}
			}

			foreach (IProcessInfo process in updatedProcesses)
			{
				this._thumbnailViews.TryGetValue(process.Handle, out IThumbnailView view);

				if (view == null)
				{
					// Something went terribly wrong
					continue;
				}

				if (process.Title != view.Title) // update thumbnail title
				{
					viewsRemoved.Add(view.Title);
					view.Title = process.Title;
					viewsAdded.Add(view.Title);

					view.RegisterHotkey(this._configuration.GetClientHotkey(process.Title));

					this.ApplyCaptionBar(view);
					this.ApplyClientLayout(view);
					this.ApplyClientWindowSize(view);
				}
			}

			foreach (IProcessInfo process in removedProcesses)
			{
				IThumbnailView view = this._thumbnailViews[process.Handle];

				this._thumbnailViews.Remove(view.Id);
				if (view.Title != ThumbnailManager.DEFAULT_CLIENT_TITLE)
				{
					viewsRemoved.Add(view.Title);
				}

				view.UnregisterHotkey();

				view.ThumbnailResized = null;
				view.ThumbnailMoved = null;
				view.ThumbnailFocused = null;
				view.ThumbnailLostFocus = null;
				view.ThumbnailActivated = null;
				view.ThumbnailToggleCycleGroup = null;
				view.ThumbnailAssignKeyboardHotkeyRequested = null;
				view.ThumbnailAssignMouseButtonRequested = null;
				view.ThumbnailAssignGamepadButtonRequested = null;
				view.ThumbnailAssignInsertHotkeyRequested = null;
				view.ThumbnailClearBindingsRequested = null;
				view.ThumbnailSetWindowSizeRequested = null;
				view.ThumbnailSetWindowSizeAllClientsRequested = null;
				view.ThumbnailClearWindowSizeRequested = null;

				view.Close();
			}

			if ((viewsAdded.Count > 0) || (viewsRemoved.Count > 0))
			{
				await this._mediator.Publish(new ThumbnailListUpdated(viewsAdded, viewsRemoved));
			}
		}

		private void RefreshThumbnails()
		{
			// TODO Split this method
			IntPtr foregroundWindowHandle = this._windowManager.GetForegroundWindowHandle();

			// The foreground window can be NULL in certain circumstances, such as when a window is losing activation.
			// It is safer to just skip this refresh round than to do something while the system state is undefined
			if (foregroundWindowHandle == IntPtr.Zero)
			{
				return;
			}

			string foregroundWindowTitle = null;

			// Check if the foreground window handle is one of the known handles for client windows or their thumbnails
			bool isClientWindow = this.IsClientWindowActive(foregroundWindowHandle);
			bool isMainWindowActive = this.IsMainWindowActive(foregroundWindowHandle);

			if (foregroundWindowHandle == this._activeClient.Handle)
			{
				foregroundWindowTitle = this._activeClient.Title;
			}
			else if (this._thumbnailViews.TryGetValue(foregroundWindowHandle, out IThumbnailView foregroundView))
			{
				// This code will work only on Alt+Tab switch between clients
				foregroundWindowTitle = foregroundView.Title;
			}
			else if (!isClientWindow)
			{
				this._externalApplication = foregroundWindowHandle;
			}

			// No need to minimize EVE clients when switching out to non-EVE window (like thumbnail)
			if (!string.IsNullOrEmpty(foregroundWindowTitle))
			{
				this.SwitchActiveClient(foregroundWindowHandle, foregroundWindowTitle);
			}

			bool hideAllThumbnails = this._configuration.HideThumbnailsOnLostFocus && !(isClientWindow || isMainWindowActive);

			// Wait for some time before hiding all previews
			if (hideAllThumbnails)
			{
				this._hideThumbnailsDelay--;
				if (this._hideThumbnailsDelay > 0)
				{
					hideAllThumbnails = false; // Postpone the 'hide all' operation
				}
				else
				{
					this._hideThumbnailsDelay = 0; // Stop the counter
				}
			}
			else
			{
				this._hideThumbnailsDelay = this._configuration.HideThumbnailsDelay; // Reset the counter
			}

			this._refreshCycleCount++;

			bool forceRefresh;
			if (this._refreshCycleCount >= ThumbnailManager.FORCED_REFRESH_CYCLE_THRESHOLD)
			{
				this._refreshCycleCount = 0;
				forceRefresh = true;
			}
			else
			{
				forceRefresh = false;
			}

			this.DisableViewEvents();

			// Snap thumbnail
			// No need to update Thumbnails while one of them is highlighted
			if ((!this._isHoverEffectActive) && this.TryDequeueLocationChange(out var locationChange))
			{
				if ((locationChange.ActiveClient == this._activeClient.Title) && this._thumbnailViews.TryGetValue(locationChange.Handle, out var view))
				{
					this.SnapThumbnailView(view);

					this.RaiseThumbnailLocationUpdatedNotification(view.Title);
				}
				else
				{
					this.RaiseThumbnailLocationUpdatedNotification(locationChange.Title);
				}
			}

			// Hide, show, resize and move - update ZoomAnchor setting
			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				IThumbnailView view = entry.Value;
				// update ZoomAnchor regardless
				view.ClientZoomAnchor = this._configuration.GetZoomAnchor(view.Title, this._configuration.ThumbnailZoomAnchor);


				if (hideAllThumbnails || this._configuration.IsThumbnailDisabled(view.Title))
				{
					if (view.IsActive)
					{
						view.Hide();
					}
					continue;
				}

				if (this._configuration.HideActiveClientThumbnail && (view.Id == this._activeClient.Handle))
				{
					if (view.IsActive)
					{
						view.Hide();
					}
					continue;
				}

				if (this._configuration.HideLoginClientThumbnail && (view.Title == DEFAULT_CLIENT_TITLE ))
				{
					if (view.IsActive)
					{
						view.Hide();
					}
					continue;
				}

				// No need to update Thumbnails while one of them is highlighted
				if (!this._isHoverEffectActive)
				{
					// Do not even move thumbnails with default caption
					if (this.IsManageableThumbnail(view))
					{
						view.ThumbnailLocation = this._configuration.GetThumbnailLocation(view.Title, this._activeClient.Title, view.ThumbnailLocation);
						view.ThumbnailSize = this._configuration.GetThumbnailSize(view.Title, this._activeClient.Title, view.ThumbnailSize);
					}

					view.SetOpacity(this._configuration.ThumbnailOpacity);
					view.SetTopMost(this._configuration.ShowThumbnailsAlwaysOnTop);
				}

				view.IsOverlayEnabled = this._configuration.ShowThumbnailOverlays;

				view.SetHighlight(
					this._configuration.EnableActiveClientHighlight && (view.Id == this._activeClient.Handle), 
					this._configuration.ActiveClientHighlightThickness);

				if (!view.IsActive)
				{
					view.Show();
				}
				else
				{
					view.Refresh(forceRefresh);
				}
			}

			this.EnableViewEvents();
		}

		public void UpdateThumbnailsSize()
		{
			this.SetThumbnailsSize(this._configuration.ThumbnailSize);
		}
		public void UpdateCycleGroupIndicator()
		{
			this.SetCycleGroupIndicator(this._configuration.CycleGroupIndicatorAnchor);
		}

		private void SetCycleGroupIndicator(ZoomAnchor anchor)
		{
			this.DisableViewEvents();

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				entry.Value.SetCycleGroupIndicator(entry.Value.IsExcludedFromCycleGroup, anchor);
				entry.Value.Refresh(false);
			}

			this.EnableViewEvents();
		}

		private void SetThumbnailsSize(Size size)
		{
			this.DisableViewEvents();

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				entry.Value.ThumbnailSize = size;
				entry.Value.Refresh(false);
			}

			this.EnableViewEvents();
		}

		public void UpdateThumbnailFrames()
		{
			this.DisableViewEvents();

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				entry.Value.SetFrames(this._configuration.ShowThumbnailFrames);
				ApplyCaptionBar(entry.Value);
				entry.Value.SetPreventPreviews();
			}

			this.EnableViewEvents();
		}

		private void EnableViewEvents()
		{
			this._ignoreViewEvents = false;
		}

		private void DisableViewEvents()
		{
			this._ignoreViewEvents = true;
		}

		private void SwitchActiveClient(IntPtr foregroundClientHandle, string foregroundClientTitle)
		{
			// Check if any actions are needed
			if (this._activeClient.Handle == foregroundClientHandle)
			{
				return;
			}

			// Minimize the currently active client if needed
			if (this._configuration.MinimizeInactiveClients && !this._configuration.IsPriorityClient(this._activeClient.Title))
			{
				this._windowManager.MinimizeWindow(this._activeClient.Handle, this._configuration.WindowsAnimationStyle, false);
#if LINUX
   			    this._windowManager.ActivateWindow(foregroundClientHandle, foregroundClientTitle);
#else
				this._windowManager.ActivateWindow(foregroundClientHandle, this._configuration.WindowsAnimationStyle);
#endif
			}

			this._activeClient = (foregroundClientHandle, foregroundClientTitle);
		}

		private void ThumbnailViewFocused(IntPtr id)
		{
			if (this._isHoverEffectActive)
			{
				return;
			}

			this._isHoverEffectActive = true;

			IThumbnailView view = this._thumbnailViews[id];

			view.SetTopMost(true);
			view.SetOpacity(1.0);

			if (this._configuration.ThumbnailZoomEnabled && ! view.IsPreventPreviews() )
			{
				this.ThumbnailZoomIn(view);
			}
		}

		private void ThumbnailViewLostFocus(IntPtr id)
		{
			if (!this._isHoverEffectActive)
			{
				return;
			}

			IThumbnailView view = this._thumbnailViews[id];

			if (this._configuration.ThumbnailZoomEnabled)
			{
				this.ThumbnailZoomOut(view);
			}

			view.SetOpacity(this._configuration.ThumbnailOpacity);

			this._isHoverEffectActive = false;
		}

		private void AssignKeyboardHotkey(IntPtr id)
		{
			if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			Keys currentHotkey = this._configuration.GetClientHotkey(view.Title);

			using (HotkeyInputDialog dialog = new HotkeyInputDialog(view.Title, currentHotkey))
			{
				if (dialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}

				Keys selectedHotkey = dialog.SelectedHotkey;

				this._configuration.SetClientHotkey(view.Title, selectedHotkey);
				view.RegisterHotkey(selectedHotkey);
				this._mediator.Send(new SaveConfiguration());
			}
		}

		private void AssignMouseButton(IntPtr id)
		{
			if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			MouseButtons currentButton = this._configuration.GetClientMouseButton(view.Title);

			this._bindingCaptureActive = true;
			try
			{
				using (MouseButtonInputDialog dialog = new MouseButtonInputDialog(view.Title, currentButton, this._mouseHook))
				{
					if (dialog.ShowDialog() != DialogResult.OK)
					{
						return;
					}

					MouseButtons selectedButton = dialog.SelectedButton;

					// A given physical mouse button can only switch to one client
					if (selectedButton != MouseButtons.None)
					{
						this.ClearMouseButtonFromOtherClients(selectedButton, view.Title);
					}

					this._configuration.SetClientMouseButton(view.Title, selectedButton);
					this._mediator.Send(new SaveConfiguration());
				}
			}
			finally
			{
				this._bindingCaptureActive = false;
			}
		}

		private void ClearMouseButtonFromOtherClients(MouseButtons button, string keepClientTitle)
		{
			foreach (IThumbnailView otherView in this._thumbnailViews.Values)
			{
				if (otherView.Title == keepClientTitle)
				{
					continue;
				}

				if (this._configuration.GetClientMouseButton(otherView.Title) == button)
				{
					this._configuration.SetClientMouseButton(otherView.Title, MouseButtons.None);
				}
			}
		}

		private void GlobalMouseButtonPressed(MouseButtons button)
		{
			if (this._bindingCaptureActive || button == MouseButtons.None)
			{
				return;
			}

			foreach (KeyValuePair<IntPtr, IThumbnailView> pair in this._thumbnailViews)
			{
				if (this._configuration.GetClientMouseButton(pair.Value.Title) == button)
				{
					IThumbnailView view = pair.Value;
					IThumbnailView previousActive = this.GetActiveClient();

					view.SetHighlight();
					this.ThumbnailActivated(pair.Key);

					if (previousActive != null && previousActive.Id != view.Id)
					{
						previousActive.ClearBorder();
					}

					break;
				}
			}
		}

		private void AssignGamepadButton(IntPtr id)
		{
			if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			string currentButton = this._configuration.GetClientGamepadButton(view.Title);

			this._bindingCaptureActive = true;
			try
			{
				using (GamepadButtonInputDialog dialog = new GamepadButtonInputDialog(view.Title, currentButton, this._gamepadHook))
				{
					if (dialog.ShowDialog() != DialogResult.OK)
					{
						return;
					}

					string selectedButton = dialog.SelectedButton;

					if (!string.IsNullOrEmpty(selectedButton))
					{
						this.ClearGamepadButtonFromOtherClients(selectedButton, view.Title);
					}

					this._configuration.SetClientGamepadButton(view.Title, selectedButton);
					this._mediator.Send(new SaveConfiguration());
				}
			}
			finally
			{
				this._bindingCaptureActive = false;
			}
		}

		private void ClearGamepadButtonFromOtherClients(string button, string keepClientTitle)
		{
			foreach (IThumbnailView otherView in this._thumbnailViews.Values)
			{
				if (otherView.Title == keepClientTitle)
				{
					continue;
				}

				if (string.Equals(this._configuration.GetClientGamepadButton(otherView.Title), button, StringComparison.Ordinal))
				{
					this._configuration.SetClientGamepadButton(otherView.Title, null);
				}
			}
		}

		private void GamepadButtonPressed(string button)
		{
			if (this._bindingCaptureActive || string.IsNullOrEmpty(button))
			{
				return;
			}

			foreach (KeyValuePair<IntPtr, IThumbnailView> pair in this._thumbnailViews)
			{
				if (string.Equals(this._configuration.GetClientGamepadButton(pair.Value.Title), button, StringComparison.Ordinal))
				{
					IThumbnailView view = pair.Value;
					IThumbnailView previousActive = this.GetActiveClient();

					view.SetHighlight();
					this.ThumbnailActivated(pair.Key);

					if (previousActive != null && previousActive.Id != view.Id)
					{
						previousActive.ClearBorder();
					}

					break;
				}
			}
		}

		private void AssignInsertHotkey(IntPtr id)
		{
			if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			Keys currentActionKey = this._configuration.GetClientInsertHotkey(view.Title);

			// The low-level keyboard hook must not switch clients while we capture.
			this._bindingCaptureActive = true;
			try
			{
				using (InsertComboInputDialog dialog = new InsertComboInputDialog(view.Title, currentActionKey))
				{
					if (dialog.ShowDialog() != DialogResult.OK)
					{
						return;
					}

					Keys selectedActionKey = dialog.SelectedActionKey;

					// A given Insert + key combo can only switch to one client
					if (selectedActionKey != Keys.None)
					{
						this.ClearInsertHotkeyFromOtherClients(selectedActionKey, view.Title);
					}

					this._configuration.SetClientInsertHotkey(view.Title, selectedActionKey);
					this._mediator.Send(new SaveConfiguration());
				}
			}
			finally
			{
				this._bindingCaptureActive = false;
			}
		}

		private void ClearInsertHotkeyFromOtherClients(Keys actionKey, string keepClientTitle)
		{
			foreach (IThumbnailView otherView in this._thumbnailViews.Values)
			{
				if (otherView.Title == keepClientTitle)
				{
					continue;
				}

				if (this._configuration.GetClientInsertHotkey(otherView.Title) == actionKey)
				{
					this._configuration.SetClientInsertHotkey(otherView.Title, Keys.None);
				}
			}
		}

		// Returns true when the Insert + key combo was bound to a client (the action
		// key is then swallowed by the hook so the game never sees it).
		private bool GlobalKeyComboPressed(Keys actionKey)
		{
			if (this._bindingCaptureActive || actionKey == Keys.None)
			{
				return false;
			}

			foreach (KeyValuePair<IntPtr, IThumbnailView> pair in this._thumbnailViews)
			{
				if (this._configuration.GetClientInsertHotkey(pair.Value.Title) == actionKey)
				{
					IThumbnailView view = pair.Value;
					IThumbnailView previousActive = this.GetActiveClient();

					view.SetHighlight();
					this.ThumbnailActivated(pair.Key);

					if (previousActive != null && previousActive.Id != view.Id)
					{
						previousActive.ClearBorder();
					}

					return true;
				}
			}

			return false;
		}

		private void ClearClientBindings(IntPtr id)
		{
			if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			this._configuration.SetClientHotkey(view.Title, Keys.None);
			this._configuration.SetClientMouseButton(view.Title, MouseButtons.None);
			this._configuration.SetClientGamepadButton(view.Title, null);
			this._configuration.SetClientInsertHotkey(view.Title, Keys.None);
			view.UnregisterHotkey();
			this._mediator.Send(new SaveConfiguration());
		}

		// Forces the configured window resolution onto a client. Position is left to
		// EVE (SWP_NOMOVE), so this cannot cause the frame to creep off-screen. Setting
		// a fixed absolute size on each launch is idempotent - the window never grows.
		private void ApplyClientWindowSize(IThumbnailView view)
		{
			if (view.Title == ThumbnailManager.DEFAULT_CLIENT_TITLE)
			{
				return;
			}

			Size size = this._configuration.GetClientWindowSize(view.Title);
			if (size.IsEmpty || size.Width <= 0 || size.Height <= 0)
			{
				return;
			}

			this._windowManager.SetClientSize(view.Id, size.Width, size.Height);
		}

		private void AssignClientWindowSize(IntPtr id)
		{
			if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			// Prefill with the configured size, or the client's current render area.
			Size current = this._configuration.GetClientWindowSize(view.Title);
			if (current.IsEmpty)
			{
				current = this._windowManager.GetClientSize(view.Id);
			}

			using (WindowSizeInputDialog dialog = new WindowSizeInputDialog(view.Title, current, false))
			{
				if (dialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}

				this._configuration.SetClientWindowSize(view.Title, dialog.SelectedSize);
				this.ApplyClientWindowSize(view);
				this._mediator.Send(new SaveConfiguration());
			}
		}

		private void AssignClientWindowSizeAllClients(IntPtr id)
		{
			// Seed from the existing global default, or the clicked client's current size.
			Size current = this._configuration.DefaultClientWindowSize;
			if (current.IsEmpty && this._thumbnailViews.TryGetValue(id, out IThumbnailView seedView))
			{
				current = this._windowManager.GetClientSize(seedView.Id);
			}

			using (WindowSizeInputDialog dialog = new WindowSizeInputDialog(null, current, true))
			{
				if (dialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}

				this._configuration.DefaultClientWindowSize = dialog.SelectedSize;

				// Re-apply to every logged-in client. Clients with a per-client override
				// keep their own size (GetClientWindowSize prefers the per-client entry).
				foreach (IThumbnailView view in this._thumbnailViews.Values)
				{
					this.ApplyClientWindowSize(view);
				}

				this._mediator.Send(new SaveConfiguration());
			}
		}

		private void ClearClientWindowSize(IntPtr id)
		{
			if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			this._configuration.SetClientWindowSize(view.Title, Size.Empty);
			this._mediator.Send(new SaveConfiguration());
		}

		private void ThumbnailActivated(IntPtr id)
		{
			IThumbnailView view = this._thumbnailViews[id];

			Task.Run(() =>
				{
#if LINUX
					this._windowManager.ActivateWindow(view.Id, view.Title);
#else
					this._windowManager.ActivateWindow(view.Id, this._configuration.WindowsAnimationStyle);
#endif
				})
				.ContinueWith((task) =>
				{
					// This code should be executed on UI thread
					this.SwitchActiveClient(view.Id, view.Title);
					this.UpdateClientLayouts();
					this.RefreshThumbnails();
				}, TaskScheduler.FromCurrentSynchronizationContext());
		}

		private void ThumbnailDeactivated(IntPtr id, bool switchOut)
		{
			if (switchOut)
			{
#if LINUX
				this._windowManager.ActivateWindow(this._externalApplication, null);
#else
				this._windowManager.ActivateWindow(this._externalApplication, this._configuration.WindowsAnimationStyle);
#endif
			}
			else
			{
				if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view))
				{
					return;
				}

				this._windowManager.MinimizeWindow(view.Id, this._configuration.WindowsAnimationStyle, true);
				this.RefreshThumbnails();
			}
		}

		private void ThumbnailToggleCycleGroup(IntPtr id)
		{
			var view = GetClientByPointer(id);
			if ( view != null )
			{
				view.IsExcludedFromCycleGroup = !view.IsExcludedFromCycleGroup;
				view.SetCycleGroupIndicator(view.IsExcludedFromCycleGroup, _configuration.CycleGroupIndicatorAnchor);

			}
			this.RefreshThumbnails();
		}


		private async void ThumbnailViewResized(IntPtr id)
		{
			if (this._ignoreViewEvents)
			{
				return;
			}

			IThumbnailView view = this._thumbnailViews[id];

			this.SetThumbnailsSize(view.ThumbnailSize);

			view.Refresh(false);

			await this._mediator.Publish(new ThumbnailActiveSizeUpdated(view.ThumbnailSize));
		}

		private void ThumbnailViewMoved(IntPtr id)
		{
			if (this._ignoreViewEvents)
			{
				return;
			}

			IThumbnailView view = this._thumbnailViews[id];
			view.Refresh(false);
			this.EnqueueLocationChange(view);
		}

		// Checks whether currently active window belongs to an EVE client or its thumbnail
		private bool IsClientWindowActive(IntPtr windowHandle)
		{
			if (windowHandle == IntPtr.Zero)
			{
				return false;
			}

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				IThumbnailView view = entry.Value;

				if (view.IsKnownHandle(windowHandle))
				{
					return true;
				}
			}

			return false;
		}

		// Check whether the currently active window belongs to EVE-O-Preview itself
		private bool IsMainWindowActive(IntPtr windowHandle)
		{
			return (this._processMonitor.GetMainProcess().Handle == windowHandle);
		}

		private void ThumbnailZoomIn(IThumbnailView view)
		{
			this.DisableViewEvents();

			view.ZoomIn(ViewZoomAnchorConverter.Convert(view.ClientZoomAnchor), this._configuration.ThumbnailZoomFactor);
			view.Refresh(false);

			this.EnableViewEvents();
		}

		private void ThumbnailZoomOut(IThumbnailView view)
		{
			this.DisableViewEvents();

			view.ZoomOut();
			view.Refresh(false);

			this.EnableViewEvents();
		}

		private void SnapThumbnailView(IThumbnailView view)
		{
			// Check if this feature is enabled
			if (!this._configuration.EnableThumbnailSnap)
			{
				return;
			}

			// Only borderless thumbnails can be docked
			if (this._configuration.ShowThumbnailFrames)
			{
				return;
			}

			int width = this._configuration.ThumbnailSize.Width;
			int height = this._configuration.ThumbnailSize.Height;

			// TODO Extract method
			int baseX = view.ThumbnailLocation.X;
			int baseY = view.ThumbnailLocation.Y;

			Point[] viewPoints = { new Point(baseX, baseY), new Point(baseX + width, baseY), new Point(baseX, baseY + height), new Point(baseX + width, baseY + height) };

			// TODO Extract constants
			int thresholdX = Math.Max(20, width / 10);
			int thresholdY = Math.Max(20, height / 10);

			foreach (var entry in this._thumbnailViews)
			{
				IThumbnailView testView = entry.Value;

				if (view.Id == testView.Id)
				{
					continue;
				}

				int testX = testView.ThumbnailLocation.X;
				int testY = testView.ThumbnailLocation.Y;

				Point[] testPoints = { new Point(testX, testY), new Point(testX + width, testY), new Point(testX, testY + height), new Point(testX + width, testY + height) };

				var delta = ThumbnailManager.TestViewPoints(viewPoints, testPoints, thresholdX, thresholdY);

				if ((delta.X == 0) && (delta.Y == 0))
				{
					continue;
				}

				view.ThumbnailLocation = new Point(view.ThumbnailLocation.X + delta.X, view.ThumbnailLocation.Y + delta.Y);
				this._configuration.SetThumbnailLocation(view.Title, this._activeClient.Title, view.ThumbnailLocation);
				break;
			}
		}

		private static (int X, int Y) TestViewPoints(Point[] viewPoints, Point[] testPoints, int thresholdX, int thresholdY)
		{
			// Point combinations that we need to check
			// No need to check all 4x4 combinations
			(int ViewOffset, int TestOffset)[] testOffsets =
								{   ( 0, 3 ), ( 0, 2 ), ( 1, 2 ),
									( 0, 1 ), ( 0, 0 ), ( 1, 0 ),
									( 2, 1 ), ( 2, 0 ), ( 3, 0 )};

			foreach (var testOffset in testOffsets)
			{
				Point viewPoint = viewPoints[testOffset.ViewOffset];
				Point testPoint = testPoints[testOffset.TestOffset];

				int deltaX = testPoint.X - viewPoint.X;
				int deltaY = testPoint.Y - viewPoint.Y;

				if ((Math.Abs(deltaX) <= thresholdX) && (Math.Abs(deltaY) <= thresholdY))
				{
					return (deltaX, deltaY);
				}
			}

			return (0, 0);
		}
		private bool SetWindowStyle(IThumbnailView view, UInt32 styleToChange, bool remove)
		{
			IntPtr handle = view.Id;
			uint style = User32NativeMethods.GetWindowLong(handle, InteropConstants.GWL_STYLE);
			if (((style & styleToChange) == styleToChange) && remove == true)
			{
				style = style & ~styleToChange;
				User32NativeMethods.SetWindowLong(handle, InteropConstants.GWL_STYLE, style);
				return true;
			}
			if (((style & styleToChange) != styleToChange) && remove == false)
			{
				style = style | styleToChange;
				User32NativeMethods.SetWindowLong(handle, InteropConstants.GWL_STYLE, style);
				return true;
			}
			return false;
		}
		private void ApplyCaptionBar(IThumbnailView view)

		{
			if (view.Title == ThumbnailManager.DEFAULT_CLIENT_TITLE) return;
			IntPtr handle = view.Id;

			bool enable = this._configuration.HideCaptionOnClients;
			bool changed = false;
			changed = changed | SetWindowStyle(view, InteropConstants.WS_CAPTION, enable);
			changed = changed | SetWindowStyle(view, InteropConstants.WS_THICKFRAME, enable);

			if (changed)
			{
				// Force the window frame to be recalculated NOW. A style change made
				// with SetWindowLong stays pending until a SetWindowPos with
				// SWP_FRAMECHANGED (or the next move). Previously it materialized during
				// the subsequent layout MoveWindow, growing the window by the caption
				// height and creeping the title bar off the top a bit more each launch.
				User32NativeMethods.SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
					InteropConstants.SWP_NOMOVE | InteropConstants.SWP_NOSIZE |
					InteropConstants.SWP_NOZORDER | InteropConstants.SWP_NOACTIVATE |
					InteropConstants.SWP_NOOWNERZORDER | InteropConstants.SWP_FRAMECHANGED);
			}
		}
		private void ApplyClientLayout(IThumbnailView view)
		{
			IntPtr clientHandle = view.Id;
			string clientTitle = view.Title;

			if (!this._configuration.EnableClientLayoutTracking)
			{
				return;
			}

			// No need to apply layout for not yet logged-in clients
			if (clientTitle == ThumbnailManager.DEFAULT_CLIENT_TITLE)
			{
				return;
			}

			ClientLayout clientLayout = this._configuration.GetClientLayout(clientTitle);

			if (clientLayout == null)
			{
				return;
			}

			if (clientLayout.IsMaximized)
			{
				this._windowManager.MaximizeWindow(clientHandle);
			}
			else
			{
				// Restore the saved top-left position, but keep the window's current
				// size. EVE controls its own window size (tied to its resolution), so
				// forcing a saved size here fought EVE and made the frame creep upward
				// off the screen a little more on every launch. Honoring the position
				// only keeps the title bar exactly where the user left it.
				(int Left, int Top, int Right, int Bottom) current = this._windowManager.GetWindowPosition(clientHandle);
				int width = Math.Abs(current.Right - current.Left);
				int height = Math.Abs(current.Bottom - current.Top);

				this._windowManager.MoveWindow(clientHandle, clientLayout.X, clientLayout.Y, width, height);
			}
		}

		private void ClientWindowMoveResizeEnded(IntPtr hwnd)
		{
			if (!this._configuration.EnableClientLayoutTracking)
			{
				return;
			}

			// Only react when the moved window is one of the tracked EVE clients
			if (!this._thumbnailViews.ContainsKey(hwnd))
			{
				return;
			}

			// Capture and persist the new position immediately - no client switch
			// required. UpdateClientLayouts only writes to disk if something changed.
			this.UpdateClientLayouts();
		}

		private void UpdateClientLayouts()
		{
			if (!this._configuration.EnableClientLayoutTracking)
			{
				return;
			}

			bool layoutChanged = false;

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				IThumbnailView view = entry.Value;

				// No need to save layout for not yet logged-in clients
				if (view.Title == ThumbnailManager.DEFAULT_CLIENT_TITLE)
				{
					continue;
				}

				(int Left, int Top, int Right, int Bottom) position = this._windowManager.GetWindowPosition(view.Id);
				int width = Math.Abs(position.Right - position.Left);
				int height = Math.Abs(position.Bottom - position.Top);

				var isMaximized = this._windowManager.IsWindowMaximized(view.Id);

				if (!(isMaximized || this.IsValidWindowPosition(position.Left, position.Top, width, height)))
				{
					continue;
				}

				ClientLayout newLayout = new ClientLayout(position.Left, position.Top, width, height, isMaximized);

				if (ThumbnailManager.HasClientLayoutChanged(this._configuration.GetClientLayout(view.Title), newLayout))
				{
					this._configuration.SetClientLayout(view.Title, newLayout);
					layoutChanged = true;
				}
			}

			// Persist client window positions to disk as soon as a window is moved
			// or resized. Previously these were only written on a clean exit, so
			// with "Minimize to tray" enabled the positions were lost.
			if (layoutChanged)
			{
				this._mediator.Send(new SaveConfiguration());
			}
		}

		private static bool HasClientLayoutChanged(ClientLayout existing, ClientLayout updated)
		{
			if (existing == null)
			{
				return true;
			}

			return existing.X != updated.X
				|| existing.Y != updated.Y
				|| existing.Width != updated.Width
				|| existing.Height != updated.Height
				|| existing.IsMaximized != updated.IsMaximized;
		}

		private void EnqueueLocationChange(IThumbnailView view)
		{
			string activeClientTitle = this._activeClient.Title;
			// TODO ??
			this._configuration.SetThumbnailLocation(view.Title, activeClientTitle, view.ThumbnailLocation);

			lock (this._locationChangeNotificationSyncRoot)
			{
				if (this._enqueuedLocationChangeNotification.Handle == IntPtr.Zero)
				{
					this._enqueuedLocationChangeNotification = (view.Id, view.Title, activeClientTitle, view.ThumbnailLocation, ThumbnailManager.DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY);
					return;
				}

				// Reset the delay and exit
				if ((this._enqueuedLocationChangeNotification.Handle == view.Id) &&
					(this._enqueuedLocationChangeNotification.ActiveClient == activeClientTitle))
				{
					this._enqueuedLocationChangeNotification.Delay = ThumbnailManager.DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY;
					return;
				}

				this.RaiseThumbnailLocationUpdatedNotification(this._enqueuedLocationChangeNotification.Title);
				this._enqueuedLocationChangeNotification = (view.Id, view.Title, activeClientTitle, view.ThumbnailLocation, ThumbnailManager.DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY);
			}
		}

		private bool TryDequeueLocationChange(out (IntPtr Handle, string Title, string ActiveClient, Point Location) change)
		{
			lock (this._locationChangeNotificationSyncRoot)
			{
				change = (IntPtr.Zero, null, null, Point.Empty);

				if (this._enqueuedLocationChangeNotification.Handle == IntPtr.Zero)
				{
					return false;
				}

				this._enqueuedLocationChangeNotification.Delay--;

				if (this._enqueuedLocationChangeNotification.Delay > 0)
				{
					return false;
				}

				change = (this._enqueuedLocationChangeNotification.Handle, this._enqueuedLocationChangeNotification.Title, this._enqueuedLocationChangeNotification.ActiveClient, this._enqueuedLocationChangeNotification.Location);
				this._enqueuedLocationChangeNotification = (IntPtr.Zero, null, null, Point.Empty, -1);

				return true;
			}
		}

		private async void RaiseThumbnailLocationUpdatedNotification(string title)
		{
			if (string.IsNullOrEmpty(title) || (title == ThumbnailManager.DEFAULT_CLIENT_TITLE))
			{
				return;
			}

			await this._mediator.Send(new SaveConfiguration());
		}

		// We shouldn't manage some thumbnails (like thumbnail of the EVE client sitting on the login screen)
		// TODO Move to a service (?)
		private bool IsManageableThumbnail(IThumbnailView view)
		{
			return view.Title != ThumbnailManager.DEFAULT_CLIENT_TITLE;
		}

		// Quick sanity check that the window is not minimized
		private bool IsValidWindowPosition(int left, int top, int width, int height)
		{
			return (left > ThumbnailManager.WINDOW_POSITION_THRESHOLD_LOW) && (left < ThumbnailManager.WINDOW_POSITION_THRESHOLD_HIGH)
					&& (top > ThumbnailManager.WINDOW_POSITION_THRESHOLD_LOW) && (top < ThumbnailManager.WINDOW_POSITION_THRESHOLD_HIGH)
					&& (width > ThumbnailManager.WINDOW_SIZE_THRESHOLD) && (height > ThumbnailManager.WINDOW_SIZE_THRESHOLD);
		}
	}
}