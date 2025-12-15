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
	public static PropertiesPanel PropertiesPanel => propertiesPanel;
	public static ScenesPanel ScenesPanel => scenesPanel;
	public static TilePickerPanel TilePickerPanel => tilePickerPanel;
	public static TilesetsPanel TilesetsPanel => tilesetsPanel;

	// public static object? Selected {
	// 	get => selected;
	// 	set => selected = value;
	// }
	
	public static Scene ActiveScene {
		get => activeScene;
		set {
			if(activeScene != null) activeScene.LastActiveLayer = activeLayer;
			activeScene = value;
			if(activeScene != null) {
				if(activeScene.LastActiveLayer != null) {
					activeLayer = activeScene.LastActiveLayer;
				} else if(activeScene.Layers.Count > 0) {
					activeLayer = activeScene.Layers[0];
				}
			}
		}
	}
	
	public static Layer ActiveLayer {
		get => activeLayer;
		set => activeLayer = value;
	}

	public static File File => file;

	private static MenuBar menuBar;
	
	private static CanvasPanel canvasPanel;
	private static LayersPanel layersPanel;
	private static ObjectsPanel objectsPanel;
	private static PropertiesPanel propertiesPanel;
	private static ScenesPanel scenesPanel;
	private static TilePickerPanel tilePickerPanel;
	private static TilesetsPanel tilesetsPanel;

	private static File file;
	private static Scene activeScene;
	private static Layer activeLayer;

	private static IWindow window;
	private static ImGuiController controller;
	private static GL gl;
	private static IInputContext input;
	private static bool requestClose;
	
	public static void Main(string[] args) {
		WindowOptions options = WindowOptions.Default with {
			WindowState = WindowState.Maximized,
			Title = "L2D"
		};
		window = Window.Create(options);
		
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
		propertiesPanel = new PropertiesPanel();
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
		
		canvasPanel.Execute();
		layersPanel.Execute();
		objectsPanel.Execute();
		propertiesPanel.Execute();
		scenesPanel.Execute();
		tilePickerPanel.Execute();
		tilesetsPanel.Execute();

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

	// public static T GetSelectedAs<T>() where T : class {
	// 	return selected as T;
	// }
	// 
	// public static bool IsSelectedAs<T>() where T : class {
	// 	return selected != null && selected is T;
	// }
	
}