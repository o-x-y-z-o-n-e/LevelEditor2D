using System;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using IconFonts;
using ImGuiNET;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Input.Extensions;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using StbImageSharp;

namespace L2D;

public static class Program {

	public const int VERSION_MAJOR = 0;
	public const int VERSION_MINOR = 1;
	public const int VERSION_PATCH = 0;

	public const string TITLE = "Level Editor 2D";

	public static readonly string[] AUTHORS = new [] {
		"Jeremy Kiel",
		"Giles McGrath"
	};
	
	public static readonly string VERSION_STRING = $"{VERSION_MAJOR}.{VERSION_MINOR}.{VERSION_PATCH}";
	
	public const string FILE_EXTENSION = "l2d";

	public const int IMGUI_STRING_MAX = 512;

	public static string Title {
		get => window.Title;
		set => window.Title = value;
	}

	public static float DeltaTime => deltaTime;
	
	public static MenuBar MenuBar => menuBar;
	
	public static CanvasPanel CanvasPanel => canvasPanel;
	public static LayersPanel LayersPanel => layersPanel;
	public static EntitiesPanel EntitiesPanel => entitiesPanel;
	public static TemplatesPanel TemplatesPanel => templatesPanel;
	public static ScenesPanel ScenesPanel => scenesPanel;
	public static TilePickerPanel TilePickerPanel => tilePickerPanel;
	public static TilesetsPanel TilesetsPanel => tilesetsPanel;
	public static TileFillModal TileFillModal => tileFillModal;
	public static ConfirmModal ConfirmModal => confirmModal;
	public static ReloadFileModal ReloadFileModal => reloadFileModal;
	public static NewProjectModal NewProjectModal => newProjectModal;
	
	public static Scene SelectedScene {
		get => selectedScene;
		set => SetSelectedScene(value);
	}
	
	public static Layer SelectedLayer {
		get => selectedLayer;
		set => SetSelectedLayer(value);
	}
	
	public static Entity SelectedEntity {
		get => selectedEntity;
		set => SetSelectedEntity(value);
	}
	
	public static Tileset SelectedTileset {
		get => selectedTileset;
		set => SetSelectedTileset(value);
	}

	public static Vector2D<int> FramebufferSize => window.FramebufferSize;
    
	public static GL GL => gl;

	public static File File => file;

	public static IInputContext Input => input;

	private static MenuBar menuBar;
	
	private static CanvasPanel canvasPanel;
	private static LayersPanel layersPanel;
	private static ScenesPanel scenesPanel;
	private static TilePickerPanel tilePickerPanel;
	private static TilesetsPanel tilesetsPanel;
	private static TileFillModal tileFillModal;
	private static EntitiesPanel entitiesPanel;
	private static TemplatesPanel templatesPanel;
	private static ConfirmModal confirmModal;
	private static ReloadFileModal reloadFileModal;
	private static NewProjectModal newProjectModal;

	private static File file;
	private static Scene selectedScene;
	private static Layer selectedLayer;
	private static Entity selectedEntity;
	private static Tileset selectedTileset;
	
	private static IWindow window;
	private static ImGuiController controller;
	private static GL gl;
	private static IInputContext input;
	private static bool requestClose;
	private static float deltaTime;
	private static bool firstLoop;
	private static Texture icon;
	private static Queue<Panel> focusPanelQueue;
	private static Queue<Action> threadMessages;
	private static List<ProjectEditorState> recentProjects;
	
	public static int Main(string[] args) {
		DateTime now = DateTime.Now;
		string logFilePath = Path.Combine(GetAppDataDirectory(),
			$"logs/{now.Year:0000}-{now.Month:00}-{now.Day:00}-{now.Hour:00}-{now.Minute:00}.txt");
		
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Console()
			.WriteTo.File(logFilePath, flushToDiskInterval: TimeSpan.FromSeconds(1))
			.CreateLogger();
		
		threadMessages = new Queue<Action>();

		try {
			WindowOptions options = WindowOptions.Default with {
				WindowState = WindowState.Maximized,
				Title = "L2D",
				API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(4, 1)),
				IsVisible = false
			};
			window = Window.Create(options);
			window.VSync = false;
			window.FramesPerSecond = 0;
			window.UpdatesPerSecond = 0;
			window.Load += Load;
			window.Update += Update;
			window.Render += Render;
			window.Closing += Closing;

			window.Run();
			window.Dispose();
		} catch(Exception e) {
			Log.Fatal(e, "Program crashed!");
			Log.CloseAndFlush();
			return 1;
		}
		Log.CloseAndFlush();
		return 0;
	}

	private static unsafe void Load() {
		Log.Information("Program loading...");
		try {
			gl = window.CreateOpenGL();
			input = window.CreateInput();
			
			SetWindowIcon();
			ChangeWin32DarkMode(true);
			// SetWin32Color(0x00261f1f); // doesn't work because microslop sucks :0
			
			window.WindowState = WindowState.Maximized;

			controller = new ImGuiController(gl, window, input);
			focusPanelQueue = new();

			menuBar = new MenuBar();
			canvasPanel = new CanvasPanel();
			layersPanel = new LayersPanel();
			scenesPanel = new ScenesPanel();
			tilePickerPanel = new TilePickerPanel();
			tilesetsPanel = new TilesetsPanel();
			tileFillModal = new TileFillModal();
			entitiesPanel = new EntitiesPanel();
			templatesPanel = new TemplatesPanel();
			confirmModal = new ConfirmModal();
			reloadFileModal = new ReloadFileModal();
			newProjectModal = new NewProjectModal();

			recentProjects = new List<ProjectEditorState>();
			LoadRecentProjects();
			
			Tilemap.CreateShaders();

			firstLoop = true;

			gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			gl.Viewport(window.FramebufferSize);
			gl.ClearColor(Color.FromArgb(255, 0, 0, 0));
			gl.Clear((uint) ClearBufferMask.ColorBufferBit);
			
			controller.Render();
			
			foreach(string arg in System.Environment.GetCommandLineArgs()) {
				if(file == null && arg.EndsWith(".l2d")) {
					OpenFile(arg);
				}
			}
		} catch(Exception e) {
			Log.Fatal(e, "Program crashed!");
			Log.CloseAndFlush();
			Environment.Exit(1);
		}
	}

	private static void Update(double deltaTime) {
		if(window.IsClosing) {
			window.IsClosing = false;
		}
	}

	private static void Render(double deltaTime) {
		try {
			controller.Update((float)deltaTime);
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			gl.Viewport(window.FramebufferSize);
			gl.ClearColor(Color.FromArgb(255, 0, 0, 0));
			gl.Clear((uint) ClearBufferMask.ColorBufferBit);

			Program.deltaTime = (float)deltaTime;

			ImGui.DockSpaceOverViewport();
			
			if(firstLoop) {
				window.IsVisible = true;
				firstLoop = false;
				SetSelectedLayer(SelectedLayer); // so that tickerpicker/entities panels are correctly focused on start
			}
			
			if(focusPanelQueue.Count > 0) {
				Panel panel = focusPanelQueue.Dequeue();
				ImGui.SetWindowFocus(panel.Title);
			}

			menuBar.Execute();

			templatesPanel.Execute();
			tilesetsPanel.Execute();
			canvasPanel.Execute();
			scenesPanel.Execute();
			layersPanel.Execute();
			entitiesPanel.Execute();
			tilePickerPanel.Execute();
			

			if(file != null && ImGui.IsKeyDown(ImGuiKey.LeftCtrl)) {
				if(ImGui.IsKeyPressed(ImGuiKey.S)) {
					SaveFile();
				}

				if(ImGui.IsKeyPressed(ImGuiKey.Q)) {
					if(file.UnsavedChanges) {
						Program.ConfirmModal.Open(
							"Confirm Quit",
							"You have unsaved changes.\nAre you sure you want to quit?",
							Close
						);
					} else {
						Close();
					}
				}

				if(ImGui.IsKeyPressed(ImGuiKey.O)) {
					if(file.UnsavedChanges) {
						Program.ConfirmModal.Open(
							"Confirm Open",
							"You have unsaved changes.\nAre you sure you want to open another file?",
							OpenFileDialog
						);
					} else {
						OpenFileDialog();
					}
				}

				if(ImGui.IsKeyPressed(ImGuiKey.R)) {
					if(file.UnsavedChanges) {
						Program.ConfirmModal.Open(
							"Confirm Reload",
							"You have unsaved changes.\nAre you sure you want to reload file from disk?",
							ReloadFile
						);
					} else {
						ReloadFile();
					}
				}

				if(ImGui.IsKeyPressed(ImGuiKey.N)) {
					if(file.UnsavedChanges) {
						Program.ConfirmModal.Open(
							"Confirm New File",
							"You have unsaved changes.\nAre you sure you want to create a new file?",
							newProjectModal.Open
						);
					} else {
						newProjectModal.Open();
					}
				}

				if(ImGui.IsKeyPressed(ImGuiKey.Z)) {
					if(file.WillUndoChangeContext()) {
						Program.ConfirmModal.Open(
							"Undo changes",
							file.GetUndoMessage(),
							file.Undo
						);
					} else {
						file.Undo();
					}
				}
				
				if(ImGui.IsKeyPressed(ImGuiKey.Y)) {
					if(file.WillRedoChangeContext()) {
						Program.ConfirmModal.Open(
							"Redo changes",
							file.GetRedoMessage(),
							file.Redo
						);
					} else {
						file.Redo();
					}
				}
			}

			confirmModal.Body();
			tileFillModal.Body();
			newProjectModal.Body();
			reloadFileModal.Body();

			if(file == null) {
				Launcher();
			}

			lock(threadMessages) {
				while(threadMessages.Count > 0) {
					Action action = threadMessages.Dequeue();
					action?.Invoke();
				}
			}

			FileDialog.CompleteThreads();

			controller.Render();

			UpdateMouseCursor();

			if(requestClose) {
				Closing();
			}
		} catch(Exception e) {
			Log.Fatal(e, "Program crashed!");
			Log.CloseAndFlush();
			Environment.Exit(1);
		}
	}

	private static void Closing() {
		if(!requestClose && Program.File != null && Program.File.UnsavedChanges) {
			Program.ConfirmModal.Open(
				"Confirm Quit",
				"You have unsaved changes.\nAre you sure you want to quit?",
				Program.Close
			);
			window.IsClosing = false;
			return;
		} else {
			window.IsClosing = true;
		}
		Log.Information("Program closing...");
		try {
			if(file != null) {
				UpdateProjectState(file);
			}

			SaveRecentProjects();

			controller?.SaveSettings();

			controller?.Dispose();
			input?.Dispose();
			gl?.Dispose();
		} catch(Exception e) {
			Log.Fatal(e, "Program crashed!");
			Log.CloseAndFlush();
			Environment.Exit(1);
		}
	}

	public static void Close() {
		requestClose = true;
	}

	public static void Focus(Panel panel) {
		focusPanelQueue.Enqueue(panel);
	}

	public static void SetSelectedScene(Scene scene) {
		selectedScene = scene;
		if(scene != null) {
			if(scene.LastActiveLayer != null) {
				SetSelectedLayer(scene.LastActiveLayer);
			} else if(scene.Root.ChildrenCount > 0) {
				SetSelectedLayer(scene.Root.GetChild(0));
			} else {
				SetSelectedLayer(null);
			}
		} else {
			SetSelectedLayer(null);
		}
	}
	
	public static void SetSelectedLayer(Layer layer) {
		if(layer != null && (selectedScene == null || layer.Scene != selectedScene)) {
			selectedLayer = null;
			SetSelectedEntity(null);
			return;
		}
		selectedLayer = layer;
		if(selectedScene != null) {
			selectedScene.LastActiveLayer = layer;
		}
		SetSelectedEntity(null);

		if(selectedLayer != null) {
			if(selectedLayer.Type == LayerType.Entities) {
				Focus(entitiesPanel);
			} else if(selectedLayer.Type == LayerType.Tiles) {
				Focus(tilePickerPanel);
			}
		}
	}
	
	public static void SetSelectedEntity(Entity entity) {
		selectedEntity = entity;
		if(selectedEntity != null) {
			Focus(entitiesPanel);
		}
	}
	
	public static void SetSelectedTileset(Tileset tileset) {
		selectedTileset = tileset;
	}

	public static void NewFile(string filePath) {
		if(file != null) {
			UpdateProjectState(file);
		}
		
		SetSelectedScene(null);
		SetSelectedTileset(null);
		
		file?.Dispose();
		
		file = new File(filePath);
		file.New();
		
		Program.UpdateWindowTitle();

		UpdateProjectState(file);
	}

	public static void OpenFile(string filePath) {
		if(file != null) {
			UpdateProjectState(file);
		}
		
		SetSelectedScene(null);
		SetSelectedTileset(null);
		
		file?.Dispose();
		
		file = new File(filePath);
		
		Program.UpdateWindowTitle();
		
		file.Read();

		ApplyProjectState(file);
		UpdateProjectState(file);
	}

	public static void OpenFileDialog() {
		FileDialog.Open(Path.GetDirectoryName(Program.File.GetPath()), Program.FILE_EXTENSION, result => {
			if(result != null) Program.OpenFile(result);
		});
	}

	public static void SaveFile(string filePath) {
		if(file == null) return;
		file.SetPath(filePath);
		file.Write();
		UpdateProjectState(file);
	}

	public static void SaveFileDialog() {
		if(file == null) return;
		FileDialog.Save(Path.GetDirectoryName(Program.File.GetPath()), Program.FILE_EXTENSION, result => {
			if(result != null) Program.SaveFile(result);
		});
	}

	public static void SaveFile() {
		if(file == null) return;
		file.Write();
		UpdateProjectState(file);
	}

	public static void ReloadFile() {
		if(file == null) return;
		UpdateProjectState(file);
		SetSelectedScene(null);
		SetSelectedTileset(null);
		file.Read();
		ApplyProjectState(file);
	}

	internal static void UpdateWindowTitle() {
		if(file == null) {
			window.Title = $"L2D";
		} else if(file.UnsavedChanges) {
			window.Title = $"L2D - {file.GetFileName()}*";
		} else {
			window.Title = $"L2D - {file.GetFileName()}";
		}
	}

	private static void UpdateMouseCursor() {
		StandardCursor cur = StandardCursor.Default;
		switch(ImGui.GetMouseCursor()) {
			default:
			case ImGuiMouseCursor.None:
			case ImGuiMouseCursor.Arrow:
				cur = StandardCursor.Default;
				break;
			case ImGuiMouseCursor.Hand:
				cur = StandardCursor.Hand;
				break;
			case ImGuiMouseCursor.NotAllowed:
				cur = StandardCursor.NotAllowed;
				break;
			case ImGuiMouseCursor.TextInput:
				cur = StandardCursor.IBeam;
				break;
			case ImGuiMouseCursor.ResizeAll:
				cur = StandardCursor.ResizeAll;
				break;
			case ImGuiMouseCursor.ResizeNS:
				cur = StandardCursor.VResize;
				break;
			case ImGuiMouseCursor.ResizeEW:
				cur = StandardCursor.HResize;
				break;
			case ImGuiMouseCursor.ResizeNESW:
				cur = StandardCursor.NeswResize;
				break;
			case ImGuiMouseCursor.ResizeNWSE:
				cur = StandardCursor.NwseResize;
				break;
			case (ImGuiMouseCursor)10:
				cur = StandardCursor.Crosshair;
				break;
		}
		input.Mice[0].Cursor.StandardCursor = cur;
	}

	private static void Launcher() {
		var style = ImGui.GetStyle();
		if(!ImGui.IsPopupOpen("Launch")) {
			ImGui.OpenPopup("Launch", ImGuiPopupFlags.AnyPopupLevel);
		}
		ImGui.SetNextWindowSize(new Vector2(500, 600));
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F));
		if(ImGui.BeginPopupModal("Launch", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar)) {
			float iconSize = 200;
			ImGui.PushStyleVar(ImGuiStyleVar.SeparatorTextAlign, new Vector2(0.5F, 0.5F));
			ImGui.SeparatorText(TITLE);
			ImGui.PopStyleVar();
			ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X / 2 - iconSize / 2);
			ImGui.Image((IntPtr)icon.Handle, new Vector2(iconSize, iconSize));
			foreach(string str in AUTHORS) {
				ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X / 2 - ImGui.CalcTextSize(str).X / 2);
				ImGui.Text(str);
			}
			for(int i = 0; i < 6; i++) ImGui.Spacing();
			ImGui.SeparatorText("Recent Projects");
			ImGui.BeginChild("recent-projects", ImGui.GetContentRegionAvail() - new Vector2(0, ImGui.GetTextLineHeight() + style.FramePadding.Y * 2 + 6), ImGuiChildFlags.Borders | ImGuiChildFlags.FrameStyle);

			int removeIndex = -1;
			
			float width = ImGui.GetContentRegionAvail().X;
			for(int i = recentProjects.Count - 1; i >= 0; i--) {
				ImGui.PushID(i);
				string fileName = Path.GetFileName(recentProjects[i].Path);
				Vector2 cur = ImGui.GetCursorPos();
				ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, 0.5F));
				if(ImGui.Selectable(fileName, false, ImGuiSelectableFlags.AllowOverlap, new Vector2(width, 24))) {
					Program.OpenFile(recentProjects[i].Path);
				}
				ImGui.PopStyleVar();
				if(ImGui.BeginItemTooltip()) {
					ImGui.Text("Full Path: ");
					ImGui.SameLine();
					ImGui.Text(recentProjects[i].Path);
					ImGui.Text("Last Opened: ");
					ImGui.SameLine();
					ImGui.Text(recentProjects[i].LastOpened.ToString());
					ImGui.EndTooltip();
				}
				ImGui.SetCursorPos(cur + new Vector2(ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(Codicons.Close).X - ImGui.GetStyle().FramePadding.X * 2, 0));
				ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1,1,1,0));
				if(ImGui.Button(Codicons.Close)) {
					removeIndex = i;
				}
				ImGui.SetItemTooltip("Remove");
				ImGui.PopStyleColor();
				ImGui.PopID();
			}

			if(removeIndex >= 0) {
				recentProjects.RemoveAt(removeIndex);
			}
			
			if(ImGui.BeginPopupContextWindow()) {
				if(ImGui.MenuItem("Clear")) {
					recentProjects.Clear();
					SaveRecentProjects();
				}
				ImGui.EndPopup();
			}
			ImGui.EndChild();
			
			if(ImGui.Button("New")) {
				newProjectModal.Open();
			}
			newProjectModal.Body();
			ImGui.SameLine();
			if(ImGui.Button("Open")) {
				FileDialog.Open("", Program.FILE_EXTENSION, result => {
					if(result != null) Program.OpenFile(result);
				});
			}
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Quit").X - style.FramePadding.X * 2.0F);
			if(ImGui.Button("Quit")) {
				Close();
			}
			ImGui.EndPopup();
		}
	}

	public static string GetAppDataDirectory() {
		string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "L2D");
		if(!Directory.Exists(appDataDir)) {
			Directory.CreateDirectory(appDataDir);
		}
		return appDataDir;
	}

	private static void LoadRecentProjects() {
		string appDataDir = GetAppDataDirectory();
		string projectInfoFile = Path.Combine(appDataDir, "projects.dat");

		if(!System.IO.File.Exists(projectInfoFile)) return;
		StreamReader reader = null;
		try {
			reader = System.IO.File.OpenText(projectInfoFile);

			ProjectEditorState info = null;

			string line = null;
			while((line = reader.ReadLine()) != null) {
				string trimmed = line.Trim();
				if(trimmed.StartsWith('<') && trimmed.EndsWith('>')) {
					string path = Path.GetFullPath(trimmed.Trim('<', '>')).Replace('\\', '/');
					info = new ProjectEditorState(path);
					recentProjects.Add(info);
				} else {
					if(info == null) continue;
					string[] split = trimmed.Split('=');
					if(split.Length < 2) continue;
					switch(split[0]) {
						case "LastOpened":
							if(!DateTime.TryParse(split[1], out info.LastOpened)) {
								Log.Error("Failed to parse DateTime!");
							}
							break;
						case "CameraPosition.X":
							float.TryParse(split[1], out info.CameraPosition.X);
							break;
						case "CameraPosition.Y":
							float.TryParse(split[1], out info.CameraPosition.Y);
							break;
						case "CameraZoom":
							float.TryParse(split[1], out info.CameraZoom);
							break;
						case "SelectedScene":
							if(!int.TryParse(split[1], out info.SelectedScene)) {
								info.SelectedScene = -1;
							}
							break;
						case "SelectedLayer":
							if(!int.TryParse(split[1], out info.SelectedLayer)) {
								info.SelectedLayer = -1;
							}
							break;
						case "TilesetPreviewScale":
							if(!int.TryParse(split[1], out info.TilesetPreviewScale)) {
								info.TilesetPreviewScale = 3;
							}
							break;
					}
				}
			}
		} catch(Exception e) {
			Log.Error(e, "Failed to load recent projects!");
		}
		reader?.Close();
	}
	
	private static void SaveRecentProjects() {
		string appDataDir = GetAppDataDirectory();
		string projectInfoFile = Path.Combine(appDataDir, "projects.dat");

		StreamWriter writer = null;

		try {
			writer = System.IO.File.CreateText(projectInfoFile);
			foreach(var p in recentProjects) {
				writer.WriteLine($"<{p.Path}>");
				writer.WriteLine($"LastOpened={p.LastOpened}");
				writer.WriteLine($"CameraPosition.X={p.CameraPosition.X}");
				writer.WriteLine($"CameraPosition.Y={p.CameraPosition.Y}");
				writer.WriteLine($"CameraZoom={p.CameraZoom}");
				writer.WriteLine($"SelectedScene={p.SelectedScene}");
				writer.WriteLine($"SelectedLayer={p.SelectedLayer}");
				writer.WriteLine($"TilesetPreviewScale={p.TilesetPreviewScale}");
			}
		} catch(Exception e) {
			Log.Error(e, "Failed to save recent projects!");
		}

		writer?.Close();
	}

	private static ProjectEditorState GetProjectState(string path) {
		foreach(var p in recentProjects) {
			if(p.Path == path) return p;
		}
		return null;
	}

	private static void ApplyProjectState(File file) {
		var info = GetProjectState(file.GetPath());
		if(info != null) {
			SetSelectedScene(file?.World?.GetScene(info.SelectedScene));
			SetSelectedLayer(selectedScene?.GetLayer(info.SelectedLayer));
			canvasPanel.Camera = info.CameraPosition;
			canvasPanel.ZoomFactor = info.CameraZoom;
			tilesetsPanel.PreviewScale = info.TilesetPreviewScale;
			info.LastOpened = DateTime.Now;
		}
	}
	
	private static ProjectEditorState UpdateProjectState(File file) {
		string path = file.GetPath();
		ProjectEditorState info = null;
		foreach(var p in recentProjects) {
			if(p.Path == path) {
				info = p;
				break;
			}
		}
		
		if(info == null) {
			info = new ProjectEditorState(path);
		} else {
			recentProjects.Remove(info);
		}
		recentProjects.Add(info);
		
		info.LastOpened = DateTime.Now;
		info.CameraPosition = canvasPanel.Camera;
		info.CameraZoom = canvasPanel.ZoomFactor;
		info.TilesetPreviewScale = tilesetsPanel.PreviewScale;
		if(selectedScene != null) {
			info.SelectedScene = selectedScene.World.GetSceneIndex(selectedScene);
		} else {
			info.SelectedScene = -1;
		}
		if(selectedLayer != null) {
			info.SelectedLayer = selectedLayer.Scene.GetLayerTreeIndex(selectedLayer);
		} else {
			info.SelectedLayer = -1;
		}
		
		return info;
	}

	public static void SendMessage(Action action) {
		lock(threadMessages) {
			threadMessages.Enqueue(action);
		}
	}
	
	public static Stream GetEmbeddedResourceStream(Assembly assembly, string location) {
		location = assembly.GetName().Name + "." + location;
		Stream stream = assembly.GetManifestResourceStream(location);
		return stream;
	}

	private static void SetWindowIcon() {
		Stream stream = GetEmbeddedResourceStream(Assembly.GetExecutingAssembly(), "Resources.Icon.png");
		ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
		Texture texture = Texture.LoadFromPixels(image.Data, image.Width, image.Height);
		RawImage rawImage = new RawImage(image.Width, image.Height, image.Data);
		window.SetWindowIcon(ref rawImage);
		icon = texture;
	}
	
	private static void ChangeWin32DarkMode(bool dark) {
		object d = dark;
		if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			DwmSetWindowAttribute(window.Native.Win32.Value.Hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref d, 8);
		}
	}

	private static void SetWin32Color(uint color) {
		object d = color;
		if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			DwmSetWindowAttribute(window.Native.Win32.Value.Hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, ref d, 4);
		}
	}
    
	[DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
	private static extern int DwmSetWindowAttribute(
		IntPtr hwnd,
		DWMWINDOWATTRIBUTE dwAttribute,
		[In] ref object pvAttribute,
		int cbAttribute
	);
    
	private enum DWMWINDOWATTRIBUTE : uint {
		DWMWA_NCRENDERING_ENABLED = 1,
		DWMWA_NCRENDERING_POLICY,
		DWMWA_TRANSITIONS_FORCEDISABLED,
		DWMWA_ALLOW_NCPAINT,
		DWMWA_CAPTION_BUTTON_BOUNDS,
		DWMWA_NONCLIENT_RTL_LAYOUT,
		DWMWA_FORCE_ICONIC_REPRESENTATION,
		DWMWA_FLIP3D_POLICY,
		DWMWA_EXTENDED_FRAME_BOUNDS,
		DWMWA_HAS_ICONIC_BITMAP,
		DWMWA_DISALLOW_PEEK,
		DWMWA_EXCLUDED_FROM_PEEK,
		DWMWA_CLOAK,
		DWMWA_CLOAKED,
		DWMWA_FREEZE_REPRESENTATION,
		DWMWA_PASSIVE_UPDATE_MODE,
		DWMWA_USE_HOSTBACKDROPBRUSH,
		DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
		DWMWA_WINDOW_CORNER_PREFERENCE = 33,
		DWMWA_BORDER_COLOR,
		DWMWA_CAPTION_COLOR,
		DWMWA_TEXT_COLOR,
		DWMWA_VISIBLE_FRAME_BORDER_THICKNESS,
		DWMWA_SYSTEMBACKDROP_TYPE,
		DWMWA_REDIRECTIONBITMAP_ALPHA,
		DWMWA_BORDER_MARGINS,
		DWMWA_LAST,
		
	}
	
}

public class ProjectEditorState {
	public string Path;
	public DateTime LastOpened;
	public Vector2 CameraPosition;
	public float CameraZoom;
	public int SelectedScene;
	public int SelectedLayer;
	public int TilesetPreviewScale;
	public ProjectEditorState(string path) {
		Path = path;
		LastOpened = DateTime.Now;
		CameraPosition = Vector2.Zero;
		CameraZoom = 0;
		SelectedScene = -1;
		SelectedLayer = -1;
		TilesetPreviewScale = 3;
	}
}