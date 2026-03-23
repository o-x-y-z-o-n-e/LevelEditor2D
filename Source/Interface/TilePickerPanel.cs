using System.Drawing;
using System.Numerics;
using ImGuiNET;

namespace L2D; 

public class TilePickerPanel : Panel {

	private bool multiSelect;
	private Vector2 multiSelectOrigin;
	private int tilesetLinkTarget;
	private int automapBrushSize;

	public TilePickerPanel() {
		Title = "Tile Picker";
		tilesetLinkTarget = -1;
		automapBrushSize = 1;
	}

	protected override void Update() {
		if(Program.File == null) {
			return;
		}
		
		if(Program.SelectedScene == null) {
			ImGui.Text("No scene selected...");
			return;
		}
		
		if(Program.SelectedLayer == null) {
			ImGui.Text("No layer selected...");
			return;
		}

		if(Program.SelectedLayer.Type != LayerType.Tiles) {
			ImGui.Text("Selected layer is not a tilemap...");
			return;
		}

		int scale = 4;

		Scene scene = Program.SelectedScene;

		int maxTilesetSlots = scene.World.MaxTilesetSlots;
		
		ImGui.PushID(scene.ID);
		
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		var style = ImGui.GetStyle();

		bool collapseAll = false;
		bool expandAll = false;

		if(ImGui.Button("Collapse All")) collapseAll = true;
		ImGui.SameLine();
		if(ImGui.Button("Expand All")) expandAll = true;
		
		ImGui.SameLine();

		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Top").X - 12);
		if(ImGui.Button("Top")) {
			ImGui.SetNextWindowScroll(new(0,0));
		}

		var windowFlags = ImGuiWindowFlags.AlwaysVerticalScrollbar;
		if(ImGui.IsKeyDown(ImGuiKey.LeftShift)) {
			windowFlags |= ImGuiWindowFlags.NoScrollWithMouse;
		}

		ImGui.BeginChild("tileset-list", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders, windowFlags);

		int moveUpIndex = -1;
		int moveDownIndex = -1;
		int removeIndex = -1;
		
		Vector2 region = ImGui.GetContentRegionAvail() - new Vector2(8, 0);
		string empty = "";
		int count = scene.Tilesets.Count;
		for(int i = 0; i < count; i++) {
			TilesetLink link = scene.Tilesets[i];
			
			string label = $"Slot [{link.Slot}]: {link.Tileset?.ID ?? "--"}";
			
			ImGui.PushID(i);

			if(collapseAll) {
				ImGui.SetNextItemOpen(false);
			} else if(expandAll) {
				ImGui.SetNextItemOpen(true);
			}

			bool open = ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen);
			
			ImGui.OpenPopupOnItemClick("menu", ImGuiPopupFlags.MouseButtonRight);
			if(ImGui.BeginPopup("menu")) {
				ImGui.BeginDisabled(i == 0);
				if(ImGui.MenuItem("Move Up")) {
					moveUpIndex = i;
				}
				ImGui.EndDisabled();
				ImGui.BeginDisabled(i == count - 1);
				if(ImGui.MenuItem("Move Down")) {
					moveDownIndex = i;
				}
				ImGui.EndDisabled();
				if(ImGui.MenuItem("Remove")) {
					removeIndex = i;
				}
				ImGui.EndPopup();
			}
			
			if(open) {
				Tileset tileset = link.Tileset;
				string tilesetLabel = "<Select Tileset>";
				if(tileset != null) {
					tilesetLabel = tileset.ID;
				}
				
				string slotPreviewLabel = $"{link.Slot}";

				if(ImGui.BeginCombo("Slot", slotPreviewLabel, ImGuiComboFlags.None)) {
					bool atLeastOneOption = false;
					for(int s = 1; s <= maxTilesetSlots; s++) {
						bool match = false;
						foreach(var t in scene.Tilesets) {
							if(t.Slot == s) {
								match = true;
								break;
							}
						}
						if(match) continue;
						atLeastOneOption = true;
						if(ImGui.Selectable($"Slot: {s}")) {
							Program.File.ApplyEdit(this, new SlotOperation(link, s));
						}
					}
					if(!atLeastOneOption) {
						ImGui.Text("No more slots available");
					}
					ImGui.EndCombo();
				}

				if(ImGui.Button(tilesetLabel, new Vector2(ImGui.CalcItemWidth(), 0))) {
					tilesetLinkTarget = i;
					ImGui.OpenPopup("Select Tileset");
				}

				if(i == tilesetLinkTarget) {
					Program.TilesetsPanel.SelectTilesetModal((selected, tileset) => {
						if(selected) {
							Program.File.ApplyEdit(this, new TilesetOperation(link, tileset));
						}
						tilesetLinkTarget = -1;
					});
				}

				ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
				ImGui.Text("Tileset");

				ImGui.BeginTabBar("tab-bar");
				if(ImGui.BeginTabItem("Tiles")) {
					TileGridView(scene, tileset, link, windowFlags, region, scale);
					ImGui.EndTabItem();
				}
				if(ImGui.BeginTabItem("Automaps")) {
					AutomapListView(scene, tileset, link, windowFlags, region, scale);
					ImGui.EndTabItem();
				}
				ImGui.EndTabBar();
				
				ImGui.Separator();
				ImGui.Spacing();
			}
			
			ImGui.PopID(); // i
		}

		int nextSlotAvailable = 1;
		while(nextSlotAvailable <= maxTilesetSlots) {
			bool match = false;
			foreach(var t in scene.Tilesets) {
				if(t.Slot == nextSlotAvailable) {
					match = true;
					break;
				}
			}
			if(match) {
				nextSlotAvailable++;
			} else {
				break;
			}
		}
		ImGui.BeginDisabled(nextSlotAvailable > maxTilesetSlots);
		if(ImGui.Button("Add", new Vector2(region.X, 0))) {
			TilesetLink link = new TilesetLink(scene.File, nextSlotAvailable, null);
			Program.File.ApplyEdit(this, new AddOperation(scene, link));
		}
		ImGui.EndDisabled();

		if(moveUpIndex >= 0) {
			Program.File.ApplyEdit(this, new MoveOperation(scene, moveUpIndex, moveUpIndex - 1));
		}
		
		if(moveDownIndex >= 0) {
			Program.File.ApplyEdit(this, new MoveOperation(scene, moveDownIndex, moveDownIndex + 1));
		}

		if(removeIndex >= 0) {
			Program.File.ApplyEdit(this, new RemoveOperation(scene, removeIndex));
		}
		
		ImGui.EndChild(); // tileset-list
		
		ImGui.PopID(); // scene.ID
	}

	private void TileGridView(Scene scene, Tileset tileset, TilesetLink link, ImGuiWindowFlags windowFlags, Vector2 region, int scale) {
		Texture texSource = tileset?.GetTexturePreview();

		Vector2 areaPos = ImGui.GetCursorScreenPos();
		Vector2 areaSize = new(region.X, texSource?.Height * scale + 34 ?? region.X);

		var childFlags = ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.AlwaysHorizontalScrollbar;
		if(!ImGui.IsKeyDown(ImGuiKey.LeftShift)) {
			windowFlags |= ImGuiWindowFlags.NoScrollWithMouse;
		}

		ImGui.BeginChild(
			"tileset-picker",
			areaSize,
			ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Borders,
			childFlags
		);

		if(ImGui.IsWindowHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Middle)) {
			ImGui.SetScrollY(ImGui.GetScrollY() - ImGui.GetIO().MouseDelta.Y);
			ImGui.SetScrollX(ImGui.GetScrollX() - ImGui.GetIO().MouseDelta.X);
		}

		if(ImGui.IsWindowHovered() && ImGui.IsKeyDown(ImGuiKey.LeftShift)) {
			ImGui.SetScrollX(ImGui.GetScrollX() + ImGui.GetIO().MouseWheel * 64.0F);
		}

		Vector2 p0 = areaPos;
		Vector2 p1 = p0 + areaSize - new Vector2(16);
		bool hoveredTile = false;
		if(tileset != null && texSource != null) {
			Vector2 origin = ImGui.GetCursorPos();
			Vector2 originScreen = ImGui.GetCursorScreenPos();
			Vector2 border = new(4, 4);
			Vector2 size = new Vector2(texSource.Width, texSource.Height) * scale;
			ImGui.Image(new IntPtr(texSource.Handle), size, new(0, 0), new(1, 1), new(1, 1, 1, 1));

			var brush = Program.CanvasPanel.TileBrush;
			bool brushChanged = false;

			int countX = tileset.GetTileCountX();
			int countY = tileset.GetTileCountY();

			Vector2 multiSelectMin = new(0);
			Vector2 multiSelectMax = new(0);
			RectangleF multiSelectRect = new(0, 0, 0, 0);

			if(ImGui.IsWindowFocused()) {

				Vector2 pos = ImGui.GetMousePos();
				multiSelectMin = new Vector2(float.Min(pos.X, multiSelectOrigin.X), float.Min(pos.Y, multiSelectOrigin.Y));
				multiSelectMax = new Vector2(float.Max(pos.X, multiSelectOrigin.X), float.Max(pos.Y, multiSelectOrigin.Y));
				multiSelectRect = new RectangleF(multiSelectMin.X, multiSelectMin.Y, multiSelectMax.X - multiSelectMin.X, multiSelectMax.Y - multiSelectMin.Y);

				if(ImGui.IsMouseDragging(ImGuiMouseButton.Left, -1.0F)) {
					if(!multiSelect) {
						multiSelect = true;
						multiSelectOrigin = ImGui.GetMousePos();
					}

					ImGui.GetWindowDrawList().AddRect(multiSelectMin, multiSelectMax, Utilities.GetPackedColor(255, 255, 255, 255));
				} else {
					if(multiSelect) {
						multiSelect = false;

						int minX = (int)((multiSelectMin.X - originScreen.X) / (float)(scale * scene.World.TileWidth));
						int minY = (int)((multiSelectMin.Y - originScreen.Y) / (float)(scale * scene.World.TileHeight));
						int maxX = (int)((multiSelectMax.X - originScreen.X) / (float)(scale * scene.World.TileWidth));
						int maxY = (int)((multiSelectMax.Y - originScreen.Y) / (float)(scale * scene.World.TileHeight));

						minX = int.Max(minX, 0);
						minY = int.Max(minY, 0);
						maxX = int.Min(maxX, countX - 1);
						maxY = int.Min(maxY, countY - 1);

						brush.SetSize(maxX - minX + 1, maxY - minY + 1);

						for(int y = minY; y <= maxY; y++) {
							for(int x = minX; x <= maxX; x++) {
								int tileID = (y * countX + x) + 1;

								brush.SetTile(x - minX, y - minY, tileID, link.Slot);
							}
						}

						brushChanged = true;
					}
				}
			}

			for(int y = 0; y < countY; y++) {
				for(int x = 0; x < countX; x++) {
					int tileID = (y * countX + x) + 1;
					bool selected = false;

					if(brush != null && brush.Tilemap != null && !brush.Resizing) {
						for(int by = 0; by < brush.Height; by++) {
							for(int bx = 0; bx < brush.Width; bx++) {
								var tile = brush.Tilemap.Grid[bx, by];
								selected = tile.TileID == tileID && tile.TilesetSlot == link.Slot;
								if(selected) break;
							}

							if(selected) break;
						}
					}

					ImGui.SetCursorPos(origin + new Vector2(scene.World.TileWidth * x, scene.World.TileHeight * y) * scale);
					ImGui.PushID(tileID);
					Vector2 c = ImGui.GetCursorScreenPos();
					Vector2 s = new Vector2(scene.World.TileWidth, scene.World.TileHeight) * scale;
					if(ImGui.InvisibleButton("##tile", s)) {
						if(brush != null) {
							brush.SetSize(1, 1, true);
							brush.SetTile(0, 0, tileID, link.Slot);
							brushChanged = true;
						}
					}

					bool inMultiSelect = false;
					if(multiSelect) {
						inMultiSelect = multiSelectRect.IntersectsWith(new RectangleF(c.X, c.Y, s.X, s.Y));
					}

					if(ImGui.IsItemHovered() || inMultiSelect) {
						ImGui.GetWindowDrawList().AddRectFilled(c, c + s, Utilities.GetPackedColor(200, 200, 200, 50));
						hoveredTile = true;
					}

					if(selected) {
						ImGui.GetWindowDrawList().AddRectFilled(c, c + s, Utilities.GetPackedColor(200, 200, 200, 50));
						ImGui.GetWindowDrawList().AddRect(c, c + s, Utilities.GetPackedColor(255, 255, 255, 255));
					}

					ImGui.PopID();
				}
			}

			if(!hoveredTile) {
				ImGui.SetCursorPos(origin);
				if(ImGui.InvisibleButton("##clear", ImGui.GetContentRegionAvail())) {
					if(brush != null) {
						brush.SetSize(1, 1, true);
						brush.SetTile(0, 0, 0, 0);
						if(Program.CanvasPanel.ActiveTool == brush) {
							Program.CanvasPanel.SetTool(Program.CanvasPanel.TileSelect);
						}
					}
				}
			}

			if(brushChanged) {
				Program.CanvasPanel.SetTool(brush);
			}
		} else {
			ImGui.Text("No tileset selected...");
		}

		ImGui.EndChild(); // tileset-picker
	}

	private void AutomapListView(Scene scene, Tileset tileset, TilesetLink link, ImGuiWindowFlags windowFlags, Vector2 region, int scale) {
		var style = ImGui.GetStyle();
		
		Texture texSource = tileset?.GetTexturePreview();
		
		Vector2 tileSize = new Vector2(scene.World.TileWidth, scene.World.TileHeight) * scale;
		Vector2 patternSize = tileSize * 3;
		
		int patternsPerRow = int.Max((int)((region.X - style.WindowPadding.X * 2) / (patternSize.X + style.ItemSpacing.X)), 1);

		Vector2 areaPos = ImGui.GetCursorScreenPos();
		Vector2 areaSize = new(region.X, tileset.AutomapPatterns.Count * (patternSize.Y + style.ItemSpacing.Y) + style.WindowPadding.Y * 2 - style.ItemSpacing.Y); // TODO

		var childFlags = ImGuiWindowFlags.None;
		if(!ImGui.IsKeyDown(ImGuiKey.LeftShift)) {
			windowFlags |= ImGuiWindowFlags.NoScrollWithMouse;
		}

		ImGui.BeginChild(
			"automap-list",
			areaSize,
			ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Borders,
			childFlags
		);
		
		for(int i = 0; i < tileset.AutomapPatterns.Count; i++) {
			AutomapPattern pattern = tileset.AutomapPatterns[i];
			ImGui.PushID(i);

			int column = i % patternsPerRow;

			if(column > 0) {
				ImGui.SameLine();
			}
			
			Vector2 cur = ImGui.GetCursorScreenPos();
			
			if(ImGui.InvisibleButton("button", patternSize)) {
				ImGui.OpenPopup("set-brush-size");
			}
			ImGui.SetItemTooltip(pattern.Name);
			
			for(int t = 0; t < 9; t++) {
				int x = t % 3;
				int y = t / 3;
				uint bitmask = 0;
				int index = 0;
				for(int ty = 1; ty >= -1; --ty) {
					for(int tx = -1; tx <= 1; ++tx) {
						int ix = x + tx;
						int iy = y + ty;
						if(ix >= 0 && ix < 3 && iy >= 0 && iy < 3) {
							bitmask |= (uint)(1 << index);
						}
						index++;
					}
				}
				Vector2 t0 = cur + new Vector2(x * tileSize.X, y * tileSize.Y);
				Vector2 t1 = t0 + tileSize;
				int matchedTile = pattern.Evaluate(bitmask);
				if(matchedTile > 0) {
					var rect = tileset.GetTileRegion(matchedTile - 1);
					Vector2 uvMin = new Vector2(rect.Left / (float)tileset.GetTextureWidth(), rect.Top / (float)tileset.GetTextureHeight());
					Vector2 uvMax = new Vector2(rect.Right / (float)tileset.GetTextureWidth(), rect.Bottom / (float)tileset.GetTextureHeight());
					ImGui.GetWindowDrawList().AddImage(new IntPtr(tileset.TexturePreview.Handle), t0, t1, uvMin, uvMax);
				}
			}

			if(ImGui.IsItemHovered()) {
				ImGui.GetWindowDrawList().AddRectFilled(cur, cur + patternSize, Utilities.GetPackedColor(200, 200, 200, 50));
			}

			if(Program.CanvasPanel.TileBrush.Pattern == pattern) {
				ImGui.GetWindowDrawList().AddRectFilled(cur, cur + patternSize, Utilities.GetPackedColor(200, 200, 200, 50));
				ImGui.GetWindowDrawList().AddRect(cur, cur + patternSize, Utilities.GetPackedColor(255, 255, 255, 255));
			}

			if(ImGui.BeginPopup("set-brush-size")) {
				ImGui.SetNextItemWidth(100);
				ImGui.DragInt("Brush Size", ref automapBrushSize, 0.03F, 1, 32);
				if(ImGui.Button("Start", new Vector2(ImGui.GetWindowSize().X - style.WindowPadding.X * 2, ImGui.GetTextLineHeight() + style.FramePadding.Y * 2))) {
					var brush = Program.CanvasPanel.TileBrush;
					brush.SetAutomapPattern(pattern, automapBrushSize, automapBrushSize);
					Program.CanvasPanel.SetTool(brush);
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}

			ImGui.PopID(); // i
		}
		
		ImGui.EndChild(); // automap-list
	}

	public class AddOperation : IFileEditOperation {
		private Scene scene;
		private TilesetLink link;
		public AddOperation(Scene scene, TilesetLink link) {
			this.scene = scene;
			this.link = link;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.scene.Tilesets.Add(op.link);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.scene.Tilesets.Remove(op.link);
		}
		public bool HasChanges() => true;
	}
	
	public class RemoveOperation : IFileEditOperation {
		private Scene scene;
		private TilesetLink link;
		private int index;
		public RemoveOperation(Scene scene, int index) {
			this.scene = scene;
			this.link = scene.Tilesets[index];
			this.index = index;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.scene.Tilesets.RemoveAt(op.index);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.scene.Tilesets.Insert(op.index, op.link);
		}
		public bool HasChanges() => true;
	}
	
	public class MoveOperation : IFileEditOperation {
		private Scene scene;
		private int index1;
		private int index2;
		public MoveOperation(Scene scene, int index1, int index2) {
			this.scene = scene;
			this.index1 = index1;
			this.index2 = index2;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			var t = op.scene.Tilesets[op.index1];
			op.scene.Tilesets[op.index1] = op.scene.Tilesets[op.index2];
			op.scene.Tilesets[op.index2] = t;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			var t = op.scene.Tilesets[op.index2];
			op.scene.Tilesets[op.index2] = op.scene.Tilesets[op.index1];
			op.scene.Tilesets[op.index1] = t;
		}
		public bool HasChanges() => true;
	}
	
	public class SlotOperation : IFileEditOperation {
		private TilesetLink link;
		private int oldSlot;
		private int newSlot;
		public SlotOperation(TilesetLink link, int slot) {
			this.link = link;
			this.oldSlot = link.Slot;
			this.newSlot = slot;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<SlotOperation>();
			op.link.Slot = op.newSlot;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<SlotOperation>();
			op.link.Slot = op.oldSlot;
		}
		public bool HasChanges() => true;
	}
	
	public class TilesetOperation : IFileEditOperation {
		private TilesetLink link;
		private Tileset oldTileset;
		private Tileset newTileset;
		public TilesetOperation(TilesetLink link, Tileset tileset) {
			this.link = link;
			this.oldTileset = link.Tileset;
			this.newTileset = tileset;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<TilesetOperation>();
			op.link.Tileset = op.newTileset;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<TilesetOperation>();
			op.link.Tileset = op.oldTileset;
		}
		public bool HasChanges() => true;
	}
	
}