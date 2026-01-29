using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace L2D; 

public class EntitiesPanel : Panel {

	public EntitiesPanel() {
		Title = "Entities";
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

		if(Program.SelectedLayer.Type != LayerType.Entities) {
			ImGui.Text("Selected layer is not used for entities...");
			return;
		}

		Layer layer = Program.SelectedLayer;
		EntityDefinition selected = Program.SelectedEntity;
		
		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.Y -= 200;
		ImGui.BeginChild("entity-list", listSize, ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);

		int index = 0;
		foreach(var entity in layer.Entities.All) {
			ImGui.PushID(index);
			if(ImGui.Selectable(entity.Name, entity == selected)) {
				if(entity != selected) {
					Program.SetSelectedEntity(entity);
				} else {
					Program.SetSelectedEntity(null);
				}
			}
			if(ImGui.IsItemHovered()) {
				Program.CanvasPanel.ShowEntityHighlight(Program.SelectedScene, entity);
			}
			ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
			if(ImGui.BeginPopup("context")) {
				if(ImGui.MenuItem("Locate")) {
					// TODO
				}
				ImGui.EndPopup();
			}
			ImGui.PopID();
			index++;
		}
		
		ImGui.EndChild();
		
		if(ImGui.Button(Codicons.DiffAdded)) {
			// TODO
		}
		
		ImGui.SameLine();
		ImGui.BeginDisabled(Program.SelectedEntity == null);
		
		if(ImGui.Button(Codicons.Copy)) {
			// TODO
		}
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.Trash)) {
			// TODO
		}
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.ChevronUp)) {
			// TODO
		}
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			// TODO
		}
		
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(Codicons.OpenInProduct).X - 12);
		if(ImGui.Button(Codicons.OpenInProduct)) {
			// TODO
		}
		
		ImGui.EndDisabled();

		if(selected != null) {
			ImGui.SeparatorText("Entity Options");
			ImGui.InputText("Name", ref selected.Name, 512);
			ImGui.InputText("Type", ref selected.Type, 512);
			ImGui.DragFloat2("Position", ref selected.Position);
			if(ImGui.DragFloat2("Size", ref selected.Size)) {
				if(selected.Size.X < 0) selected.Size.X = 0;
				if(selected.Size.Y < 0) selected.Size.Y = 0;
			}
			PropertyView.Run(selected.Properties);
		} else {
			ImGui.Text("No entity selected...");
		}
	}
}