using System.Numerics;
using ImGuiNET;

namespace L2D; 

public class TilePickerPanel : Panel {

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
			
			if(ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen)) {
				string tilesetLabel = "<Select Tileset>";
				if(link.Tileset != null) {
					tilesetLabel = link.Tileset.ID;
				}

				if(ImGui.BeginCombo("Slot", $"{link.Slot}", ImGuiComboFlags.None)) {
					for(int s = 0; s < 16; s++) {
						bool match = false;
						foreach(var t in scene.Tilesets) {
							if(t.Slot == s) {
								match = true;
								break;
							}
						}
						if(match) continue;
						if(ImGui.Selectable($"Slot: {s}")) {
							link.Slot = s;
						}
					}
					ImGui.EndCombo();
				}

				if(ImGui.Button(tilesetLabel, new Vector2(ImGui.CalcItemWidth(), 0))) {
					
				}
				ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
				ImGui.Text("Tileset");
				
				Texture texSource = link.Tileset?.GetTexture();
				
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
				// drawList.AddRectFilled(areaPos, p1, Utilities.GetPackedColor(50, 50, 50, 255)); // background
				// drawList.AddRect(p0, p1, Utilities.GetPackedColor(180, 180, 180, 255)); // border
				// ImGui.Dummy(areaSize);
				if(texSource != null) {
					Vector2 border = new(4,4);
					if(texSource != null) {
						Vector2 size = new Vector2(texSource.Width, texSource.Height) * scale;
						//ImGui.PushClipRect(p0 + new Vector2(1), p1 - new Vector2(1), true);
						ImGui.Image(new IntPtr(texSource.Handle), size, new(0, 0), new(1, 1), new(1,1,1,1));
						//ImGui.PopClipRect();
					}
				} else {
					ImGui.Text("No tileset selected...");
				}
				ImGui.EndChild(); // tileset-picker
			}
			ImGui.Separator();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.PopID(); // i
		}
		
		ImGui.Button("Add", new Vector2(region.X / 2, 0));
		ImGui.SameLine();
		ImGui.Button("Remove", new Vector2(region.X / 2, 0));
		
		ImGui.EndChild(); // tileset-list
		
		ImGui.PopID(); // scene.ID
	}
}