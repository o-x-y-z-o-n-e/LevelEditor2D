using System;
using System.Drawing;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace L2D;

public static class Program {

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

	public static GL GL => gl;

	public static File File => file;

	private static MenuBar menuBar;
	
	private static CanvasPanel canvasPanel;
	private static LayersPanel layersPanel;
	private static ObjectsPanel objectsPanel;
	private static ScenesPanel scenesPanel;
	private static TilePickerPanel tilePickerPanel;
	private static TilesetsPanel tilesetsPanel;

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
			Title = "L2D"
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

	private static void Load() {
		controller = new ImGuiController(
			gl = window.CreateOpenGL(), // load OpenGL
			window, // pass in our window
			input = window.CreateInput() // create an input context
		);

		menuBar = new MenuBar();
		canvasPanel = new CanvasPanel();
		layersPanel = new LayersPanel();
		objectsPanel = new ObjectsPanel();
		scenesPanel = new ScenesPanel();
		tilePickerPanel = new TilePickerPanel();
		tilesetsPanel = new TilesetsPanel();

		foreach(string arg in System.Environment.GetCommandLineArgs()) {
			if(file == null && arg.EndsWith(".l2d")) {
				file = new File(arg);
			}
		}

		if(file != null) file.Read();
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
		
		if(!test) {
			test = true;
			ImGui.SetWindowFocus("Tilesets");
		}

		controller.Render();

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

}