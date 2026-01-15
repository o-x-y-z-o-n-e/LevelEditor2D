using System;
using System.Drawing;
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
	
	public static MenuBar MenuBar => menuBar;
	
	public static CanvasPanel CanvasPanel => canvasPanel;
	public static LayersPanel LayersPanel => layersPanel;
	public static ObjectsPanel ObjectsPanel => objectsPanel;
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
	private static ObjectsPanel objectsPanel;
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
	
	static bool test = false;
	
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
		objectsPanel = new ObjectsPanel();
		scenesPanel = new ScenesPanel();
		tilePickerPanel = new TilePickerPanel();
		tilesetsPanel = new TilesetsPanel();
		newProjectModal = new NewProjectModal();

		foreach(string arg in System.Environment.GetCommandLineArgs()) {
			if(file == null && arg.EndsWith(".l2d")) {
				OpenFile(arg);
			}
		}

		if(file == null) {
			newProjectModal.Open();
		}
	}

	private static void Render(double deltaTime) {
		controller.Update((float)deltaTime);
		gl.Viewport(window.FramebufferSize);
		gl.ClearColor(Color.FromArgb(255, 0, 0, 0));
		gl.Clear((uint) ClearBufferMask.ColorBufferBit);
		
		ImGui.DockSpaceOverViewport();
		
		menuBar.Execute();
		
		ImGui.ShowDemoWindow();
		
		tilesetsPanel.Execute();
		scenesPanel.Execute();
		layersPanel.Execute();
		objectsPanel.Execute();
		tilePickerPanel.Execute();
		canvasPanel.Execute();
		newProjectModal.Execute();
		
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
		if(selectedScene == null || (layer != null && !selectedScene.HasLayer(layer))) return;
		selectedLayer = layer;
		selectedScene.LastActiveLayer = layer;
		SetSelectedEntity(null);
	}
	
	public static void SetSelectedEntity(EntityDefinition entity) {
		
	}
	
	public static void SetSelectedTileset(Tileset tileset) {
		selectedTileset = tileset;
	}

	public static void NewFile(string filePath) {
		// TODO
	}

	public static void OpenFile(string filePath) {
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

	public static void AllowInput(bool enabled) {
		controller.AllowInput = enabled;
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
	
}