using System.Buffers;
using System.Drawing;
using System.Numerics;
using System.Xml.Linq;
using Serilog;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace L2D; 

public class Tileset {

	// TODO: fix size when not same as world tile size
	
	public string ID {
		get => id;
		set => id = value;
	}
	
	public string Group {
		get => group;
		set => group = value;
	}

	public string TextureFilePath {
		get => textureFilePath;
		set {
			textureFilePath = value;
			OnTextureFilePathChanged();
		}
	}

	public int OffsetX {
		get => offset.X;
		set => offset.X = value;
	}
	
	public int OffsetY {
		get => offset.Y;
		set => offset.Y = value;
	}
	
	public int SpacingX {
		get => spacing.X;
		set => spacing.X = value;
	}
	
	public int SpacingY {
		get => spacing.Y;
		set => spacing.Y = value;
	}
	
	public int SizeX {
		get => size.X;
		set => size.X = value;
	}
	
	public int SizeY {
		get => size.Y;
		set => size.Y = value;
	}
	
	public Texture TexturePreview => texturePreview;
	public TextureArray TextureArray => textureArray;
	
	public List<AutomapPattern> AutomapPatterns => automapPatterns;
	public List<PresetPattern> PresetPatterns => presetPatterns;

	private File file;
	private string id;
	private string group;
	private string textureFilePath;
	private Point offset;
	private Point spacing;
	private Point size;
	private SortedList<int, TileData> tileData;
	private bool disposed;
	private Texture texturePreview;
	private TextureArray textureArray;
	
	private List<AutomapPattern> automapPatterns;
	private List<PresetPattern> presetPatterns;
	
	private string textureFileFullPath;
	private FileSystemWatcher textureFileWatcher;

	internal Tileset(File file) {
		this.file = file;
		
		id = "new_tileset";
		textureFilePath = "";
		textureFileFullPath = "";
		offset = new(0, 0);
		spacing = new(0, 0);
		size = new(0, 0);
		tileData = new();
		automapPatterns = new();
		presetPatterns = new();
		textureFileWatcher = null;
	}

	internal void Parse(XElement tilesetElement) {
		id = tilesetElement.Attribute("id").Value;
		group = tilesetElement.Attribute("group").Value;
		textureFilePath =  tilesetElement.Attribute("texture_file").Value;
		offset.X = tilesetElement.Attribute("px_offset.x").ParseAsInt();
		offset.Y = tilesetElement.Attribute("px_offset.y").ParseAsInt();
		spacing.X = tilesetElement.Attribute("px_spacing.x").ParseAsInt();
		spacing.Y = tilesetElement.Attribute("px_spacing.y").ParseAsInt();
		size.X = tilesetElement.Attribute("px_size.x").ParseAsInt(file.World.TileWidth);
		size.Y = tilesetElement.Attribute("px_size.y").ParseAsInt(file.World.TileHeight);
		foreach(var automapElement in tilesetElement.Elements("automap")) {
			AutomapPattern automap = new AutomapPattern(this, automapElement.Attribute("name").ParseAsString());
			string type = automapElement.Attribute("type").ParseAsString();
			if(type == "Mask2x2") {
				automap.MaskType = AutomapMaskType.Mask2x2;
			} else if(type == "Mask3x3") {
				automap.MaskType = AutomapMaskType.Mask3x3;
			}
			foreach(var tileElement in automapElement.Elements("tile")) {
				int id = tileElement.Attribute("num").ParseAsInt();
				uint bitmask = (uint)tileElement.Attribute("mask").ParseAsInt();
				automap.Set(id, bitmask);
			}
			automapPatterns.Add(automap);
		}
		foreach(var presetElement in tilesetElement.Elements("preset")) {
			// TODO
		}
		foreach(var tileElement in tilesetElement.Elements("tile")) {
			int id = tileElement.Attribute("num").ParseAsInt();
			var data = new TileData(id);
			foreach(var shapeElement in tileElement.Elements("shape")) {
				Vector2 p = new(0);
				Vector2 s = new(0);
				p.X = shapeElement.Attribute("position.x").ParseAsFloat();
				p.Y = shapeElement.Attribute("position.y").ParseAsFloat();
				s.X = shapeElement.Attribute("size.x").ParseAsFloat();
				s.Y = shapeElement.Attribute("size.y").ParseAsFloat();
				data.Shapes.Add(new TileShape(p, s));
			}
			tileData.Add(id, data);
		}
		
		OnTextureFilePathChanged();
	}

	internal XElement Serialize() {
		var element = new XElement("tileset");
		element.Add(
			new XAttribute("id", id),
			new XAttribute("group", group),
			new XAttribute("texture_file", textureFilePath),
			new XAttribute("px_offset.x", offset.X),
			new XAttribute("px_offset.y", offset.Y),
			new XAttribute("px_spacing.x", spacing.X),
			new XAttribute("px_spacing.y", spacing.Y)
			// new XAttribute("px_texels_x", size.X),
			// new XAttribute("px_texels_y", size.Y)
		);
		foreach(var automap in automapPatterns) {
			XElement automapElement = new XElement("automap");
			automapElement.Add(new XAttribute("name", automap.Name));
			automapElement.Add(new XAttribute("type", automap.MaskType));
			foreach(var pair in automap.TileList) {
				XElement tileElement = new XElement("tile");
				tileElement.Add(new XAttribute("num", pair.Key));
				tileElement.Add(new XAttribute("mask", pair.Value));
				automapElement.Add(tileElement);
			}
			element.Add(automapElement);
		}
		foreach(var preset in presetPatterns) {
			// TODO
		}
		foreach(var data in tileData) {
			XElement tileElement = new XElement("tile");
			tileElement.Add(new XAttribute("num", data.Key));
			foreach(var shape in data.Value.Shapes) {
				XElement s = new XElement("shape");
				s.Add(
					new XAttribute("position.x", shape.Position.X),
					new XAttribute("position.y", shape.Position.Y),
					new XAttribute("size.x", shape.Size.X),
					new XAttribute("size.y", shape.Size.Y)
				);
				tileElement.Add(s);
			}
			element.Add(tileElement);
		}
		return element;
	}

	internal void SetTexturePath(string path, bool updateResources = true) {
		textureFilePath = path;
		textureFileFullPath = file.GetPath(textureFilePath);
		if(updateResources) {
			UpdateFileWatcher();
			ReloadTexture();
		}
	}

	private void OnTextureFilePathChanged() {
		if(textureFilePath == null) textureFilePath = "";
		textureFileFullPath = file.GetPath(textureFilePath);
		UpdateFileWatcher();
		ReloadTexture();
	}

	private void OnTextureFileChanged(object sender, FileSystemEventArgs e) {
		Program.SendMessage(ReloadTexture);
	}

	public void UpdateFileWatcher() {
		if(textureFileWatcher == null) {
			textureFileWatcher = new FileSystemWatcher();
			textureFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
			textureFileWatcher.Changed += OnTextureFileChanged;
		}
		textureFileWatcher.Path = Path.GetDirectoryName(textureFileFullPath).Replace('\\', '/');
		textureFileWatcher.Filter = Path.GetFileName(textureFilePath);
		textureFileWatcher.EnableRaisingEvents = textureFilePath != "";
	}

	public Texture GetTexturePreview() => texturePreview;

	public TextureArray GetTextureArray() => textureArray;

	public int GetTextureWidth() {
		if(texturePreview == null) return 0;
		return texturePreview.Width;
	}
	
	public int GetTextureHeight() {
		if(texturePreview == null) return 0;
		return texturePreview.Height;
	}

	public int GetTileCount() => GetTileCountX() * GetTileCountY();

	public int GetTileCountX() {
		if(texturePreview == null || size.X + spacing.X == 0) return 0;
		return (texturePreview.Width - offset.X + spacing.X) / (size.X + spacing.X);
	}

	public int GetTileCountY() {
		if(texturePreview == null || size.Y + spacing.Y == 0) return 0;
		return (texturePreview.Height - offset.Y + spacing.Y) / (size.Y + spacing.Y);
	}

	public Rectangle GetTileRegion(int tileIndex) {
		int cx = GetTileCountX();
		int cy = GetTileCountY();

		if(tileIndex < 0 || tileIndex >= cx * cy) return new(0, 0, 0, 0);

		int tx = tileIndex % cx;
		int ty = tileIndex / cx;

		return new(
			offset.X + tx * size.X,
			offset.Y + ty * size.Y,
			size.X,
			size.Y
		);
	}

	public TileData GetTileData(int id) {
		if(!tileData.ContainsKey(id)) return null;
		return tileData[id];
	}

	public TileData AddTileData(int id) {
		if(tileData.ContainsKey(id)) return tileData[id];
		if(id < 1 || id > GetTileCount()) return null;
		var data = new TileData(id);
		tileData.Add(id, data);
		return data;
	}

	public AutomapPattern CreateAutomapPattern(string name = "New Automap Pattern") {
		AutomapPattern pattern = new AutomapPattern(this, name);
		automapPatterns.Add(pattern);
		return pattern;
	}
	
	public void RemoveAutoMapPattern(AutomapPattern pattern) {
		RemoveAutoMapPattern(automapPatterns.IndexOf(pattern));
	}

	public void RemoveAutoMapPattern(int index) {
		if(index < 0 || index >= automapPatterns.Count) return;
		automapPatterns.RemoveAt(index);
	}

	public void ReloadTexture() {
		if(texturePreview != null) {
			texturePreview.Dispose();
		}
		if(textureArray != null) {
			textureArray.Dispose();
		}
		
		if(!System.IO.File.Exists(textureFileFullPath)) {
			return;
		}

		GL gl = Program.GL;

		try {
			byte[] raw = System.IO.File.ReadAllBytes(textureFileFullPath);

			texturePreview = Texture.LoadFromMemory(raw);

			ImageResult image = ImageResult.FromMemory(raw, ColorComponents.RedGreenBlueAlpha);

			int tileCountX = GetTileCountX();
			int tileCountY = GetTileCountY();
			int tileCount = tileCountX * tileCountY;

			textureArray = new TextureArray();

			textureArray.Bind();

			gl.TexParameterI(GLEnum.Texture2DArray, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
			gl.TexParameterI(GLEnum.Texture2DArray, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
			gl.TexParameterI(GLEnum.Texture2DArray, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
			gl.TexParameterI(GLEnum.Texture2DArray, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

			gl.TexStorage3D(TextureTarget.Texture2DArray, 1, GLEnum.Rgba8, (uint)size.X, (uint)size.Y, (uint)tileCount);

			var readOnlyData = new ReadOnlySpan<byte>(image.Data);

			gl.PixelStore(GLEnum.UnpackRowLength, image.Width);
			for(int y = 0; y < tileCountY; y++) {
				for(int x = 0; x < tileCountX; x++) {
					int ox = x * size.X;
					int oy = y * size.Y;
					int tile = y * tileCountX + x;

					gl.PixelStore(GLEnum.UnpackSkipPixels, ox);
					gl.PixelStore(GLEnum.UnpackSkipRows, oy);

					gl.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, tile, (uint)size.X, (uint)size.Y, 1,
						GLEnum.Rgba, GLEnum.UnsignedByte, readOnlyData);
				}
			}

			textureArray.UnBind();

			gl.PixelStore(GLEnum.UnpackRowLength, 0);
			gl.PixelStore(GLEnum.UnpackSkipPixels, 0);
			gl.PixelStore(GLEnum.UnpackSkipRows, 0);
		} catch(IOException e) {
			
		} catch(UnauthorizedAccessException e) {
			
		} catch(Exception e) {
			Log.Error(e, "Failed to load tileset texture: {@textureFileFullPath}", textureFileFullPath);
		}
	}

	public void ReleaseResources() {
		textureFileWatcher?.Dispose();
		texturePreview?.Dispose();
		textureArray?.Dispose();
		textureFileWatcher = null;
		texturePreview = null;
		textureArray = null;
	}
	
	public class AddOperation : IFileEditOperation {
		private World world;
		private Tileset tileset;
		private int index;
		public AddOperation(World world, Tileset tileset) {
			this.world = world;
			this.tileset = tileset;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.world.AddTileset(op.tileset);
			op.tileset.UpdateFileWatcher();
			op.tileset.ReloadTexture();
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.world.RemoveTileset(op.tileset);
			op.tileset.ReleaseResources();
		}
		public bool HasChanges() => true;
	}

	public class NameOperation : IFileEditOperation {
		private Tileset tileset;
		private string oldValue;
		private string newValue;
		public NameOperation(Tileset tileset, string newValue) {
			this.tileset = tileset;
			this.oldValue = tileset.ID;
			this.newValue = newValue;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<NameOperation>();
			op.tileset.ID = op.newValue;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<NameOperation>();
			op.tileset.ID = op.oldValue;
		}
		public bool HasChanges() => oldValue != newValue;
	}
	
	public class GroupOperation : IFileEditOperation {
		private Tileset tileset;
		private string oldValue;
		private string newValue;
		public GroupOperation(Tileset tileset, string newValue) {
			this.tileset = tileset;
			this.oldValue = tileset.Group;
			this.newValue = newValue;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<GroupOperation>();
			op.tileset.Group = op.newValue;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<GroupOperation>();
			op.tileset.Group = op.oldValue;
		}
		public bool HasChanges() => oldValue != newValue;
	}
	
}

public class TileData {
	public int Tile => tile;
	public List<TileShape> Shapes {
		get => shapes;
		set => shapes = value;
	}
	private int tile;
	private List<TileShape> shapes;
	public TileData(int tile) {
		this.tile = tile;
		this.shapes = new List<TileShape>();
	}
	public class ShapeCountOperation : IFileEditOperation {
		public List<TileShape> NewList => newList;
		private TileData data;
		private List<TileShape> oldList;
		private List<TileShape> newList;
		public ShapeCountOperation(TileData data, Tileset tileset, int newCount) {
			this.data = data;
			this.oldList = data.Shapes;
			this.newList = new();
			int i = 0;
			while(i < newCount && i < data.Shapes.Count) {
				this.newList.Add(data.Shapes[i]);
				i++;
			}
			while(i < newCount) {
				this.newList.Add(new TileShape(new(0, 0), new(tileset.SizeX, tileset.SizeY)));
				i++;
			}
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<ShapeCountOperation>();
			op.data.Shapes = op.newList;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<ShapeCountOperation>();
			op.data.Shapes = op.oldList;
		}
		public bool HasChanges() => oldList.Count != newList.Count;
	}
	public class ShapeEditOperation : IFileEditOperation {
		public TileShape NewShape => newShape;
		private TileData data;
		private int index;
		private TileShape oldShape;
		private TileShape newShape;
		public ShapeEditOperation(TileData data, int index) {
			this.data = data;
			this.index = index;
			this.oldShape = data.Shapes[index];
			this.newShape = data.Shapes[index];
		}
		public void SetPosition(Vector2 position) {
			newShape.Position = position;
		}
		public void SetSize(Vector2 size) {
			newShape.Size = size;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<ShapeEditOperation>();
			op.data.Shapes[op.index] = op.newShape;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<ShapeEditOperation>();
			op.data.Shapes[op.index] = op.oldShape;
		}
		public bool HasChanges() => oldShape != newShape;
	}
}

public struct TileShape {
	public Vector2 Position;
	public Vector2 Size;
	public TileShape() {
		Position = new(0);
		Size = new(0);
	}
	public TileShape(Vector2 p, Vector2 s) {
		Position = p;
		Size = s;
	}
	public static bool operator==(TileShape obj1, TileShape obj2) {
		return (obj1.Position == obj2.Position && obj1.Size == obj2.Size);
	}
	public static bool operator!=(TileShape obj1, TileShape obj2) {
		return !(obj1.Position == obj2.Position && obj1.Size == obj2.Size);
	}
	public override bool Equals(object obj) {
		return obj is TileShape shape && (this.Position == shape.Position && this.Size == shape.Size);
	}
}

public class PresetPattern {
	
	public Tileset Tileset => tileset;

	public string Name {
		get => name;
		set => name = value;
	}

	public int Width => width;
	public int Height => height;

	private Tileset tileset;
	private string name;
	private int width;
	private int height;
	private int[] tiles;

	public PresetPattern(Tileset tileset, string name, int width, int height) {
		this.tileset = tileset;
		this.name = name;
		this.width = width;
		this.height = height;
		this.tiles = new int[width * height];
	}

	public void SetTile(int x, int y, int tileID) {
		if(x < 0 || x >= width || y < 0 || y >= height) return;
		tiles[x + y * width] = tileID;
	}

	public int GetTile(int x, int y) {
		if(x < 0 || x >= width || y < 0 || y >= height) return 0;
		return tiles[x + y * width];
	}

}

public enum AutomapMaskType {
	Mask2x2,
	Mask3x3,
}

public class AutomapPattern {
	
	public Tileset Tileset => tileset;
	public IEnumerable<KeyValuePair<int, uint>> TileList => tileToBitmask;

	public string Name {
		get => name;
		set => name = value;
	}

	public AutomapMaskType MaskType {
		get => maskType;
		set => maskType = value;
	}

	private Tileset tileset;
	private string name;
	private AutomapMaskType maskType;
	private SortedList<int, uint> tileToBitmask;
	private Dictionary<uint, List<int>> bitmaskToTiles;

	public AutomapPattern(Tileset tileset, string name, AutomapMaskType maskType = AutomapMaskType.Mask2x2) {
		this.tileset = tileset;
		this.name = name;
		this.maskType = maskType;
		this.tileToBitmask = new SortedList<int, uint>();
		this.bitmaskToTiles = new Dictionary<uint, List<int>>();
	}

	public void Set(int tileID, uint bitmask) {
		if(tileToBitmask.TryGetValue(tileID, out uint oldBitmask)) {
			if(bitmask == oldBitmask) return;
			if(bitmaskToTiles.TryGetValue(oldBitmask, out List<int> oldTileList)) {
				if(oldTileList.Count > 1) {
					oldTileList.Remove(tileID);
				} else {
					oldTileList.Clear();
					bitmaskToTiles.Remove(oldBitmask);
				}
			}
		}
		if(bitmask == 0) {
			tileToBitmask.Remove(tileID);
			return;
		}
		if(tileToBitmask.ContainsKey(tileID)) {
			tileToBitmask[tileID] = bitmask;
		} else {
			tileToBitmask.Add(tileID, bitmask);
		}
		if(bitmaskToTiles.TryGetValue(bitmask, out List<int> newTileList)) {
			newTileList.Add(tileID);
		} else {
			newTileList = new List<int>();
			newTileList.Add(tileID);
			bitmaskToTiles.Add(bitmask, newTileList);
		}
	}

	public uint GetMask(int tileID) {
		if(tileToBitmask.TryGetValue(tileID, out var bitmask)) {
			return bitmask;
		} else {
			return 0;
		}
	}
	
	public int GetTile(uint bitmask) {
		if(bitmaskToTiles.TryGetValue(bitmask, out List<int> tileList)) {
			if(tileList.Count > 0) {
				return tileList[0];
			}
		}
		return 0;
	}

	public void Clear() {
		tileToBitmask.Clear();
		bitmaskToTiles.Clear();
	}

	public int Evaluate(uint bitmask) {
		bitmask = maskType switch {
			AutomapMaskType.Mask2x2 => AutomapPattern.Convert2x2Mask(bitmask),
			AutomapMaskType.Mask3x3 => AutomapPattern.Convert3x3Mask(bitmask),
			_ => bitmask
		};
		return GetTile(bitmask);
	}

	public void Print(Tilemap tilemap, int x, int y, Func<int, int, TileRef, bool> setFunc) {
		int tilesetSlot = 0;
		foreach(var link in tilemap.Scene.Tilesets) {
			if(link.Tileset == this.tileset) {
				tilesetSlot = link.Slot;
				break;
			}
		}
		
		setFunc?.Invoke(x, y, new TileRef(Evaluate(0b111111111), tilesetSlot));
		
		for(int ty = 1; ty >= -1; --ty) {
			for(int tx = -1; tx <= 1; ++tx) {
				int ix = x + tx;
				int iy = y + ty;
				if(ix >= 0 && ix < tilemap.Width && iy >= 0 && iy < tilemap.Height) {
					TileRef tile = tilemap.Get(ix, iy);
					if(tile.TilesetSlot == tilesetSlot) {
						if(tileToBitmask.ContainsKey(tile.TileID)) {
							int tileID = GetTile(tilemap, ix, iy, tilesetSlot);
							if(tileID > 0) {
								setFunc?.Invoke(ix, iy, new TileRef(tileID, tilesetSlot));
							}
						}
					}
				}
			}
		}
	}
	
	private int GetTile(Tilemap tilemap, int x, int y, int tilesetSlot) {
		uint bitmask = 0;
		int index = 0;
		for(int ty = 1; ty >= -1; --ty) {
			for(int tx = -1; tx <= 1; ++tx) {
				int ix = x + tx;
				int iy = y + ty;
				if(ix >= 0 && ix < tilemap.Width && iy >= 0 && iy < tilemap.Height) {
					TileRef tile = tilemap.Get(ix, iy);
					if(tile.TilesetSlot == tilesetSlot) {
						if(tileToBitmask.ContainsKey(tile.TileID)) {
							bitmask |= (uint)(1 << index);
						}
					}
				}
				index++;
			}
		}
		return Evaluate(bitmask);
	}
	
	public class AddOperation : IFileEditOperation {
		private Tileset tileset;
		private AutomapPattern pattern;
		public AddOperation(Tileset tileset, AutomapPattern pattern) {
			this.tileset = tileset;
			this.pattern = pattern;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.tileset.AutomapPatterns.Add(op.pattern);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.tileset.AutomapPatterns.Remove(op.pattern);
		}
		public bool HasChanges() => true;
	}
	
	public class MoveOperation : IFileEditOperation {
		private Tileset tileset;
		private int index1;
		private int index2;
		public MoveOperation(Tileset tileset, int index1, int index2) {
			this.tileset = tileset;
			this.index1 = index1;
			this.index2 = index2;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			var t = op.tileset.AutomapPatterns[op.index1];
			op.tileset.AutomapPatterns[op.index1] = op.tileset.AutomapPatterns[op.index2];
			op.tileset.AutomapPatterns[op.index2] = t;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			var t = op.tileset.AutomapPatterns[op.index2];
			op.tileset.AutomapPatterns[op.index2] = op.tileset.AutomapPatterns[op.index1];
			op.tileset.AutomapPatterns[op.index1] = t;
		}
		public bool HasChanges() => true;
	}
	
	public class RemoveOperation : IFileEditOperation {
		private Tileset tileset;
		private AutomapPattern pattern;
		private int index;
		public RemoveOperation(Tileset tileset, AutomapPattern pattern) {
			this.tileset = tileset;
			this.pattern = pattern;
			this.index = tileset.AutomapPatterns.IndexOf(pattern);
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.tileset.AutomapPatterns.Remove(op.pattern);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.tileset.AutomapPatterns.Insert(op.index, op.pattern);
		}
		public bool HasChanges() => true;
	}
	
	public class NameOperation : IFileEditOperation {
		private AutomapPattern pattern;
		private string oldValue;
		private string newValue;
		public NameOperation(AutomapPattern pattern, string newValue) {
			this.pattern = pattern;
			this.oldValue = pattern.name;
			this.newValue = newValue;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<NameOperation>();
			op.pattern.name = op.newValue;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<NameOperation>();
			op.pattern.name = op.oldValue;
		}
		public bool HasChanges() => oldValue != newValue;
	}
	
	public class MaskTypeOperation : IFileEditOperation {
		private AutomapPattern pattern;
		private AutomapMaskType oldValue;
		private AutomapMaskType newValue;
		public MaskTypeOperation(AutomapPattern pattern, AutomapMaskType newValue) {
			this.pattern = pattern;
			this.oldValue = pattern.maskType;
			this.newValue = newValue;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<MaskTypeOperation>();
			op.pattern.maskType = op.newValue;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<MaskTypeOperation>();
			op.pattern.maskType = op.oldValue;
		}
		public bool HasChanges() => oldValue != newValue;
	}
	
	public class BitmaskOperation : IFileEditOperation {
		public AutomapPattern Pattern => pattern;
		public int TileID => tileID;
		public bool Adding => adding;
		private AutomapPattern pattern;
		private int tileID;
		private bool adding;
		private uint oldBitmask;
		private uint newBitmask;
		public BitmaskOperation(AutomapPattern pattern, int tileID, bool adding) {
			this.pattern = pattern;
			this.tileID = tileID;
			this.adding = adding;
			this.oldBitmask = pattern.GetMask(tileID);
			this.newBitmask = this.oldBitmask;
		}
		public void SetBitmask(uint bitmask) {
			newBitmask = bitmask;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<BitmaskOperation>();
			op.pattern.Set(op.tileID, op.newBitmask);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<BitmaskOperation>();
			op.pattern.Set(op.tileID, op.oldBitmask);
		}
		public bool HasChanges() => newBitmask != oldBitmask;
	}
	
	public static uint Convert2x2Mask(uint mask) {
		// 4 8
		// 1 2
		uint newMask = 0;
		if((mask & 1 << 0) > 0 && (mask & 1 << 1) > 0 && (mask & 1 << 3) > 0) {
			newMask |= 1 << 0;
		}
		if((mask & 1 << 1) > 0 && (mask & 1 << 2) > 0 && (mask & 1 << 5) > 0) {
			newMask |= 1 << 1;
		}
		if((mask & 1 << 3) > 0 && (mask & 1 << 6) > 0 && (mask & 1 << 7) > 0) {
			newMask |= 1 << 2;
		}
		if((mask & 1 << 5) > 0 && (mask & 1 << 7) > 0 && (mask & 1 << 8) > 0) {
			newMask |= 1 << 3;
		}
		return newMask;
	}

	public static uint Convert3x3Mask(uint mask) {
		// 64 128 256
		//  8  16  32
		//  1   2   4
		switch (mask) {
			// Left
			case 1 + 16 + 32:
			case 4 + 16 + 32:
			case 16 + 32 + 64:
			case 16 + 32 + 256:
			case 1 + 16 + 32 + 64:
			case 4 + 16 + 32 + 256:
			case 1 + 4 + 16 + 32:
			case 1 + 16 + 32 + 256:
			case 4 + 16 + 32 + 64:
			case 16 + 32 + 64 + 256:
			case 1 + 16 + 32 + 64 + 256:
			case 1 + 4 + 16 + 32 + 64:
			case 1 + 4 + 16 + 32 + 256:
			case 4 + 16 + 32 + 64 + 256:
			case 1 + 4 + 16 + 32 + 64 + 256: {
				mask = 16 + 32;
				break;
			}
			// Right
			case 1 + 8 + 16:
			case 8 + 16 + 64:
			case 1 + 8 + 16 + 64:
			case 4 + 8 + 16:
			case 8 + 16 + 256:
			case 1 + 4 + 8 + 16:
			case 1 + 8 + 16 + 256:
			case 4 + 8 + 16 + 64:
			case 4 + 8 + 16 + 256:
			case 8 + 16 + 64 + 256:
			case 1 + 4 + 8 + 16 + 64:
			case 1 + 4 + 8 + 16 + 256:
			case 1 + 8 + 16 + 64 + 256:
			case 4 + 8 + 16 + 64 + 256:
			case 1 + 4 + 8 + 16 + 64 + 256: {
				mask = 8 + 16;
				break;
			}
			// Top
			case 1 + 2 + 16:
			case 2 + 4 + 16:
			case 1 + 2 + 4 + 16:
			case 2 + 16 + 64:
			case 2 + 16 + 256:
			case 2 + 16 + 64 + 256:
			case 1 + 2 + 16 + 64:
			case 1 + 2 + 16 + 256:
			case 1 + 2 + 16 + 64 + 256:
			case 2 + 4 + 16 + 64:
			case 2 + 4 + 16 + 256:
			case 2 + 4 + 16 + 64 + 256:
			case 1 + 2 + 4 + 16 + 64:
			case 1 + 2 + 4 + 16 + 256:
			case 1 + 2 + 4 + 16 + 64 + 256: {
				mask = 2 + 16;
				break;
			}
			// Bottom
			case 16 + 64 + 128:
			case 16 + 128 + 256:
			case 16 + 64 + 128 + 256:
			case 1 + 16 + 64 + 128:
			case 4 + 16 + 64 + 128:
			case 1 + 4 + 16 + 64 + 128:
			case 1 + 16 + 128 + 256:
			case 4 + 16 + 128 + 256:
			case 1 + 4 + 16 + 128 + 256:
			case 1 + 16 + 64 + 128 + 256:
			case 4 + 16 + 64 + 128 + 256:
			case 1 + 4 + 16 + 64 + 128 + 256:
			case 1 + 16 + 128:
			case 4 + 16 + 128:
			case 1 + 4 + 16 + 128: {
				mask = 16 + 128;
				break;
			}
			// Vertical Straight
			case 1 + 2 + 16 + 128:
			case 2 + 4 + 16 + 128:
			case 1 + 2 + 4 + 16 + 128:
			case 2 + 16 + 64 + 128:
			case 2 + 16 + 128 + 256:
			case 2 + 16 + 64 + 128 + 256:
			case 1 + 2 + 16 + 64 + 128:
			case 1 + 2 + 16 + 128 + 256:
			case 2 + 4 + 16 + 64 + 128:
			case 2 + 4 + 16 + 128 + 256:
			case 1 + 2 + 16 + 64 + 128 + 256:
			case 1 + 2 + 4 + 64 + 128 + 256:
			case 1 + 2 + 4 + 16 + 128 + 256:
			case 1 + 2 + 4 + 16 + 64 + 128:
			case 2 + 4 + 16 + 64 + 128 + 256:
			case 1 + 2 + 4 + 16 + 64 + 128 + 256: {
				mask = 2 + 16 + 128;
				break;
			}
			// Horizontal Straight
			case 1 + 8 + 16 + 32:
			case 8 + 16 + 32 + 64:
			case 1 + 8 + 16 + 32 + 64:
			case 4 + 8 + 16 + 32:
			case 8 + 16 + 32 + 256:
			case 4 + 8 + 16 + 32 + 64:
			case 4 + 8 + 16 + 32 + 256:
			case 1 + 4 + 8 + 16 + 32:
			case 1 + 8 + 16 + 32 + 256:
			case 8 + 16 + 32 + 64 + 256:
			case 1 + 4 + 8 + 16 + 32 + 64:
			case 1 + 4 + 8 + 16 + 32 + 256:
			case 1 + 8 + 16 + 32 + 64 + 256:
			case 4 + 8 + 16 + 32 + 64 + 256:
			case 1 + 4 + 8 + 16 + 32 + 64 + 256: {
				mask = 8 + 16 + 32;
				break;
			}
			// Top Left Corner
			case 1 + 2 + 4 + 16 + 32:
			case 2 + 4 + 16 + 32 + 256:
			case 2 + 4 + 16 + 32 + 64:
			case 1 + 2 + 4 + 16 + 32 + 256:
			case 1 + 2 + 4 + 16 + 32 + 64:
			case 2 + 4 + 16 + 32 + 64 + 256:
			case 1 + 2 + 4 + 16 + 32 + 64 + 256: {
				mask = 2 + 4 + 16 + 32;
				break;
			}
			// Bottom Left Corner
			case 1 + 16 + 32 + 128 + 256:
			case 4 + 16 + 32 + 128 + 256:
			case 16 + 32 + 64 + 128 + 256:
			case 4 + 16 + 32 + 64 + 128 + 256:
			case 1 + 4 + 16 + 32 + 128 + 256:
			case 1 + 16 + 32 + 64 + 128 + 256:
			case 1 + 4 + 16 + 32 + 64 + 128 + 256: {
				mask = 16 + 32 + 128 + 256;
				break;
			}
			// Top Right Corner
			case 1 + 2 + 4 + 8 + 16:
			case 1 + 2 + 8 + 16 + 64:
			case 1 + 2 + 8 + 16 + 256:
			case 1 + 2 + 4 + 8 + 16 + 64:
			case 1 + 2 + 8 + 16 + 64 + 256:
			case 1 + 2 + 4 + 8 + 16 + 256:
			case 1 + 2 + 4 + 8 + 16 + 64 + 256: {
				mask = 1 + 2 + 8 + 16;
				break;
			}
			// Bottom Right Corner
			case 1 + 8 + 16 + 64 + 128:
			case 8 + 16 + 64 + 128 + 256:
			case 4 + 8 + 16 + 64 + 128:
			case 1 + 4 + 8 + 16 + 64 + 128:
			case 1 + 8 + 16 + 64 + 128 + 256:
			case 4 + 8 + 16 + 64 + 128 + 256:
			case 1 + 4 + 8 + 16 + 64 + 128 + 256: {
				mask = 8 + 16 + 64 + 128;
				break;
			}
			// Full Top
			case 1 + 2 + 4 + 8 + 16 + 32 + 64:
			case 1 + 2 + 4 + 8 + 16 + 32 + 256:
			case 1 + 2 + 4 + 8 + 16 + 32 + 64 + 256: {
				mask = 1 + 2 + 4 + 8 + 16 + 32;
				break;
			}
			// Full Bottom
			case 1 + 8 + 16 + 32 + 64 + 128 + 256:
			case 4 + 8 + 16 + 32 + 64 + 128 + 256:
			case 1 + 4 + 8 + 16 + 32 + 64 + 128 + 256: {
				mask = 8 + 16 + 32 + 64 + 128 + 256;
				break;
			}
			// Full Left
			case 1 + 2 + 4 + 16 + 32 + 128 + 256:
			case 2 + 4 + 16 + 32 + 64 + 128 + 256:
			case 1 + 2 + 4 + 16 + 32 + 64 + 128 + 256: {
				mask = 2 + 4 + 16 + 32 + 128 + 256;
				break;
			}
			// Full Right
			case 1 + 2 + 4 + 8 + 16 + 64 + 128:
			case 1 + 2 + 8 + 16 + 64 + 128 + 256:
			case 1 + 2 + 4 + 8 + 16 + 64 + 128 + 256: {
				mask = 1 + 2 + 8 + 16 + 64 + 128;
				break;
			}
			// Top Left Tricorner
			case 1 + 2 + 16 + 32:
			case 2 + 16 + 32 + 64:
			case 2 + 16 + 32 + 256:
			case 1 + 2 + 16 + 32 + 64:
			case 1 + 2 + 16 + 32 + 256:
			case 2 + 16 + 32 + 64 + 256:
			case 1 + 2 + 16 + 32 + 64 + 256: {
				mask = 2 + 16 + 32;
				break;
			}
			// Bottom Left Tricorner
			case 1 + 16 + 32 + 128:
			case 4 + 16 + 32 + 128:
			case 16 + 32 + 64 + 128:
			case 4 + 16 + 32 + 64 + 128:
			case 1 + 16 + 32 + 64 + 128:
			case 1 + 4 + 16 + 32 + 64 + 128:
			case 1 + 4 + 16 + 32 + 128: {
				mask = 16 + 32 + 128;
				break;
			}
			// Top Right Tricorner
			case 2 + 4 + 8 + 16:
			case 2 + 8 + 16 + 64:
			case 2 + 8 + 16 + 256:
			case 2 + 4 + 8 + 16 + 64:
			case 2 + 8 + 16 + 64 + 256:
			case 2 + 4 + 8 + 16 + 256:
			case 2 + 4 + 8 + 16 + 64 + 256: {
				mask = 2 + 8 + 16;
				break;
			}
			// Bottom Right Tricorner
			case 1 + 8 + 16 + 128:
			case 4 + 8 + 16 + 128:
			case 8 + 16 + 128 + 256:
			case 1 + 8 + 16 + 128 + 256:
			case 1 + 4 + 8 + 16 + 128:
			case 4 + 8 + 16 + 128 + 256:
			case 1 + 4 + 8 + 16 + 128 + 256: {
				mask = 8 + 16 + 128;
				break;
			}
			// Three-way Left
			case 2 + 4 + 8 + 16 + 128:
			case 2 + 8 + 16 + 128 + 256:
			case 2 + 4 + 8 + 16 + 128 + 256: {
				mask = 2 + 8 + 16 + 128;
				break;
			}
			// Three-way Right
			case 1 + 2 + 16 + 32 + 128:
			case 2 + 16 + 32 + 64 + 128:
			case 1 + 2 + 16 + 32 + 64 + 128: {
				mask = 2 + 16 + 32 + 128;
				break;
			}
			// Three-way Top
			case 1 + 8 + 16 + 32 + 128:
			case 4 + 8 + 16 + 32 + 128:
			case 1 + 4 + 8 + 16 + 32 + 128: {
				mask = 8 + 16 + 32 + 128;
				break;
			}
			// Three-way Bottom
			case 2 + 8 + 16 + 32 + 64:
			case 2 + 8 + 16 + 32 + 256:
			case 2 + 8 + 16 + 32 + 64 + 256: {
				mask = 2 + 8 + 16 + 32;
				break;
			}
			// Three-corner Top Left
			case 2 + 4 + 8 + 16 + 32 + 64:
			case 2 + 4 + 8 + 16 + 32 + 256:
			case 2 + 4 + 8 + 16 + 32 + 64 + 256: {
				mask = 2 + 4 + 8 + 16 + 32;
				break;
			}
			// Three-corner Bottom Left
			case 1 + 8 + 16 + 32 + 128 + 256:
			case 4 + 8 + 16 + 32 + 128 + 256:
			case 1 + 4 + 8 + 16 + 32 + 128 + 256: {
				mask = 8 + 16 + 32 + 128 + 256;
				break;
			}
			// Three-corner Top Right
			case 1 + 2 + 8 + 16 + 32 + 64:
			case 1 + 2 + 8 + 16 + 32 + 256:
			case 1 + 2 + 8 + 16 + 32 + 64 + 256: {
				mask = 1 + 2 + 8 + 16 + 32;
				break;
			}
			// Three-corner Bottom Right
			case 1 + 8 + 16 + 32 + 64 + 128:
			case 4 + 8 + 16 + 32 + 64 + 128:
			case 1 + 4 + 8 + 16 + 32 + 64 + 128: {
				mask = 8 + 16 + 32 + 64 + 128;
				break;
			}
			// Left Side Top Right Corner
			case 1 + 2 + 4 + 16 + 32 + 128:
			case 2 + 4 + 16 + 32 + 64 + 128:
			case 1 + 2 + 4 + 16 + 32 + 64 + 128: {
				mask = 2 + 4 + 16 + 32 + 128;
				break;
			}
			// Left Side Bottom Right Corner
			case 1 + 2 + 16 + 32 + 128 + 256:
			case 2 + 16 + 32 + 64 + 128 + 256:
			case 1 + 2 + 16 + 32 + 64 + 128 + 256: {
				mask = 2 + 16 + 32 + 128 + 256;
				break;
			}
			// Right Side Top Left Corner
			case 1 + 2 + 4 + 8 + 16 + 128:
			case 1 + 2 + 8 + 16 + 128 + 256:
			case 1 + 2 + 4 + 8 + 16 + 128 + 256: {
				mask = 1 + 2 + 8 + 16 + 128;
				break;
			}
			// Right Side Bottom Left Corner
			case 2 + 4 + 8 + 16 + 64 + 128:
			case 2 + 8 + 16 + 64 + 128 + 256:
			case 2 + 4 + 8 + 16 + 64 + 128 + 256: {
				mask = 2 + 8 + 16 + 64 + 128;
				break;
			}
			// Single
			case 1 + 16:
			case 4 + 16:
			case 16 + 64:
			case 16 + 256:
			case 1 + 4 + 16:
			case 1 + 16 + 64:
			case 1 + 16 + 256:
			case 4 + 16 + 64:
			case 4 + 16 + 256:
			case 16 + 64 + 256:
			case 1 + 4 + 16 + 64:
			case 1 + 4 + 16 + 256:
			case 1 + 16 + 64 + 256:
			case 4 + 16 + 64 + 256:
			case 1 + 4 + 16 + 64 + 256: {
				mask = 16;
				break;
			}
		}

		return mask;
	}

}