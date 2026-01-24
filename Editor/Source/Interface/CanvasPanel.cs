using System.Drawing;
using System.Numerics;
using ImGuiNET;

namespace L2D;

public class CanvasPanel : Panel {
	
	private bool gridScenesOnly;
	
	private Vector2 camera;
	private float zooming;
	private bool isHovered;
	private bool isPressed;
	
	public TileSelectTool TileSelect => tileSelect;
	public TileBrushTool TileBrush => tileBrush;
	public TileEraserTool TileEraser => tileEraser;

	private TileSelectTool tileSelect;
	private TileBrushTool tileBrush;
	private TileEraserTool tileEraser;

	public TileTool ActiveTool => activeTool;

	private TileTool activeTool;

	private bool previewSceneEnabled;
	private Scene previewSceneExisting;
	private Rectangle previewSceneArea;
	
	private const float ZOOM_RANGE_MIN = -5;
	private const float ZOOM_RANGE_MAX = 15;
	private const float ZOOM_RANGE_SCALE = 0.25F;

	public CanvasPanel() {
		Title = "Canvas";

		flags |= ImGuiWindowFlags.NoScrollWithMouse;
		
		camera = new(0, 0);
		zooming = 0;
		gridScenesOnly = true;
		tileSelect = new TileSelectTool();
		tileBrush = null;
		tileEraser = null;
		activeTool = null;
	}

	private float GetZoom() {
		return MathF.Exp(zooming * ZOOM_RANGE_SCALE);
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

		ImGui.SetNextItemWidth(150);
		if(ImGui.BeginCombo("Active Tool", activeTool?.DisplayName ?? "None")) {
			if(ImGui.Selectable(tileSelect.DisplayName, activeTool == tileSelect)) {
				SetTool(tileSelect);
			}
			ImGui.BeginDisabled(tileBrush.IsEmpty());
			if(ImGui.Selectable(tileBrush.DisplayName, activeTool == tileBrush)) {
				SetTool(tileBrush);
			}
			ImGui.EndDisabled();
			if(ImGui.Selectable(tileEraser.DisplayName, activeTool == tileEraser)) {
				SetTool(tileEraser);
			}
			ImGui.EndCombo();
		}
		
		ImGui.SameLine();

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
		
		// ImGui.InvisibleButton("canvas", canvas_sz, ImGuiButtonFlags.MouseButtonRight);
		ImGui.Dummy(canvas_sz);
		isHovered = ImGui.IsItemHovered();  // Hovered
		//isPressed = ImGui.IsItemActive();   // Held

		float zoom = GetZoom();
		
		Matrix4x4 transform = Matrix4x4.Identity;

		Vector2 tileSize = new(world.TileWidth, world.TileHeight);
		Vector2 zoomScale = tileSize * zoom;

		if(previewSceneEnabled) {
			Vector2 p = new(previewSceneArea.X + previewSceneArea.Width / 2.0F, previewSceneArea.Y + previewSceneArea.Height / 2.0F);
			camera = -p * tileSize;
		}

		transform *= Matrix4x4.CreateTranslation(canvas_sz.X / 2 / zoomScale.X, canvas_sz.Y / 2 / zoomScale.Y, 0);
		transform *= Matrix4x4.CreateScale(zoomScale.X, zoomScale.Y, 1);
		transform *= Matrix4x4.CreateTranslation(camera.X * zoom, camera.Y * zoom, 0);
		transform *= Matrix4x4.CreateTranslation(canvas_p0.X, canvas_p0.Y, 0);
		
		// TODO: mouse center zoom option

		bool movingCamera = false;
		
		if(isHovered) {
			// ImGui.SetMouseCursor((ImGuiMouseCursor)10);
			// if(ImGui.IsMouseDragging(ImGuiMouseButton.Middle, -1.0F)) {
			if(ImGui.IsMouseDown(ImGuiMouseButton.Middle)) {
				movingCamera = true;
				ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
				camera.X += io.MouseDelta.X / zoom;
				camera.Y += io.MouseDelta.Y / zoom;
			}
			zooming += io.MouseWheel;
			if(io.MouseWheel != 0.0F) {
				zooming = float.Clamp(float.Round(zooming), ZOOM_RANGE_MIN, ZOOM_RANGE_MAX);
			}
		}

		// Vector2 drag_delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Right);
		// if(drag_delta.X == 0.0f && drag_delta.Y == 0.0f) {
		// 	ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
		// }
		// if(ImGui.BeginPopup("context")) {
		// 	if(ImGui.MenuItem("yo mama", null, false, true)) { }
		// 	ImGui.EndPopup();
		// }
		
		drawList.PushClipRect(canvas_p0 + new Vector2(1), canvas_p1 - new Vector2(1), true);

		Rectangle worldBorder = DrawWorldBorder(world, drawList, transform);
		
		for(int i = 0; i < world.SceneCount; i++) {
			ImGui.PushID(i);
			DrawScene(world.GetScene(i), drawList, transform);
			ImGui.PopID();
		}
		
		Scene activeScene = Program.SelectedScene;
		
		if(activeScene == null && tileBrush != null) {
			if(tileBrush == activeTool) activeTool = null;
			tileBrush?.Dispose();
			tileBrush = null;
		} else if(activeScene != null && (tileBrush == null || tileBrush.Scene != activeScene)) {
			if(tileBrush == activeTool) activeTool = null;
			tileBrush?.Dispose();
			tileBrush = new TileBrushTool(activeScene);
		}

		if(activeScene == null && tileEraser != null) {
			if(tileEraser == activeTool) activeTool = null;
			tileEraser?.Dispose();
			tileEraser = null;
		} else if(activeScene != null && (tileEraser == null || tileEraser.Scene != activeScene)) {
			if(tileEraser == activeTool) activeTool = null;
			tileEraser?.Dispose();
			tileEraser = new TileEraserTool(activeScene);
		}

		if(tileSelect.Scene != activeScene) {
			tileSelect.SetScene(activeScene);
		}

		if(activeTool == null) {
			SetTool(tileSelect);
		}
			
		if(activeTool != null) {
			if(tileSelect == activeTool) {
				tileSelect.Update(drawList, transform, worldBorder, movingCamera, isHovered);
			} else if(tileBrush == activeTool) {
				tileBrush.Update(drawList, transform, worldBorder, movingCamera, isHovered);
			} else if(tileEraser == activeTool) {
				tileEraser.Update(drawList, transform, worldBorder, movingCamera, isHovered);
			}
		}

		if(previewSceneEnabled) {
			DrawScenePreview(drawList, transform);
		}
		
		drawList.PopClipRect();
	}

	private Rectangle DrawWorldBorder(World world, ImDrawListPtr drawList, Matrix4x4 transform) {
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
			DrawGrid(region, drawList, transform);
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

	private void DrawScene(Scene scene, ImDrawListPtr drawList, Matrix4x4 transform) {
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
		
		//ImGui.PushID(scene.ID);
		ImGui.SetCursorScreenPos(idTextPos);
		if(ImGui.InvisibleButton("select-scene", idTextSize)) {
			Program.SetSelectedScene(scene);
		}
		// if(ImGui.Selectable(scene.ID, Program.SelectedScene == scene, ImGuiSelectableFlags.AllowOverlap, idTextSize)) {
		// 	Program.SetSelectedScene(scene);
		// }
		if(ImGui.IsItemHovered()) {
			ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
		}
		//ImGui.PopID();
		
		// Tilemap layers
		for(int i = 0; i < scene.LayerCount; i++) {
			bool hide = !scene.Layers[i].Visible;
			if(Program.LayersPanel.IsolateLayerView && scene == Program.SelectedScene) {
				hide = scene.Layers[i] != Program.SelectedLayer;
			}
			if(hide || !scene.Layers[i].HasTilemap) continue;
			scene.Layers[i].Tilemap.Render();
			uint tex = scene.Layers[i].Tilemap.GetFrameBufferTexture();
			drawList.AddImage((nint)tex, p0, p3, new(0,1), new(1,0));
		}
		
		// Grid
		if(gridScenesOnly) {
			DrawGrid(new Rectangle(scene.WorldX, scene.WorldY, scene.TileCountX, scene.TileCountY), drawList, transform);
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

	private void DrawGrid(Rectangle region, ImDrawListPtr drawList, Matrix4x4 transform) {
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

	private void DrawScenePreview(ImDrawListPtr drawList, Matrix4x4 transform) {
		Vector2 p0 = Vector2.Transform(new Vector2(previewSceneArea.X, previewSceneArea.Y), transform);
		Vector2 p3 = Vector2.Transform(new Vector2(previewSceneArea.X+previewSceneArea.Width, previewSceneArea.Y+previewSceneArea.Height), transform);

		bool overlaps = false;

		foreach(var scene in Program.File.World.Scenes) {
			if(scene == previewSceneExisting) continue;
			Rectangle area = new(scene.WorldX, scene.WorldY, scene.TileCountX, scene.TileCountY);
			if(area.IntersectsWith(previewSceneArea)) {
				overlaps = true;
				break;
			}
		}

		if(overlaps) {
			drawList.AddRectFilled(p0, p3, Utilities.GetPackedColor(255, 10, 10, 128));
			drawList.AddRect(p0, p3, Utilities.GetPackedColor(255, 10, 10, 255));
		} else {
			drawList.AddRectFilled(p0, p3, Utilities.GetPackedColor(20, 255, 20, 128));
			drawList.AddRect(p0, p3, Utilities.GetPackedColor(20, 255, 20, 255));
		}
	}

	public void EnableScenePreview(Rectangle area, Scene existingScene = null) {
		previewSceneEnabled = true;
		previewSceneArea = area;
		previewSceneExisting = existingScene;
	}

	public void DisableScenePreview() {
		previewSceneEnabled = false;
		previewSceneArea = default;
		previewSceneExisting = null;
	}

	public void LocateScene(Scene scene) {
		float zoom = GetZoom();
		Vector2 p = new(scene.WorldX + scene.TileCountX / 2.0F, scene.WorldY + scene.TileCountY / 2.0F);
		camera = -p * new Vector2(scene.World.TileWidth, scene.World.TileHeight);
	}

	public void SetTool(TileTool tool) {
		if(activeTool == tool) return;
		activeTool = tool;
		activeTool.OnActive();
	}
}

public class TileTool {
	public string DisplayName;

	public virtual void OnActive() {
		
	}
	public virtual void Update(ImDrawListPtr drawList, Matrix4x4 transform, Rectangle worldBorder, bool movingCamera, bool isHovered) {
		
	}
}