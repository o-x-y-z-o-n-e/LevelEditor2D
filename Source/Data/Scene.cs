using System.Drawing;
using System.Xml.Linq;
using Serilog;
using StbImageWriteSharp;

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
	}

	public int TileCountY {
		get => tileCountY;
	}

	public int LayerCount => layers.Count;

	public List<Layer> Layers => layers;
	
	public List<TilesetLink> Tilesets => tilesets;

	public PropertyCollection Properties => properties;

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
	private PropertyCollection properties;
	private bool disposed;

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
		properties = new();
	}

	internal void Parse(XElement sceneElement) {
		id = sceneElement.Attribute("id").Value;
		worldX = sceneElement.Attribute("world.x").ParseAsInt();
		worldY = sceneElement.Attribute("world.y").ParseAsInt();
		tileCountX = sceneElement.Attribute("tiles.x").ParseAsInt();
		tileCountY = sceneElement.Attribute("tiles.y").ParseAsInt();

		foreach(var linkElement in sceneElement.Element("links").Elements("link")) {
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

		properties.ParseFromElement(sceneElement);
	}

	internal XElement Serialize() {
		var element = new XElement("scene");
		element.Add(
			new XAttribute("id", id),
			new XAttribute("world.x", worldX),
			new XAttribute("world.y", worldY),
			new XAttribute("tiles.x", tileCountX),
			new XAttribute("tiles.y", tileCountY)
		);
		
		properties.SerializeToElement(element);

		var linksParent = new XElement("links");
		foreach(var link in tilesets) {
			linksParent.Add(link.Serialize());
		}
		element.Add(linksParent);

		var groupsParent = new XElement("groups");
		element.Add(groupsParent);

		var layersParent = new XElement("layers");
		foreach(var layer in layers) {
			layersParent.Add(layer.Serialize());
		}
		element.Add(layersParent);
        
		return element;
	}

	public Layer AddLayer(LayerType type) {
		Layer layer = new Layer(this, type);
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
		if(index < 0 || index >= layers.Count) return null;
		return layers[index];
	}
	
	public int GetLayerIndex(Layer layer) {
		return layers.IndexOf(layer);
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

	public void RemoveLayer(int index) {
		RemoveLayer(layers[index]);
	}
	
	public void RemoveLayer(Layer layer) {
		if(layer == null || layer.Scene != this || !layers.Contains(layer)) return;
		if(LastActiveLayer == layer) LastActiveLayer = null;
		layers.Remove(layer);
	}

	public void InsertLayer(Layer layer, int index) {
		if(layer == null || layer.Scene != this || layers.Contains(layer)) return;
		layers.Insert(index, layer);
	}
	
	public Layer CopyLayer(int index) {
		return CopyLayer(layers[index]);
	}
	
	public Layer CopyLayer(Layer srcLayer) {
		if(srcLayer.Scene != this) return null;

		Layer newLayer = AddLayer(srcLayer.Type);
		newLayer.Visible = srcLayer.Visible;
		newLayer.Color = srcLayer.Color;
		srcLayer.Properties.CopyTo(newLayer.Properties);
		if(srcLayer.Type == LayerType.Tiles) {
			for(int y = 0; y < tileCountY; y++) {
				for(int x = 0; x < tileCountX; x++) {
					newLayer.Tilemap.Grid[x, y] = srcLayer.Tilemap.Grid[x, y];
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

		return newLayer;
	}

	public bool HasLayer(Layer layer) {
		return layers.Contains(layer);
	}

	public void Resize(int tilesX, int tilesY) {
		tileCountX = tilesX;
		tileCountY = tilesY;
		foreach(var layer in layers) {
			if(layer.Type == LayerType.Tiles) {
				layer.Tilemap?.Resize(tileCountX, tileCountY);
			}
		}
	}

	public void ExportToFile(string filename) {
		int pixelsWidth = tileCountX * file.World.TileWidth;
		int pixelsHeight = tileCountY * file.World.TileHeight;
		byte[] buffer = new byte[pixelsWidth * pixelsHeight * 4];
		// Manual color blending becuase I'm tool lazy to deal with opengl
		foreach(var layer in layers) {
			if(layer.Type != LayerType.Tiles) continue;
			layer.Tilemap.ExportToPixels(out byte[] data);
			for(int p = 0; p < pixelsWidth * pixelsHeight; p++) {
				int i = p * 4;
				float src_r = data[i + 0] / 255.0F;
				float src_g = data[i + 1] / 255.0F;
				float src_b = data[i + 2] / 255.0F;
				float src_a = data[i + 3] / 255.0F;
				float dst_r = buffer[i + 0] / 255.0F;
				float dst_g = buffer[i + 1] / 255.0F;
				float dst_b = buffer[i + 2] / 255.0F;
				float dst_a = buffer[i + 3] / 255.0F;
				dst_r = float.Clamp(dst_r * (1.0F - src_a) + src_r * src_a, 0.0F, 1.0F);
				dst_g = float.Clamp(dst_g * (1.0F - src_a) + src_g * src_a, 0.0F, 1.0F);
				dst_b = float.Clamp(dst_b * (1.0F - src_a) + src_b * src_a, 0.0F, 1.0F);
				dst_a = float.Clamp(dst_a + src_a, 0.0F, 1.0F);
				buffer[i + 0] = (byte)(dst_r * 255.0F);
				buffer[i + 1] = (byte)(dst_g * 255.0F);
				buffer[i + 2] = (byte)(dst_b * 255.0F);
				buffer[i + 3] = (byte)(dst_a * 255.0F);
			}
		}
		FileStream stream = null;
		try {
			stream = System.IO.File.Create(filename);
			new StbImageWriteSharp.ImageWriter().WritePng(buffer, pixelsWidth, pixelsHeight, ColorComponents.RedGreenBlueAlpha, stream);
			stream.Close();
		} catch(Exception e) {
			stream?.Close();
			Log.Error(e, "Failed to export scene image!");
		}
	}
	
	public class AddOperation : IFileEditOperation {
		private World world;
		private Scene scene;
		public AddOperation(World world, Scene scene) {
			this.world = world;
			this.scene = scene;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.world.InsertScene(op.scene, op.world.SceneCount);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.world.RemoveScene(op.scene);
			if(op.scene == Program.SelectedScene) {
				Program.SetSelectedScene(null);
			}
		}
		public bool HasChanges() => true;
	}
	
	public class MoveOperation : IFileEditOperation {
		private World world;
		private int index1;
		private int index2;
		public MoveOperation(World world, int index1, int index2) {
			this.world = world;
			this.index1 = index1;
			this.index2 = index2;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			op.world.SwapScenes(op.index1, op.index2);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			op.world.SwapScenes(op.index2, op.index1);
		}
		public bool HasChanges() => index1 != index2;
	}
	
	public class RemoveOperation : IFileEditOperation {
		private World world;
		private Scene scene;
		private int index;
		public RemoveOperation(World world, Scene scene) {
			this.world = world;
			this.scene = scene;
			this.index = world.GetSceneIndex(scene);
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.world.RemoveScene(op.scene);
			if(op.scene == Program.SelectedScene) {
				Program.SetSelectedScene(null);
			}
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.world.InsertScene(op.scene, op.index);
		}
		public bool HasChanges() => true;
	}
	
	public class RenameOperation : IFileEditOperation {
		private Scene scene;
		private string oldName;
		private string newName;
		public RenameOperation(Scene scene, string newName) {
			this.scene = scene;
			this.oldName = scene.ID;
			this.newName = newName;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RenameOperation>();
			op.scene.ID = op.newName;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RenameOperation>();
			op.scene.ID = op.oldName;
		}
		public bool HasChanges() => oldName != newName;
	}
	
	public class RepositionOperation : IFileEditOperation {
		private Scene scene;
		private Point oldPosition;
		private Point newPosition;
		public RepositionOperation(Scene scene, Point newPosition) {
			this.scene = scene;
			this.oldPosition = new(scene.WorldX, scene.WorldY);
			this.newPosition = newPosition;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RepositionOperation>();
			op.scene.WorldX = op.newPosition.X;
			op.scene.WorldY = op.newPosition.Y;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RepositionOperation>();
			op.scene.WorldX = op.oldPosition.X;
			op.scene.WorldY = op.oldPosition.Y;
		}
		public bool HasChanges() => oldPosition != newPosition;
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

	internal void Parse(XElement linkElement) {
		slot = linkElement.Attribute("slot").ParseAsInt();
		string id = linkElement.Attribute("tileset").ParseAsString();
		if(id != "") {
			foreach(var tileset in file.World.Tilesets) {
				if(tileset.ID == id) {
					this.tileset = tileset;
				}
			}
		}
	}

	internal XElement Serialize() {
		XElement linkElement = new XElement("link");
		linkElement.Add(
			new XAttribute("slot", slot),
			new XAttribute("tileset", tileset?.ID ?? "")
		);
		return linkElement;
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