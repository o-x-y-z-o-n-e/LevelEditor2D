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
						ImGui.ResetMouseDragDelta();
						Program.File.ApplyEdit(this, new MoveOperation(scene, i, n_next));
					}
				}
				
				ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
				if(ImGui.BeginPopup("context")) {
					if(ImGui.MenuItem(layer.Visible ? "Hide" : "Show")) {
						Program.File.ApplyEdit(this, new VisiblityOperation(layer, !layer.Visible));
					}
					ImGui.EndPopup();
				}
				
				ImGui.BeginDisabled(isolateLayerView);
				ImGui.SetCursorPos(cur);
				ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(layer.Visible ? Codicons.Eye : Codicons.EyeClosed).X - 6);
				if(ImGui.SmallButton(layer.Visible ? Codicons.Eye : Codicons.EyeClosed)) {
					Program.File.ApplyEdit(this, new VisiblityOperation(layer, !layer.Visible));
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
				Program.File.ApplyEdit(this, new AddOperation(scene, layer));
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
				Program.File.ApplyEdit(this, new AddOperation(scene, layer));
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
				Program.File.ApplyEdit(this, new RemoveOperation(scene, Program.SelectedLayer));
				ImGui.CloseCurrentPopup();
				layerRenameBuffer = "";
				// if(scene.LayerCount > 0) {
				// 	if(deleteLayerIndex >= scene.LayerCount) deleteLayerIndex = scene.LayerCount - 1;
				// 	Program.SelectedLayer = scene.GetLayer(deleteLayerIndex);
				// } else {
				// 	Program.SelectedLayer = null;
				// }
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

		int layerIndex = -1;
		if(Program.SelectedLayer != null) {
			layerIndex = scene.Layers.IndexOf(Program.SelectedLayer);
		}
		
		ImGui.BeginDisabled(layerIndex <= 0);
		if(ImGui.Button(Codicons.ChevronUp)) {
			Program.File.ApplyEdit(this, new MoveOperation(scene, layerIndex, layerIndex-1));
		}
		ImGui.EndDisabled();
		ImGui.BeginDisabled(scene == null || layerIndex >= scene.LayerCount - 1);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			Program.File.ApplyEdit(this, new MoveOperation(scene, layerIndex, layerIndex+1));
		}
		ImGui.EndDisabled();
		
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
					Program.File.ApplyEdit(this, new RenameOperation(layer, name));
				}
			}
			
			ImGui.BeginDisabled();
			string group = "--unused--";
			if(ImGui.InputText("Group", ref group, 256)) { }
			ImGui.EndDisabled();
			
			PropertyView.Run(layer.Properties);
		} else {
			ImGui.Text("No layer selected...");
		}
	}
	
	public class AddOperation : IFileEditOperation {
		private Scene scene;
		private Layer layer;
		public AddOperation(Scene scene, Layer layer) {
			this.scene = scene;
			this.layer = layer;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.scene.InsertLayer(op.layer, op.scene.LayerCount);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.scene.RemoveLayer(op.layer);
			if(Program.SelectedLayer == op.layer) {
				Program.SetSelectedLayer(null);
			}
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
			op.scene.SwapLayers(op.index1, op.index2);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			op.scene.SwapLayers(op.index2, op.index1);
		}
		public bool HasChanges() => index1 != index2;
	}
	
	public class VisiblityOperation : IFileEditOperation {
		private Layer layer;
		private bool oldValue;
		private bool newValue;
		public VisiblityOperation(Layer layer, bool newValue) {
			this.layer = layer;
			this.oldValue = layer.Visible;
			this.newValue = newValue;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<VisiblityOperation>();
			op.layer.Visible = op.newValue;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<VisiblityOperation>();
			op.layer.Visible = op.oldValue;
		}
		public bool HasChanges() => oldValue != newValue;
	}
	
	public class RenameOperation : IFileEditOperation {
		private Layer layer;
		private string oldName;
		private string newName;
		public RenameOperation(Layer layer, string newName) {
			this.layer = layer;
			this.oldName = layer.Name;
			this.newName = newName;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RenameOperation>();
			op.layer.Name = op.newName;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RenameOperation>();
			op.layer.Name = op.oldName;
		}
		public bool HasChanges() => oldName != newName;
	}
	
	public class RemoveOperation : IFileEditOperation {
		private Scene scene;
		private Layer layer;
		private int index;
		public RemoveOperation(Scene scene, Layer layer) {
			this.scene = scene;
			this.layer = layer;
			this.index = scene.GetLayerIndex(layer);
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.scene.RemoveLayer(op.layer);
			if(op.layer == Program.SelectedLayer) {
				Program.SetSelectedLayer(null);
			}
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.scene.InsertLayer(op.layer, op.index);
		}
		public bool HasChanges() => true;
	}
}