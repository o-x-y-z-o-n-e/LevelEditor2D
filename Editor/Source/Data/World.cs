using System.Runtime.InteropServices.ComTypes;
using System.Xml.Linq;

namespace L2D; 

public class World {

	public string Name {
		get => name;
		set => name = value;
	}
	
	public int TileWidth {
		get => tileWidth;
		set => tileWidth = value;
	}
	
	public int TileHeight {
		get => tileHeight;
		set => tileHeight = value;
	}

	public int TilesetCount => tilesets.Count;
	public int SceneCount => scenes.Count;

	public int MaxTilesetSlots => maxTilesetSlots;

	public IEnumerable<Tileset> Tilesets => tilesets;
	public IEnumerable<Scene> Scenes => scenes;

	private File file;
	private string name;
	private int tileWidth;
	private int tileHeight;
	private List<Tileset> tilesets;
	private List<Scene> scenes;
	private int maxTilesetSlots;
	private bool disposed;

	internal World(File file) {
		this.file = file;
		
		name = "New World";
		tileWidth = 16;
		tileHeight = 16;
		tilesets = new();
		scenes = new();
		maxTilesetSlots = 16;
	}
	
	public Tileset GetTileset(int index) {
		if(index < 0 || index >= tilesets.Count) return null;
		return tilesets[index];
	}

	public Scene GetScene(int index) {
		if(index < 0 || index >= scenes.Count) return null;
		return scenes[index];
	}

	public int GetSceneIndex(Scene scene) {
		return scenes.IndexOf(scene);
	}

	public Scene CreateScene(string id, int width, int height, int x, int y) {
		foreach(var s in scenes) if(s.ID == id) return null;
		Scene scene = new Scene(file);
		scene.ID = id;
		scene.WorldX = x;
		scene.WorldY = y;
		scene.TileCountX = width;
		scene.TileCountY = height;
		scene.AddLayer();
		scenes.Add(scene);
		return scene;
	}
	
	public void SwapScenes(int index1, int index2) {
		if(index1 < 0 || index1 >= scenes.Count || index2 < 0 || index2 >= scenes.Count) return;
		var t = scenes[index1];
		scenes[index1] = scenes[index2];
		scenes[index2] = t;
	}
	
	public void SwapScenes(Scene scene1, Scene scene2) {
		SwapScenes(scenes.IndexOf(scene1), scenes.IndexOf(scene2));
	}

	public void DeleteScene(int index) {
		if(index < 0 || index >= scenes.Count) return;
		DeleteScene(scenes[index]);
	}
	
	public void DeleteScene(Scene scene) {
		if(!scenes.Contains(scene)) return;
		scenes.Remove(scene);
		scene.Dispose();
		if(scene == Program.SelectedScene) {
			Program.SetSelectedScene(null);
		}
	}

	internal void Parse(XElement worldElement) {
		name = worldElement.Attribute("name").Value;
		tileWidth = worldElement.Attribute("tile_width").ParseAsInt(16);
		tileHeight = worldElement.Attribute("tile_height").ParseAsInt(16);
		foreach(var tilesetElement in worldElement.Element("tilesets").Elements("tileset")) {
			Tileset tileset = new Tileset(file);
			tileset.Parse(tilesetElement);
			tilesets.Add(tileset);
		}
		foreach(var sceneElement in worldElement.Element("scenes").Elements("scene")) {
			Scene scene = new Scene(file);
			scene.Parse(sceneElement);
			scenes.Add(scene);
		}
	}
	
	internal XElement Serialize() {
		XElement rootElement = new XElement("world");
		rootElement.Add(
			new XAttribute("version", Program.VERSION_STRING),
			new XAttribute("name", name),
			new XAttribute("tile_width", tileWidth),
			new XAttribute("tile_height", tileHeight)
		);
		var tilesetsParent = new XElement("tilesets");
		foreach(var tileset in tilesets) {
			tilesetsParent.Add(tileset.Serialize());
		}
		rootElement.Add(tilesetsParent);
		var scenesParent = new XElement("scenes");
		foreach(var scene in scenes) {
			scenesParent.Add(scene.Serialize());
		}
		rootElement.Add(scenesParent);
		return rootElement;
	}
	
	public void Dispose() {
		if(disposed) return;
		for(int i = 0; i < tilesets.Count; i++) tilesets[i]?.Dispose();
		for(int i = 0; i < scenes.Count; i++) scenes[i]?.Dispose();
		disposed = true;
	}

}