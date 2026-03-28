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

	public Layer Root => root;
	
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
	private Layer root;
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
		properties = new();
		root = new Layer(this, LayerType.Group);
		root.Name = "Root";
	}

	internal void Parse(XElement sceneElement) {
		id = sceneElement.Attribute("id").Value;
		worldX = sceneElement.Attribute("world.x").ParseAsInt();
		worldY = sceneElement.Attribute("world.y").ParseAsInt();
		tileCountX = sceneElement.Attribute("tiles.x").ParseAsInt();
		tileCountY = sceneElement.Attribute("tiles.y").ParseAsInt();

		var linksElement = sceneElement.Element("links");
		if(linksElement != null) {
			foreach(var linkElement in linksElement.Elements("link")) {
				TilesetLink tileset = new TilesetLink(file);
				tilesets.Add(tileset);
				tileset.Parse(linkElement);
			}
		}

		var layersElement = sceneElement.Element("layers");
		if(layersElement != null) {
			foreach(var layerElement in layersElement.Elements("layer")) {
				Layer layer = new Layer(this);
				layer.Parse(layerElement);
				root.AddChild(layer);
			}
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

		var rootElement = new XElement("layers");
		foreach(var layer in root.Children) {
			rootElement.Add(layer.Serialize());
		}
		element.Add(rootElement);
        
		return element;
	}

	public IEnumerable<Layer> GetAllLayers() => GetAllLayers(root);
	
	private IEnumerable<Layer> GetAllLayers(Layer parent) {
		foreach(var child1 in parent.Children) {
			yield return child1;
			if(child1.Type == LayerType.Group) {
				foreach(var child2 in GetAllLayers(child1)) {
					yield return child2;
				}
			}
		}
	}

	public int GetLayerTreeIndex(Layer layer) {
		int index = 0;
		foreach(var l in GetAllLayers(root)) {
			if(layer == l) {
				return index;
			}
			index++;
		}
		return -1;
	}

	public Layer GetLayer(int treeIndex) {
		int index = 0;
		foreach(var l in GetAllLayers(root)) {
			if(index == treeIndex) {
				return l;
			}
			index++;
		}
		return null;
	}

	public string GetNewDefaultLayerName() {
		List<Layer> allLayers = new(GetAllLayers());
		string name = "";
		int n = allLayers.Count + 1;
		bool looking = true;
		while(looking) {
			name = $"new_layer_{n}";
			looking = false;
			foreach(var l in allLayers) {
				if(l.Name == name) {
					looking = true;
					break;
				}
			}
			n++;
		}
		return name;
	}

	public void Resize(int tilesX, int tilesY) {
		tileCountX = tilesX;
		tileCountY = tilesY;
		foreach(var layer in GetAllLayers()) {
			if(layer.Type == LayerType.Tiles) {
				layer.Tilemap?.Resize(tileCountX, tileCountY);
			}
		}
	}

	public void MarkTilemapsAsDirty() {
		foreach(var layer in GetAllLayers()) {
			if(layer.Type == LayerType.Tiles) {
				layer.Tilemap.MarkDirty();
			}
		}
	}

	public void ExportToFile(string filename) {
		int pixelsWidth = tileCountX * file.World.TileWidth;
		int pixelsHeight = tileCountY * file.World.TileHeight;
		byte[] buffer = new byte[pixelsWidth * pixelsHeight * 4];
		// Manual color blending becuase I'm tool lazy to deal with opengl
		foreach(var layer in GetAllLayers()) {
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
	
	public class AddOperation : IFileEditOperation {
		private Scene scene;
		private TilesetLink link;
		public AddOperation(Scene scene, TilesetLink link) {
			this.scene = scene;
			this.link = link;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.scene.Tilesets.Add(op.link);
			op.scene.MarkTilemapsAsDirty();
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.scene.Tilesets.Remove(op.link);
			op.scene.MarkTilemapsAsDirty();
		}
		public bool HasChanges() => true;
	}
	
	public class RemoveOperation : IFileEditOperation {
		private Scene scene;
		private TilesetLink link;
		private int index;
		public RemoveOperation(Scene scene, int index) {
			this.scene = scene;
			this.link = scene.Tilesets[index];
			this.index = index;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.scene.Tilesets.RemoveAt(op.index);
			op.scene.MarkTilemapsAsDirty();
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.scene.Tilesets.Insert(op.index, op.link);
			op.scene.MarkTilemapsAsDirty();
		}
		public bool HasChanges() => true;
	}
	
	public class MoveOperation : IFileEditOperation {
		private Scene scene;
		private int index1;
		private int index2;
		public MoveOperation(Scene scene, int index1, int index2) {
			this.scene = scene;
			this.index1 = index1;
			this.index2 = index2;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			var t = op.scene.Tilesets[op.index1];
			op.scene.Tilesets[op.index1] = op.scene.Tilesets[op.index2];
			op.scene.Tilesets[op.index2] = t;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			var t = op.scene.Tilesets[op.index2];
			op.scene.Tilesets[op.index2] = op.scene.Tilesets[op.index1];
			op.scene.Tilesets[op.index1] = t;
		}
		public bool HasChanges() => true;
	}
	
	public class SlotOperation : IFileEditOperation {
		private Scene scene;
		private TilesetLink link;
		private int oldSlot;
		private int newSlot;
		public SlotOperation(Scene scene, TilesetLink link, int slot) {
			this.scene = scene;
			this.link = link;
			this.oldSlot = link.Slot;
			this.newSlot = slot;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<SlotOperation>();
			op.link.Slot = op.newSlot;
			op.scene.MarkTilemapsAsDirty();
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<SlotOperation>();
			op.link.Slot = op.oldSlot;
			op.scene.MarkTilemapsAsDirty();
		}
		public bool HasChanges() => true;
	}
	
	public class TilesetOperation : IFileEditOperation {
		private Scene scene;
		private TilesetLink link;
		private Tileset oldTileset;
		private Tileset newTileset;
		public TilesetOperation(Scene scene, TilesetLink link, Tileset tileset) {
			this.scene = scene;
			this.link = link;
			this.oldTileset = link.Tileset;
			this.newTileset = tileset;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<TilesetOperation>();
			op.link.Tileset = op.newTileset;
			op.scene.MarkTilemapsAsDirty();
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<TilesetOperation>();
			op.link.Tileset = op.oldTileset;
			op.scene.MarkTilemapsAsDirty();
		}
		public bool HasChanges() => true;
	}
}