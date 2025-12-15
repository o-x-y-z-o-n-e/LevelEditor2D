using System.Drawing;
using System.Xml.Linq;

namespace L2D;

public class Scene {

	public string ID {
		get => id;
		set => id = value;
	}
	public int WorldX {
		get => worldX;
		set => worldX = value;
	}
	
	public int WorldY {
		get => worldY;
		set => worldY = value;
	}
	
	public int TileCountX {
		get => tileCountX;
		set => tileCountX = value;
	}
	
	public int TileCountY {
		get => tileCountY;
		set => tileCountY = value;
	}

	public List<Layer> Layers => layers;

	public Layer LastActiveLayer;

	private string id;
	private int worldX;
	private int worldY;
	private int tileCountX;
	private int tileCountY;
	private List<TilesetSlot> tilesetSlots;
	private List<LayerGroup> groups;
	private List<Layer> layers;

	internal Scene() {
		id = "new_scene";
		worldX = 0;
		worldY = 0;
		tileCountX = 64;
		tileCountY = 64;
		tilesetSlots = new();
		groups = new();
		layers = new();
	}

	internal void Parse(XElement sceneElement, string dir) {
		id = sceneElement.Attribute("id").Value;
		worldX = File.ParseAsInt(sceneElement.Attribute("world_x"));
		worldY = File.ParseAsInt(sceneElement.Attribute("world_y"));
		tileCountX = File.ParseAsInt(sceneElement.Attribute("tile_count_x"));
		tileCountY = File.ParseAsInt(sceneElement.Attribute("tile_count_y"));
		
		foreach(var tilesetElement in sceneElement.Element("tilesets").Elements("tileset")) {
			// TilesetSlot tilesetSlot = new TilesetSlot();
			// tilesetSlot.Parse(tilesetElement, dir);
			// tilesetSlots.Add(tilesetSlot);
		}
		
		foreach(var groupElement in sceneElement.Element("groups").Elements("group")) {
			// LayerGroup group = new LayerGroup();
			// groups.Add(group);
		}
		
		foreach(var layerElement in sceneElement.Element("layers").Elements("layer")) {
			Layer layer = new Layer();
			layer.Parse(layerElement, dir);
			layers.Add(layer);
		}
		
	}
	
}

public class TilesetSlot {
	private int slot;
	private Tileset tileset;
	internal TilesetSlot() {
		slot = 1;
		tileset = null;
	}
	internal TilesetSlot(int slot, Tileset tileset) {
		this.slot = slot;
		this.tileset = tileset;
	}
}

public class LayerGroup {

	private string id;
	private Color color;
	internal LayerGroup() {
		id = "new_group";
		color = Color.White;
	}
}