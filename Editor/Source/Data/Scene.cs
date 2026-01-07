using System.Drawing;
using System.Xml.Linq;

namespace L2D;

public class Scene {

	public File File => file;
	public World World => file.World;

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

	public int LayerCount => layers.Count;

	public List<Layer> Layers => layers;
	
	public List<TilesetLink> Tilesets => tilesets;

	public Layer LastActiveLayer;

	private File file;
	private string id;
	private int worldX;
	private int worldY;
	private int tileCountX;
	private int tileCountY;
	private List<TilesetLink> tilesets;
	private List<LayerGroup> groups;
	private List<Layer> layers;

	internal Scene(File file) {
		this.file = file;
		id = "new_scene";
		worldX = 0;
		worldY = 0;
		tileCountX = 64;
		tileCountY = 64;
		tilesets = new();
		groups = new();
		layers = new();
	}

	internal void Parse(XElement sceneElement) {
		id = sceneElement.Attribute("id").Value;
		worldX = sceneElement.Attribute("world_x").ParseAsInt();
		worldY = sceneElement.Attribute("world_y").ParseAsInt();
		tileCountX = sceneElement.Attribute("tile_count_x").ParseAsInt();
		tileCountY = sceneElement.Attribute("tile_count_y").ParseAsInt();

		foreach(var linkElement in sceneElement.Element("tilesets").Elements("link")) {
			TilesetLink tileset = new TilesetLink(file);
			tilesets.Add(tileset);
			tileset.Parse(linkElement);
		}

		foreach(var groupElement in sceneElement.Element("groups").Elements("group")) {
			// LayerGroup group = new LayerGroup();
			// groups.Add(group);
		}

		foreach(var layerElement in sceneElement.Element("layers").Elements("layer")) {
			Layer layer = new Layer(this);
			layer.Parse(layerElement);
			layers.Add(layer);
		}

	}

	public Layer AddLayer() {
		Layer layer = new Layer(this);
		int n = layers.Count + 1;
		bool looking = true;
		while(looking) {
			layer.Name = $"new_layer_{n}";
			looking = false;
			foreach(var l in layers) {
				if(l.Name == layer.Name) {
					looking = true;
					break;
				}
			}
			n++;
		}
		layers.Add(layer);
		return layer;
	}
	
	public Layer GetLayer(int index) {
		return layers[index];
	}

	public void SwapLayers(int index1, int index2) {
		if(index1 < 0 || index1 >= layers.Count || index2 < 0 || index2 >= layers.Count) return;
		var t = layers[index1];
		layers[index1] = layers[index2];
		layers[index2] = t;
	}
	
	public void SwapLayers(Layer layer1, Layer layer2) {
		SwapLayers(layers.IndexOf(layer1), layers.IndexOf(layer2));
	}

	public void DeleteLayer(int index) {
		layers.RemoveAt(index);
	}
	
	public void DeleteLayer(Layer layer) {
		layers.Remove(layer);
	}

	public bool HasLayer(Layer layer) {
		return layers.Contains(layer);
	}

}

public class TilesetLink {

	public int Slot {
		get => slot;
		set => slot = value;
	}
	
	public Tileset Tileset {
		get => tileset;
		set => tileset = value;
	}

	private File file;
	private int slot;
	private Tileset tileset;
	
	internal TilesetLink(File file) {
		this.file = file;
		slot = 1;
		tileset = null;
	}
	
	internal TilesetLink(File file, int slot) {
		this.file = file;
		this.slot = slot;
		tileset = null;
	}
	
	internal TilesetLink(File file, int slot, Tileset tileset) {
		this.file = file;
		this.slot = slot;
		this.tileset = tileset;
	}

	internal void Parse(XElement sceneElement) {
		slot = sceneElement.Attribute("slot").ParseAsInt();
		string id = sceneElement.Attribute("id").Value;
		foreach(var tileset in file.World.Tilesets) {
			if(tileset.ID == id) {
				this.tileset = tileset;
			}
		}
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