using System.Drawing;
using System.Numerics;
using ImGuiNET;
using Silk.NET.Maths;
using Rectangle = System.Drawing.Rectangle;

namespace L2D;

public class CanvasPanel : Panel {
	
	private bool gridScenesOnly;
	
	private Vector2 camera;
	private float zooming;
	private bool isHovered;
	private bool isPressed;

	private TileDrawBrush activeBrush;
	
	private const float ZOOM_RANGE_MIN = -5;
	private const float ZOOM_RANGE_MAX = 15;
	private const float ZOOM_RANGE_SCALE = 0.25F;

	public CanvasPanel() {
		Title = "Canvas";

		flags |= ImGuiWindowFlags.NoScrollWithMouse;
		
		camera = new(0, 0);
		zooming = 0;
		gridScenesOnly = true;
		activeBrush = null;
	}

	protected override void Update() {
		if(Program.File == null) {
			ImGui.Text("No file loaded...");
			return;
		}
		if(Program.File.World == null) {
			ImGui.Text("No world active...");
			return;
		}
		
		var io = ImGui.GetIO();

		World world = Program.File.World;

		ImGui.Checkbox("Grid Scenes Only", ref gridScenesOnly);
		
		ImGui.SameLine();
		
		ImGui.SetNextItemWidth(400);
		ImGui.SliderFloat("Zoom", ref zooming, ZOOM_RANGE_MIN, ZOOM_RANGE_MAX);
		ImGui.OpenPopupOnItemClick("zoom-menu", ImGuiPopupFlags.MouseButtonRight);
		if(ImGui.BeginPopup("zoom-menu")) {
			if(ImGui.MenuItem("Reset", null, false, true)) {
				zooming = 0;
			}
			ImGui.EndPopup();
		}
		
		Vector2 canvas_p0 = ImGui.GetCursorScreenPos();      // ImDrawList API uses screen coordinates!
		Vector2 canvas_sz = ImGui.GetContentRegionAvail();   // Resize canvas to what's available
		if(canvas_sz.X < 50.0f) canvas_sz.X = 50.0f;
		if(canvas_sz.Y < 50.0f) canvas_sz.Y = 50.0f;
		Vector2 canvas_p1 = new Vector2(canvas_p0.X + canvas_sz.X, canvas_p0.Y + canvas_sz.Y);
		
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		drawList.AddRectFilled(canvas_p0, canvas_p1, Color.FromArgb(255, 50, 50, 50).GetPackedValue());
		drawList.AddRect(canvas_p0, canvas_p1, Color.FromArgb(255, 180, 180, 180).GetPackedValue());
		
		ImGui.InvisibleButton("canvas", canvas_sz, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
		isHovered = ImGui.IsItemHovered();  // Hovered
		isPressed = ImGui.IsItemActive();   // Held
		
		float zoom = MathF.Exp(zooming * ZOOM_RANGE_SCALE);
		
		Matrix4x4 transform = Matrix4x4.Identity;

		Vector2 zoomScale = new(world.TileWidth * zoom, world.TileHeight * zoom);
		transform *= Matrix4x4.CreateTranslation(canvas_sz.X / 2 / zoomScale.X, canvas_sz.Y / 2 / zoomScale.Y, 0);
		transform *= Matrix4x4.CreateScale(zoomScale.X, zoomScale.Y, 1);
		transform *= Matrix4x4.CreateTranslation(camera.X * zoom, camera.Y * zoom, 0);
		transform *= Matrix4x4.CreateTranslation(canvas_p0.X, canvas_p0.Y, 0);
		
		// TODO: mouse center zoom option
		// TODO: clamp scroll
		
		if(isPressed && ImGui.IsMouseDragging(ImGuiMouseButton.Right, -1.0F)) {
			camera.X += io.MouseDelta.X / zoom;
			camera.Y += io.MouseDelta.Y / zoom;
		}
		if(isHovered) {
			zooming += io.MouseWheel;
			if(io.MouseWheel != 0.0F) {
				zooming = float.Clamp(float.Round(zooming), ZOOM_RANGE_MIN, ZOOM_RANGE_MAX);
			}
		}

		Vector2 drag_delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Right);
		if(drag_delta.X == 0.0f && drag_delta.Y == 0.0f) {
			ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
		}
		if(ImGui.BeginPopup("context")) {
			if(ImGui.MenuItem("yo mama", null, false, true)) { }
			ImGui.EndPopup();
		}
		
		drawList.PushClipRect(canvas_p0 + new Vector2(1), canvas_p1 - new Vector2(1), true);

		Rectangle worldBorder = WorldBorder(world, drawList, transform);
		
		for(int i = 0; i < world.SceneCount; i++) {
			ProcessScene(world.GetScene(i), drawList, transform);
		}
		
		// TODO: brush

		Scene activeScene = Program.SelectedScene;

		if(activeScene == null && activeBrush != null) {
			activeBrush = null;
		} else if(activeScene != null && (activeBrush == null || activeBrush.Scene != activeScene)) {
			activeBrush = new TileDrawBrush(activeScene);
		}

		if(isHovered && activeBrush != null) {
			UpdateBrush(activeBrush, drawList, transform, worldBorder);
		}
		
		drawList.PopClipRect();
	}

	private Rectangle WorldBorder(World world, ImDrawListPtr drawList, Matrix4x4 transform) {
		if(world.SceneCount == 0) return new Rectangle(0,0,0,0);
		int minX = int.MaxValue;
		int minY = int.MaxValue;
		int maxX = int.MinValue;
		int maxY = int.MinValue;
		for(int i = 0; i < world.SceneCount; i++) {
			Scene scene = world.GetScene(i);
			if(scene.WorldX < minX) minX = scene.WorldX;
			if(scene.WorldY < minY) minY = scene.WorldY;
			if(scene.WorldX+scene.TileCountX > maxX) maxX = scene.WorldX + scene.TileCountX;
			if(scene.WorldY+scene.TileCountY > maxY) maxY = scene.WorldY + scene.TileCountY;
		}
		minX--;
		minY--;
		maxX++;
		maxY++;
		Vector2 p0 = Vector2.Transform(new Vector2(minX, minY), transform);
		Vector2 p1 = Vector2.Transform(new Vector2(maxX, minY), transform);
		Vector2 p2 = Vector2.Transform(new Vector2(minX, maxY), transform);
		Vector2 p3 = Vector2.Transform(new Vector2(maxX, maxY), transform);

		Rectangle region = new Rectangle(minX, minY, maxX - minX, maxY - minY);

		if(!gridScenesOnly) {
			PlaceGrid(region, drawList, transform);
		}

		uint boundryLineColor = Color.FromArgb(255, 180, 180, 180).GetPackedValue();
		int lineSize = 1;
		int halfLineSize = lineSize / 2;
		drawList.AddLine(
			p0 + new Vector2(0, halfLineSize),
			p1 + new Vector2(0, halfLineSize),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p0 + new Vector2(halfLineSize, 0),
			p2 + new Vector2(halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p3 + new Vector2(-halfLineSize, 0),
			p1 + new Vector2(-halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p3 + new Vector2(0, -halfLineSize),
			p2 + new Vector2(0, -halfLineSize),
			boundryLineColor,
			lineSize
		);

		return region;
	}

	private void ProcessScene(Scene scene, ImDrawListPtr drawList, Matrix4x4 transform) {
		Vector2 p0 = Vector2.Transform(new Vector2(scene.WorldX, scene.WorldY), transform);
		Vector2 p1 = Vector2.Transform(new Vector2(scene.WorldX + scene.TileCountX, scene.WorldY), transform);
		Vector2 p2 = Vector2.Transform(new Vector2(scene.WorldX, scene.WorldY + scene.TileCountY), transform);
		Vector2 p3 = Vector2.Transform(new Vector2(scene.WorldX + scene.TileCountX, scene.WorldY + scene.TileCountY), transform);
		
		// ID label
		Vector2 idTextSize = ImGui.CalcTextSize(scene.ID);
		Vector2 idTextPos = p0 - Vector2.UnitY * idTextSize.Y;
		uint idTextColor = scene == Program.SelectedScene ? Color.FromArgb(255, 20, 220, 20).GetPackedValue() : 0xFFFFFFFF;
		drawList.AddText(idTextPos, idTextColor, scene.ID);
		drawList.AddRectFilled(idTextPos, idTextPos + idTextSize, Color.FromArgb(40, 180, 180, 180).GetPackedValue());

		// Tilemap layers
		for(int i = 0; i < scene.LayerCount; i++) {
			if(!scene.Layers[i].Visible || !scene.Layers[i].HasTilemap) continue;
			scene.Layers[i].Tilemap.Render();
			uint tex = scene.Layers[i].Tilemap.GetFrameBufferTexture();
			drawList.AddImage((nint)tex, p0, p3, new(0,1), new(1,0));
		}
		
		// Grid
		if(gridScenesOnly) {
			PlaceGrid(new Rectangle(scene.WorldX, scene.WorldY, scene.TileCountX, scene.TileCountY), drawList, transform);
		}
		
		// Boundry lines
		uint boundryLineColor = Color.FromArgb(255, 0, 0, 0).GetPackedValue();
		int lineSize = 1;
		int halfLineSize = lineSize / 2;
		drawList.AddLine(
			p0 + new Vector2(0, halfLineSize),
			p1 + new Vector2(0, halfLineSize),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p0 + new Vector2(halfLineSize, 0),
			p2 + new Vector2(halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p3 + new Vector2(-halfLineSize, 0),
			p1 + new Vector2(-halfLineSize, 0),
			boundryLineColor,
			lineSize
		);
		drawList.AddLine(
			p3 + new Vector2(0, -halfLineSize),
			p2 + new Vector2(0, -halfLineSize),
			boundryLineColor,
			lineSize
		);
	}

	private void PlaceGrid(Rectangle region, ImDrawListPtr drawList, Matrix4x4 transform) {
		int gridAlpha = (int)(20 * Utilities.Map(zooming, ZOOM_RANGE_MIN / 2.0F, 0, 0.0F, 1.0F));
		for(int x = region.X; x <= region.X + region.Width; x++) {
			drawList.AddLine(
				Vector2.Transform(new Vector2(x, region.Y), transform),
				Vector2.Transform(new Vector2(x, region.Y + region.Height), transform),
				Color.FromArgb(gridAlpha, 200, 200, 200).GetPackedValue()
			);
		}
		for(int y = region.Y; y <= region.Y + region.Height; y++) {
			drawList.AddLine(
				Vector2.Transform(new Vector2(region.X, y), transform),
				Vector2.Transform(new Vector2(region.X + region.Width, y), transform),
				Color.FromArgb(gridAlpha, 200, 200, 200).GetPackedValue()
			);
		}
	}

	private void UpdateBrush(TileDrawBrush brush, ImDrawListPtr drawList, Matrix4x4 transform, Rectangle worldBorder) {
		Scene scene = brush.Scene;
		Layer layer = Program.SelectedLayer;
		if(layer.Scene != scene) return;
		
		Vector2 mousePos = ImGui.GetIO().MousePos;
		Matrix4x4.Invert(transform, out var transformInverted);
		Vector2 mousePosTileCoord = Vector2.Transform(mousePos, transformInverted);

		int mx = (int)MathF.Floor(mousePosTileCoord.X);
		int my = (int)MathF.Floor(mousePosTileCoord.Y);
		// if(!worldBorder.Contains(mx, my)) return;
		
		var offset = brush.TileDrawOffset;

		Rectangle area = new Rectangle(offset.X + mx, offset.Y + my, brush.Width, brush.Height);
		
		Vector2 w0 = new Vector2(offset.X+mx,             offset.Y+my             );
		Vector2 w1 = new Vector2(offset.X+mx+brush.Width, offset.Y+my             );
		Vector2 w2 = new Vector2(offset.X+mx,             offset.Y+my+brush.Height);
		Vector2 w3 = new Vector2(offset.X+mx+brush.Width, offset.Y+my+brush.Height);
		
		Vector2 p0 = Vector2.Transform(w0, transform);
		Vector2 p1 = Vector2.Transform(w1, transform);
		Vector2 p2 = Vector2.Transform(w2, transform);
		Vector2 p3 = Vector2.Transform(w3, transform);

		uint borderColorBlue = Utilities.GetPackedColor(40, 40, 255, 255);
		uint fillColorBlue = Utilities.GetPackedColor(40, 40, 255, 64);
		uint borderColorRed = Utilities.GetPackedColor(255, 40, 40, 255);
		uint fillColorRed = Utilities.GetPackedColor(255, 40, 40, 64);

		Rectangle sceneRegion = new Rectangle(scene.WorldX, scene.WorldY, scene.TileCountX, scene.TileCountY);

		bool imprint = ImGui.IsMouseDown(ImGuiMouseButton.Left);
		bool copy = ImGui.IsMouseDown(ImGuiMouseButton.Middle);
		
		if(imprint) {
			for(int y = 0; y < brush.Height; y++) {
				for(int x = 0; x < brush.Width; x++) {
					int tx = offset.X + mx - scene.WorldX + x;
					int ty = offset.Y + my - scene.WorldY + y;
					if(tx < 0 || ty < 0 || tx >= scene.TileCountX || ty >= scene.TileCountY) continue;
					if(brush.Tilemap.Grid[x, y].TileID == 0 || brush.Tilemap.Grid[x, y].TilesetSlot == 0) continue;
					layer.Tilemap.Grid[tx, ty] = brush.Tilemap.Grid[x, y];
				}
			}
		} else {
			if(copy) {
				// TODO: drag resize
				brush.SetSize(1, 1, false);
			} else if(!copy && brush.Resizing) {
				brush.SetSize(brush.Width, brush.Height, true);
				// TODO: copy tiles from layer
			}
		}

		if(!brush.Resizing) {
			brush.Tilemap.Render();
			uint tex = brush.Tilemap.GetFrameBufferTexture();
			drawList.AddImage((nint)tex, p0, p3, new(0,1), new(1,0));
		}

		// valid tile overlay
		for(int y = 0; y < brush.Height; y++) {
			for(int x = 0; x < brush.Width; x++) {
				int wx = offset.X + mx + x;
				int wy = offset.Y + my + y;
				if(!sceneRegion.Contains(wx, wy) || !brush.HasTile(x, y)) continue;
				
				Vector2 t0 = Vector2.Transform(new Vector2(wx, wy), transform);
				Vector2 t1 = Vector2.Transform(new Vector2(wx+1, wy), transform);
				Vector2 t2 = Vector2.Transform(new Vector2(wx, wy+1), transform);
				Vector2 t3 = Vector2.Transform(new Vector2(wx+1, wy+1), transform);
				
				drawList.AddRectFilled(t0, t3, fillColorBlue);
				
				{	// left
					bool inb = sceneRegion.Contains(wx - 1, wy);
					bool hst = brush.HasTile(x - 1, y);
					if(x - 1 < 0 || !inb || !hst) {
						drawList.AddLine(
							t0, t2, borderColorBlue
						);
					}
				}
				{	// right
					bool inb = sceneRegion.Contains(wx + 1, wy);
					bool hst = brush.HasTile(x + 1, y);
					if(x + 1 >= brush.Width || !inb || !hst) {
						drawList.AddLine(
							t1, t3, borderColorBlue
						);
					}
				}
				{	// top
					bool inb = sceneRegion.Contains(wx, wy - 1);
					bool hst = brush.HasTile(x, y - 1);
					if(y - 1 < 0 || !inb || !hst) {
						drawList.AddLine(
							t0, t1, borderColorBlue
						);
					}
				}
				{	// top
					bool inb = sceneRegion.Contains(wx, wy + 1);
					bool hst = brush.HasTile(x, y + 1);
					if(y + 1 >= brush.Height || !inb || !hst) {
						drawList.AddLine(
							t2, t3, borderColorBlue
						);
					}
				}
			}
		}
		
		
		
		// invalid tile overlay
		for(int y = 0; y < brush.Height; y++) {
			for(int x = 0; x < brush.Width; x++) {
				int wx = offset.X + mx + x;
				int wy = offset.Y + my + y;
				if(sceneRegion.Contains(wx, wy) || !brush.HasTile(x, y)) continue;
				
				Vector2 t0 = Vector2.Transform(new Vector2(wx, wy), transform);
				Vector2 t1 = Vector2.Transform(new Vector2(wx+1, wy), transform);
				Vector2 t2 = Vector2.Transform(new Vector2(wx, wy+1), transform);
				Vector2 t3 = Vector2.Transform(new Vector2(wx+1, wy+1), transform);
				
				drawList.AddRectFilled(t0, t3, fillColorRed);

				{	// left
					bool inb = sceneRegion.Contains(wx - 1, wy);
					bool hst = brush.HasTile(x - 1, y);
					if(x - 1 < 0 || inb || !hst) {
						drawList.AddLine(
							t0, t2, borderColorRed
						);
					}
				}
				{	// right
					bool inb = sceneRegion.Contains(wx + 1, wy);
					bool hst = brush.HasTile(x + 1, y);
					if(x + 1 >= brush.Width || inb || !hst) {
						drawList.AddLine(
							t1, t3, borderColorRed
						);
					}
				}
				{	// top
					bool inb = sceneRegion.Contains(wx, wy - 1);
					bool hst = brush.HasTile(x, y - 1);
					if(y - 1 < 0 || inb || !hst) {
						drawList.AddLine(
							t0, t1, borderColorRed
						);
					}
				}
				{	// top
					bool inb = sceneRegion.Contains(wx, wy + 1);
					bool hst = brush.HasTile(x, y + 1);
					if(y + 1 >= brush.Height || inb || !hst) {
						drawList.AddLine(
							t2, t3, borderColorRed
						);
					}
				}
			}
		}
	}

	public TileDrawBrush GetActiveBrush() {
		return activeBrush;
	}
}