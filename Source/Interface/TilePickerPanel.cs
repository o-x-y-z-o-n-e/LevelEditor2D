using System.Drawing;
using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace E2D; 

public class TilePickerPanel : Panel {

	private bool multiSelect;
	private Vector2 multiSelectOrigin;
	private TilesetLink tilesetLinkTarget;
	private int automapBrushSize;

	public TilePickerPanel() {
		Title = $"{Codicons.Combine} Tile Picker";
		multiSelect = false;
		multiSelectOrigin = Vector2.Zero;
		tilesetLinkTarget = null;
		automapBrushSize = 1;
	}

	protected override void Update() {
		if(Program.Project == null) {
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

		Scene scene = Program.SelectedScene;

		int maxTilesetSlots = scene.World.MaxTilesetSlots;
		
		ImGui.PushID(scene.ID);
		
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		var style = ImGui.GetStyle();

		bool collapseAll = false;
		bool expandAll = false;

		MenuBar(ref collapseAll, ref expandAll);

		var windowFlags = ImGuiWindowFlags.AlwaysVerticalScrollbar;
		if(ImGui.IsKeyDown(ImGuiKey.LeftShift)) {
			windowFlags |= ImGuiWindowFlags.NoScrollWithMouse;
		}

		ImGui.BeginChild("tileset-list", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders, windowFlags);
		
		Vector2 region = ImGui.GetContentRegionAvail() - new Vector2(8, 0);

		TilesetLink.AddOperation addOperation = null;
		TilesetLink.MoveOperation moveOperation = null;
		TilesetLink.RemoveOperation removeOperation = null;
		
		Links(windowFlags, region, 4, collapseAll, expandAll, ref moveOperation, ref removeOperation);

		AddButton(region, ref addOperation);

		if(addOperation != null) {
			Program.Project.ApplyEdit(scene, addOperation);
		}
		if(moveOperation != null) {
			Program.Project.ApplyEdit(scene, moveOperation);
		}
		if(removeOperation != null) {
			Program.Project.ApplyEdit(scene, removeOperation);
		}
		
		ImGui.EndChild(); // tileset-list
		
		ImGui.PopID(); // scene.ID
	}

	private void MenuBar(ref bool collapseAll, ref bool expandAll) {
		if(ImGui.Button("Collapse All")) collapseAll = true;
		ImGui.SameLine();
		if(ImGui.Button("Expand All")) expandAll = true;
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Top").X - 12);
		if(ImGui.Button("Top")) {
			ImGui.SetNextWindowScroll(new(0,0));
		}
	}

	private unsafe void Links(
		ImGuiWindowFlags windowFlags,
		Vector2 region,
		int scale,
		bool collapseAll,
		bool expandAll,
		ref TilesetLink.MoveOperation moveOperation,
		ref TilesetLink.RemoveOperation removeOperation
	) {
		Scene scene = Program.SelectedScene;
		Vector2 cur = ImGui.GetCursorPos();
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
			
			cur = ImGui.GetCursorPos();
			
			bool open = ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen);
			
			ImGui.OpenPopupOnItemClick("menu", ImGuiPopupFlags.MouseButtonRight);
			if(ImGui.BeginPopup("menu")) {
				ImGui.BeginDisabled(i == 0);
				if(ImGui.MenuItem("Move Up")) {
					moveOperation = new TilesetLink.MoveOperation(scene, i, i - 1);
				}
				ImGui.EndDisabled();
				ImGui.BeginDisabled(i == count - 1);
				if(ImGui.MenuItem("Move Down")) {
					moveOperation = new TilesetLink.MoveOperation(scene, i, i + 1);
				}
				ImGui.EndDisabled();
				if(ImGui.MenuItem("Remove")) {
					removeOperation = new TilesetLink.RemoveOperation(scene, i);
				}
				ImGui.EndPopup();
			}
			
			if(ImGui.BeginDragDropSource()) {
				ImGui.Text(scene.ID);
				ImGui.SetDragDropPayload("MOVE_TILESETLINK_DATA", (IntPtr)(&i), sizeof(int));
				ImGui.EndDragDropSource();
			}
			Vector2 nextCur = ImGui.GetCursorPos();
			ImGui.SetCursorPos(cur - new Vector2(0, 4));
			Vector2 scur = ImGui.GetCursorScreenPos();
			ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
			if(moveOperation == null) {
				if(ImGui.BeginDragDropTarget()) {
					ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_TILESETLINK_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
					if(payloadPtr.NativePtr != null) {
						if(payloadPtr.IsPreview()) {
							ImGui.GetWindowDrawList().AddRectFilled(
								scur,
								scur + new Vector2(ImGui.GetContentRegionAvail().X, 3),
								Utilities.GetPackedColor(50, 80, 220, 255)
							);
						}
						if(payloadPtr.IsDelivery()) {
							int srcIndex = ((int*)payloadPtr.Data)[0];
							int insertIndex = i;
							if(srcIndex < i) insertIndex--;
							if(srcIndex != insertIndex) {
								moveOperation = new TilesetLink.MoveOperation(scene, srcIndex, insertIndex);
							}
						}
					}
					ImGui.EndDragDropTarget();
				}
			}
			ImGui.SetCursorPos(nextCur);

			if(open) {
				Tileset tileset = link.Tileset;
				string tilesetLabel = "<Select Tileset>";
				if(tileset != null) {
					tilesetLabel = tileset.ID;
				}

				string slotPreviewLabel = $"{link.Slot}";

				if(ImGui.BeginCombo("Slot", slotPreviewLabel, ImGuiComboFlags.None)) {
					bool atLeastOneOption = false;
					for(int s = 1; s <= scene.World.MaxTilesetSlots; s++) {
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
							Program.Project.ApplyEdit(scene, new TilesetLink.SlotOperation(scene, link, s));
						}
					}

					if(!atLeastOneOption) {
						ImGui.Text("No more slots available");
					}

					ImGui.EndCombo();
				}

				if(ImGui.Button(tilesetLabel, new Vector2(ImGui.CalcItemWidth(), 0))) {
					tilesetLinkTarget = link;
					ImGui.OpenPopup("Select Tileset");
				}

				if(tilesetLinkTarget == link) {
					Program.TilesetsPanel.SelectTilesetModal((selected, tileset) => {
						if(selected) {
							Program.Project.ApplyEdit(scene, new TilesetLink.TilesetOperation(scene, link, tileset));
						}
						tilesetLinkTarget = null;
					});
				}

				ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
				ImGui.Text("Tileset");

				ImGui.BeginTabBar("tab-bar");
				if(ImGui.BeginTabItem("Tiles")) {
					TileGridView(scene, link, windowFlags, region, scale);
					ImGui.EndTabItem();
				}

				if(ImGui.BeginTabItem("Automaps")) {
					AutomapListView(scene, link, windowFlags, region, scale);
					ImGui.EndTabItem();
				}

				if(ImGui.BeginTabItem("Presets")) {
					PresetListView(scene, link, windowFlags, region, scale);
					ImGui.EndTabItem();
				}

				ImGui.EndTabBar();

				ImGui.Separator();
				ImGui.Spacing();
			}

			ImGui.PopID(); // i
		}
	}

	private void AddButton(Vector2 region, ref TilesetLink.AddOperation addOperation) {
		Scene scene = Program.SelectedScene;
		int nextSlotAvailable = 1;
		while(nextSlotAvailable <= scene.World.MaxTilesetSlots) {
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
		ImGui.BeginDisabled(nextSlotAvailable > scene.World.MaxTilesetSlots);
		if(ImGui.Button("Add", new Vector2(region.X, 0))) {
			addOperation = new TilesetLink.AddOperation(scene, new TilesetLink(scene.Project, nextSlotAvailable, null));
		}
		ImGui.EndDisabled();
		if(nextSlotAvailable > scene.World.MaxTilesetSlots) {
			ImGui.SetItemTooltip($"Max tilesets used: {scene.World.MaxTilesetSlots}");
		}
	}

	private void TileGridView(Scene scene, TilesetLink link, ImGuiWindowFlags windowFlags, Vector2 region, int scale) {
		Tileset tileset = link.Tileset;
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
								var tile = brush.Tilemap.Get(bx, by);
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

	private void AutomapListView(Scene scene, TilesetLink link, ImGuiWindowFlags windowFlags, Vector2 region, int scale) {
		var style = ImGui.GetStyle();
		
		Tileset tileset = link.Tileset;
		Texture texSource = tileset?.GetTexturePreview();
		
		Vector2 tileSize = new Vector2(scene.World.TileWidth, scene.World.TileHeight) * scale;
		Vector2 patternSize = tileSize * 3;
		
		int patternsPerRow = int.Max((int)((region.X - style.WindowPadding.X * 2) / (patternSize.X + style.ItemSpacing.X)), 1);

		Vector2 areaPos = ImGui.GetCursorScreenPos();
		Vector2 areaSize = new(
			region.X,
			tileset == null ? region.X :
				tileset.AutomapPatterns.Count * (patternSize.Y + style.ItemSpacing.Y) +
				style.WindowPadding.Y * 2 -
				style.ItemSpacing.Y
		);

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

		if(tileset != null) {
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
						Vector2 uvMin = new Vector2(rect.Left / (float)tileset.GetTextureWidth(),
							rect.Top / (float)tileset.GetTextureHeight());
						Vector2 uvMax = new Vector2(rect.Right / (float)tileset.GetTextureWidth(),
							rect.Bottom / (float)tileset.GetTextureHeight());
						ImGui.GetWindowDrawList()
							.AddImage(new IntPtr(tileset.TextureAtlas.Handle), t0, t1, uvMin, uvMax);
					}
				}

				if(ImGui.IsItemHovered()) {
					ImGui.GetWindowDrawList()
						.AddRectFilled(cur, cur + patternSize, Utilities.GetPackedColor(200, 200, 200, 50));
				}

				if(Program.CanvasPanel.TileBrush.Automap == pattern) {
					ImGui.GetWindowDrawList()
						.AddRectFilled(cur, cur + patternSize, Utilities.GetPackedColor(200, 200, 200, 50));
					ImGui.GetWindowDrawList()
						.AddRect(cur, cur + patternSize, Utilities.GetPackedColor(255, 255, 255, 255));
				}

				if(ImGui.BeginPopup("set-brush-size")) {
					ImGui.SetNextItemWidth(100);
					ImGui.DragInt("Brush Size", ref automapBrushSize, 0.03F, 1, 32);
					if(ImGui.Button("Start",
						new Vector2(ImGui.GetWindowSize().X - style.WindowPadding.X * 2,
							ImGui.GetTextLineHeight() + style.FramePadding.Y * 2))) {
						var brush = Program.CanvasPanel.TileBrush;
						brush.SetAutomap(pattern, automapBrushSize, automapBrushSize);
						Program.CanvasPanel.SetTool(brush);
						ImGui.CloseCurrentPopup();
					}

					ImGui.EndPopup();
				}

				ImGui.PopID(); // i
			}
		} else {
			ImGui.Text("No tileset selected...");
		}

		ImGui.EndChild(); // automap-list
	}

	private void PresetListView(Scene scene, TilesetLink link, ImGuiWindowFlags windowFlags, Vector2 region, int scale) {
		var style = ImGui.GetStyle();
		
		Tileset tileset = link.Tileset;
		Texture texSource = tileset?.GetTexturePreview();
		
		Vector2 tileSize = new Vector2(scene.World.TileWidth, scene.World.TileHeight) * scale;

		float maxHeight = 0;
		Vector2 cur = new Vector2(0, 0);
		Vector2 areaSize = new Vector2(region.X - style.WindowPadding.X * 2);
		if(tileset != null) {
			for(int i = 0; i < tileset.PresetPatterns.Count; i++) {
				PresetPattern preset = tileset.PresetPatterns[i];
				Vector2 presetSize = tileSize * new Vector2(preset.Width, preset.Height);
				if(cur.X + presetSize.X > areaSize.X) {
					cur.X = 0.0F;
					cur.Y += maxHeight + style.ItemSpacing.Y;
					maxHeight = 0.0F;
				}

				maxHeight = float.Max(maxHeight, presetSize.Y);
				cur.X += presetSize.X + style.ItemSpacing.X;
			}
			cur.Y += maxHeight;
			areaSize.Y = cur.Y;
		}

		var childFlags = ImGuiWindowFlags.None;
		if(!ImGui.IsKeyDown(ImGuiKey.LeftShift)) {
			windowFlags |= ImGuiWindowFlags.NoScrollWithMouse;
		}

		ImGui.BeginChild(
			"preset-list",
			new Vector2(region.X, areaSize.Y + style.WindowPadding.Y * 2),
			ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Borders,
			childFlags
		);

		Vector2 origin = ImGui.GetCursorPos();
		cur = Vector2.Zero;

		if(tileset != null) {
			for(int i = 0; i < tileset.PresetPatterns.Count; i++) {
				PresetPattern preset = tileset.PresetPatterns[i];
				ImGui.PushID(i);

				Vector2 presetSize = tileSize * new Vector2(preset.Width, preset.Height);
				if(cur.X + presetSize.X > areaSize.X) {
					cur.X = 0.0F;
					cur.Y += maxHeight + style.ItemSpacing.Y;
					maxHeight = 0.0F;
				}

				maxHeight = float.Max(maxHeight, presetSize.Y);

				ImGui.SetCursorPos(origin + cur);

				Vector2 scur = ImGui.GetCursorScreenPos();

				if(ImGui.InvisibleButton("button", presetSize)) {
					var brush = Program.CanvasPanel.TileBrush;
					brush.SetPreset(preset);
					Program.CanvasPanel.SetTool(brush);
				}

				ImGui.SetItemTooltip(preset.Name);

				for(int y = 0; y < preset.Height; y++) {
					for(int x = 0; x < preset.Width; x++) {
						Vector2 t0 = scur + new Vector2(x * tileSize.X, y * tileSize.Y);
						Vector2 t1 = t0 + tileSize;
						int tileID = preset.GetTile(x, y);
						if(tileID > 0) {
							var rect = tileset.GetTileRegion(tileID - 1);
							Vector2 uvMin = new Vector2(rect.Left / (float)tileset.GetTextureWidth(),
								rect.Top / (float)tileset.GetTextureHeight());
							Vector2 uvMax = new Vector2(rect.Right / (float)tileset.GetTextureWidth(),
								rect.Bottom / (float)tileset.GetTextureHeight());
							ImGui.GetWindowDrawList().AddImage(new IntPtr(tileset.TextureAtlas.Handle), t0, t1, uvMin,
								uvMax);
						}
					}
				}

				if(ImGui.IsItemHovered()) {
					ImGui.GetWindowDrawList().AddRectFilled(scur, scur + presetSize,
						Utilities.GetPackedColor(200, 200, 200, 50));
				}

				if(Program.CanvasPanel.TileBrush.Preset == preset) {
					ImGui.GetWindowDrawList().AddRectFilled(scur, scur + presetSize,
						Utilities.GetPackedColor(200, 200, 200, 50));
					ImGui.GetWindowDrawList()
						.AddRect(scur, scur + presetSize, Utilities.GetPackedColor(255, 255, 255, 255));
				}

				cur.X += presetSize.X + style.ItemSpacing.X;

				ImGui.PopID(); // i
			}
		} else {
			ImGui.Text("No tileset selected...");
		}

		ImGui.EndChild(); // preset-list
	}
	
}