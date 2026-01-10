using System.Drawing;
using System.Numerics;
using ImGuiNET;

namespace L2D; 

public class TileBrush {
	
	public Scene Scene => scene;
	public Tilemap Tilemap => tilemap;

	public int Width => width;
	public int Height => height;
	public bool Resizing => resizing;

	private Scene scene;
	private Tilemap tilemap;
	private int width;
	private int height;
	private bool resizing;
	private Point resizeTileOrigin;
	private bool disposed;

	public TileBrush(Scene scene) {
		this.scene = scene;
		tilemap = null;
		width = 0;
		height = 0;
		resizing = false;
		SetSize(1, 1, true);
	}

	public void SetSize(int w, int h, bool set = true) {
		if(w < 1 || h < 1) return;
		
		width = w;
		height = h;
		resizing = !set;
		
		if(resizing) return;
		
		if(tilemap != null) {
			tilemap.Resize(width, height);
		} else {
			tilemap = new Tilemap(this);
		}
	}

	public void SetTile(int x, int y, int tileID, int tilesetSlot) {
		if(resizing || x < 0 || y < 0 || x >= tilemap.Width || y >= tilemap.Height) return;
		tilemap.Grid[x, y].TileID = tileID;
		tilemap.Grid[x, y].TilesetSlot = tilesetSlot;
	}

	public bool HasTile(int x, int y) {
		if(resizing || x < 0 || y < 0 || x >= tilemap.Width || y >= tilemap.Height) return false;
		return tilemap.Grid[x, y].TileID > 0 && tilemap.Grid[x, y].TilesetSlot > 0;
	}

	public void Dispose() {
		if(disposed) return;
		tilemap?.Dispose();
		disposed = true;
	}
	
	public void Update(ImDrawListPtr drawList, Matrix4x4 transform, Rectangle worldBorder) {
		Layer layer = Program.SelectedLayer;
		if(layer.Scene != scene) return;
		
		Vector2 mousePos = ImGui.GetIO().MousePos;
		Matrix4x4.Invert(transform, out var transformInverted);
		Vector2 mousePosTileCoord = Vector2.Transform(mousePos, transformInverted);

		int mx = (int)MathF.Floor(mousePosTileCoord.X);
		int my = (int)MathF.Floor(mousePosTileCoord.Y);
		
		bool imprint = ImGui.IsMouseDown(ImGuiMouseButton.Left);
		bool resize = ImGui.IsMouseDown(ImGuiMouseButton.Middle);
		
		if(resize) {
			if(!resizing) {
				resizeTileOrigin = new(mx, my);
			}

			int sx = int.Abs(mx - resizeTileOrigin.X) + 1;
			int sy = int.Abs(my - resizeTileOrigin.Y) + 1;
			
			SetSize(sx, sy, false);
		} else if(!resize && resizing) {
			int offsetX = int.Min(resizeTileOrigin.X - mx, 0);
			int offsetY = int.Min(resizeTileOrigin.Y - my, 0);
			
			int trimLeft = 0;
			int trimRight = 0;
			int trimTop = 0;
			int trimBottom = 0;
			
			// left
			for(int x = 0; x < width; x++) {
				int tx = offsetX + mx - scene.WorldX + x;
				if(tx < 0 || tx >= scene.TileCountX) {
					trimLeft++;
					continue;
				}
				bool found = false;
				for(int y = 0; y < height; y++) {
					int ty = offsetY + my - scene.WorldY + y;
					if(ty < 0 || ty >= scene.TileCountY) continue;
					if(layer.Tilemap.Grid[tx, ty].TileID == 0 || layer.Tilemap.Grid[tx, ty].TilesetSlot == 0) continue;
					found = true;
					break;
				}
				if(found) break;
				else trimLeft++;
			}
			
			// right
			for(int x = width - 1; x >= 0; x--) {
				int tx = offsetX + mx - scene.WorldX + x;
				if(tx < 0 || tx >= scene.TileCountX) {
					trimRight++;
					continue;
				}
				bool found = false;
				for(int y = 0; y < height; y++) {
					int ty = offsetY + my - scene.WorldY + y;
					if(ty < 0 || ty >= scene.TileCountY) continue;
					if(layer.Tilemap.Grid[tx, ty].TileID == 0 || layer.Tilemap.Grid[tx, ty].TilesetSlot == 0) continue;
					found = true;
					break;
				}
				if(found) break;
				else trimRight++;
			}
			
			// top
			for(int y = 0; y < height; y++) {
				int ty = offsetY + my - scene.WorldY + y;
				if(ty < 0 || ty >= scene.TileCountY) {
					trimTop++;
					continue;
				}
				bool found = false;
				for(int x = 0; x < width; x++) {
					int tx = offsetX + mx - scene.WorldX + x;
					if(tx < 0 || tx >= scene.TileCountX) continue;
					if(layer.Tilemap.Grid[tx, ty].TileID == 0 || layer.Tilemap.Grid[tx, ty].TilesetSlot == 0) continue;
					found = true;
					break;
				}
				if(found) break;
				else trimTop++;
			}
			
			// bottom
			for(int y = height - 1; y >= 0; y--) {
				int ty = offsetY + my - scene.WorldY + y;
				if(ty < 0 || ty >= scene.TileCountY) {
					trimBottom++;
					continue;
				}
				bool found = false;
				for(int x = 0; x < width; x++) {
					int tx = offsetX + mx - scene.WorldX + x;
					if(tx < 0 || tx >= scene.TileCountX) continue;
					if(layer.Tilemap.Grid[tx, ty].TileID == 0 || layer.Tilemap.Grid[tx, ty].TilesetSlot == 0) continue;
					found = true;
					break;
				}
				if(found) break;
				else trimBottom++;
			}

			int w = width - trimLeft - trimRight;
			int h = height - trimTop - trimBottom;
			
			SetSize(int.Max(1, w), int.Max(1, h), true);

			offsetX += trimLeft;
			offsetY += trimTop;
			for(int y = 0; y < h; y++) {
				for(int x = 0; x < w; x++) {
					int tx = offsetX + mx - scene.WorldX + x;
					int ty = offsetY + my - scene.WorldY + y;
					if(tx < 0 || ty < 0 || tx >= scene.TileCountX || ty >= scene.TileCountY) {
						tilemap.Grid[x, y] = new TileRef(0, 0);
					} else {
						tilemap.Grid[x, y] = layer.Tilemap.Grid[tx, ty];
					}
				}
			}

			if(w <= 0 || h <= 0) {
				tilemap.Grid[0, 0] = new TileRef(0, 0);
			}
		}
		
		Point offset = resizing ? new(int.Min(resizeTileOrigin.X - mx, 0), int.Min(resizeTileOrigin.Y - my, 0)) : new(-width / 2, -height / 2);

		Rectangle area = new Rectangle(offset.X + mx, offset.Y + my, width, height);
		
		Vector2 w0 = new Vector2(offset.X+mx,       offset.Y+my       );
		Vector2 w1 = new Vector2(offset.X+mx+width, offset.Y+my       );
		Vector2 w2 = new Vector2(offset.X+mx,       offset.Y+my+height);
		Vector2 w3 = new Vector2(offset.X+mx+width, offset.Y+my+height);
		
		Vector2 p0 = Vector2.Transform(w0, transform);
		Vector2 p1 = Vector2.Transform(w1, transform);
		Vector2 p2 = Vector2.Transform(w2, transform);
		Vector2 p3 = Vector2.Transform(w3, transform);

		uint borderColorValid = Utilities.GetPackedColor(40, 40, 255, 255);
		uint fillColorValid = Utilities.GetPackedColor(40, 40, 255, 64);
		uint borderColorInvalid = Utilities.GetPackedColor(255, 40, 40, 255);
		uint fillColorInvalid = Utilities.GetPackedColor(255, 40, 40, 64);

		Rectangle sceneRegion = new Rectangle(scene.WorldX, scene.WorldY, scene.TileCountX, scene.TileCountY);
		
		if(!resizing) {
			if(imprint) {
				for(int y = 0; y < height; y++) {
					for(int x = 0; x < width; x++) {
						int tx = offset.X + mx - scene.WorldX + x;
						int ty = offset.Y + my - scene.WorldY + y;
						if(tx < 0 || ty < 0 || tx >= scene.TileCountX || ty >= scene.TileCountY) continue;
						if(tilemap.Grid[x, y].TileID == 0 || tilemap.Grid[x, y].TilesetSlot == 0) continue;
						layer.Tilemap.Grid[tx, ty] = tilemap.Grid[x, y];
					}
				}
			}
			
			tilemap.Render();
			uint tex = tilemap.GetFrameBufferTexture();
			drawList.AddImage((nint)tex, p0, p3, new(0,1), new(1,0));
		}

		// valid tile overlay
		for(int y = 0; y < height; y++) {
			for(int x = 0; x < width; x++) {
				int wx = offset.X + mx + x;
				int wy = offset.Y + my + y;
				if(!sceneRegion.Contains(wx, wy)) continue;
				if(!resizing && !HasTile(x, y)) continue;
				
				Vector2 t0 = Vector2.Transform(new Vector2(wx, wy), transform);
				Vector2 t1 = Vector2.Transform(new Vector2(wx+1, wy), transform);
				Vector2 t2 = Vector2.Transform(new Vector2(wx, wy+1), transform);
				Vector2 t3 = Vector2.Transform(new Vector2(wx+1, wy+1), transform);
				
				drawList.AddRectFilled(t0, t3, fillColorValid);
				
				{	// left
					bool inb = sceneRegion.Contains(wx - 1, wy);
					bool hst = HasTile(x - 1, y);
					if(x - 1 < 0 || !inb || (!resizing && !hst)) {
						drawList.AddLine(
							t0, t2, borderColorValid
						);
					}
				}
				{	// right
					bool inb = sceneRegion.Contains(wx + 1, wy);
					bool hst = HasTile(x + 1, y);
					if(x + 1 >= width || !inb || (!resizing && !hst)) {
						drawList.AddLine(
							t1, t3, borderColorValid
						);
					}
				}
				{	// top
					bool inb = sceneRegion.Contains(wx, wy - 1);
					bool hst = HasTile(x, y - 1);
					if(y - 1 < 0 || !inb || (!resizing && !hst)) {
						drawList.AddLine(
							t0, t1, borderColorValid
						);
					}
				}
				{	// bottom
					bool inb = sceneRegion.Contains(wx, wy + 1);
					bool hst = HasTile(x, y + 1);
					if(y + 1 >= height || !inb || (!resizing && !hst)) {
						drawList.AddLine(
							t2, t3, borderColorValid
						);
					}
				}
			}
		}
		
		
		// invalid tile overlay
		for(int y = 0; y < height; y++) {
			for(int x = 0; x < width; x++) {
				int wx = offset.X + mx + x;
				int wy = offset.Y + my + y;
				if(sceneRegion.Contains(wx, wy)) continue;
				if(!resizing && !HasTile(x, y)) continue;
				
				Vector2 t0 = Vector2.Transform(new Vector2(wx, wy), transform);
				Vector2 t1 = Vector2.Transform(new Vector2(wx+1, wy), transform);
				Vector2 t2 = Vector2.Transform(new Vector2(wx, wy+1), transform);
				Vector2 t3 = Vector2.Transform(new Vector2(wx+1, wy+1), transform);
				
				drawList.AddRectFilled(t0, t3, fillColorInvalid);

				{	// left
					bool inb = sceneRegion.Contains(wx - 1, wy);
					bool hst = HasTile(x - 1, y);
					if(x - 1 < 0 || inb || (!resizing && !hst)) {
						drawList.AddLine(
							t0, t2, borderColorInvalid
						);
					}
				}
				{	// right
					bool inb = sceneRegion.Contains(wx + 1, wy);
					bool hst = HasTile(x + 1, y);
					if(x + 1 >= width || inb || (!resizing && !hst)) {
						drawList.AddLine(
							t1, t3, borderColorInvalid
						);
					}
				}
				{	// top
					bool inb = sceneRegion.Contains(wx, wy - 1);
					bool hst = HasTile(x, y - 1);
					if(y - 1 < 0 || inb || (!resizing && !hst)) {
						drawList.AddLine(
							t0, t1, borderColorInvalid
						);
					}
				}
				{	// bottom
					bool inb = sceneRegion.Contains(wx, wy + 1);
					bool hst = HasTile(x, y + 1);
					if(y + 1 >= height || inb || (!resizing && !hst)) {
						drawList.AddLine(
							t2, t3, borderColorInvalid
						);
					}
				}
			}
		}
	}

}