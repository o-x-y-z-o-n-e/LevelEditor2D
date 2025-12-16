using System.Numerics;
using ImGuiNET;

namespace L2D; 

public class LayersPanel : Panel {

	public LayersPanel() {
		Title = "Layers";
	}

	protected override void Update() {
		if(Program.File == null) {
			return;
		}

		World world = Program.File.World;
		Scene scene = Program.SelectedScene;
		
		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.Y -= 200;
		ImGui.BeginChild("layer_list", listSize, ImGuiChildFlags.Borders);

		int copyLayerIndex = -1;
		int deleteLayerIndex = -1;

		if(scene != null) {
			ImGui.PushItemFlag(ImGuiItemFlags.AllowDuplicateId, true);

			int count = scene.Layers.Count;
			for(int i = 0; i < count; i++) {
				ImGui.PushID(i);
				Vector2 cur = ImGui.GetCursorPos();
				
				Layer layer = scene.Layers[i];
				bool selected = Program.SelectedLayer == layer;
				if(ImGui.Selectable(layer.Name, selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap)) {
					if(selected) {
						Program.SelectedLayer = null;
					} else {
						Program.SelectedLayer = layer;
					}
				}
				if(ImGui.IsItemActive() && !ImGui.IsItemHovered()) {
					int n_next = i + (ImGui.GetMouseDragDelta(0).Y < 0.0F ? -1 : 1);
					if(n_next >= 0 && n_next < scene.Layers.Count) {
						scene.SwapLayers(i, n_next);
						ImGui.ResetMouseDragDelta();
					}
				}
				
				ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
				if(ImGui.BeginPopup("context")) {
					if(ImGui.MenuItem(layer.Visible ? "Hide" : "Show")) {
						layer.Visible = !layer.Visible;
					}
					if(ImGui.MenuItem("Copy")) {
						copyLayerIndex = i;
					}
					if(ImGui.MenuItem("Delete")) {
						deleteLayerIndex = i;
					}
					if(ImGui.MenuItem("Move Up")) {
						scene.SwapLayers(i, i-1);
					}
					if(ImGui.MenuItem("Move Down")) {
						scene.SwapLayers(i, i+1);
					}
					ImGui.EndPopup();
				}
				
				// TODO: visibility toggle
				//ImGui.SameLine();
				ImGui.SetCursorPos(cur);
				ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - 12);
				if(ImGui.SmallButton(layer.Visible ? "V" : "H")) {
					layer.Visible = !layer.Visible;
				}
				
				ImGui.PopID();
			}
			
			ImGui.PopItemFlag();
		}

		ImGui.EndChild();
		
		ImGui.BeginDisabled(Program.SelectedScene == null);

		if(ImGui.SmallButton("Add")) {
			Program.SelectedLayer = scene.AddLayer();
		}

		ImGui.SameLine();
		ImGui.BeginDisabled(Program.SelectedLayer == null);
		if(ImGui.SmallButton("Copy")) {
			// TODO: deep copy
		}
		ImGui.SameLine();
		if(ImGui.SmallButton("Delete")) {
			if(Program.SelectedLayer != null) {
				deleteLayerIndex = scene.Layers.IndexOf(Program.SelectedLayer);
			}
		}
		ImGui.SameLine();
		if(ImGui.SmallButton("Move Up")) {
			if(Program.SelectedLayer != null) {
				int i = scene.Layers.IndexOf(Program.SelectedLayer);
				scene.SwapLayers(i, i-1);
			}
		}
		ImGui.SameLine();
		if(ImGui.SmallButton("Move Down")) {
			if(Program.SelectedLayer != null) {
				int i = scene.Layers.IndexOf(Program.SelectedLayer);
				scene.SwapLayers(i, i+1);
			}
		}

		if(deleteLayerIndex >= 0) {
			scene.DeleteLayer(scene.GetLayer(deleteLayerIndex));
			if(scene.LayerCount > 0) {
				if(deleteLayerIndex >= scene.LayerCount) deleteLayerIndex = scene.LayerCount - 1;
				Program.SelectedLayer = scene.GetLayer(deleteLayerIndex);
			} else {
				Program.SelectedLayer = null;
			}
		}

		ImGui.EndDisabled(); // Program.SelectedLayer == null
		ImGui.EndDisabled(); // Program.SelectedScene == null
		
		ImGui.SeparatorText("Layer Settings");

		if(Program.SelectedLayer != null) {
			Layer layer = Program.SelectedLayer;
			ImGui.BeginDisabled(scene == null);
			string name = layer.Name;
			if(ImGui.InputText("Name", ref name, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
				layer.Name = name;
			}
			
			ImGui.BeginDisabled();
			string group = "--unused--";
			if(ImGui.InputText("Group", ref group, 256)) {
				// TODO
			}
			ImGui.EndDisabled();
			ImGui.EndDisabled();
		} else {
			ImGui.Text("No layer selected...");
		}
	
		// TODO: local tool bar for actions; new, up, down, copy, delete
		// TODO: drag selectables
	}
}