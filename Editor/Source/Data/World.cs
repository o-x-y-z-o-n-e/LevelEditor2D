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

	private string name;
	private int tileWidth;
	private int tileHeight;
	private List<Tileset> tilesets;
	private List<Scene> scenes;

	internal World() {
		name = "New World";
		tileWidth = 16;
		tileHeight = 16;
		tilesets = new();
		scenes = new();
	}
	
	public Tileset GetTileset(int index) {
		return tilesets[index];
	}

	public Scene GetScene(int index) {
		return scenes[index];
	}

	internal void Parse(XElement worldElement, string dir) {
		name = worldElement.Attribute("name").Value;
		tileWidth = File.ParseAsInt(worldElement.Attribute("tile_width"), 16);
		tileHeight = File.ParseAsInt(worldElement.Attribute("tile_height"), 16);
		foreach(var tilesetElement in worldElement.Element("tilesets").Elements("tileset")) {
			Tileset tileset = new Tileset();
			tileset.Parse(tilesetElement, dir);
			tilesets.Add(tileset);
		}
		foreach(var sceneElement in worldElement.Element("scenes").Elements("scene")) {
			Scene scene = new Scene();
			scene.Parse(sceneElement, dir);
			scenes.Add(scene);
		}
	}

}