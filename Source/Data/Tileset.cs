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
		foreach(var data in tileData) {
			XElement e = new XElement("tile");
			e.Add(new XAttribute("num", data.Key));
			foreach(var shape in data.Value.Shapes) {
				XElement s = new XElement("shape");
				s.Add(
					new XAttribute("position.x", shape.Position.X),
					new XAttribute("position.y", shape.Position.Y),
					new XAttribute("size.x", shape.Size.X),
					new XAttribute("size.y", shape.Size.Y)
				);
				e.Add(s);
			}
			element.Add(e);
		}
		return element;
	}

	private void OnTextureFilePathChanged() {
		if(textureFilePath == null) textureFilePath = "";
		textureFileFullPath = file.GetPath(textureFilePath);
		if(textureFileWatcher == null) {
			textureFileWatcher = new FileSystemWatcher();
			textureFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
			textureFileWatcher.Changed += OnTextureFileChanged;
		}
		textureFileWatcher.Path = Path.GetDirectoryName(textureFileFullPath).Replace('\\', '/');
		textureFileWatcher.Filter = Path.GetFileName(textureFilePath);
		textureFileWatcher.EnableRaisingEvents = textureFilePath != "";
		
		ReloadTexture();
	}

	private void OnTextureFileChanged(object sender, FileSystemEventArgs e) {
		Program.SendMessage(ReloadTexture);
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

	public void Dispose() {
		if(disposed) return;
		texturePreview?.Dispose();
		textureArray?.Dispose();
		textureFileWatcher?.Dispose();
		disposed = true;
	}
}

public class TileData {
	public int Tile => tile;
	public List<TileShape> Shapes => shapes;
	private int tile;
	private List<TileShape> shapes;
	public TileData(int tile) {
		this.tile = tile;
		this.shapes = new List<TileShape>();
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
}