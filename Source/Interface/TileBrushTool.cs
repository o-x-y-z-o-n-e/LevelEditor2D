using System;
using System.Drawing;
using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace L2D; 

public class TileBrushTool : CanvasTool {
	
	public Tilemap Tilemap => tilemap;
	public AutomapPattern Automap => automap;
	public PresetPattern Preset => preset;

	public int Width => width;
	public int Height => height;
	public bool Resizing => resizing;

	private Tilemap tilemap;
	private int width;
	private int height;
	private bool resizing;
	private bool imprinting;
	private Point resizeTileOrigin;
	private bool clearAfterPlace;
	private bool disposed;
	private FileEditEntry edit;
	private AutomapPattern automap;
	private PresetPattern preset;

	public TileBrushTool() : base($"{Codicons.Pencil} Brush", LayerType.Tiles) {
		tilemap = null;
		width = 0;
		height = 0;
		resizing = false;
		imprinting = false;
		edit = null;
		SetSize(1, 1, true);
	}

	public override void OnActive() {
		clearAfterPlace = false;
	}

	public void SetAutomap(AutomapPattern automap, int w = 1, int h = 1) {
		this.automap = automap;
		SetSize(int.Max(w, 1), int.Max(h, 1), true);
		int tilesetSlot = 0;
		foreach(var link in tilemap.Scene.Tilesets) {
			if(link.Tileset == automap.Tileset) {
				tilesetSlot = link.Slot;
				break;
			}
		}
		int fillTileID = automap.Evaluate(0b111111111);
		for(int y = 0; y < height; y++) {
			for(int x = 0; x < width; x++) {
				tilemap.Set(x, y, fillTileID, tilesetSlot);
			}
		}
	}

	public void SetPreset(PresetPattern preset) {
		this.preset = preset;
		SetSize(preset.Width, preset.Height);
		int tilesetSlot = 0;
		foreach(var link in tilemap.Scene.Tilesets) {
			if(link.Tileset == preset.Tileset) {
				tilesetSlot = link.Slot;
				break;
			}
		}
		for(int y = 0; y < preset.Height; y++) {
			for(int x = 0; x < preset.Width; x++) {
				tilemap.Set(x, y, preset.GetTile(x, y), tilesetSlot);
			}
		}
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
		automap = null;
		preset = null;
		tilemap.Set(x, y, tileID, tilesetSlot);
	}

	public bool HasTile(int x, int y) {
		if(resizing || x < 0 || y < 0 || x >= tilemap.Width || y >= tilemap.Height) return false;
		return tilemap.Grid[x, y].TileID > 0 && tilemap.Grid[x, y].TilesetSlot > 0;
	}
	
	public bool IsEmpty() {
		if(tilemap == null) return true;
		for(int x = 0; x < tilemap.Width; x++) {
			for(int y = 0; y < tilemap.Height; y++) {
				if(tilemap.Grid[x, y].TileID > 0 && tilemap.Grid[x, y].TilesetSlot > 0) {
					return false;
				}
			}
		}
		return true;
	}

	public void Dispose() {
		if(disposed) return;
		tilemap?.ReleaseResources();
		disposed = true;
	}

	public override void SetScene(Scene scene) {
		base.SetScene(scene);
		if(tilemap?.Scene != scene) {
			automap = null;
			preset = null;
			tilemap?.ReleaseResources();
			if(scene != null) {
				tilemap = new Tilemap(this);
			} else {
				tilemap = null;
			}
		}
	}

	public override void Update(ImDrawListPtr drawList, Matrix4x4 transform, Rectangle worldBorder, bool movingCamera, bool isHovered) {
		Layer layer = Program.SelectedLayer;
		if(layer == null || layer.Type != LayerType.Tiles) return;

		Scene scene = layer.Scene;
		
		Vector2 mousePos = ImGui.GetIO().MousePos;
		Matrix4x4.Invert(transform, out var transformInverted);
		Vector2 mousePosTileCoord = Vector2.Transform(mousePos, transformInverted);

		int mx = (int)MathF.Floor(mousePosTileCoord.X);
		int my = (int)MathF.Floor(mousePosTileCoord.Y);

		Rectangle sceneRegion = new Rectangle(scene.WorldX, scene.WorldY, scene.TileCountX, scene.TileCountY);

		if(sceneRegion.Contains(mx, my) && !movingCamera) {
			ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
		}
		
		bool resize = isHovered && ImGui.IsMouseDown(ImGuiMouseButton.Right);
		bool imprint = isHovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) && !resize;
		
		if(IsEmpty() && !resizing && !resize) {
			if(edit != null) EndImprint();
			Program.CanvasPanel.SetTool(Program.CanvasPanel.TileSelect);
			return;
		}

		if(ImGui.IsKeyPressed(ImGuiKey.Escape)) {
			if(edit != null) EndImprint();
			Program.CanvasPanel.SetTool(Program.CanvasPanel.TileSelect);
			return;
		}
		
		if(ImGui.IsKeyPressed(ImGuiKey.LeftShift)) {
			if(edit != null) EndImprint();
			Program.CanvasPanel.SetTool(Program.CanvasPanel.TileEraser);
			return;
		}
		
		if(resize) {
			if(!resizing) {
				resizeTileOrigin = new(mx, my);
			}

			int sx = int.Abs(mx - resizeTileOrigin.X) + 1;
			int sy = int.Abs(my - resizeTileOrigin.Y) + 1;
			
			ImGui.SetMouseCursor((ImGuiMouseCursor)10);
			
			SetSize(sx, sy, false);
			automap = null;
			preset = null;
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
						tilemap.Set(x, y, 0, 0);
					} else {
						tilemap.Set(x, y, layer.Tilemap.Get(tx, ty));
					}
				}
			}

			if(w <= 0 || h <= 0) {
				tilemap.Set(0, 0, 0, 0);
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
		
		if(imprint) {
			if(clearAfterPlace) {
				clearAfterPlace = false;
				Imprint(new Point(offset.X + mx, offset.Y + my), layer);
				SetSize(1, 1);
				tilemap.Set(0, 0, 0, 0);
				automap = null;
				preset = null;
			} else {
				if(!imprinting) {
					imprinting = true;
					BeginImprint(layer);
				}
				UpdateImprint(new Point(offset.X + mx, offset.Y + my));
			}
		} else if(!imprint && imprinting) {
			imprinting = false;
			EndImprint();
		}
		
		if(!resizing) {
			tilemap.Render();
			uint tex = tilemap.GetFrameBufferTexture();
			drawList.AddImage((nint)tex, p0, p3, new(0,0), new(1,1));
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

	public void Imprint(Point offset, Layer layer) {
		BeginImprint(layer);
		UpdateImprint(offset);
		EndImprint();
	}

	private void BeginImprint(Layer layer) {
		if(edit != null) {
			Program.File.EndEdit(ref edit, discard: true);
		}
		var operation = new TileEditOperation(layer.Tilemap);
		edit = Program.File.BeginEdit(Program.CanvasPanel, operation,
			redo: TileEditOperation.ApplyNextState,
			undo: TileEditOperation.ApplyPrevState
		);
	}

	private void UpdateImprint(Point offset) {
		if(edit == null) return;
		var operation = edit.GetData<TileEditOperation>();
		for(int y = 0; y < height; y++) {
			for(int x = 0; x < width; x++) {
				int tx = offset.X - operation.Tilemap.Scene.WorldX + x;
				int ty = offset.Y - operation.Tilemap.Scene.WorldY + y;
				if(tx < 0 || ty < 0 || tx >= operation.Tilemap.Scene.TileCountX || ty >= operation.Tilemap.Scene.TileCountY) continue;
				if(automap != null) {
					automap.Print(operation.Tilemap, tx, ty, operation.Set);
				} else {
					if(this.tilemap.Grid[x, y].TileID == 0 || this.tilemap.Grid[x, y].TilesetSlot == 0) continue;
					operation.Set(tx, ty, this.tilemap.Get(x, y));
				}
			}
		}
	}

	private void EndImprint() {
		if(edit == null) return;
		Program.File.EndEdit(ref edit, discard: !edit.GetData<TileEditOperation>().HasChanges());
	}

	public void MoveRegion(Rectangle region, Layer layer) {
		automap = null;
		preset = null;
		
		CopyRegion(region, layer);
		
		var operation = new TileEditOperation(layer.Tilemap);
		
		for(int y = 0; y < region.Height; y++) {
			for(int x = 0; x < region.Width; x++) {
				int tx = region.X - layer.Scene.WorldX + x;
				int ty = region.Y - layer.Scene.WorldY + y;
				if(tx < 0 || ty < 0 || tx >= layer.Scene.TileCountX || ty >= layer.Scene.TileCountY) continue;
				operation.Set(tx, ty, new TileRef(0, 0));
			}
		}
		
		if(operation.HasChanges()) {
			Program.File.ApplyEdit(this, operation,
				redo: TileEditOperation.ApplyNextState,
				undo: TileEditOperation.ApplyPrevState
			);
		}
		
		clearAfterPlace = true;
	}

	public void CopyRegion(Rectangle region, Layer layer) {
		automap = null;
		preset = null;
		
		int trimLeft = 0;
		int trimRight = 0;
		int trimTop = 0;
		int trimBottom = 0;

		// left
		for(int x = 0; x < region.Width; x++) {
			int tx = region.X - layer.Scene.WorldX + x;
			if(tx < 0 || tx >= layer.Scene.TileCountX) {
				trimLeft++;
				continue;
			}

			bool found = false;
			for(int y = 0; y < region.Height; y++) {
				int ty = region.Y - layer.Scene.WorldY + y;
				if(ty < 0 || ty >= layer.Scene.TileCountY) continue;
				if(layer.Tilemap.Grid[tx, ty].TileID == 0 || layer.Tilemap.Grid[tx, ty].TilesetSlot == 0) continue;
				found = true;
				break;
			}

			if(found) break;
			else trimLeft++;
		}

		// right
		for(int x = region.Width - 1; x >= 0; x--) {
			int tx = region.X - layer.Scene.WorldX + x;
			if(tx < 0 || tx >= layer.Scene.TileCountX) {
				trimRight++;
				continue;
			}

			bool found = false;
			for(int y = 0; y < region.Height; y++) {
				int ty = region.Y - layer.Scene.WorldY + y;
				if(ty < 0 || ty >= layer.Scene.TileCountY) continue;
				if(layer.Tilemap.Grid[tx, ty].TileID == 0 || layer.Tilemap.Grid[tx, ty].TilesetSlot == 0) continue;
				found = true;
				break;
			}

			if(found) break;
			else trimRight++;
		}

		// top
		for(int y = 0; y < region.Height; y++) {
			int ty = region.Y - layer.Scene.WorldY + y;
			if(ty < 0 || ty >= layer.Scene.TileCountY) {
				trimTop++;
				continue;
			}

			bool found = false;
			for(int x = 0; x < region.Width; x++) {
				int tx = region.X - layer.Scene.WorldX + x;
				if(tx < 0 || tx >= layer.Scene.TileCountX) continue;
				if(layer.Tilemap.Grid[tx, ty].TileID == 0 || layer.Tilemap.Grid[tx, ty].TilesetSlot == 0) continue;
				found = true;
				break;
			}

			if(found) break;
			else trimTop++;
		}

		// bottom
		for(int y = region.Height - 1; y >= 0; y--) {
			int ty = region.Y - layer.Scene.WorldY + y;
			if(ty < 0 || ty >= layer.Scene.TileCountY) {
				trimBottom++;
				continue;
			}

			bool found = false;
			for(int x = 0; x < region.Width; x++) {
				int tx = region.X - layer.Scene.WorldX + x;
				if(tx < 0 || tx >= layer.Scene.TileCountX) continue;
				if(layer.Tilemap.Grid[tx, ty].TileID == 0 || layer.Tilemap.Grid[tx, ty].TilesetSlot == 0) continue;
				found = true;
				break;
			}

			if(found) break;
			else trimBottom++;
		}

		region.Width -= trimLeft + trimRight;
		region.Height -= trimTop + trimBottom;
		region.X += trimLeft;
		region.Y += trimTop;

		SetSize(int.Max(1, region.Width), int.Max(1, region.Height), true);
		
		for(int y = 0; y < region.Height; y++) {
			for(int x = 0; x < region.Width; x++) {
				int tx = region.X - layer.Scene.WorldX + x;
				int ty = region.Y - layer.Scene.WorldY + y;
				if(tx < 0 || ty < 0 || tx >= layer.Scene.TileCountX || ty >= layer.Scene.TileCountY) {
					tilemap.Set(x, y, 0, 0);
				} else {
					tilemap.Set(x, y, layer.Tilemap.Get(tx, ty));
				}
			}
		}

		if(region.Width <= 0 || region.Height <= 0) {
			tilemap.Set(0, 0, 0, 0);
		}
	}

	public void FlipVertical() {
		if(tilemap == null || tilemap.Height <= 1) return;
		automap = null;
		preset = null;
		for(int x = 0; x < tilemap.Width; x++) {
			for(int y = 0; y < tilemap.Height / 2; y++) {
				tilemap.Get(x, y, out int tile1, out int tileset1);
				tilemap.Get(x, tilemap.Height - y - 1, out int tile2, out int tileset2);
				tilemap.Set(x, y, tile2, tileset2);
				tilemap.Set(x, tilemap.Height - y - 1, tile1, tileset1);
			}
		}
	}

	public void FlipHorizontal() {
		if(tilemap == null || tilemap.Width <= 1) return;
		automap = null;
		preset = null;
		for(int y = 0; y < tilemap.Height; y++) {
			for(int x = 0; x < tilemap.Width / 2; x++) {
				tilemap.Get(x, y, out int tile1, out int tileset1);
				tilemap.Get(tilemap.Width - x - 1, y, out int tile2, out int tileset2);
				tilemap.Set(x, y, tile2, tileset2);
				tilemap.Set(tilemap.Width - x - 1, y, tile1, tileset1);
			}
		}
	}

	public void RotateLeft() {
		if(tilemap == null || (tilemap.Width <= 1 && tilemap.Height <= 1)) return;
		automap = null;
		preset = null;
		int w = tilemap.Width;
		int h = tilemap.Height;
		var grid = new TileRef[w, h];
		for(int y = 0; y < h; y++) {
			for(int x = 0; x < w; x++) {
				grid[x, y] = tilemap.Get(x, y);
			}
		}
		SetSize(h, w);
		for(int y = 0; y < w; y++) {
			for(int x = 0; x < h; x++) {
				tilemap.Set(x, y, grid[w - y - 1, x]);
			}
		}
	}
	
	public void RotateRight() {
		if(tilemap == null || (tilemap.Width <= 1 && tilemap.Height <= 1)) return;
		automap = null;
		preset = null;
		int w = tilemap.Width;
		int h = tilemap.Height;
		var grid = new TileRef[w, h];
		for(int y = 0; y < h; y++) {
			for(int x = 0; x < w; x++) {
				grid[x, y] = tilemap.Get(x, y);
			}
		}
		SetSize(h, w);
		for(int y = 0; y < w; y++) {
			for(int x = 0; x < h; x++) {
				tilemap.Set(x, y, grid[y, h - x - 1]);
			}
		}
	}

}