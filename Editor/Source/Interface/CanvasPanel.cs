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

	private TileBrush tileBrush;
	private TileEraser tileEraser;

	private object activeTool;
	
	private const float ZOOM_RANGE_MIN = -5;
	private const float ZOOM_RANGE_MAX = 15;
	private const float ZOOM_RANGE_SCALE = 0.25F;

	public CanvasPanel() {
		Title = "Canvas";

		flags |= ImGuiWindowFlags.NoScrollWithMouse;
		
		camera = new(0, 0);
		zooming = 0;
		gridScenesOnly = true;
		tileBrush = null;
		tileEraser = null;
		activeTool = null;
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
		
		Scene activeScene = Program.SelectedScene;
		
		if(activeScene == null && tileBrush != null) {
			if(tileBrush == activeTool) activeTool = null;
			tileBrush?.Dispose();
			tileBrush = null;
		} else if(activeScene != null && (tileBrush == null || tileBrush.Scene != activeScene)) {
			if(tileBrush == activeTool) activeTool = null;
			tileBrush?.Dispose();
			tileBrush = new TileBrush(activeScene);
		}

		if(activeScene == null && tileEraser != null) {
			if(tileEraser == activeTool) activeTool = null;
			tileEraser?.Dispose();
			tileEraser = null;
		} else if(activeScene != null && (tileEraser == null || tileEraser.Scene != activeScene)) {
			if(tileEraser == activeTool) activeTool = null;
			tileEraser?.Dispose();
			tileEraser = new TileEraser(activeScene);
		}
		
		if(isHovered) {
			
			if(activeTool == null) activeTool = tileBrush;
			if(ImGui.IsKeyPressed(ImGuiKey.LeftShift)) {
				activeTool = tileEraser;
			} else if(ImGui.IsKeyReleased(ImGuiKey.LeftShift)) {
				activeTool = tileBrush;
			}
			
			if(tileBrush == activeTool) {
				tileBrush.Update(drawList, transform, worldBorder);
			} else if(tileEraser == activeTool) {
				tileEraser.Update(drawList, transform, worldBorder);
			}
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

	public TileBrush GetCurrentBrush() {
		return tileBrush;
	}
}