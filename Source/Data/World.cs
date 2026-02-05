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

	public int GetTilesetIndex(Tileset tileset) {
		return tilesets.IndexOf(tileset);
	}

	public Scene GetScene(int index) {
		if(index < 0 || index >= scenes.Count) return null;
		return scenes[index];
	}

	public int GetSceneIndex(Scene scene) {
		return scenes.IndexOf(scene);
	}

	public Scene CreateScene(string id, int width, int height, int x, int y, bool blankLayer = true) {
		foreach(var s in scenes) if(s.ID == id) return null;
		Scene scene = new Scene(file);
		scene.ID = id;
		scene.WorldX = x;
		scene.WorldY = y;
		scene.Resize(width, height);
		if(blankLayer) scene.AddLayer(LayerType.Tiles);
		scenes.Add(scene);
		return scene;
	}

	internal void InsertScene(Scene scene, int index) {
		if(scene == null || scene.World != this || scenes.Contains(scene)) return;
		scenes.Insert(index, scene);
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

	public Scene CopyScene(int index, string newID, int x, int y) {
		if(index < 0 || index >= scenes.Count) return null;
		return CopyScene(scenes[index], newID, x, y);
	}
	
	public Scene CopyScene(Scene srcScene, string newID, int x, int y) {
		if(!scenes.Contains(srcScene)) return null;
		Scene newScene = CreateScene(newID, srcScene.TileCountX, srcScene.TileCountY, x, y, false);
		
		// TODO: groups

		for(int i = 0; i < srcScene.Tilesets.Count; i++) {
			var src = srcScene.Tilesets[i];
			var link = new TilesetLink(file, src.Slot);
			link.Tileset = src.Tileset;
			newScene.Tilesets.Add(link);
		}
		
		for(int i = 0; i < srcScene.LayerCount; i++) {
			Layer srcLayer = srcScene.Layers[i];
			Layer newLayer = newScene.AddLayer(srcLayer.Type);
			
			newLayer.Name = srcLayer.Name;
			newLayer.Visible = srcLayer.Visible;
			if(srcLayer.Type == LayerType.Tiles) {
				for(int ty = 0; ty < srcScene.TileCountY; ty++) {
					for(int tx = 0; tx < srcScene.TileCountX; tx++) {
						newLayer.Tilemap.Grid[tx, ty] = srcLayer.Tilemap.Grid[tx, ty];
					}
				}
			} else if(srcLayer.Type == LayerType.Entities) {
				foreach(var srcEntity in srcLayer.Entities.All) {
					var newEntity = newLayer.Entities.Add();
					newEntity.Name = srcEntity.Name;
					newEntity.Type = srcEntity.Type;
					newEntity.Position = srcEntity.Position;
					newEntity.Size = srcEntity.Size;
					foreach(var p in srcEntity.Properties.All) {
						var newP = newEntity.Properties.Add(p.Name, p.Type);
						newP.String = p.String;
						newP.Integer = p.Integer;
						newP.Float = p.Float;
						newP.Boolean = p.Boolean;
					}
				}
			}
		}
		
		return newScene;
	}

	public void RemoveScene(int index) {
		if(index < 0 || index >= scenes.Count) return;
		RemoveScene(scenes[index]);
	}
	
	public void RemoveScene(Scene scene) {
		if(!scenes.Contains(scene)) return;
		scenes.Remove(scene);
		for(int i = 0; i < scene.LayerCount; i++) {
			if(scene.Layers[i].Type == LayerType.Tiles) {
				scene.Layers[i].Tilemap?.ReleaseResources();
			}
		}
	}

	internal void AddTileset(Tileset tileset) {
		if(tilesets.Contains(tileset)) return;
		tilesets.Add(tileset);
	}
	
	public void RemoveTileset(Tileset tileset) {
		if(!tilesets.Contains(tileset)) return;
		tilesets.Remove(tileset);
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
		for(int i = 0; i < tilesets.Count; i++) tilesets[i]?.ReleaseResources();
		
		for(int s = 0; s < scenes.Count; s++) {
			for(int i = 0; i < scenes[s].LayerCount; i++) {
				if(scenes[s].Layers[i].Type == LayerType.Tiles) {
					scenes[s].Layers[i].Tilemap?.ReleaseResources();
				}
			}
		}
		
		disposed = true;
	}

}