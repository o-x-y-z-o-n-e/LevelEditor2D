using Silk.NET.Maths;

namespace L2D; 

public class TileDrawBrush {
	
	public Scene Scene => scene;
	public Tilemap Tilemap => tilemap;

	public int Width => size.X;
	public int Height => size.Y;
	public bool Resizing => resizing;

	public Vector2D<int> TileDrawOffset => new(-size.X / 2, -size.Y / 2);

	private Scene scene;
	private Tilemap tilemap;
	private Vector2D<int> size;
	private bool resizing;

	public TileDrawBrush(Scene scene) {
		this.scene = scene;
		tilemap = null;
		size = new(0, 0);
		resizing = true;
		SetSize(5, 4, true);
	}

	public void SetSize(int w, int h, bool set = true) {
		if(w < 1 || h < 1) return;
		
		size.X = w;
		size.Y = h;
		resizing = !set;
		
		if(resizing) return;
		
		if(tilemap != null) {
			tilemap.Resize(size.X, size.Y);
		} else {
			tilemap = new Tilemap(this);
		}
		
		// for(int x = 0; x < size.X && x < w; x++) {
		// 	for(int y = 0; y < size.Y && y < h; y++) {
		// 		tilemap.Grid[x, y].TileID = 2;
		// 		tilemap.Grid[x, y].TilesetSlot = 1;
		// 	}
		// }
		// 
		// tilemap.Grid[0, 0].TileID = 0;
		// tilemap.Grid[2, 3].TileID = 0;
		// 
		// tilemap.Grid[2, 2].TileID = 5;
	}

	public void SetTile(int x, int y, int tileID, int tilesetSlot) {
		if(resizing || x < 0 || y < 0 || x >= size.X || y >= size.Y) return;
		tilemap.Grid[x, y].TileID = tileID;
		tilemap.Grid[x, y].TilesetSlot = tilesetSlot;
	}

	public bool HasTile(int x, int y) {
		if(x < 0 || y < 0 || x >= size.X || y >= size.Y) return false;
		return tilemap.Grid[x, y].TileID > 0 && tilemap.Grid[x, y].TilesetSlot > 0;
	}

}