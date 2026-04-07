using System.Xml.Linq;

namespace E2D;

public class World {
	
	public const string FILE_EXTENSION = "w2d";
	
	public Project Project => project;

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

	public string TilesetsDirectory {
		get => tilesetsDirectory;
		set => tilesetsDirectory = value;
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
	private string tilesetsDirectory;
	private List<Tileset> tilesets;
	private EntityCollection templates;
	private int maxTilesetSlots;
	private bool disposed;

	internal World(Project project) {
		this.project = project;

		name = "New World";
		tileWidth = 16;
		tileHeight = 16;
		scenesDirectory = "Scenes";
		scenes = new();
		tilesetsDirectory = "Tilesets";
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

	public Scene CreateScene(string id, int width, int height, int x, int y, bool embedded, bool blankLayer = true) {
		Scene scene = new Scene(project, id, embedded);
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
		scene.UpdateFileWatcher();
		scene.MarkTilemapsAsDirty();
		if(!scene.IsEmbedded) {
			project.DontDeleteFileOnSave(scene.FileAbsolutePath);
		}
	}

	public void MoveScene(int oldIndex, int newIndex) {
		if(oldIndex == newIndex || oldIndex < 0 || oldIndex >= scenes.Count || newIndex < 0 || newIndex >= scenes.Count) return;
		Scene scene = scenes[oldIndex];
		scenes.RemoveAt(oldIndex);
		scenes.Insert(newIndex, scene);
	}

	public Scene CopyScene(int index, string newID, int x, int y, bool embedded) {
		if(index < 0 || index >= scenes.Count) return null;
		return CopyScene(scenes[index], newID, x, y, embedded);
	}
	
	public Scene CopyScene(Scene srcScene, string newID, int x, int y, bool embedded) {
		if(!scenes.Contains(srcScene)) return null;
		Scene newScene = CreateScene(newID, srcScene.TileCountX, srcScene.TileCountY, x, y, embedded, false);
		
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
			project.DeleteFileOnSave(scene.FileAbsolutePath);
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
		tileset.UpdateFileWatcher();
		tileset.UpdateTextureFileWatcher();
		tileset.ReloadTexture();
		if(!tileset.IsEmbedded) {
			project.DontDeleteFileOnSave(tileset.FileAbsolutePath);
		}
	}
	
	public void RemoveTileset(Tileset tileset) {
		if(!tilesets.Contains(tileset)) return;
		tilesets.Remove(tileset);
		tileset.ReleaseResources();
		if(!tileset.IsEmbedded) {
			project.DeleteFileOnSave(tileset.FileAbsolutePath);
		}
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
			tilesetsDirectory = tilesetsElement.Attribute("directory").ParseAsString(tilesetsDirectory);
			foreach(var tilesetElement in tilesetsElement.Elements("tileset")) {
				Tileset tileset = Tileset.Parse(project, tilesetElement);
				if(tileset != null) {
					tilesets.Add(tileset);
				}
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
		tilesetsParent.Add(new XAttribute("directory", tilesetsDirectory));
		foreach(var tileset in tilesets) {
			tilesetsParent.Add(Tileset.Serialize(tileset));
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