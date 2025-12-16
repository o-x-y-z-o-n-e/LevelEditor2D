using System.Xml.Linq;
using Silk.NET.Maths;

namespace L2D; 

public class Tileset {

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

	private File file;
	private string id;
	private string group;
	private string textureFilePath;
	private Texture textureSource;
	private Vector2D<int> offset;
	private Vector2D<int> spacing;
	private Vector2D<int> size;

	internal Tileset(File file) {
		this.file = file;
		
		id = "new_tileset";
		textureFilePath = "";
		offset = new(0, 0);
		spacing = new(0, 0);
		size = new(0, 0);
	}

	internal void Parse(XElement tilesetElement) {
		id = tilesetElement.Attribute("id").Value;
		group = tilesetElement.Attribute("group").Value;
		textureFilePath =  tilesetElement.Attribute("texture_file").Value;
		offset.X = tilesetElement.Attribute("px_offset_x").ParseAsInt();
		offset.Y = tilesetElement.Attribute("px_offset_y").ParseAsInt();
		spacing.X = tilesetElement.Attribute("px_spacing_x").ParseAsInt();
		spacing.Y = tilesetElement.Attribute("px_spacing_y").ParseAsInt();
		size.X = tilesetElement.Attribute("px_size_x").ParseAsInt(file.World.TileWidth);
		size.Y = tilesetElement.Attribute("px_size_y").ParseAsInt(file.World.TileHeight);
		
		ReloadTexture();
	}

	public Texture GetTexture() {
		return textureSource;
	}

	public int GetTextureWidth() {
		if(textureSource == null) return 0;
		return textureSource.Width;
	}
	
	public int GetTextureHeight() {
		if(textureSource == null) return 0;
		return textureSource.Height;
	}

	public int GetTileCount() => GetTileCountX() * GetTileCountY();

	public int GetTileCountX() {
		if(textureSource == null || size.X + spacing.X == 0) return 0;
		return (textureSource.Width - offset.X + spacing.X) / (size.X + spacing.X);
	}

	public int GetTileCountY() {
		if(textureSource == null || size.Y + spacing.Y == 0) return 0;
		return (textureSource.Height - offset.Y + spacing.Y) / (size.Y + spacing.Y);
	}

	public Rectangle<int> GetTileRegion(int tileIndex) {
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

	public void ReloadTexture() {
		if(textureSource != null) {
			textureSource.Dispose();
		}
		textureSource = Texture.Load(file.GetAbsolutePath(textureFilePath));
	}
}