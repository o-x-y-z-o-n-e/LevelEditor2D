using System.Drawing;
using System.Numerics;
using ImGuiNET;

namespace L2D; 

public class TilePickerPanel : Panel {

	private bool multiSelect;
	private Vector2 multiSelectOrigin;
	private int tilesetLinkTarget;

	public TilePickerPanel() {
		Title = "Tile Picker";
	}

	protected override void Update() {
		if(Program.SelectedScene == null) {
			ImGui.Text("No scene selected...");
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

		ImGui.BeginChild("tileset-list", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders, ImGuiWindowFlags.AlwaysVerticalScrollbar);
		
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
				if(ImGui.MenuItem("Move Up")) {
					// TODO
				}
				if(ImGui.MenuItem("Move Down")) {
					// TODO
				}
				if(ImGui.MenuItem("Remove")) {
					// TODO
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
							link.Slot = s;
						}
					}
					if(!atLeastOneOption) {
						ImGui.Text("No more slots available");
					}
					ImGui.EndCombo();
				}

				if(ImGui.Button(tilesetLabel, new Vector2(ImGui.CalcItemWidth(), 0))) {
					tilesetLinkTarget = i;
					ImGui.OpenPopup("select-tileset");
				}
				
				if(ImGui.BeginPopupModal("select-tileset")) {
			
					if(ImGui.Button("Cancel")) {
						ImGui.CloseCurrentPopup();
					}
					ImGui.EndPopup();
				}
				
				ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
				ImGui.Text("Tileset");
				
				Texture texSource = tileset?.GetTexturePreview();
				
				Vector2 areaPos = ImGui.GetCursorScreenPos();
				Vector2 areaSize = new(region.X, texSource?.Height * scale + 20 ?? region.X);
				ImGui.BeginChild(
					"tileset-picker",
					areaSize,
					ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Borders,
					ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.HorizontalScrollbar
				);
				Vector2 p0 = areaPos;
				Vector2 p1 = p0 + areaSize - new Vector2(16);
				bool hoveredTile = false;
				if(tileset != null && texSource != null) {
					Vector2 origin = ImGui.GetCursorPos();
					Vector2 originScreen = ImGui.GetCursorScreenPos();
					Vector2 border = new(4,4);
					Vector2 size = new Vector2(texSource.Width, texSource.Height) * scale;
					ImGui.Image(new IntPtr(texSource.Handle), size, new(0, 0), new(1, 1), new(1,1,1,1));
					
					var brush = Program.CanvasPanel.GetCurrentBrush();

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
							}
						}
					}

					for(int y = 0; y < countY; y++) {
						for(int x = 0; x < countX; x++) {
							int tileID = (y * countX + x) + 1;
							bool selected = false;
							
							if(brush != null && !brush.Resizing) {
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
							}
						}
					}
				} else {
					ImGui.Text("No tileset selected...");
				}
				
				ImGui.EndChild(); // tileset-picker
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
			scene.Tilesets.Add(new TilesetLink(scene.File, nextSlotAvailable, null));
		}
		ImGui.EndDisabled();
		
		ImGui.EndChild(); // tileset-list
		
		ImGui.PopID(); // scene.ID
	}
}