using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;
using IconFonts;
using ImGuiNET;
using Serilog;
using Silk.NET.Input;
using Silk.NET.Maths;
using Rectangle = System.Drawing.Rectangle;

namespace E2D;

public class TilesetsPanel : Panel {

	private enum EditMode {
		TileColliders,
		AutomapPatterns,
		PresetPatterns
	}

	private enum ViewMode {
		List,
		Grid
	}

	public int PreviewScale {
		get => previewScale;
		set => previewScale = value;
	}

	private static ViewMode mode;
	private static string search;
	private static SortedList<int, Tileset> searchMatches;
	private static int worldTilesetCount;
	
	// Temp editing data, controlled by Edit()
	// private static string idEditBuffer;
	private static int tileEditIndex;
	private Tileset lastSelectedTileset;
	private int previewScale;
	private bool showColliders;

	private bool importOpenModal;
	private string importID;
	private string importGroup;
	private string importSaveDirectory;
	private bool importEmbeddedOption;
	private string importTexturePath;
	private Vector2D<int> importOffset;
	private Vector2D<int> importSpacing;
	private Vector2D<int> importTexels;
	private bool reimport;
	private Tileset reimportTileset;
	private Vector2 colliderDragOrigin;
	private bool colliderDrag;
	private int colliderHighlightIndex;

	private EditMode editMode;
	private AutomapPattern selectedAutomapPattern;
	private PresetPattern selectedPresetPattern;
	private FileEditEntry automapBitmaskEdit;
	private string automapNameEdit;
	private bool automapAddPopup;
	private bool automapDeletePopup;
	private AutomapPattern automapDeleteTarget;
	private AutomapMaskType automapMaskTypeOption;
	private int automapDemoIndex;

	private string presetNameEdit;
	private int presetWidthEdit;
	private int presetHeightEdit;
	private bool presetAddPopup;
	private bool presetCopyPopup;
	private PresetPattern presetCopyTarget;
	private bool presetDeletePopup;
	private PresetPattern presetDeleteTarget;
	private bool presetResizePopup;
	private PresetPattern presetResizeTarget;

	private FileEditEntry shapeEdit;
	
	public TilesetsPanel() {
		Title = $"{Codicons.Table} Tilesets";

		mode = ViewMode.List;
		search = "";
		searchMatches = new(new DuplicateKeyComparer<int>());
		worldTilesetCount = 0;
		lastSelectedTileset = null;
		previewScale = 3;
		showColliders = false;
		colliderDragOrigin = new(0);
		colliderDrag = false;
		colliderHighlightIndex = -1;
		importID = "";
		importGroup = "";
		importSaveDirectory = "";
		importEmbeddedOption = false;
		importTexturePath = "";
		importOffset = new(0);
		importSpacing = new(0);
		importTexels = new(16);
		shapeEdit = null;
		editMode = EditMode.TileColliders;
		selectedAutomapPattern = null;
		automapNameEdit = "";
		automapBitmaskEdit = null;
		automapAddPopup = false;
		automapDeletePopup = false;
		automapDeleteTarget = null;
		automapMaskTypeOption = AutomapMaskType.Mask2x2;
		automapDemoIndex = 0;
		presetNameEdit = "";
		presetWidthEdit = 3;
		presetHeightEdit = 3;
		presetAddPopup = false;
		presetCopyPopup = false;
		presetCopyTarget = null;
		presetDeletePopup = false;
		presetDeleteTarget = null;
	}

	protected override void Update() {
		if(Program.Project == null) {
			ImGui.Text("No file loaded...");
			return;
		}
		if(Program.Project.World == null) {
			ImGui.Text("No world active...");
			return;
		}
		if(worldTilesetCount != Program.Project.World.TilesetCount) {
			worldTilesetCount = Program.Project.World.TilesetCount;
			MatchSearch();
		}
		Menu();
		ImGui.Columns(2);
		Edit();
		ImGui.NextColumn();
		var selected = Items(Program.SelectedTileset);
		if(selected != Program.SelectedTileset) {
			Program.SetSelectedTileset(selected);
		}

		if(importOpenModal) {
			importOpenModal = false;
			ImGui.OpenPopup("Import Tileset");
		}
		
		ImportTilesetModal();
	}

	private void Menu() {
		if(ImGui.Button("Import")) {
			reimport = false;
			importID = "";
			importTexturePath = "";
			if(importEmbeddedOption) {
				importSaveDirectory = "N/A";
			} else {
				importSaveDirectory = Program.Project.World.TilesetsDirectory;
			}
			importOpenModal = true;
		}

		ImGui.SameLine();

		ImGui.SetNextItemWidth(300);
		ImGui.SliderInt("Preview Scale", ref previewScale, 1, 10);
		
		ImGui.SameLine();
		
		var style = ImGui.GetStyle();
		
		float searchWidth = 300 + ImGui.CalcTextSize("Search").X + style.FramePadding.X * 2.0F;
		float buttonWidth1 = ImGui.CalcTextSize("List").X + style.FramePadding.X * 2.0F;
		float buttonWidth2 = ImGui.CalcTextSize("Grid").X + style.FramePadding.X * 2.0F;
		float widthNeeded = buttonWidth1 + style.ItemSpacing.X + buttonWidth2 + searchWidth + style.FramePadding.X;
		
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - widthNeeded);
		
		ImGui.BeginDisabled(mode == ViewMode.List);
		if(ImGui.Button("List")) {
			mode = ViewMode.List;
		}
		ImGui.EndDisabled();
		ImGui.SameLine();
		ImGui.BeginDisabled(mode == ViewMode.Grid);
		if(ImGui.Button("Grid")) {
			mode = ViewMode.Grid;
		}
		ImGui.EndDisabled();
		ImGui.SameLine();
		ImGui.SetNextItemWidth(300);
		if(ImGui.InputText("Search", ref search, Program.IMGUI_STRING_MAX)) {
			MatchSearch();
		}
		if(ImGui.BeginPopupContextItem()) {
			if(ImGui.MenuItem("Clear")) {
				search = "";
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		}
	}

	private void MatchSearch() {
		World world = Program.Project.World;
		
		var regex = new Regex(Regex.Escape(search));
		
		searchMatches.Clear();
		for(int i = 0; i < world.TilesetCount; i++) {
			var tileset = world.GetTileset(i);
			if(search != "") {
				var idMatch = regex.Match(tileset.ID);
				var groupMatch = regex.Match(tileset.Group);
				var pathMatch = regex.Match(tileset.TextureFilePath);
				int maxMatchLength = int.Max(int.Max(idMatch.Length, groupMatch.Length), pathMatch.Length);
				if(idMatch.Success && idMatch.Length >= maxMatchLength) {
					searchMatches.Add(idMatch.Length, tileset);
				} else if(groupMatch.Success && groupMatch.Length >= maxMatchLength) {
					searchMatches.Add(groupMatch.Length, tileset);
				} else if(pathMatch.Success && pathMatch.Length >= maxMatchLength) {
					searchMatches.Add(pathMatch.Length, tileset);
				}
			} else {
				searchMatches.Add(i, tileset);
			}
		}
	}

	private Tileset Items(Tileset selected) {
		ImGui.BeginChild("tileset-select");
		
		World world = Program.Project.World;
		
		Action displayItemList = () => {
			Vector2 itemSize = new Vector2(ImGui.GetContentRegionAvail().X, 24);
			Vector2 itemSpacing = new Vector2(4, 4);

			int i = 0;
			foreach(var entry in searchMatches) {
				Tileset tileset = entry.Value;
				
				ImGui.TableNextRow();
				ImGui.TableNextColumn();
				if(ImGui.Selectable(tileset.ID, Program.SelectedTileset == tileset, ImGuiSelectableFlags.SpanAllColumns)) {
					if(tileset == selected) {
						selected = null;
					} else {
						selected = tileset;
					}
				}
				
				if(ImGui.BeginItemTooltip()) {
					if(tileset.TexturePreview != null) {
						ImGui.Image((IntPtr)tileset.TexturePreview.Handle, new Vector2(tileset.TexturePreview.Width, tileset.TexturePreview.Height));
					} else {
						ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
						ImGui.Text("Preview image could not be loaded!");
						ImGui.PopStyleColor();
					}
					ImGui.EndTooltip();
				}
				
				ImGui.TableNextColumn();
				ImGui.Text(tileset.Group);
				ImGui.TableNextColumn();
				ImGui.Text(tileset.TextureFilePath);

				i++;
			}
		};
		
		Action displayItemGrid = () => {
			Vector2 itemSize = new Vector2(300, 300);
			Vector2 itemSpacing = new Vector2(4, 4);

			ImGui.BeginChild("tileset-grid", ImGui.GetContentRegionAvail());
			
			Vector2 areaPos = ImGui.GetCursorScreenPos();
			Vector2 areaSize = ImGui.GetContentRegionAvail();
			Vector2 areaOffset = new Vector2(0, 0);
			int i = 0;
			foreach(var entry in searchMatches) {
				Tileset tileset = entry.Value;
				ImGui.PushID(i);

				ImDrawListPtr drawList = ImGui.GetWindowDrawList();
			
				Vector2 p0 = areaPos + areaOffset;
				Vector2 p1 = p0 + itemSize;
				
				drawList.AddRectFilled(p0, p1, Utilities.GetPackedColor(40, 40, 40, 255));
				drawList.AddRect(p0, p1, Utilities.GetPackedColor(80, 80, 80, 255));
				
				ImGui.SetCursorScreenPos(areaPos + areaOffset);
				ImGui.PushClipRect(p0, p1, true);
				
				if(tileset == selected) {
					drawList.AddRectFilled(p0, p1, Utilities.GetPackedColor(80, 80, 80, 80));
				}

				if(tileset != null) {
					Texture texSource = tileset.GetTexturePreview();
					Vector2 header = new(0, 12);
					Vector2 border = new(4,4);
					if(texSource != null) {
						Vector2 origin = p0 + border + header;
						Vector2 maxSize = p1 - p0 - border * 2 - header;
						Vector2 size  = new(texSource.Width, texSource.Height);
						float scale = 1.0F;
						if(size.Y > size.X) {
							scale = maxSize.Y / size.Y;
						} else {
							scale = maxSize.X / size.X;
						}
						drawList.AddImage(new IntPtr(texSource.Handle), origin, origin + size * scale, new(0, 0), new(1, 1));
					} else {
						drawList.AddRectFilled(p0 + border + header, p1 - border, Utilities.GetPackedColor(255, 0, 255, 255));
					}
				}
				
				if(ImGui.InvisibleButton("select", itemSize)) {
					if(tileset == selected) {
						selected = null;
					} else {
						selected = tileset;
					}
				}
				ImGui.SetItemTooltip(tileset.ID);
				if(ImGui.IsItemHovered()) {
					drawList.AddRectFilled(p0, p1, Utilities.GetPackedColor(120, 120, 120, 40));
				}
				
				ImGui.PopClipRect();
				
				// Calculate next item offset from areaPos
				areaOffset.X += itemSize.X + itemSpacing.X;
				if(areaSize.X - areaOffset.X < itemSize.X) {
					areaOffset.X = 0;
					areaOffset.Y += itemSize.Y + itemSpacing.Y;
				}
				ImGui.PopID();
				i++;
			}
			
			ImGui.SetCursorScreenPos(areaPos);
			ImGui.Dummy(areaOffset + itemSize);
			
			ImGui.EndChild();
		};

		ImGuiTableFlags tableFlags = ImGuiTableFlags.Borders;
		
		if(ImGui.BeginTable("tileset-table", 3, tableFlags)) {
			ImGui.TableSetupColumn("ID");
			ImGui.TableSetupColumn("Group");
			ImGui.TableSetupColumn("Path");
			ImGui.TableHeadersRow();
			
			if(mode == ViewMode.List) {
				displayItemList();
			}
			
			ImGui.EndTable();
		}
		
		// grid items outside of table layout, but still use table column sorting.
		if(mode == ViewMode.Grid) {
			displayItemGrid();
		}

		ImGui.EndChild();

		return selected;
	}
	
	private unsafe void Edit() {
		ImGui.BeginChild("tileset-edit");
		
		World world = Program.Project.World;

		Tileset tileset = Program.SelectedTileset;
		
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		
		Vector2 areaPos = ImGui.GetCursorScreenPos();
		Vector2 areaSize = ImGui.GetContentRegionAvail();
		
		ImGui.BeginChild("tileset-controls", new Vector2(ImGui.GetContentRegionAvail().X, 110), ImGuiChildFlags.Borders);
		ImGui.BeginDisabled(tileset == null);
		
		ImGui.Columns(2);

		if(tileset != lastSelectedTileset) {
			tileEditIndex = 0;
			selectedAutomapPattern = null;
			selectedPresetPattern = null;
		}

		string id = tileset != null ? tileset.ID : "";
		if(ImGui.InputText("ID", ref id, Program.IMGUI_STRING_MAX)) {}
		if(ImGui.IsItemDeactivatedAfterEdit()) {
			bool allowed = id != "";
			foreach(var ts in Program.Project.World.Tilesets) {
				if(ts.ID == id) {
					allowed = false;
					break;
				}
			}
			if(allowed) {
				Program.Project.ApplyEdit(this, new Tileset.NameOperation(tileset, id));
			}
		}

		string group = tileset != null ? tileset.Group : "";
		if(ImGui.InputText("Group", ref group, Program.IMGUI_STRING_MAX)) { }
		if(ImGui.IsItemDeactivatedAfterEdit()) {
			Program.Project.ApplyEdit(this, new Tileset.GroupOperation(tileset, group));
		}
		
		if(ImGui.Button("Reimport")) {
			reimport = true;
			reimportTileset = tileset;
			importOpenModal = true;
			importEmbeddedOption = tileset.IsEmbedded;
			if(tileset.IsEmbedded) {
				importSaveDirectory = "N/A";
				importTexturePath = tileset.TextureFilePath;
			} else {
				importSaveDirectory = Program.Project.GetDirectoryName(tileset.FileRelativePath);
				importTexturePath = Program.Project.GetRelativePath(
					Program.Project.GetAbsolutePath(
						tileset.TextureFilePath,
						tileset.FileAbsolutePath
					),
					Program.Project.GetAbsolutePath()
				);
			}
			importID = tileset.ID;
			importGroup = tileset.Group;
			importOffset = new(tileset.OffsetX, tileset.OffsetY);
			importSpacing = new(tileset.SpacingX, tileset.SpacingY);
			importTexels = new(tileset.SizeX, tileset.SizeY);
		}
		
		ImGui.NextColumn();
		
		if(tileset != null) {
			ImGui.Text($"Path: {tileset.TextureFilePath}");
			ImGui.Text($"Offset: {tileset.OffsetX} {tileset.OffsetY}");
			ImGui.Text($"Spacing: {tileset.SpacingX} {tileset.SpacingY}");
			ImGui.Text($"Texels: {tileset.SizeX} {tileset.SizeY}");
		} else {
			ImGui.Text("Path: --");
			ImGui.Text("Offset: --");
			ImGui.Text("Spacing: --");
			ImGui.Text("Texels: --");
		}
		
		ImGui.EndDisabled();
		ImGui.EndChild(); // tileset-controls

		ImGui.SetNextWindowSizeConstraints(new(1, 1), new(areaSize.X, areaSize.Y - 100));
		ImGui.BeginChild(
			"tileset-preview",
			new(areaSize.X, areaSize.X), // square, based on width available
			ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY,
			ImGuiWindowFlags.AlwaysHorizontalScrollbar | ImGuiWindowFlags.AlwaysVerticalScrollbar
		);
		
		if(ImGui.IsWindowHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Middle)) {
			ImGui.SetScrollY(ImGui.GetScrollY() - ImGui.GetIO().MouseDelta.Y);
			ImGui.SetScrollX(ImGui.GetScrollX() - ImGui.GetIO().MouseDelta.X);
		}
		
		Vector2 region = ImGui.GetContentRegionAvail();
		Vector2 p0 = areaPos;
		Vector2 p1 = p0 + region;
		if(tileset != null) {
			Texture texSource = tileset.GetTexturePreview();
			Vector2 border = new(4,4);
			if(texSource != null) {
				Vector2 origin = ImGui.GetCursorPos();
				Vector2 size = new Vector2(texSource.Width, texSource.Height) * previewScale;
				ImGui.Image(new IntPtr(texSource.Handle), size, new(0, 0), new(1, 1), new(1,1,1,1));
				
				int countX = tileset.GetTileCountX();
				int countY = tileset.GetTileCountY();

				int hoveredTile = 0;
				Vector2 hoveredTilePos = Vector2.Zero;
				Vector2 hoveredTileSize = Vector2.Zero;
				for(int y = 0; y < countY; y++) {
					for(int x = 0; x < countX; x++) {
						int tileID = (y * countX + x) + 1;
						bool selected = editMode == EditMode.TileColliders && tileEditIndex == tileID;
						
						ImGui.SetCursorPos(origin + new Vector2(world.TileWidth * x, world.TileHeight * y) * previewScale);
						ImGui.PushID(tileID);
						Vector2 c = ImGui.GetCursorScreenPos();
						Vector2 s = new Vector2(world.TileWidth, world.TileHeight) * previewScale;
						
						if(editMode == EditMode.TileColliders && showColliders) {
							var tiledata = tileset.GetTileData(tileID);
							if(tiledata != null) {
								for(int i = 0; i < tiledata.Shapes.Count; i++) {
									var shape = tiledata.Shapes[i];
									Vector2 s0 = new(shape.Position.X / tileset.SizeX, shape.Position.Y / tileset.SizeY);
									Vector2 s1 = new((shape.Position.X + shape.Size.X) / tileset.SizeX, (shape.Position.Y + shape.Size.Y) / tileset.SizeY);
									Vector2 t0 = new(float.Lerp(c.X, c.X + s.X, s0.X), float.Lerp(c.Y, c.Y + s.Y, s0.Y));
									Vector2 t1 = new(float.Lerp(c.X, c.X + s.X, s1.X), float.Lerp(c.Y, c.Y + s.Y, s1.Y));
									ImGui.GetWindowDrawList().AddRectFilled(t0, t1, Utilities.GetPackedColor(80, 80, 255, 60));
									ImGui.GetWindowDrawList().AddRect(t0, t1, Utilities.GetPackedColor(40, 40, 255, 200));
								}
							}
						}

						if(editMode == EditMode.AutomapPatterns && selectedAutomapPattern != null) {
							uint bitmask = selectedAutomapPattern.GetMask(tileID);
							if(selectedAutomapPattern.MaskType == AutomapMaskType.Mask3x3) {
								for(int i = 0; i < 9; i++) {
									bool a = (bitmask & (1 << i)) != 0;
									if(!a) continue;
									int tx = (i % 3);
									int ty = 2 - (i / 3);
									Vector2 s0 = new((s.X / 3.0F) * tx, (s.Y / 3.0F) * ty);
									Vector2 s1 = s0 + s / 3.0F;
									ImGui.GetWindowDrawList().AddRectFilled(c + s0, c + s1, Utilities.GetPackedColor(255, 80, 80, 120));
								}
							} else if(selectedAutomapPattern.MaskType == AutomapMaskType.Mask2x2) {
								for(int i = 0; i < 4; i++) {
									bool a = (bitmask & (1 << i)) != 0;
									if(!a) continue;
									int tx = (i % 2);
									int ty = 1 - (i / 2);
									Vector2 s0 = new((s.X / 2.0F) * tx, (s.Y / 2.0F) * ty);
									Vector2 s1 = s0 + s / 2.0F;
									ImGui.GetWindowDrawList().AddRectFilled(c + s0, c + s1, Utilities.GetPackedColor(255, 80, 80, 120));
								}
							}
						}
						
						if(ImGui.InvisibleButton("##tile", s)) {
							if(editMode == EditMode.TileColliders) {
								tileEditIndex = tileID;
								selected = true;
							}
						}

						if(editMode == EditMode.PresetPatterns) {
							if(ImGui.BeginDragDropSource()) {
								ImGui.Text($"Tile: {tileID}");
								var rect = tileset.GetTileRegion(tileID - 1);
								Vector2 uvMin = new Vector2(rect.Left / (float)tileset.GetTextureWidth(), rect.Top / (float)tileset.GetTextureHeight());
								Vector2 uvMax = new Vector2(rect.Right / (float)tileset.GetTextureWidth(), rect.Bottom / (float)tileset.GetTextureHeight());
								ImGui.Image((IntPtr)tileset.TexturePreview.Handle, new Vector2(world.TileWidth, world.TileHeight) * previewScale, uvMin, uvMax);
								ImGui.SetDragDropPayload("PRESET_TILE_ID", (IntPtr)(&tileID), sizeof(int));
								ImGui.EndDragDropSource();
							}
						}
						
						if(ImGui.IsItemHovered()) {
							ImGui.GetWindowDrawList().AddRectFilled(c, c + s, Utilities.GetPackedColor(200, 200, 200, 50));
							hoveredTile = tileID;
							hoveredTilePos = c;
							hoveredTileSize = s;
						}
						if(selected) {
							ImGui.GetWindowDrawList().AddRectFilled(c, c + s, Utilities.GetPackedColor(200, 200, 200, 50));
							ImGui.GetWindowDrawList().AddRect(c, c + s, Utilities.GetPackedColor(255, 255, 255, 255));
						}
						ImGui.PopID();
					}
				}
				if(editMode == EditMode.AutomapPatterns && selectedAutomapPattern != null) {
					if(ImGui.IsMouseDown(ImGuiMouseButton.Left)) {
						if(hoveredTile > 0) {
							uint bitmask = selectedAutomapPattern.GetMask(hoveredTile);

							Vector2 m = ImGui.GetMousePos();
							int ib = 0;
							float fx = Utilities.Map(m.X, hoveredTilePos.X, hoveredTilePos.X + hoveredTileSize.X, 0.0F, 1.0F);
							float fy = Utilities.Map(m.Y, hoveredTilePos.Y, hoveredTilePos.Y + hoveredTileSize.Y, 0.0F, 1.0F);
							if(selectedAutomapPattern.MaskType == AutomapMaskType.Mask3x3) {
								int ix = int.Clamp((int)(fx * 3.0F), 0, 2);
								int iy = 2 - int.Clamp((int)(fy * 3.0F), 0, 2);
								ib = ix + iy * 3;
							} else if(selectedAutomapPattern.MaskType == AutomapMaskType.Mask2x2) {
								int ix = int.Clamp((int)(fx * 2.0F), 0, 1);
								int iy = 1 - int.Clamp((int)(fy * 2.0F), 0, 1);
								ib = ix + iy * 2;
							}

							var op = automapBitmaskEdit?.GetData<AutomapPattern.BitmaskOperation>();
							if(op == null || op.Automap != selectedAutomapPattern || op.TileID != hoveredTile) {
								bool adding = (bitmask & (1 << ib)) == 0;
								op = new AutomapPattern.BitmaskOperation(selectedAutomapPattern, hoveredTile, adding);
								automapBitmaskEdit = Program.Project.BeginEdit(this, op);
							}

							if(op.Adding) {
								bitmask |= (uint)(1 << ib);
							} else {
								bitmask &= ~(uint)(1 << ib);
							}

							op.SetBitmask(bitmask);
							selectedAutomapPattern.Set(hoveredTile, bitmask);
						}
					} else {
						if(automapBitmaskEdit != null) {
							Program.Project.EndEdit(ref automapBitmaskEdit);
						}
					}
				}
				if(editMode == EditMode.TileColliders) {
					if(hoveredTile == 0) {
						ImGui.SetCursorPos(origin);
						Vector2 c = ImGui.GetContentRegionAvail();
						Vector2 s = new Vector2(float.Max(size.X, c.X), float.Max(size.Y, c.Y));
						if(ImGui.InvisibleButton("##clear", s)) {
							tileEditIndex = 0;
						}
					}
					if(ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
						tileEditIndex = 0;
					}
				}
				
			} else {
				ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
				ImGui.Text("Error: Image source could not be loaded!");
				ImGui.PopStyleColor();
			}
		} else {
			ImGui.Text("No tileset selected...");
		}
		ImGui.EndChild();

		ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1);
		ImGui.BeginChild("edit-windows", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Borders);
		ImGui.PopStyleVar();
		
		ImGui.BeginTabBar("tab-bar");
		if(ImGui.BeginTabItem("Tile Colliders")) {
			editMode = EditMode.TileColliders;
			TileColliderEdit();
			ImGui.EndTabItem();
		}
		if(ImGui.BeginTabItem("Automap Patterns")) {
			editMode = EditMode.AutomapPatterns;
			AutomapPatternEdit();
			ImGui.EndTabItem();
		}
		if(ImGui.BeginTabItem("Preset Patterns")) {
			editMode = EditMode.PresetPatterns;
			PresetPatternEdit();
			ImGui.EndTabItem();
		}
		ImGui.EndTabBar();
		ImGui.EndChild();
		
		ImGui.EndChild(); // tileset-edit
		
		lastSelectedTileset = tileset;
	}

	private void TileColliderEdit() {
		Tileset tileset = Program.SelectedTileset;

		ImGui.BeginDisabled(tileset == null);

		Vector2 origin = ImGui.GetCursorPos();
		Vector2 areaSize = ImGui.GetContentRegionAvail();
				
		if(ImGui.IsKeyDown(ImGuiKey.LeftShift)) {
			ImGui.GetIO().MouseWheelRequestAxisSwap = true;
		} else {
			ImGui.GetIO().MouseWheelRequestAxisSwap = false;
		}
		
		ImGui.BeginChild(
			"tile-preview",
			new(areaSize.Y, areaSize.Y), // square, based on width available
			ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeX
		);
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		Vector2 previewPos = ImGui.GetWindowPos();
		Vector2 previewSize = ImGui.GetWindowSize();
		Vector2 contentSize = ImGui.GetContentRegionAvail();
		Vector2 contentPos = ImGui.GetCursorScreenPos();
		
		Vector2 tileSize = contentSize * 0.75F;
		if(tileSize.X < tileSize.Y) {
			tileSize.Y = tileSize.X;
		} else {
			tileSize.X = tileSize.Y;
		}

		if(tileset != null && tileEditIndex > 0) {
			Vector2 p0 = contentPos + (contentSize / 2.0F) - (tileSize / 2.0F);
			Vector2 p1 = p0 + tileSize;
			Texture previewTexture = tileset.GetTexturePreview();
			if(previewTexture != null) {
				Rectangle rect = tileset.GetTileRegion(tileEditIndex - 1);
				Vector2 tilesetSize = new(tileset.GetTextureWidth(),  tileset.GetTextureHeight());
				Vector2 uv0 = new(rect.Left / tilesetSize.X, rect.Top / tilesetSize.Y);
				Vector2 uv1 = new(rect.Right / tilesetSize.X, rect.Bottom / tilesetSize.Y);
				drawList.AddImage((IntPtr)previewTexture.Handle, p0, p1, uv0, uv1);
				drawList.AddRect(p0-new Vector2(1), p1+new Vector2(1), Utilities.GetPackedColor(255, 255, 255, 255));
			}
			var tiledata = tileset.GetTileData(tileEditIndex);
			if(tiledata != null) {
				for(int i = 0; i < tiledata.Shapes.Count; i++) {
					var shape = tiledata.Shapes[i];
					Vector2 s0 = new(shape.Position.X / tileset.SizeX, shape.Position.Y / tileset.SizeY);
					Vector2 s1 = new((shape.Position.X + shape.Size.X) / tileset.SizeX, (shape.Position.Y + shape.Size.Y) / tileset.SizeY);
					Vector2 t0 = new(float.Lerp(p0.X, p1.X, s0.X), float.Lerp(p0.Y, p1.Y, s0.Y));
					Vector2 t1 = new(float.Lerp(p0.X, p1.X, s1.X), float.Lerp(p0.Y, p1.Y, s1.Y));
					if(i == colliderHighlightIndex) {
						drawList.AddRectFilled(t0, t1, Utilities.GetPackedColor(80, 80, 255, 80));
						drawList.AddRect(t0, t1, Utilities.GetPackedColor(40, 40, 255, 200));
					} else {
						drawList.AddRectFilled(t0, t1, Utilities.GetPackedColor(30, 30, 255, 80));
						drawList.AddRect(t0, t1, Utilities.GetPackedColor(30, 30, 255, 200));
					}
				}
			}
			if(ImGui.IsWindowFocused()) {
				Vector2 pos = ImGui.GetMousePos();
				bool drag = ImGui.IsMouseDragging(ImGuiMouseButton.Left, -1.0F);
				
				// protected child window resizing on the right
				if(pos.X < previewPos.X + previewSize.X - 12) {
					if(drag && !colliderDrag) {
						colliderDrag = true;
						colliderDragOrigin = pos;
					}
				}
				
				Vector2 d0 = new(float.Min(pos.X, colliderDragOrigin.X), float.Min(pos.Y, colliderDragOrigin.Y));
				Vector2 d1 = new(float.Max(pos.X, colliderDragOrigin.X), float.Max(pos.Y, colliderDragOrigin.Y));
				Vector2 c0 = new(float.Floor(Utilities.Map(d0.X, p0.X, p1.X, 0.0F, tileset.SizeX)), float.Floor(Utilities.Map(d0.Y, p0.Y, p1.Y, 0.0F, tileset.SizeY)));
				Vector2 c1 = new(float.Ceiling(Utilities.Map(d1.X, p0.X, p1.X, 0.0F, tileset.SizeX)), float.Ceiling(Utilities.Map(d1.Y, p0.Y, p1.Y, 0.0F, tileset.SizeY)));
				
				if(drag) {
					Vector2 t0 = new(float.Lerp(p0.X, p1.X, c0.X / tileset.SizeX), float.Lerp(p0.Y, p1.Y, c0.Y / tileset.SizeY));
					Vector2 t1 = new(float.Lerp(p0.X, p1.X, c1.X / tileset.SizeX), float.Lerp(p0.Y, p1.Y, c1.Y / tileset.SizeY));
					ImGui.GetWindowDrawList().AddRect(t0, t1, Utilities.GetPackedColor(255, 255, 255, 255));
				} else if(colliderDrag) {
					colliderDrag = false;
					if(tiledata == null) {
						tiledata = tileset.AddTileData(tileEditIndex);
					}
					int c = tiledata.Shapes.Count;
					var edit = Program.Project.BeginEdit(this, new TileData.ShapeCountOperation(tiledata, c + 1));
					var op = edit.GetData<TileData.ShapeCountOperation>();
					op.NewList[c] = new TileShape(c0, c1 - c0);
					Program.Project.EndEdit(ref edit);
				}
			}
		}

		ImGui.EndChild(); // tile-preview
		
		ImGui.SetCursorPos(origin + new Vector2(previewSize.X + 8, 0));

		ImGui.BeginChild("tile-options");
		
		ImGui.Checkbox("Show All Colliders", ref showColliders);
		
		ImGui.Separator();
		
		int tileCount = tileset != null ? tileset.GetTileCount() : 0;
		ImGui.DragInt("Tile ID", ref tileEditIndex, 0.02F, 0, tileCount+1);
		
		ImGui.BeginDisabled(tileEditIndex == 0);
		
		int count = 0;
		TileData data = null;
		if(tileset != null) {
			data = tileset.GetTileData(tileEditIndex);
			if(data != null) count = data.Shapes.Count;
		}

		if(ImGui.InputInt("Shape Count", ref count, 1, 1)) {
			if(ImGui.IsItemDeactivatedAfterEdit() || ImGui.IsItemClicked()) {
				if(count < 0) count = 0;
				if(data == null) {
					data = tileset.AddTileData(tileEditIndex);
				}
				Program.Project.ApplyEdit(this, new TileData.ShapeCountOperation(data, count));
			}
		}
		
		ImGui.Separator();

		colliderHighlightIndex = -1;
		if(data != null) {
			count = data.Shapes.Count;
			for(int i = 0; i < count; i++) {
				ImGui.PushID(i);

				ImGui.Text($"Shape #{i + 1}");
				if(ImGui.IsItemHovered()) {
					colliderHighlightIndex = i;
				}

				Vector2 pos = data.Shapes[i].Position;
				Vector2 size = data.Shapes[i].Size;
				bool end = false;
				
				if(ImGui.DragFloat2("Shape Pos", ref pos, 1.0F)) {
					if(shapeEdit == null) {
						shapeEdit = Program.Project.BeginEdit(this, new TileData.ShapeEditOperation(data, i));
					}
				}
				if(ImGui.IsItemDeactivatedAfterEdit()) {
					end = true;
				}
				if(ImGui.IsItemHovered()) {
					colliderHighlightIndex = i;
				}
				
				pos.X = float.Clamp(pos.X, 0.0F, tileset.SizeX);
				pos.Y = float.Clamp(pos.Y, 0.0F, tileset.SizeY);

				if(ImGui.DragFloat2("Shape Size", ref size, 1.0F)) {
					if(shapeEdit == null) {
						shapeEdit = Program.Project.BeginEdit(this, new TileData.ShapeEditOperation(data, i));
					}
				}
				if(ImGui.IsItemDeactivatedAfterEdit()) {
					end = true;
				}
				if(ImGui.IsItemHovered()) {
					colliderHighlightIndex = i;
				}
				
				size.X = float.Clamp(size.X, 0.0F, tileset.SizeX - pos.X);
				size.Y = float.Clamp(size.Y, 0.0F, tileset.SizeY - pos.Y);

				if(shapeEdit != null) {
					var op = shapeEdit.GetData<TileData.ShapeEditOperation>();
					op.SetPosition(pos);
					op.SetSize(size);
					data.Shapes[i] = op.NewShape;
					if(end) {
						Program.Project.EndEdit(ref shapeEdit, !op.HasChanges());
					}
				}

				ImGui.PopID();
			}
		}

		ImGui.EndDisabled(); // tileEditIndex == 0
		ImGui.EndChild(); // tile-options
		ImGui.EndDisabled(); // tileset == null
	}

	private unsafe void AutomapPatternEdit() {
		Tileset selectedTileset = Program.SelectedTileset;
		ImGui.BeginDisabled(selectedTileset == null);

		AutomapPattern.AddOperation addOperation = null;
		AutomapPattern.MoveOperation moveOperation = null;
		AutomapPattern.RemoveOperation removeOperation = null;

		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.X = 280;
		listSize.Y -= 96;
		Vector2 origin = ImGui.GetCursorPos();
		ImGui.BeginChild("automap-list", listSize, ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeX);
		listSize.X = ImGui.GetWindowSize().X;
		if(selectedTileset != null) {
			Vector2 cur = ImGui.GetCursorPos();
			for(int i = 0; i < selectedTileset.AutomapPatterns.Count; i++) {
				AutomapPattern automap = selectedTileset.AutomapPatterns[i];
				ImGui.PushID(i);
				cur = ImGui.GetCursorPos();
				bool selected = selectedAutomapPattern == automap;
				if(ImGui.Selectable(automap.Name, selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap)) {
					if(selected) {
						selectedAutomapPattern = null;
					} else {
						selectedAutomapPattern = automap;
					}
				}
				
				ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
				if(ImGui.BeginPopup("context")) {
					if(ImGui.MenuItem("Move Up")) {
						moveOperation = new AutomapPattern.MoveOperation(selectedTileset, i, i - 1);
					}
					if(ImGui.MenuItem("Move Down")) {
						moveOperation = new AutomapPattern.MoveOperation(selectedTileset, i, i + 1);
					}
					if(ImGui.MenuItem("Delete")) {
						removeOperation = new AutomapPattern.RemoveOperation(selectedTileset, automap);
					}
					ImGui.EndPopup();
				}
				
				if(ImGui.BeginDragDropSource()) {
					ImGui.Text(automap.Name);
					ImGui.SetDragDropPayload("MOVE_AUTOMAP_DATA", (IntPtr)(&i), sizeof(int));
					ImGui.EndDragDropSource();
				}
				Vector2 nextCur = ImGui.GetCursorPos();
				ImGui.SetCursorPos(cur - new Vector2(0, 4));
				Vector2 scur = ImGui.GetCursorScreenPos();
				ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
				if(moveOperation == null) {
					if(ImGui.BeginDragDropTarget()) {
						ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_AUTOMAP_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
						if(payloadPtr.NativePtr != null) {
							if(payloadPtr.IsPreview()) {
								ImGui.GetWindowDrawList().AddRectFilled(
									scur,
									scur + new Vector2(ImGui.GetContentRegionAvail().X, 3),
									Utilities.GetPackedColor(50, 80, 220, 255)
								);
							}
							if(payloadPtr.IsDelivery()) {
								int index = ((int*)payloadPtr.Data)[0];
								int insertIndex = i;
								if(index < i) insertIndex--;
								if(index != insertIndex) {
									moveOperation = new AutomapPattern.MoveOperation(selectedTileset, index, insertIndex);
								}
							}
						}
						ImGui.EndDragDropTarget();
					}
				}
				ImGui.SetCursorPos(nextCur);
				
				ImGui.PopID(); // i
			}
			if(selectedTileset.AutomapPatterns.Count > 0) {
				float height = ImGui.GetCursorPosY() - cur.Y;
				ImGui.SetCursorPos(cur + new Vector2(0, height - 4));
				Vector2 scur = ImGui.GetCursorScreenPos();
				ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
				if(moveOperation == null) {
					if(ImGui.BeginDragDropTarget()) {
						ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_AUTOMAP_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
						if(payloadPtr.NativePtr != null) {
							if(payloadPtr.IsPreview()) {
								ImGui.GetWindowDrawList().AddRectFilled(
									scur,
									scur + new Vector2(ImGui.GetContentRegionAvail().X, 3),
									Utilities.GetPackedColor(50, 80, 220, 255)
								);
							}
							if(payloadPtr.IsDelivery()) {
								int index = ((int*)payloadPtr.Data)[0];
								if(index < selectedTileset.AutomapPatterns.Count - 1) {
									moveOperation = new AutomapPattern.MoveOperation(selectedTileset, index, selectedTileset.AutomapPatterns.Count - 1);
								}
							}
						}
						ImGui.EndDragDropTarget();
					}
				}
			}
		}
		ImGui.EndChild(); // automap-list
		
		int selectedAutomapIndex = selectedTileset != null && selectedAutomapPattern != null ? selectedTileset.AutomapPatterns.IndexOf(selectedAutomapPattern) : -1;
		
		if(ImGui.Button(Codicons.DiffAdded)) {
			automapAddPopup = true;
		}
		ImGui.SetItemTooltip("Create");
		
		ImGui.BeginDisabled(selectedAutomapPattern == null);
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Trash)) {
			automapDeletePopup = true;
			automapDeleteTarget = selectedAutomapPattern;
		}
		ImGui.SetItemTooltip("Delete");
		
		ImGui.BeginDisabled(selectedAutomapIndex == 0);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronUp)) {
			moveOperation = new AutomapPattern.MoveOperation(selectedTileset, selectedAutomapIndex, selectedAutomapIndex - 1);
		}
		ImGui.SetItemTooltip("Move Up");
		ImGui.EndDisabled();
		
		ImGui.BeginDisabled(selectedAutomapIndex == (selectedTileset?.AutomapPatterns.Count ?? 0) - 1);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			moveOperation = new AutomapPattern.MoveOperation(selectedTileset, selectedAutomapIndex, selectedAutomapIndex + 1);
		}
		ImGui.SetItemTooltip("Move Down");
		ImGui.EndDisabled();

		AutomapOptions(listSize);
		
		ImGui.EndDisabled(); // selectedAutomapPattern == null
		
		AutomapAddPopup(ref addOperation);
		AutomapDeletePopup(ref removeOperation);

		if(addOperation != null) {
			Program.Project.ApplyEdit(this, addOperation);
		}
		
		if(moveOperation != null) {
			Program.Project.ApplyEdit(this, moveOperation);
		}

		if(removeOperation != null) {
			Program.Project.ApplyEdit(this, removeOperation);
		}
		
		AutomapPreview(listSize, origin);
		
		ImGui.EndDisabled(); // selectedTileset == null
	}

	private void AutomapAddPopup(ref AutomapPattern.AddOperation addOperation) {
		if(automapAddPopup) {
			automapAddPopup = false;
			automapNameEdit = "";
			ImGui.OpenPopup("add-automap");
		}
		if(ImGui.BeginPopup("add-automap")) {
			ImGui.Text("Create new automap");
			ImGui.InputText("Name", ref automapNameEdit, Program.IMGUI_STRING_MAX);
			if(ImGui.BeginCombo("Mask Type", automapMaskTypeOption.ToString())) {
				if(ImGui.Selectable(nameof(AutomapMaskType.Mask2x2), automapMaskTypeOption == AutomapMaskType.Mask2x2)) {
					automapMaskTypeOption = AutomapMaskType.Mask2x2;
				}
				if(ImGui.Selectable(nameof(AutomapMaskType.Mask3x3), automapMaskTypeOption == AutomapMaskType.Mask3x3)) {
					automapMaskTypeOption = AutomapMaskType.Mask3x3;
				}
				ImGui.EndCombo();
			}
			if(ImGui.Button("Confirm")) {
				AutomapPattern automap = new AutomapPattern(Program.SelectedTileset, automapNameEdit, automapMaskTypeOption);
				addOperation = new AutomapPattern.AddOperation(Program.SelectedTileset, automap);
				ImGui.CloseCurrentPopup();
			}
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		}
	}

	private void AutomapDeletePopup(ref AutomapPattern.RemoveOperation removeOperation) {
		if(automapDeletePopup) {
			automapDeletePopup = false;
			if(automapDeleteTarget != null) {
				ImGui.OpenPopup("delete-automap");
			}
		}
		if(ImGui.BeginPopup("delete-automap")) {
			ImGui.Text("Delete selected automap?");
			if(ImGui.Button("Confirm")) {
				removeOperation = new AutomapPattern.RemoveOperation(Program.SelectedTileset, automapDeleteTarget);
				if(selectedAutomapPattern == automapDeleteTarget) selectedAutomapPattern = null;
				ImGui.CloseCurrentPopup();
			}
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			automapDeleteTarget = null;
		}
	}

	private void AutomapOptions(Vector2 listSize) {
		Tileset selectedTileset = Program.SelectedTileset;
		string nameValue = selectedAutomapPattern?.Name ?? "";
		ImGui.SetNextItemWidth(listSize.X);
		if(ImGui.InputText("Name", ref nameValue, Program.IMGUI_STRING_MAX)) {}
		if(ImGui.IsItemDeactivatedAfterEdit()) {
			Program.Project.ApplyEdit(selectedTileset, new AutomapPattern.NameOperation(selectedAutomapPattern, nameValue));
		}
		string maskValueLabel = selectedAutomapPattern?.MaskType.ToString() ?? "";
		ImGui.SetNextItemWidth(listSize.X);
		if(ImGui.BeginCombo("Mask Type", maskValueLabel)) {
			if(ImGui.Selectable("Mask2x2")) {
				Program.Project.ApplyEdit(selectedTileset, new AutomapPattern.MaskTypeOperation(selectedAutomapPattern, AutomapMaskType.Mask2x2));
			}
			if(ImGui.Selectable("Mask3x3")) {
				Program.Project.ApplyEdit(selectedTileset, new AutomapPattern.MaskTypeOperation(selectedAutomapPattern, AutomapMaskType.Mask3x3));
			}
			ImGui.EndCombo();
		}
	}

	private void AutomapPreview(Vector2 listSize, Vector2 origin) {
		Tileset selectedTileset = Program.SelectedTileset;
		ImGui.SetCursorPos(origin + new Vector2(listSize.X + 8, 0));
		ImGui.BeginChild("pattern-preview", new Vector2(ImGui.GetContentRegionAvail().X, listSize.Y), ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar);
		
		if(selectedTileset != null && selectedAutomapPattern != null) {
			var world = Program.Project.World;
			Vector2 size = new Vector2(world.TileWidth, world.TileHeight) * previewScale;

			int w = AUTOMAP_DEMOS[automapDemoIndex].Width;
			int h = AUTOMAP_DEMOS[automapDemoIndex].Height;
			ImGui.Dummy(size * new Vector2(w, h));
			for(int i = 0; i < AUTOMAP_DEMOS[automapDemoIndex].Array.Length; i++) {
				if(AUTOMAP_DEMOS[automapDemoIndex].Array[i] == 0) continue;

				int x = i % w;
				int y = i / w;

				uint bitmask = 0;
				int index = 0;
				for(int ty = 1; ty >= -1; --ty) {
					for(int tx = -1; tx <= 1; ++tx) {
						int ix = x + tx;
						int iy = y + ty;
						if(ix >= 0 && ix < w && iy >= 0 && iy < h) {
							if(AUTOMAP_DEMOS[automapDemoIndex].Array[ix + iy * w] > 0) {
								bitmask |= (uint)(1 << index);
							}
						}
						index++;
					}
				}

				ImGui.SetCursorPos(new Vector2(10 + x * size.X, 10 + y * size.Y));
				Vector2 t0 = ImGui.GetCursorScreenPos();
				Vector2 t1 = t0 + size;

				int matchedTile = selectedAutomapPattern.Evaluate(bitmask);
				if(matchedTile > 0) {
					var rect = selectedTileset.GetTileRegion(matchedTile - 1);
					Vector2 uvMin = new Vector2(rect.Left / (float)selectedTileset.GetTextureWidth(), rect.Top / (float)selectedTileset.GetTextureHeight());
					Vector2 uvMax = new Vector2(rect.Right / (float)selectedTileset.GetTextureWidth(), rect.Bottom / (float)selectedTileset.GetTextureHeight());
					ImGui.GetWindowDrawList().AddImage(new IntPtr(selectedTileset.TexturePreview.Handle), t0, t1, uvMin, uvMax);
				} else {
					ImGui.GetWindowDrawList().AddRect(t0, t1, Utilities.GetPackedColor(255, 255, 255, 255));
				}
			}
		}

		ImGui.EndChild(); // pattern-preview
		
		ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - 300);
		ImGui.SetNextItemWidth(250);
		if(ImGui.BeginCombo("Preview", AUTOMAP_DEMOS[automapDemoIndex].Name)) {
			for(int i = 0; i < AUTOMAP_DEMOS.Length; i++) {
				if(ImGui.Selectable(AUTOMAP_DEMOS[i].Name)) {
					automapDemoIndex = i;
				}
			}
			ImGui.EndCombo();
		}
	}

	private unsafe void PresetPatternEdit() {
		Tileset tileset = Program.SelectedTileset;
		ImGui.BeginDisabled(tileset == null);
		
		int selectedPresetIndex = tileset != null && selectedPresetPattern != null ? tileset.PresetPatterns.IndexOf(selectedPresetPattern) : -1;
		
		PresetPattern.MoveOperation moveOperation = null;

		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.X = 280;
		listSize.Y -= 96;
		Vector2 origin = ImGui.GetCursorPos();
		ImGui.BeginChild("preset-list", listSize, ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeX);
		listSize.X = ImGui.GetWindowSize().X;
		if(tileset != null) {
			Vector2 cur = ImGui.GetCursorPos();
			for(int i = 0; i < tileset.PresetPatterns.Count; i++) {
				PresetPattern preset = tileset.PresetPatterns[i];
				ImGui.PushID(i);
				cur = ImGui.GetCursorPos();
				bool selected = selectedPresetPattern == preset;
				if(ImGui.Selectable(preset.Name, selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap)) {
					if(selected) {
						selectedPresetPattern = null;
						selectedPresetIndex = -1;
					} else {
						selectedPresetPattern = preset;
						selectedPresetIndex = i;
					}
				}
				ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
				if(ImGui.BeginPopup("context")) {
					if(ImGui.MenuItem("Move Up")) {
						moveOperation = new PresetPattern.MoveOperation(tileset, i, i - 1);
					}
					if(ImGui.MenuItem("Move Down")) {
						moveOperation = new PresetPattern.MoveOperation(tileset, i, i + 1);
					}
					if(ImGui.MenuItem("Delete")) {
						presetDeletePopup = true;
						presetDeleteTarget = preset;
					}
					ImGui.EndPopup();
				}
				if(ImGui.BeginDragDropSource()) {
					ImGui.Text(preset.Name);
					ImGui.SetDragDropPayload("MOVE_PRESET_DATA", (IntPtr)(&i), sizeof(int));
					ImGui.EndDragDropSource();
				}
				Vector2 nextCur = ImGui.GetCursorPos();
				ImGui.SetCursorPos(cur - new Vector2(0, 4));
				Vector2 scur = ImGui.GetCursorScreenPos();
				ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
				if(moveOperation == null) {
					if(ImGui.BeginDragDropTarget()) {
						ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_PRESET_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
						if(payloadPtr.NativePtr != null) {
							if(payloadPtr.IsPreview()) {
								ImGui.GetWindowDrawList().AddRectFilled(
									scur,
									scur + new Vector2(ImGui.GetContentRegionAvail().X, 3),
									Utilities.GetPackedColor(50, 80, 220, 255)
								);
							}
							if(payloadPtr.IsDelivery()) {
								int index = ((int*)payloadPtr.Data)[0];
								int insertIndex = i;
								if(index < i) insertIndex--;
								if(index != insertIndex) {
									moveOperation = new PresetPattern.MoveOperation(tileset, index, insertIndex);
								}
							}
						}
						ImGui.EndDragDropTarget();
					}
				}
				ImGui.SetCursorPos(nextCur);
				
				ImGui.PopID(); // i
			}
			if(tileset.PresetPatterns.Count > 0) {
				float height = ImGui.GetCursorPosY() - cur.Y;
				ImGui.SetCursorPos(cur + new Vector2(0, height - 4));
				Vector2 scur = ImGui.GetCursorScreenPos();
				ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
				if(moveOperation == null) {
					if(ImGui.BeginDragDropTarget()) {
						ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_PRESET_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
						if(payloadPtr.NativePtr != null) {
							if(payloadPtr.IsPreview()) {
								ImGui.GetWindowDrawList().AddRectFilled(
									scur,
									scur + new Vector2(ImGui.GetContentRegionAvail().X, 3),
									Utilities.GetPackedColor(50, 80, 220, 255)
								);
							}
							if(payloadPtr.IsDelivery()) {
								int index = ((int*)payloadPtr.Data)[0];
								if(index < tileset.PresetPatterns.Count - 1) {
									moveOperation = new PresetPattern.MoveOperation(tileset, index, tileset.PresetPatterns.Count - 1);
								}
							}
						}
						ImGui.EndDragDropTarget();
					}
				}
			}
		}
		ImGui.EndChild(); // preset-list
		
		if(ImGui.Button(Codicons.DiffAdded)) {
			presetAddPopup = true;
		}
		ImGui.SetItemTooltip("Create");
		
		ImGui.BeginDisabled(selectedPresetPattern == null);
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Copy)) {
			presetCopyPopup = true;
			presetCopyTarget = selectedPresetPattern;
		}
		ImGui.SetItemTooltip("Copy");
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Trash)) {
			presetDeletePopup = true;
			presetDeleteTarget = selectedPresetPattern;
		}
		ImGui.SetItemTooltip("Delete");
		
		ImGui.BeginDisabled(selectedPresetIndex <= 0);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronUp)) {
			moveOperation = new PresetPattern.MoveOperation(tileset, selectedPresetIndex, selectedPresetIndex - 1);
		}
		ImGui.SetItemTooltip("Move Up");
		ImGui.EndDisabled();
		
		ImGui.BeginDisabled(tileset == null || selectedPresetIndex >= tileset.PresetPatterns.Count - 1);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			moveOperation = new PresetPattern.MoveOperation(tileset, selectedPresetIndex, selectedPresetIndex + 1);
		}
		ImGui.SetItemTooltip("Move Down");
		ImGui.EndDisabled();

		string name = selectedPresetPattern?.Name ?? "";
		ImGui.SetNextItemWidth(listSize.X);
		ImGui.InputText("Name", ref name, Program.IMGUI_STRING_MAX);
		if(ImGui.IsItemDeactivatedAfterEdit()) {
			Program.Project.ApplyEdit(tileset, new PresetPattern.NameOperation(selectedPresetPattern, name));
		}

		Vector2 buttonSize = new Vector2(listSize.X, ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2);
		if(selectedPresetPattern != null) {
			if(ImGui.Button($"{selectedPresetPattern.Width} {selectedPresetPattern.Height}", buttonSize)) {
				presetResizePopup = true;
				presetResizeTarget = selectedPresetPattern;
			}
		} else {
			ImGui.Button("##nullsize", buttonSize);
		}
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
		ImGui.Text("Size");
		
		ImGui.SetCursorPos(origin + new Vector2(listSize.X + 8, 0));
		ImGui.BeginChild("preset-preview", new Vector2(ImGui.GetContentRegionAvail().X, listSize.Y), ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar);
		
		if(tileset != null && selectedPresetPattern != null) {
			var world = Program.Project.World;
			Vector2 tileSize = new Vector2(world.TileWidth, world.TileHeight) * previewScale;
			Vector2 start = ImGui.GetCursorPos();
			int w = selectedPresetPattern.Width;
			int h = selectedPresetPattern.Height;
			for(int i = 0; i < w * h; i++) {
				ImGui.PushID(i);

				int x = i % w;
				int y = i / w;

				int tileID = selectedPresetPattern.GetTile(i);

				ImGui.SetCursorPos(start + new Vector2(x * tileSize.X, y * tileSize.Y));
				Vector2 t0 = ImGui.GetCursorScreenPos();
				Vector2 t1 = t0 + tileSize;
				
				ImGui.GetWindowDrawList().AddRect(t0, t1, Utilities.GetPackedColor(200, 200, 200, 20));
				
				ImGui.InvisibleButton("##t", tileSize);

				if(ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
					tileID = 0;
				}

				if(tileID > 0) {
					if(ImGui.BeginDragDropSource()) {
						ImGui.Text($"Tile: {tileID}");
						var rect = tileset.GetTileRegion(tileID - 1);
						Vector2 uvMin = new Vector2(rect.Left / (float)tileset.GetTextureWidth(), rect.Top / (float)tileset.GetTextureHeight());
						Vector2 uvMax = new Vector2(rect.Right / (float)tileset.GetTextureWidth(), rect.Bottom / (float)tileset.GetTextureHeight());
						ImGui.Image((IntPtr)tileset.TexturePreview.Handle, new Vector2(world.TileWidth, world.TileHeight) * previewScale, uvMin, uvMax);
						ImGui.SetDragDropPayload("PRESET_TILE_ID", (IntPtr)(&tileID), sizeof(int));
						ImGui.EndDragDropSource();
					}
				}
				
				bool border = false;
				if(ImGui.BeginDragDropTarget()) {
					ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("PRESET_TILE_ID", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
					if(payloadPtr.NativePtr != null) {
						if(payloadPtr.IsPreview()) {
							border = true;
						}
						if(payloadPtr.IsDelivery()) {
							tileID = ((int*)payloadPtr.Data)[0];
						}
					}
					ImGui.EndDragDropTarget();
				}
				
				if(tileID > 0) {
					ImGui.SetItemTooltip($"Tile: {tileID}");
					var rect = tileset.GetTileRegion(tileID - 1);
					Vector2 uvMin = new Vector2(rect.Left / (float)tileset.GetTextureWidth(), rect.Top / (float)tileset.GetTextureHeight());
					Vector2 uvMax = new Vector2(rect.Right / (float)tileset.GetTextureWidth(), rect.Bottom / (float)tileset.GetTextureHeight());
					ImGui.GetWindowDrawList().AddImage(new IntPtr(tileset.TexturePreview.Handle), t0, t1, uvMin, uvMax);
				} else {
					ImGui.SetItemTooltip("Drag tile here to set");
				}

				if(border) {
					ImGui.GetWindowDrawList().AddRect(t0, t1, Utilities.GetPackedColor(255, 255, 255, 255));
				}

				if(tileID != selectedPresetPattern.GetTile(i)) {
					Program.Project.ApplyEdit(this, new PresetPattern.TileOperation(selectedPresetPattern, i, tileID));
				}
				
				ImGui.PopID();
			}

			{	// border
				ImGui.SetCursorPos(start);
				Vector2 t0 = ImGui.GetCursorScreenPos();
				Vector2 t1 = t0 + new Vector2(w * tileSize.X, h * tileSize.Y);
				ImGui.GetWindowDrawList().AddRect(t0, t1, Utilities.GetPackedColor(255, 255, 255, 255));
			}
		}
		
		ImGui.EndChild(); // preset-preview
		
		ImGui.EndDisabled(); // selectedPresetPattern == null
		ImGui.EndDisabled(); // tileset == null
		
		PresetAddPopup();
		PresetCopyPopup();
		PresetDeletePopup();
		PresetResizePopup();
		
		if(moveOperation != null) {
			Program.Project.ApplyEdit(this, moveOperation);
		}
	}

	private void PresetAddPopup() {
		if(presetAddPopup) {
			presetAddPopup = false;
			presetNameEdit = "";
			ImGui.OpenPopup("add-preset");
		}
		if(ImGui.BeginPopup("add-preset")) {
			ImGui.Text("Create new preset");
			ImGui.InputText("Name", ref presetNameEdit, Program.IMGUI_STRING_MAX);
			ImGui.InputInt("Width", ref presetWidthEdit);
			ImGui.InputInt("Height", ref presetHeightEdit);
			if(presetWidthEdit < 1) presetWidthEdit = 1;
			if(presetHeightEdit < 1) presetHeightEdit = 1;
			if(ImGui.Button("Confirm")) {
				PresetPattern preset = new PresetPattern(Program.SelectedTileset, presetNameEdit, presetWidthEdit, presetHeightEdit);
				Program.Project.ApplyEdit(Program.SelectedTileset, new PresetPattern.AddOperation(Program.SelectedTileset, preset));
				ImGui.CloseCurrentPopup();
			}
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		}
	}

	private void PresetCopyPopup() {
		if(presetCopyPopup) {
			presetCopyPopup = false;
			presetNameEdit = "";
			if(presetCopyTarget != null) {
				ImGui.OpenPopup("copy-preset");
			}
		}
		if(ImGui.BeginPopup("copy-preset")) {
			ImGui.Text("Copy selected preset");
			ImGui.SetNextItemWidth(250);
			ImGui.InputText("New Name", ref presetNameEdit, Program.IMGUI_STRING_MAX);
			if(ImGui.Button("Confirm")) {
				PresetPattern preset = new PresetPattern(Program.SelectedTileset, presetNameEdit, presetCopyTarget.Width, presetCopyTarget.Height);
				for(int i = 0; i < presetCopyTarget.Width * presetCopyTarget.Height; i++) {
					preset.SetTile(i, presetCopyTarget.GetTile(i));
				}
				Program.Project.ApplyEdit(Program.SelectedTileset, new PresetPattern.AddOperation(Program.SelectedTileset, preset));
				selectedPresetPattern = preset;
				ImGui.CloseCurrentPopup();
			}
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			presetCopyTarget = null;
		}
	}

	private void PresetDeletePopup() {
		if(presetDeletePopup) {
			presetDeletePopup = false;
			if(presetDeleteTarget != null) {
				ImGui.OpenPopup("delete-preset");
			}
		}
		if(ImGui.BeginPopup("delete-preset")) {
			ImGui.Text("Delete selected preset");
			if(ImGui.Button("Confirm")) {
				Program.Project.ApplyEdit(Program.SelectedTileset, new PresetPattern.RemoveOperation(Program.SelectedTileset, presetDeleteTarget));
				ImGui.CloseCurrentPopup();
			}
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			presetDeleteTarget = null;
		}
	}

	private void PresetResizePopup() {
		if(presetResizePopup) {
			presetResizePopup = false;
			if(presetResizeTarget != null) {
				presetWidthEdit = presetResizeTarget.Width;
				presetHeightEdit = presetResizeTarget.Height;
				ImGui.OpenPopup("resize-preset");
			}
		}
		if(ImGui.BeginPopup("resize-preset")) {
			ImGui.Text("Resize selected preset");
			ImGui.InputInt("Width", ref presetWidthEdit);
			ImGui.InputInt("Height", ref presetHeightEdit);
			if(presetWidthEdit < 1) presetWidthEdit = 1;
			if(presetHeightEdit < 1) presetHeightEdit = 1;
			ImGui.BeginDisabled(presetWidthEdit == presetResizeTarget.Width && presetHeightEdit == presetResizeTarget.Height);
			if(ImGui.Button("Confirm")) {
				Program.Project.ApplyEdit(Program.SelectedTileset, new PresetPattern.ResizeOperation(presetResizeTarget, presetWidthEdit, presetHeightEdit));
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			presetResizeTarget = null;
		}
	}

	public void ImportTilesetModal() {
		Project project = Program.Project;
		bool open = true;
		// ImGui.SetNextWindowSizeConstraints(new Vector2(400, 300), ImGui.GetIO().DisplaySize);
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F, 0.5F));
		if(ImGui.BeginPopupModal("Import Tileset", ref open, ImGuiWindowFlags.AlwaysAutoResize)) {
			Vector2 area = ImGui.GetContentRegionAvail();
			var style = ImGui.GetStyle();
			
			ImGui.BeginDisabled(reimport);

			ImGui.InputText("ID", ref importID, Program.IMGUI_STRING_MAX);
			
			ImGui.InputText("Group", ref importGroup, Program.IMGUI_STRING_MAX);

			string saveRelativePath = project.GetCombinedPath(importSaveDirectory, $"{importID}.{Tileset.FILE_EXTENSION}");
			string saveExternalLocation = project.GetAbsolutePath(saveRelativePath);
			if(ImGui.BeginCombo("Location Mode", importEmbeddedOption ? "Embedded" : "External")) {
				if(ImGui.Selectable("Embedded", importEmbeddedOption)) {
					importEmbeddedOption = true;
					importSaveDirectory = "N/A";
				}
				ImGui.SetItemTooltip($"Tileset will be embedded into:\n{project.GetAbsolutePath()}");
				if(ImGui.Selectable("External", !importEmbeddedOption)) {
					importEmbeddedOption = false;
					importSaveDirectory = project.World.TilesetsDirectory;
				}
				ImGui.SetItemTooltip($"Tileset will be saved to:\n{saveExternalLocation}");
				ImGui.EndCombo();
			}

			if(!reimport) {
				if(importEmbeddedOption) {
					ImGui.SetItemTooltip($"Tileset will be embedded into:\n{project.GetAbsolutePath()}");
				} else {
					ImGui.SetItemTooltip($"Tileset will be saved to:\n{saveExternalLocation}");
				}
			}

			ImGui.BeginDisabled(importEmbeddedOption);
			ImGui.InputText("##location-path", ref importSaveDirectory, Program.IMGUI_STRING_MAX);
			if(!importEmbeddedOption) ImGui.SetItemTooltip(project.GetAbsolutePath(importSaveDirectory));
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
			if(ImGui.Button("Select Location")) {
				FolderDialog.Select(importSaveDirectory, result => {
					if(result != null) {
						importSaveDirectory = project.GetRelativePath(result);
						if(importSaveDirectory == ".") {
							importSaveDirectory = "";
						}
					}
				});
			}
			ImGui.EndDisabled(); // importEmbeddedOption
			
			ImGui.EndDisabled(); // reimport
			
			ImGui.Spacing();
			ImGui.Spacing();
			
			string textureAbsolutePath = project.GetAbsolutePath(importTexturePath);

			ImGui.InputText("##texture-path", ref importTexturePath, Program.IMGUI_STRING_MAX);
			ImGui.SetItemTooltip(textureAbsolutePath);
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
			if(ImGui.Button("Select Texture")) {
				FileDialog.Open(importTexturePath, "png;bmp;jpg;jpeg", result => {
					if(result != null) {
						importTexturePath = project.GetRelativePath(result);
					}
				});
			}

			ImGui.BeginDisabled();
			// ImGui.Text("Under construction");
			ImGui.InputInt2("Offset", ref importOffset.X);
			ImGui.InputInt2("Spacing", ref importSpacing.X);
			ImGui.InputInt2("Texels", ref importTexels.X);
			ImGui.EndDisabled();
			
			ImGui.Spacing();
			ImGui.Spacing();

			bool valid = importID != "";
			bool duplicateID = false;
			bool textureDoesNotExist = !File.Exists(textureAbsolutePath);
			
			if(!reimport) {
				foreach(var t in Program.Project.World.Tilesets) {
					if(t.ID == importID) {
						duplicateID = true;
						break;
					}
				}
			}
			
			valid &= !textureDoesNotExist;
			valid &= !duplicateID;

			string importLabel = "Save As";
			if(reimport) {
				importLabel = "Apply";
			} else if(importID != "") {
				if(importEmbeddedOption) {
					importLabel = $"Save As: {importID}";
				} else {
					importLabel = $"Save As: {saveRelativePath}";
				}
			}

			if(duplicateID) {
				ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
				ImGui.Text($"ID [{importID}] already exists in world!");
				ImGui.PopStyleColor();
			}

			if(textureDoesNotExist && importTexturePath != "") {
				ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
				ImGui.Text($"Texture [{importTexturePath}] does not exist!");
				ImGui.PopStyleColor();
			}

			ImGui.BeginDisabled(!valid);
			ImGui.PushID("import-button");
			if(ImGui.Button(importLabel)) {
				Tileset tileset = null;
				if(!importEmbeddedOption) {
					importTexturePath = project.GetRelativePath(textureAbsolutePath, project.GetAbsolutePath(saveRelativePath));
				}
				if(reimport) {
					tileset = reimportTileset;
					tileset.OffsetX = importOffset.X;
					tileset.OffsetY = importOffset.Y;
					tileset.SpacingX = importSpacing.X;
					tileset.SpacingY = importSpacing.Y;
					tileset.SizeX = importTexels.X;
					tileset.SizeY = importTexels.Y;
					tileset.SetTexturePath(importTexturePath, true);
					project.MarkDirty();
					project.ClearEditHistory();
				} else {
					tileset = new Tileset(project, importEmbeddedOption);
					tileset.ID = importID;
					tileset.Group = importGroup;
					tileset.OffsetX = importOffset.X;
					tileset.OffsetY = importOffset.Y;
					tileset.SpacingX = importSpacing.X;
					tileset.SpacingY = importSpacing.Y;
					tileset.SizeX = importTexels.X;
					tileset.SizeY = importTexels.Y;
					if(!importEmbeddedOption) {
						tileset.FileRelativePath = saveRelativePath;
					}
					tileset.SetTexturePath(importTexturePath, false); // file watcher & texture will be updated from add operation
					project.ApplyEdit(this, new Tileset.AddOperation(project.World, tileset));
				}
				MatchSearch();
				ImGui.CloseCurrentPopup();
			}
			if(reimport) {
				ImGui.SetItemTooltip("Warning! This action will clear the project's current undo/redo history");
			}
			ImGui.PopID();
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		}
	}

	public void SelectTilesetModal(Action<bool, Tileset> onFinish) {
		bool selected = false;
		Tileset result = null;
		bool open = true;
		ImGui.SetNextWindowSizeConstraints(new Vector2(400, 300), ImGui.GetIO().DisplaySize);
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F, 0.5F));
		if(ImGui.BeginPopupModal("Select Tileset", ref open)) {
			if(worldTilesetCount != Program.Project.World.TilesetCount) {
				worldTilesetCount = Program.Project.World.TilesetCount;
				MatchSearch();
			}
			
			Vector2 area = ImGui.GetContentRegionAvail();
			var style = ImGui.GetStyle();

			float searchWidth = 300 + ImGui.CalcTextSize("Search").X + style.FramePadding.X * 2.0F;
			float buttonWidth1 = ImGui.CalcTextSize("List").X + style.FramePadding.X * 2.0F;
			float buttonWidth2 = ImGui.CalcTextSize("Grid").X + style.FramePadding.X * 2.0F;
			float widthNeeded = buttonWidth1 + style.ItemSpacing.X + buttonWidth2 + searchWidth + style.FramePadding.X;
		
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - widthNeeded);
		
			ImGui.BeginDisabled(mode == ViewMode.List);
			if(ImGui.Button("List")) {
				mode = ViewMode.List;
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			ImGui.BeginDisabled(mode == ViewMode.Grid);
			if(ImGui.Button("Grid")) {
				mode = ViewMode.Grid;
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			ImGui.SetNextItemWidth(300);
			if(ImGui.InputText("Search", ref search, Program.IMGUI_STRING_MAX)) {
				MatchSearch();
			}
			if(ImGui.BeginPopupContextItem()) {
				if(ImGui.MenuItem("Clear")) {
					search = "";
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
            
			result = Items(null);
			if(result != null) {
				selected = true;
				open = false;
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		}
		if(!open) {
			onFinish?.Invoke(selected, result);
		}
	}
	
	public class DuplicateKeyComparer<TKey> : IComparer<TKey> where TKey : IComparable {
		public int Compare(TKey x, TKey y) {
			int result = x.CompareTo(y);
			if(result == 0) return 1; // Handle equality as being greater. Note: this will break Remove(key) or
			else return result; // IndexOfKey(key) since the comparer never returns 0 to signal key equality
		}
	}

	public struct AutomapPreviewDemo {
		public string Name;
		public int Width;
		public int Height;
		public byte[] Array;
	}

	private readonly AutomapPreviewDemo[] AUTOMAP_DEMOS = new AutomapPreviewDemo[] {
		new AutomapPreviewDemo {
			Name = "Cardinal Cross",
			Width = 9,
			Height = 9,
			Array = new byte[] {
				0, 0, 0, 1, 1, 1, 0, 0, 0,
				0, 0, 0, 1, 1, 1, 0, 0, 0,
				0, 0, 0, 1, 1, 1, 0, 0, 0,
				1, 1, 1, 1, 1, 1, 1, 1, 1,
				1, 1, 1, 1, 1, 1, 1, 1, 1,
				1, 1, 1, 1, 1, 1, 1, 1, 1,
				0, 0, 0, 1, 1, 1, 0, 0, 0,
				0, 0, 0, 1, 1, 1, 0, 0, 0,
				0, 0, 0, 1, 1, 1, 0, 0, 0,
			}
		},
		new AutomapPreviewDemo {
			Name = "Miscellaneous",
			Width = 9,
			Height = 9,
			Array = new byte[] {
				1, 1, 1, 1, 1, 1, 1, 1, 1,
				1, 0, 1, 0, 0, 0, 1, 0, 1,
				1, 1, 1, 1, 0, 0, 1, 0, 1,
				1, 0, 0, 0, 0, 0, 0, 0, 1,
				1, 0, 0, 0, 1, 0, 1, 1, 1,
				1, 0, 0, 0, 0, 0, 1, 0, 1,
				1, 1, 1, 0, 0, 1, 1, 1, 1,
				1, 0, 0, 1, 0, 0, 1, 0, 1,
				1, 1, 1, 1, 1, 1, 1, 1, 1,
			}
		}
	};
	
}