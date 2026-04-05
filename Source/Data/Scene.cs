using System.Drawing;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Serilog;
using StbImageWriteSharp;

namespace E2D;

public class Scene {
	
	public const string FILE_EXTENSION = "s2d";

	public Project Project => project;
	public World World => project.World;
	
	public bool IsEmbedded => embedded;
	public string FilePath => filePath;

	public string ID {
		get => id;
		set => SetID(value);
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

	private Project project;
	private string id;
	private bool embedded;
	private string filePath;
	private FileSystemWatcher fileWatcher;
	private int worldX;
	private int worldY;
	private int tileCountX;
	private int tileCountY;
	private List<TilesetLink> tilesets;
	private PropertyCollection properties;
	private Layer root;
	private bool disposed;

	public Scene(Project project, string id, bool embedded) {
		this.project = project;
		this.id = id;
		this.embedded = embedded;
		filePath = "";
		fileWatcher = null;
		worldX = 0;
		worldY = 0;
		tileCountX = 64;
		tileCountY = 64;
		tilesets = new();
		properties = new();
		root = new Layer(this, LayerType.Group);
		root.Name = "Root";
	}

	public static Scene Parse(Project project, XElement sceneElement) {
		string id = sceneElement.Attribute("id").ParseAsString();
		bool embedded = sceneElement.Attribute("embedded").ParseAsBool();
		if(id == "") {
			Log.Error("Missing scene id");
			return null;
		}
		if(project.World.HasScene(id)) {
			Log.Error($"Duplicate scene id [{id}]");
			return null;
		}
		if(embedded) {
			Scene scene = new Scene(project, id, true);
			scene.ParseData(sceneElement);
			return scene;
		} else {
			string path = project.GetScenePath(id);
			if(!File.Exists(path)) {
				Log.Error($"File [{path}] does not exist");
				return null;
			}
			Scene scene = new Scene(project, id, false);
			scene.UpdateFilePath();
			scene.ReadExternalFile();
			return scene;
		}
	}

	private void ParseData(XElement sceneElement) {
		worldX = sceneElement.Attribute("world.x").ParseAsInt();
		worldY = sceneElement.Attribute("world.y").ParseAsInt();
		tileCountX = sceneElement.Attribute("tiles.x").ParseAsInt();
		tileCountY = sceneElement.Attribute("tiles.y").ParseAsInt();
		var linksElement = sceneElement.Element("links");
		if(linksElement != null) {
			foreach(var linkElement in linksElement.Elements("link")) {
				TilesetLink tileset = new TilesetLink(project);
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

	public static XElement Serialize(Scene scene) {
		var element = new XElement("scene");
		element.Add(new XAttribute("id", scene.id));

		if(scene.embedded) {
			element.Add(new XAttribute("embedded", scene.embedded));
			scene.SerializeData(element);
		} else {
			scene.WriteExternalFile();
		}
        
		return element;
	}

	private void SerializeData(XElement sceneElement) {
		sceneElement.Add(
			new XAttribute("world.x", worldX),
			new XAttribute("world.y", worldY),
			new XAttribute("tiles.x", tileCountX),
			new XAttribute("tiles.y", tileCountY)
		);
		properties.SerializeToElement(sceneElement);
		var linksParent = new XElement("links");
		foreach(var link in tilesets) {
			linksParent.Add(link.Serialize());
		}
		sceneElement.Add(linksParent);
		var rootElement = new XElement("layers");
		foreach(var layer in root.Children) {
			rootElement.Add(layer.Serialize());
		}
		sceneElement.Add(rootElement);
	}
	
	private void ReadExternalFile() {
		if(!File.Exists(filePath)) {
			Log.Error($"File [{filePath}] does not exist");
			return;
		}
		try {
			string contents = File.ReadAllText(filePath);
			XDocument doc = XDocument.Parse(contents);
			ParseData(doc.Root);
		} catch(Exception e) {
			Log.Error("Failed to load external scene file", e);
		}
	}

	private void WriteExternalFile() {
		XmlWriter writer = null;
		fileWatcher.EnableRaisingEvents = false;
		Log.Information("Writing scene file... [{@filePath}]", filePath);
		try {
			StringBuilder builder = new StringBuilder();
			XDocument document = new XDocument();
			document.Add(new XElement("scene"));
			SerializeData(document.Root);
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.OmitXmlDeclaration = true;
			settings.CloseOutput = false;
			settings.Indent = true;
			writer = XmlTextWriter.Create(builder, settings);
			document.Save(writer);
			writer.Close();
			File.WriteAllText(filePath, builder.ToString());
		} catch(Exception e) {
			Log.Error(e, "Failed to write scene file: {@filePath}", filePath);
			writer?.Close();
		} finally {
			fileWatcher.EnableRaisingEvents = true;
		}
	}

	public void SetID(string id) {
		foreach(var scene in project.World.Scenes) {
			if(scene.ID == id) {
				Log.Error("Failed to change scene ID. Duplicate in project: {id}", id);
				return;
			}
		}

		if(!embedded && filePath != "") {
			project.DeleteFileOnSave(filePath);
		}
		
		this.id = id;
		
		if(!embedded) {
			UpdateFilePath();
		}
	}

	public void UpdateFilePath() {
		if(embedded) return;
		filePath = project.GetScenePath(id);
		if(fileWatcher == null) {
			fileWatcher = new FileSystemWatcher(Path.GetDirectoryName(filePath));
			fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
			fileWatcher.Changed += OnFileChanged;
		} else {
			fileWatcher.Path = Path.GetDirectoryName(filePath).Replace('\\', '/');
		}
		fileWatcher.Filter = Path.GetFileName(filePath);
		fileWatcher.EnableRaisingEvents = id != "";
	}

	public void ReleaseResources() {
		if(fileWatcher != null) {
			fileWatcher.Dispose();
			fileWatcher = null;
		}
		foreach(var layer in GetAllLayers()) {
			if(layer.Type == LayerType.Tiles) {
				layer.Tilemap?.ReleaseResources();
			}
		}
	}

	private void OnFileChanged(object sender, FileSystemEventArgs e) {
		if(e.ChangeType != WatcherChangeTypes.Changed) {
			return;
		}
		string p = e.FullPath.Replace('\\', '/');
		Program.SendMessage(() => {
			if(p == filePath) {
				Log.Information("Detected change in file: {@filePath}", filePath);
				project.MarkDirty();
				Program.ConfirmModal.Open(
					$"Scene [{filePath}] File Changed",
					$"Detected changes to scene [{filePath}] file from outside this editor.\nDo you want to reload the file from disk?",
					ReadExternalFile
				);
			}
		});
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
		int pixelsWidth = tileCountX * project.World.TileWidth;
		int pixelsHeight = tileCountY * project.World.TileHeight;
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
		public object? Context => world;
		private World world;
		private Scene scene;
		public AddOperation(World world, Scene scene) {
			this.world = world;
			this.scene = scene;
		}
		public void ApplyNextState(FileEditEntry entry) {
			world.InsertScene(scene, world.SceneCount);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			world.RemoveScene(scene);
			if(scene == Program.SelectedScene) {
				Program.SetSelectedScene(null);
			}
		}
		public bool HasChanges() => true;
		public string GetNextStateMessage() => $"Add scene: {scene.ID}";
		public string GetPrevStateMessage() => $"Undo add scene: {scene.ID}";
	}
	
	public class MoveOperation : IFileEditOperation {
		public object? Context => world;
		private World world;
		private int oldIndex;
		private int newIndex;
		public MoveOperation(World world, int oldIndex, int newIndex) {
			this.world = world;
			this.oldIndex = oldIndex;
			this.newIndex = newIndex;
		}
		public void ApplyNextState(FileEditEntry entry) {
			Scene scene = world.Scenes[oldIndex];
			world.Scenes.RemoveAt(oldIndex);
			world.Scenes.Insert(newIndex, scene);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			Scene scene = world.Scenes[newIndex];
			world.Scenes.RemoveAt(newIndex);
			world.Scenes.Insert(oldIndex, scene);
		}
		public bool HasChanges() => oldIndex != newIndex;
		public string GetNextStateMessage() => $"Move scene order";
		public string GetPrevStateMessage() => $"Undo move scene order";
	}
	
	public class RemoveOperation : IFileEditOperation {
		public object? Context => world;
		private World world;
		private Scene scene;
		private int index;
		public RemoveOperation(World world, Scene scene) {
			this.world = world;
			this.scene = scene;
			this.index = world.GetSceneIndex(scene);
		}
		public void ApplyNextState(FileEditEntry entry) {
			world.RemoveScene(scene);
			if(scene == Program.SelectedScene) {
				Program.SetSelectedScene(null);
			}
		}
		public void ApplyPrevState(FileEditEntry entry) {
			world.InsertScene(scene, index);
		}
		public bool HasChanges() => true;
		public string GetNextStateMessage() => $"Remove scene: {scene.ID}";
		public string GetPrevStateMessage() => $"Undo remove scene: {scene.ID}";
	}
	
	public class RenameOperation : IFileEditOperation {
		public object? Context => scene.World;
		private Scene scene;
		private string oldName;
		private string newName;
		public RenameOperation(Scene scene, string newName) {
			this.scene = scene;
			this.oldName = scene.ID;
			this.newName = newName;
		}
		public void ApplyNextState(FileEditEntry entry) {
			scene.ID = newName;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			scene.ID = oldName;
		}
		public bool HasChanges() => oldName != newName;
		public string GetNextStateMessage() => $"Rename scene from '{oldName}' to '{newName}'";
		public string GetPrevStateMessage() => $"Undo rename scene from '{oldName}' to '{newName}'";
	}
	
	public class RepositionOperation : IFileEditOperation {
		public object? Context => scene.World;
		private Scene scene;
		private Point oldPosition;
		private Point newPosition;
		public RepositionOperation(Scene scene, Point newPosition) {
			this.scene = scene;
			this.oldPosition = new(scene.WorldX, scene.WorldY);
			this.newPosition = newPosition;
		}
		public void ApplyNextState(FileEditEntry entry) {
			scene.WorldX = newPosition.X;
			scene.WorldY = newPosition.Y;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			scene.WorldX = oldPosition.X;
			scene.WorldY = oldPosition.Y;
		}
		public bool HasChanges() => oldPosition != newPosition;
		public string GetNextStateMessage() => $"Position scene: {scene.ID}";
		public string GetPrevStateMessage() => $"Undo position scene: {scene.ID}";
	}

	public class ResizeOperation : IFileEditOperation {
		public object? Context => scene.World;
		private Scene scene;
		private Point oldSize;
		private Point newSize;
		private List<Tuple<Tilemap, TileRef[,], TileRef[,]>> tilemapGrids;
		public ResizeOperation(Scene scene, Point newSize) {
			this.scene = scene;
			this.oldSize = new(scene.TileCountX, scene.TileCountY);
			this.newSize = newSize;
			this.tilemapGrids = new();
			foreach(var layer in scene.GetAllLayers()) {
				if(layer.Type == LayerType.Tiles) {
					TileRef[,] oldGrid = layer.Tilemap.Grid;
					TileRef[,] newGrid = new TileRef[newSize.X, newSize.Y];
					for(int x = 0; x < oldSize.X && x < newSize.X; x++) {
						for(int y = 0; y < oldSize.Y && y < newSize.Y; y++) {
							newGrid[x, y] = oldGrid[x, y];
						}
					}
					this.tilemapGrids.Add(new(layer.Tilemap, oldGrid, newGrid));
				}
			}
		}
		public void ApplyNextState(FileEditEntry entry) {
			scene.tileCountX = newSize.X;
			scene.tileCountY = newSize.Y;
			foreach(var tilemapData in tilemapGrids) {
				(var tilemap, var oldGrid, var newGrid) = tilemapData;
				tilemap.SetGrid(newSize.X, newSize.Y, newGrid);
			}
		}
		public void ApplyPrevState(FileEditEntry entry) {
			scene.tileCountX = oldSize.X;
			scene.tileCountY = oldSize.Y;
			foreach(var tilemapData in tilemapGrids) {
				(var tilemap, var oldGrid, var newGrid) = tilemapData;
				tilemap.SetGrid(oldSize.X, oldSize.Y, oldGrid);
			}
		}
		public bool HasChanges() => oldSize != newSize;
		public string GetNextStateMessage() => $"Resize scene: {scene.ID}";
		public string GetPrevStateMessage() => $"Undo resize scene: {scene.ID}";
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

	private Project project;
	private int slot;
	private Tileset tileset;
	
	internal TilesetLink(Project project) {
		this.project = project;
		slot = 1;
		tileset = null;
	}
	
	internal TilesetLink(Project project, int slot) {
		this.project = project;
		this.slot = slot;
		tileset = null;
	}
	
	internal TilesetLink(Project project, int slot, Tileset tileset) {
		this.project = project;
		this.slot = slot;
		this.tileset = tileset;
	}

	internal void Parse(XElement linkElement) {
		slot = linkElement.Attribute("slot").ParseAsInt();
		string id = linkElement.Attribute("tileset").ParseAsString();
		if(id != "") {
			foreach(var tileset in project.World.Tilesets) {
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
		public object? Context => scene;
		private Scene scene;
		private TilesetLink link;
		public AddOperation(Scene scene, TilesetLink link) {
			this.scene = scene;
			this.link = link;
		}
		public void ApplyNextState(FileEditEntry entry) {
			scene.Tilesets.Add(link);
			scene.MarkTilemapsAsDirty();
		}
		public void ApplyPrevState(FileEditEntry entry) {
			scene.Tilesets.Remove(link);
			scene.MarkTilemapsAsDirty();
		}
		public bool HasChanges() => true;
		public string GetNextStateMessage() => $"Add tileset link to scene [{scene.ID}]";
		public string GetPrevStateMessage() => $"Undo add tileset link to scene [{scene.ID}]";
	}
	
	public class RemoveOperation : IFileEditOperation {
		public object? Context => scene;
		private Scene scene;
		private TilesetLink link;
		private int index;
		public RemoveOperation(Scene scene, int index) {
			this.scene = scene;
			this.link = scene.Tilesets[index];
			this.index = index;
		}
		public void ApplyNextState(FileEditEntry entry) {
			scene.Tilesets.RemoveAt(index);
			scene.MarkTilemapsAsDirty();
		}
		public void ApplyPrevState(FileEditEntry entry) {
			scene.Tilesets.Insert(index, link);
			scene.MarkTilemapsAsDirty();
		}
		public bool HasChanges() => true;
		public string GetNextStateMessage() => $"Remove tileset link from scene [{scene.ID}]";
		public string GetPrevStateMessage() => $"Undo remove tileset link from scene [{scene.ID}]";
	}
	
	public class MoveOperation : IFileEditOperation {
		public object? Context => scene;
		private Scene scene;
		private int oldIndex;
		private int newIndex;
		public MoveOperation(Scene scene, int oldIndex, int newIndex) {
			this.scene = scene;
			this.oldIndex = oldIndex;
			this.newIndex = newIndex;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var t = scene.Tilesets[oldIndex];
			scene.Tilesets.RemoveAt(oldIndex);
			scene.Tilesets.Insert(newIndex, t);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var t = scene.Tilesets[newIndex];
			scene.Tilesets.RemoveAt(newIndex);
			scene.Tilesets.Insert(oldIndex, t);
		}
		public bool HasChanges() => oldIndex != newIndex;
		public string GetNextStateMessage() => $"Reorder tileset links in scene [{scene.ID}]";
		public string GetPrevStateMessage() => $"Undo reorder tileset links in scene [{scene.ID}]";
	}
	
	public class SlotOperation : IFileEditOperation {
		public object? Context => scene;
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
			link.Slot = newSlot;
			scene.MarkTilemapsAsDirty();
		}
		public void ApplyPrevState(FileEditEntry entry) {
			link.Slot = oldSlot;
			scene.MarkTilemapsAsDirty();
		}
		public bool HasChanges() => true;
		public string GetNextStateMessage() => $"Change tileset link slot in scene [{scene.ID}]";
		public string GetPrevStateMessage() => $"Undo tileset link slot in scene [{scene.ID}]";
	}
	
	public class TilesetOperation : IFileEditOperation {
		public object? Context => scene;
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
			link.Tileset = newTileset;
			scene.MarkTilemapsAsDirty();
		}
		public void ApplyPrevState(FileEditEntry entry) {
			link.Tileset = oldTileset;
			scene.MarkTilemapsAsDirty();
		}
		public bool HasChanges() => true;
		public string GetNextStateMessage() => $"Change tileset link in scene [{scene.ID}]";
		public string GetPrevStateMessage() => $"Undo tileset link in scene [{scene.ID}]";
	}
}