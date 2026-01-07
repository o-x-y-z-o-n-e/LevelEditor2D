using System.Buffers;
using System.Xml.Linq;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using StbImageSharp;

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

	private uint textureArrayHandle;
	private uint textureTileCountX;
	private uint textureTileCountY;
	
	public uint TextureArrayHandle => textureArrayHandle;
	public uint TextureTileCountX  => textureTileCountX;
	public uint TextureTileCountY  => textureTileCountY;
	

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

		GL gl = Program.GL;

		string path = file.GetAbsolutePath(textureFilePath);
		
		textureSource = Texture.Load(path);
		
		// temp
		try {
			byte[] raw = System.IO.File.ReadAllBytes(path);

			ImageResult image = ImageResult.FromMemory(raw, ColorComponents.RedGreenBlueAlpha);

			int tileCountX = image.Width / size.X;
			int tileCountY = image.Height / size.Y;
			int tileCount = tileCountX * tileCountY;
			
			// int tileComps = 4;
			// int tileSize = size.X * size.Y * tileComps;
			// byte[] reorganized = new byte[image.Data.Length];
			// for(int i = 0; i < tileCount; i++) {
			// 	for(int py = 0; py < size.Y; py++) {
			// 		for(int px = 0; px < size.X; px++) {
			// 			int dst = 0;
			// 			int src = 0;
			// 			
			// 			dst = (i * tileSize) + ((px + size.X * py) * tileComps);
			// 			int tx = i % tileCountX;
			// 			int ty = i / tileCountX;
            //             
			// 			src = ()
			// 			
			// 			// copy single pixel (all component offsets)
			// 			for(int c = 0; c < tileComps; c++) {
			// 				reorganized[dst+c] = image.Data[src+c];
			// 			}
			// 		}
			// 	}
			// }

			if(textureArrayHandle != 0) {
				gl.DeleteTexture(textureArrayHandle);
			}

			textureArrayHandle = gl.GenTexture();

			gl.BindTexture(TextureTarget.Texture2DArray, textureArrayHandle);
			
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
			
			// gl.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, (uint)size.X, (uint)size.Y, (uint)tileCount, GLEnum.Rgba8, GLEnum.UnsignedByte, readOnlyData);
			
			gl.BindTexture(TextureTarget.Texture2DArray, 0);
			
			gl.PixelStore(GLEnum.UnpackRowLength, 0);
			gl.PixelStore(GLEnum.UnpackSkipPixels, 0);
			gl.PixelStore(GLEnum.UnpackSkipRows, 0);
			
		} catch { }
	}
}