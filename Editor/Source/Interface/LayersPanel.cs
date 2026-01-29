using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace L2D; 

public class LayersPanel : Panel {

	public bool IsolateLayerView => isolateLayerView;

	private bool isolateLayerView;

	private string layerRenameBuffer;
	private int layerTypeOption;

	public LayersPanel() {
		Title = "Layers";
		isolateLayerView = false;
		layerRenameBuffer = "";
		layerTypeOption = 0;
	}

	protected override void Update() {
		if(Program.File == null) {
			return;
		}

		if(Program.SelectedLayer == null) isolateLayerView = false;

		World world = Program.File.World;
		Scene scene = Program.SelectedScene;
		
		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.Y -= 200;
		ImGui.BeginChild("layer-list", listSize, ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);

		if(scene != null) {
			ImGui.PushItemFlag(ImGuiItemFlags.AllowDuplicateId, true);
			
			int count = scene.Layers.Count;
			for(int i = 0; i < count; i++) {
				ImGui.PushID(i);
				Vector2 cur = ImGui.GetCursorPos();
				
				Layer layer = scene.Layers[i];
				bool selected = Program.SelectedLayer == layer;

				string iconTxt = layer.Type == LayerType.Entities ? Codicons.SymbolMisc : Codicons.Table;
				Vector2 iconOrigin = ImGui.GetCursorPos();
				ImGui.Dummy(ImGui.CalcTextSize(iconTxt));
				ImGui.SameLine();
				
				if(!layer.Visible) ImGui.PushStyleColor(ImGuiCol.Text, Utilities.GetPackedColor(255, 255, 255, 128));
				if(isolateLayerView && selected) ImGui.PushStyleColor(ImGuiCol.Text, Utilities.GetPackedColor(0, 0, 255, 255));
				if(ImGui.Selectable(layer.Name, selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap)) {
					if(selected) {
						Program.SelectedLayer = null;
					} else {
						Program.SelectedLayer = layer;
					}
				}
				if(isolateLayerView && selected) ImGui.PopStyleColor();
				if(!layer.Visible) ImGui.PopStyleColor();
				
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
					ImGui.EndPopup();
				}
				
				ImGui.BeginDisabled(isolateLayerView);
				ImGui.SetCursorPos(cur);
				ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(layer.Visible ? Codicons.Eye : Codicons.EyeClosed).X - 6);
				if(ImGui.SmallButton(layer.Visible ? Codicons.Eye : Codicons.EyeClosed)) {
					layer.Visible = !layer.Visible;
				}
				ImGui.EndDisabled();

				// Vector2 selCur = ImGui.GetCursorPos();
				ImGui.SetCursorPos(iconOrigin);
				ImGui.Text(iconTxt);
				// ImGui.SetCursorPos(selCur);
				
				ImGui.PopID();
			}
			
			ImGui.PopItemFlag();
		}

		ImGui.EndChild();
		
		ImGui.BeginDisabled(Program.SelectedScene == null);

		if(ImGui.Button(Codicons.DiffAdded)) {
			ImGui.OpenPopup("add-layer");
		}
		
		if(ImGui.BeginPopup("add-layer")) {
			ImGui.Text("Create new layer");
			ImGui.InputText("Name", ref layerRenameBuffer, 512);
			bool invalidName = layerRenameBuffer == "";
			foreach(var l in scene.Layers) {
				if(l.Name == layerRenameBuffer) {
					invalidName = true;
					break;
				}
			}
			if(ImGui.BeginCombo("Type", layerTypeOption == 0 ? "Tiles" : "Entities")) {
				if(ImGui.Selectable("Tiles", layerTypeOption == 0)) {
					layerTypeOption = 0;
				}
				if(ImGui.Selectable("Entities", layerTypeOption == 1)) {
					layerTypeOption = 1;
				}
				ImGui.EndCombo();
			}
			ImGui.BeginDisabled(invalidName);
			if(ImGui.Button("Ok")) {
				LayerType type = layerTypeOption == 0 ? LayerType.Tiles : LayerType.Entities;
				Layer layer = scene.AddLayer(type);
				layer.Name = layerRenameBuffer;
				Program.SelectedLayer = layer;
				ImGui.CloseCurrentPopup();
				layerRenameBuffer = "";
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
				layerRenameBuffer = "";
			}
			ImGui.EndPopup();
		}

		ImGui.SameLine();
		ImGui.BeginDisabled(Program.SelectedLayer == null);
		
		if(ImGui.Button(Codicons.Copy)) {
			ImGui.OpenPopup("copy-layer");
		}
		
		if(ImGui.BeginPopup("copy-layer")) {
			ImGui.Text("Copy selected layer");
			ImGui.InputText("New Name", ref layerRenameBuffer, 512);
			bool invalidName = layerRenameBuffer == "";
			foreach(var l in scene.Layers) {
				if(l.Name == layerRenameBuffer) {
					invalidName = true;
					break;
				}
			}
			ImGui.BeginDisabled(invalidName);
			if(ImGui.Button("Ok")) {
				Layer layer = scene.CopyLayer(Program.SelectedLayer);
				layer.Name = layerRenameBuffer;
				Program.SetSelectedLayer(layer);
				ImGui.CloseCurrentPopup();
				layerRenameBuffer = "";
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
				layerRenameBuffer = "";
			}
			ImGui.EndPopup();
		}
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.Trash)) {
			ImGui.OpenPopup("delete-layer");
		}
		
		if(ImGui.BeginPopup("delete-layer")) {
			ImGui.Text("Delete selected layer?");
			if(ImGui.Button("Ok")) {
				int deleteLayerIndex = scene.Layers.IndexOf(Program.SelectedLayer);
				scene.DeleteLayer(scene.GetLayer(deleteLayerIndex));
				if(scene.LayerCount > 0) {
					if(deleteLayerIndex >= scene.LayerCount) deleteLayerIndex = scene.LayerCount - 1;
					Program.SelectedLayer = scene.GetLayer(deleteLayerIndex);
				} else {
					Program.SelectedLayer = null;
				}
				ImGui.CloseCurrentPopup();
				layerRenameBuffer = "";
			}
			ImGui.SameLine();
			ImGui.Dummy(new Vector2(80, 0));
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Cancel").X - ImGui.GetStyle().FramePadding.X * 2);
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
				layerRenameBuffer = "";
			}
			ImGui.EndPopup();
		}
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.ChevronUp)) {
			if(Program.SelectedLayer != null) {
				int i = scene.Layers.IndexOf(Program.SelectedLayer);
				scene.SwapLayers(i, i-1);
			}
		}
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			if(Program.SelectedLayer != null) {
				int i = scene.Layers.IndexOf(Program.SelectedLayer);
				scene.SwapLayers(i, i+1);
			}
		}
		
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X -ImGui.CalcTextSize(isolateLayerView ? Codicons.GoToSearch : Codicons.SearchFuzzy).X - 12);
		if(ImGui.Button(isolateLayerView ? Codicons.GoToSearch : Codicons.SearchFuzzy)) {
			isolateLayerView = !isolateLayerView;
		}

		ImGui.EndDisabled(); // Program.SelectedLayer == null
		ImGui.EndDisabled(); // Program.SelectedScene == null
		
		if(Program.SelectedLayer != null) {
			Layer layer = Program.SelectedLayer;
			ImGui.SeparatorText("Layer Options");
			string name = layer.Name;
			if(ImGui.InputText("Name", ref name, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
				bool invalidName = name == "";
				foreach(var l in scene.Layers) {
					if(l.Name == name) {
						invalidName = true;
						break;
					}
				}
				if(!invalidName) {
					layer.Name = name;
				}
			}
			
			ImGui.BeginDisabled();
			string group = "--unused--";
			if(ImGui.InputText("Group", ref group, 256)) {
				// TODO
			}
			ImGui.EndDisabled();
			
			PropertyView.Run(layer.Properties);
		} else {
			ImGui.Text("No layer selected...");
		}
	}
}