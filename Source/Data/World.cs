using System.Xml.Linq;

namespace E2D;

public class World {
	
	public const string FILE_EXTENSION = "w2d";

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
	
	public string ScenesDirectory {
		get => scenesDirectory;
		set => scenesDirectory = value;
	}

	public int TilesetCount => tilesets.Count;
	public int SceneCount => scenes.Count;

	public int MaxTilesetSlots => maxTilesetSlots;

	public List<Tileset> Tilesets => tilesets;
	public List<Scene> Scenes => scenes;
	
	public EntityCollection Templates => templates;

	private Project project;
	private string name;
	private int tileWidth;
	private int tileHeight;
	private string scenesDirectory;
	private List<Scene> scenes;
	private List<Tileset> tilesets;
	private EntityCollection templates;
	private int maxTilesetSlots;
	private bool disposed;

	internal World(Project project) {
		this.project = project;

		name = "New World";
		tileWidth = 16;
		tileHeight = 16;
		scenesDirectory = "scenes";
		scenes = new();
		tilesets = new();
		templates = new(this);
		maxTilesetSlots = Tilemap.MAX_TILESETS;
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
		Scene scene = new Scene(project, id, false);
		scene.WorldX = x;
		scene.WorldY = y;
		scene.Resize(width, height);
		if(blankLayer) {
			Layer layer = new Layer(scene, LayerType.Tiles);
			layer.Name = scene.GetNewDefaultLayerName();
			scene.Root.AddChild(layer);
		}
		return scene;
	}

	internal void InsertScene(Scene scene, int index) {
		if(scene == null || scene.World != this || scenes.Contains(scene)) return;
		scenes.Insert(index, scene);
		scene.UpdateFilePath();
		scene.MarkTilemapsAsDirty();
		if(!scene.IsEmbedded) {
			project.DontDeleteFileOnSave(scene.FilePath);
		}
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
		
		srcScene.Properties.CopyTo(newScene.Properties);
		
		for(int i = 0; i < srcScene.Tilesets.Count; i++) {
			var src = srcScene.Tilesets[i];
			var link = new TilesetLink(project, src.Slot);
			link.Tileset = src.Tileset;
			newScene.Tilesets.Add(link);
		}
		
		Layer.Copy(srcScene.Root, newScene.Root);
		
		return newScene;
	}

	public void RemoveScene(int index) {
		if(index < 0 || index >= scenes.Count) return;
		RemoveScene(scenes[index]);
	}
	
	public void RemoveScene(Scene scene) {
		if(!scenes.Contains(scene)) return;
		scenes.Remove(scene);
		scene.ReleaseResources();
		if(!scene.IsEmbedded) {
			project.DeleteFileOnSave(scene.FilePath);
		}
	}

	public bool HasScene(string id) {
		foreach(var scene in scenes) {
			if(scene.ID == id) return true;
		}
		return false;
	}

	internal void AddTileset(Tileset tileset) {
		if(tilesets.Contains(tileset)) return;
		tilesets.Add(tileset);
	}
	
	public void RemoveTileset(Tileset tileset) {
		tilesets.Remove(tileset);
	}

	public bool HasTileset(string id) {
		foreach(var tileset in tilesets) {
			if(tileset.ID == id) return true;
		}
		return false;
	}

	internal void Parse(XElement worldElement) {
		name = worldElement.Attribute("name").Value;
		tileWidth = worldElement.Attribute("tile_width").ParseAsInt(16);
		tileHeight = worldElement.Attribute("tile_height").ParseAsInt(16);
		XElement templatesElement = worldElement.Element("templates");
		if(templatesElement != null) {
			templates.ParseFromElement(templatesElement);
		}
		XElement tilesetsElement = worldElement.Element("tilesets");
		if(tilesetsElement != null) {
			foreach(var tilesetElement in tilesetsElement.Elements("tileset")) {
				string id = tilesetElement.Attribute("id").ParseAsString();
				if(id == "" || HasTileset(id)) continue; // ignore duplicate or missing tileset IDs
				Tileset tileset = new Tileset(project);
				tileset.Parse(tilesetElement);
				tilesets.Add(tileset);
			}
		}
		XElement scenesElement = worldElement.Element("scenes");
		if(scenesElement != null) {
			scenesDirectory = scenesElement.Attribute("directory").ParseAsString(scenesDirectory);
			foreach(var sceneElement in scenesElement.Elements("scene")) {
				Scene scene = Scene.Parse(project, sceneElement);
				if(scene != null) {
					scenes.Add(scene);
				}
			}
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
		XElement templatesParent = new XElement("templates");
		templates.SerializeToElement(templatesParent);
		rootElement.Add(templatesParent);
		XElement tilesetsParent = new XElement("tilesets");
		foreach(var tileset in tilesets) {
			tilesetsParent.Add(tileset.Serialize());
		}
		rootElement.Add(tilesetsParent);
		XElement scenesParent = new XElement("scenes");
		scenesParent.Add(new XAttribute("directory", scenesDirectory));
		foreach(var scene in scenes) {
			scenesParent.Add(Scene.Serialize(scene));
		}
		rootElement.Add(scenesParent);
		return rootElement;
	}
	
	public void Dispose() {
		if(disposed) return;
		for(int i = 0; i < tilesets.Count; i++) tilesets[i]?.ReleaseResources();
		for(int i = 0; i < scenes.Count; i++) scenes[i]?.ReleaseResources();
		disposed = true;
	}

}