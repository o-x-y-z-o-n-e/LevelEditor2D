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

	private File file;
	private string name;
	private int tileWidth;
	private int tileHeight;
	private List<Tileset> tilesets;
	private List<Scene> scenes;
	private int maxTilesetSlots;

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

}