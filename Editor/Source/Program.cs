using System;
using System.Drawing;
using System.Numerics;
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
	public static NewProjectModal NewProjectModal => newProjectModal;
	
	public static Scene SelectedScene {
		get => selectedScene;
		set => SetSelectedScene(value);
	}
	
	public static Layer SelectedLayer {
		get => selectedLayer;
		set => SetSelectedLayer(value);
	}
	
	public static EntityDefinition SelectedEntity {
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
	private static NewProjectModal newProjectModal;

	private static File file;
	private static Scene selectedScene;
	private static Layer selectedLayer;
	private static EntityDefinition selectedEntity;
	private static Tileset selectedTileset;

	private static IWindow window;
	private static ImGuiController controller;
	private static GL gl;
	private static IInputContext input;
	private static bool requestClose;
	private static float deltaTime;

	private static List<string> recentProjects;
	
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
		newProjectModal = new NewProjectModal();

		recentProjects = new List<string>();
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
		tilePickerPanel.Execute();
		entitiesPanel.Execute();

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
	
	public static void SetSelectedEntity(EntityDefinition entity) {
		selectedEntity = entity;
	}
	
	public static void SetSelectedTileset(Tileset tileset) {
		selectedTileset = tileset;
	}

	public static void NewFile(string filePath) {
		file?.Dispose();
		
		file = new File(filePath);
		file.New();
		
		Program.UpdateWindowTitle();
		
		SetSelectedScene(null);
	}

	public static void OpenFile(string filePath) {
		file?.Dispose();
		
		file = new File(filePath);
		
		Program.UpdateWindowTitle();
		
		if(file != null) {
			file.Read();
			SetSelectedScene(file?.World?.GetScene(0));
		}
	}

	public static void SaveFile(string filePath) {
		file.SetPath(filePath);
		file.Write();
	}

	public static void SaveFile() {
		file.Write();
	}

	public static void ReloadFile() {
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
				if(ImGui.Selectable(recentProjects[i], false, ImGuiSelectableFlags.None, new Vector2(width, 24))) {
					Program.OpenFile(recentProjects[i]);
					break;
				}
				ImGui.SetItemTooltip(recentProjects[i]);
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

	private static void LoadRecentProjects() {
		// TODO
		recentProjects.Add("test1");
		recentProjects.Add("test2");
		recentProjects.Add("test3");
	}
	
	private static void SaveRecentProjects() {
		// TODO
	}
	
}