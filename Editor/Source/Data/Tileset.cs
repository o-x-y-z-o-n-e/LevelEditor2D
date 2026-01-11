using System.Buffers;
using System.Xml.Linq;
using Silk.NET.Maths;
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
	private Vector2D<int> offset;
	private Vector2D<int> spacing;
	private Vector2D<int> size;
	private bool disposed;
	private Texture texturePreview;
	private TextureArray textureArray;

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

	internal XElement Serialize() {
		var element = new XElement("tileset");
		element.Add(
			new XAttribute("id", id),
			new XAttribute("group", group),
			new XAttribute("texture_file", textureFilePath),
			new XAttribute("px_offset_x", offset.X),
			new XAttribute("px_offset_y", offset.Y),
			new XAttribute("px_spacing_x", spacing.X),
			new XAttribute("px_spacing_y", spacing.Y)
			// new XAttribute("px_texels_x", size.X),
			// new XAttribute("px_texels_y", size.Y)
		);
		return element;
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
		if(texturePreview != null) {
			texturePreview.Dispose();
		}

		GL gl = Program.GL;

		string path = file.GetAbsolutePath(textureFilePath);
		
		texturePreview = Texture.LoadFromFile(path);
		
		// temp
		try {
			byte[] raw = System.IO.File.ReadAllBytes(path);

			ImageResult image = ImageResult.FromMemory(raw, ColorComponents.RedGreenBlueAlpha);

			int tileCountX = GetTileCountX();
			int tileCountY = GetTileCountY();
			int tileCount = tileCountX * tileCountY;

			if(textureArray != null) {
				textureArray.Dispose();
			}

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
					
					gl.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, tile, (uint)size.X, (uint)size.Y, 1, GLEnum.Rgba, GLEnum.UnsignedByte, readOnlyData);
				}
			}
			
			textureArray.UnBind();
			
			gl.PixelStore(GLEnum.UnpackRowLength, 0);
			gl.PixelStore(GLEnum.UnpackSkipPixels, 0);
			gl.PixelStore(GLEnum.UnpackSkipRows, 0);
			
		} catch { }
	}

	public void Dispose() {
		if(disposed) return;
		texturePreview?.Dispose();
		textureArray?.Dispose();
		disposed = true;
	}
}