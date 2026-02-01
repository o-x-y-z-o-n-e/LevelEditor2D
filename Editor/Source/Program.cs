using System;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Input.Extensions;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace L2D;

public static class Program {

	public const int VERSION_MAJOR = 0;
	public const int VERSION_MINOR = 1;
	public const int VERSION_PATCH = 0;
	
	public static readonly string VERSION_STRING = $"{VERSION_MAJOR}.{VERSION_MINOR}.{VERSION_PATCH}";
	
	public const string FILE_EXTENSION = "l2d";

	public const int IMGUI_STRING_MAX = 1024;

	public static string Title {
		get => window.Title;
		set => window.Title = value;
	}

	public static float DeltaTime => deltaTime;
	
	public static MenuBar MenuBar => menuBar;
	
	public static CanvasPanel CanvasPanel => canvasPanel;
	public static LayersPanel LayersPanel => layersPanel;
	public static EntitiesPanel EntitiesPanel => entitiesPanel;
	public static ScenesPanel ScenesPanel => scenesPanel;
	public static TilePickerPanel TilePickerPanel => tilePickerPanel;
	public static TilesetsPanel TilesetsPanel => tilesetsPanel;
	public static TileFillModal TileFillModal => tileFillModal;
	public static ConfirmModal ConfirmModal => confirmModal;
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
	private static EntitiesPanel entitiesPanel;
	private static ScenesPanel scenesPanel;
	private static TilePickerPanel tilePickerPanel;
	private static TilesetsPanel tilesetsPanel;
	private static TileFillModal tileFillModal;
	private static ConfirmModal confirmModal;
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

	private static List<ProjectInfoState> recentProjects;
	
	public static void Main(string[] args) {
		WindowOptions options = WindowOptions.Default with {
			WindowState = WindowState.Maximized,
			Title = "L2D",
			API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(4, 1))
		};
		window = Window.Create(options);
		window.VSync = false;
		window.FramesPerSecond = 0;
		window.UpdatesPerSecond = 0;
		window.Load += Load;
		window.Render += Render;
		window.Closing += Closing;
		
		window.Run();
		window.Dispose();
	}

	private static unsafe void Load() {
		gl = window.CreateOpenGL();
		input = window.CreateInput();

		controller = new ImGuiController(gl, window, input);
		
		menuBar = new MenuBar();
		canvasPanel = new CanvasPanel();
		layersPanel = new LayersPanel();
		entitiesPanel = new EntitiesPanel();
		scenesPanel = new ScenesPanel();
		tilePickerPanel = new TilePickerPanel();
		tilesetsPanel = new TilesetsPanel();
		tileFillModal = new TileFillModal();
		confirmModal = new ConfirmModal();
		newProjectModal = new NewProjectModal();

		recentProjects = new List<ProjectInfoState>();
		LoadRecentProjects();

		foreach(string arg in System.Environment.GetCommandLineArgs()) {
			if(file == null && arg.EndsWith(".l2d")) {
				OpenFile(arg);
			}
		}
	}

	private static void Render(double deltaTime) {
		controller.Update((float)deltaTime);
		gl.Viewport(window.FramebufferSize);
		gl.ClearColor(Color.FromArgb(255, 0, 0, 0));
		gl.Clear((uint) ClearBufferMask.ColorBufferBit);

		Program.deltaTime = (float)deltaTime;
		
		ImGui.DockSpaceOverViewport();
		
		menuBar.Execute();
		
		// ImGui.ShowDemoWindow();
		
		tilesetsPanel.Execute();
		canvasPanel.Execute();
		scenesPanel.Execute();
		layersPanel.Execute();
		entitiesPanel.Execute();
		tilePickerPanel.Execute();
		
		if(ImGui.IsKeyDown(ImGuiKey.LeftCtrl)) {
			if(file != null) {
				if(ImGui.IsKeyPressed(ImGuiKey.S)) {
					SaveFile();
				}
			}
			if(ImGui.IsKeyPressed(ImGuiKey.Q)) {
				if(file != null && file.UnsavedChanges) {
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
				if(file != null && file.UnsavedChanges) {
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
				if(file != null && file.UnsavedChanges) {
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
				if(file != null && file.UnsavedChanges) {
					Program.ConfirmModal.Open(
						"Confirm New File",
						"You have unsaved changes.\nAre you sure you want to create a new file?",
						newProjectModal.Open
					);
				} else {
					newProjectModal.Open();
				}
			}
		}
		
		confirmModal.Body();
		tileFillModal.Body();
		newProjectModal.Body();

		if(file == null) {
			Launcher();
		}
		
		FileDialog.CompleteThreads();

		controller.Render();

		UpdateMouseCursor();
		
		if(requestClose) {
			window?.Close();
		}
	}

	private static void Closing() {
		controller?.Dispose();
		input?.Dispose();
		gl?.Dispose();

		if(file != null) {
			UpdateProjectInfo(file);
		}
		
		SaveRecentProjects();
	}

	public static void Close() {
		requestClose = true;
	}

	public static void SetSelectedScene(Scene scene) {
		selectedScene = scene;
		if(scene != null) {
			if(scene.LastActiveLayer != null) {
				SetSelectedLayer(scene.LastActiveLayer);
			} else if(scene.Layers.Count > 0) {
				SetSelectedLayer(scene.Layers[0]);
			} else {
				SetSelectedLayer(null);
			}
		} else {
			SetSelectedLayer(null);
		}
	}
	
	public static void SetSelectedLayer(Layer layer) {
		if(layer != null && (selectedScene == null || !selectedScene.HasLayer(layer))) {
			selectedLayer = null;
			SetSelectedEntity(null);
			return;
		}
		selectedLayer = layer;
		if(selectedScene != null) {
			selectedScene.LastActiveLayer = layer;
		}
		SetSelectedEntity(null);
	}
	
	public static void SetSelectedEntity(Entity entity) {
		selectedEntity = entity;
	}
	
	public static void SetSelectedTileset(Tileset tileset) {
		selectedTileset = tileset;
	}

	public static void NewFile(string filePath) {
		if(file != null) {
			UpdateProjectInfo(file);
		}
		
		SetSelectedScene(null);
		
		file?.Dispose();
		
		file = new File(filePath);
		file.New();
		
		Program.UpdateWindowTitle();

		UpdateProjectInfo(file);
	}

	public static void OpenFile(string filePath) {
		if(file != null) {
			UpdateProjectInfo(file);
		}
		
		SetSelectedScene(null);
		
		file?.Dispose();
		
		file = new File(filePath);
		
		Program.UpdateWindowTitle();
		
		if(file != null) {
			file.Read();
			SetSelectedScene(file?.World?.GetScene(0));
		}
		
		UpdateProjectInfo(file);
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
		UpdateProjectInfo(file);
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
		UpdateProjectInfo(file);
	}

	public static void ReloadFile() {
		if(file == null) return;
		UpdateProjectInfo(file);
		SetSelectedScene(null);
		file.Read();
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
		ImGui.SetNextWindowSize(new Vector2(600, 500));
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F));
		if(ImGui.BeginPopupModal("Launch", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar)) {
			ImGui.BeginChild("recent-projects", ImGui.GetContentRegionAvail() - new Vector2(0, ImGui.GetTextLineHeight() + style.FramePadding.Y * 2 + 6), ImGuiChildFlags.Borders | ImGuiChildFlags.FrameStyle);

			float width = ImGui.GetContentRegionAvail().X;
			for(int i = recentProjects.Count - 1; i >= 0; i--) {
				ImGui.PushID(i);
				string fileName = Path.GetFileName(recentProjects[i].Path);
				if(ImGui.Selectable(fileName, false, ImGuiSelectableFlags.None, new Vector2(width, 24))) {
					Program.OpenFile(recentProjects[i].Path);
				}
				ImGui.SetItemTooltip(recentProjects[i].Path);
				ImGui.PopID();
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

	private static string GetAppDataDirectory() {
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

		StreamReader reader = System.IO.File.OpenText(projectInfoFile);

		ProjectInfoState info = null;

		string line = null;
		while((line = reader.ReadLine()) != null) {
			string trimmed = line.Trim();
			if(trimmed.StartsWith('<') && trimmed.EndsWith('>')) {
				string path = Path.GetFullPath(trimmed.Trim('<', '>')).Replace('\\', '/');
				info = new ProjectInfoState(path);
				recentProjects.Add(info);
			} else {
				if(info == null) continue;
				string[] split = trimmed.Split('=');
				if(split.Length < 2) continue;
				switch(split[0]) {
					case "LastOpened":
						if(!DateTime.TryParse(split[1], out info.LastOpened)) {
							Console.WriteLine("Failed to parse DateTime");
						}
						break;
					case "CameraPosition.X":
						float.TryParse(split[1], out info.CameraPosition.X);
						break;
					case "CameraPosition.Y":
						float.TryParse(split[1], out info.CameraPosition.Y);
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
				}
			}
		}
		
		reader.Close();
	}
	
	private static void SaveRecentProjects() {
		string appDataDir = GetAppDataDirectory();
		string projectInfoFile = Path.Combine(appDataDir, "projects.dat");

		StreamWriter writer = System.IO.File.CreateText(projectInfoFile);
		
		foreach(var p in recentProjects) {
			writer.WriteLine($"<{p.Path}>");
			writer.WriteLine($"LastOpened={p.LastOpened}");
			writer.WriteLine($"CameraPosition.X={p.CameraPosition.X}");
			writer.WriteLine($"CameraPosition.Y={p.CameraPosition.Y}");
			writer.WriteLine($"SelectedScene={p.SelectedScene}");
			writer.WriteLine($"SelectedLayer={p.SelectedLayer}");
		}
		
		writer.Close();
	}

	private static ProjectInfoState GetProjectInfo(string path) {
		foreach(var p in recentProjects) {
			if(p.Path == path) return p;
		}
		return null;
	}
	
	private static ProjectInfoState UpdateProjectInfo(File file) {
		string path = file.GetPath();
		ProjectInfoState info = null;
		foreach(var p in recentProjects) {
			if(p.Path == path) {
				info = p;
				break;
			}
		}
		
		if(info == null) {
			info = new ProjectInfoState(path);
		} else {
			recentProjects.Remove(info);
		}
		recentProjects.Insert(0, info);
		
		info.LastOpened = DateTime.Now;
		info.CameraPosition = canvasPanel.Camera;
		if(selectedScene != null) {
			info.SelectedScene = selectedScene.World.GetSceneIndex(selectedScene);
		} else {
			info.SelectedScene = -1;
		}
		if(selectedLayer != null) {
			info.SelectedLayer = selectedLayer.Scene.GetLayerIndex(selectedLayer);
		} else {
			info.SelectedLayer = -1;
		}
		
		return info;
	}
	
}

public class ProjectInfoState {
	public string Path;
	public DateTime LastOpened;
	public Vector2 CameraPosition;
	public int SelectedScene;
	public int SelectedLayer;
	public ProjectInfoState(string path) {
		Path = path;
		LastOpened = DateTime.Now;
		CameraPosition = Vector2.Zero;
		SelectedScene = -1;
		SelectedLayer = -1;
	}
}