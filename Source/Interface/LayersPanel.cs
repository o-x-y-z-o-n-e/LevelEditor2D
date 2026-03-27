using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using IconFonts;
using ImGuiNET;

namespace L2D;

public class LayersPanel : Panel {

	public bool IsolateLayerView => isolateLayerView;

	private bool isolateLayerView;

	private string layerRenameBuffer;
	private int layerTypeOption;

	private bool addPopup;
	private Layer addLayerToGroup;
	private bool deletePopup;
	private Layer deleteLayer;
	private bool copyPopup;
	private Layer copyLayer;

	private FileEditEntry colorEdit;
	
	public LayersPanel() {
		Title = $"{Codicons.Layers} Layers";
		isolateLayerView = false;
		layerRenameBuffer = "";
		layerTypeOption = 0;
		colorEdit = null;
	}

	protected override void Update() {
		if(Program.File == null) {
			return;
		}

		if(Program.SelectedLayer == null) {
			isolateLayerView = false;
		}
		
		World world = Program.File.World;

		Layer.MoveOperation moveOperation = null;

		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.Y -= 200;
		ImGui.BeginChild("layer-tree", listSize, ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);
		if(Program.SelectedScene != null) {
			Layers(Program.SelectedScene.Root, false, ref moveOperation, layer => {
				ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
				if(ImGui.BeginPopup("context")) {
					if(ImGui.MenuItem(layer.Visible ? "Hide" : "Show")) {
						Program.File.ApplyEdit(this, new Layer.VisiblityOperation(layer, !layer.Visible));
					}
					if(layer.Type == LayerType.Group) {
						if(layer.Collapsed) {
							if(ImGui.MenuItem("Expand", "Shift+Click")) {
								layer.Collapsed = false;
							}
						} else {
							if(ImGui.MenuItem("Collapse", "Shift+Click")) {
								layer.Collapsed = true;
							}
						}
						if(ImGui.MenuItem("Add Child")) {
							addPopup = true;
							addLayerToGroup = layer;
						}
					}
					if(layer.Type == LayerType.Tiles) {
						if(ImGui.MenuItem("Export as Image")) {
							FileDialog.Save("", "png", path => {
								if(path != null) {
									layer.Tilemap.ExportToFile(path);
								}
							});
						}
					}
					if(ImGui.MenuItem("Copy")) {
						copyPopup = true;
						copyLayer = layer;
					}
					if(ImGui.MenuItem("Delete")) {
						deletePopup = true;
						deleteLayer = layer;
					}
					ImGui.EndPopup();
				}
			});
		}
		ImGui.EndChild();
		
		int selectedLayerIndex = -1;
		if(Program.SelectedLayer != null) {
			selectedLayerIndex = Program.SelectedLayer.Group.GetChildIndex(Program.SelectedLayer);
		}

		ImGui.BeginDisabled(Program.SelectedScene == null);

		if(ImGui.Button(Codicons.DiffAdded)) {
			addPopup = true;
			addLayerToGroup = Program.SelectedScene.Root;
		}
		ImGui.SetItemTooltip("Create");

		ImGui.BeginDisabled(Program.SelectedLayer == null);
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Copy)) {
			copyPopup = true;
			copyLayer = Program.SelectedLayer;
		}
		ImGui.SetItemTooltip("Copy");

		ImGui.SameLine();
		if(ImGui.Button(Codicons.Trash)) {
			deletePopup = true;
			deleteLayer = Program.SelectedLayer;
		}
		ImGui.SetItemTooltip("Delete");

		ImGui.BeginDisabled(selectedLayerIndex <= 0);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronUp)) {
			moveOperation = new Layer.MoveOperation(
				Program.SelectedLayer.Group,
				selectedLayerIndex,
				Program.SelectedLayer.Group,
				selectedLayerIndex - 1
			);
		}
		ImGui.SetItemTooltip("Move Up");
		ImGui.EndDisabled();
		
		ImGui.BeginDisabled(Program.SelectedLayer == null || selectedLayerIndex >= Program.SelectedLayer.Group.ChildrenCount - 1);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			moveOperation = new Layer.MoveOperation(
				Program.SelectedLayer.Group,
				selectedLayerIndex,
				Program.SelectedLayer.Group,
				selectedLayerIndex + 1
			);
		}
		ImGui.SetItemTooltip("Move Down");
		ImGui.EndDisabled();
		
		ImGui.SameLine();
		ImGui.SetCursorPosX(
			ImGui.GetCursorPosX() +
			ImGui.GetContentRegionAvail().X -
			ImGui.CalcTextSize(isolateLayerView ? Codicons.GoToSearch : Codicons.SearchFuzzy).X - 
			12
		);
		if(ImGui.Button(isolateLayerView ? Codicons.GoToSearch : Codicons.SearchFuzzy)) {
			isolateLayerView = !isolateLayerView;
		}
		ImGui.SetItemTooltip("Isolate");

		ImGui.EndDisabled(); // Program.SelectedLayer == null
		ImGui.EndDisabled(); // Program.SelectedScene == null
		
		AddPopup();
		CopyPopup();
		DeletePopup();
		
		if(moveOperation != null) {
			Program.File.ApplyEdit(this, moveOperation);
		}
		
		if(Program.SelectedLayer != null) {
			Inspect(Program.SelectedLayer);
		} else {
			ImGui.Text("No layer selected...");
		}
	}

	private unsafe void Layers(Layer group, bool indent, ref Layer.MoveOperation moveOperation, Action<Layer> contextMenu) {
		Scene scene = group.Scene;
		if(indent) ImGui.Indent();
		int count = group.ChildrenCount;
		Vector2 cur = ImGui.GetCursorPos();
		for(int i = 0; i < count; i++) {
			ImGui.PushID(i);
			cur = ImGui.GetCursorPos();
			
			Layer layer = group.GetChild(i);
			bool selected = Program.SelectedLayer == layer;

			string iconTxt = "";

			if(layer.Type == LayerType.Entities) {
				iconTxt = Codicons.SymbolMisc;
			} else if(layer.Type == LayerType.Tiles) {
				iconTxt = Codicons.Table;
			} else if(layer.Type == LayerType.Group) {
				if(layer.Collapsed) {
					iconTxt = Codicons.ChevronRight;
				} else {
					iconTxt = Codicons.ChevronDown;
				}
			}
			Vector2 iconOrigin = ImGui.GetCursorPos();
			Vector2 iconSize = ImGui.CalcTextSize(iconTxt);
			ImGui.Dummy(iconSize);
			ImGui.SameLine();

			Vector2 scur = ImGui.GetCursorScreenPos();
			Vector2 labelSize = ImGui.CalcTextSize(layer.Name);
			if(!layer.IsGloballyVisible) ImGui.PushStyleColor(ImGuiCol.Text, Utilities.GetPackedColor(255, 255, 255, 128));
			if(isolateLayerView && selected) ImGui.PushStyleColor(ImGuiCol.Text, Utilities.GetPackedColor(0, 0, 255, 255));
			if(ImGui.Selectable(layer.Name, selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap)) {
				if(layer.Type == LayerType.Group && (ImGui.IsKeyDown(ImGuiKey.LeftShift) ||  ImGui.IsKeyDown(ImGuiKey.RightShift))) {
					layer.Collapsed = !layer.Collapsed;
				} else {
					if(selected) {
						Program.SelectedLayer = null;
					} else {
						Program.SelectedLayer = layer;
					}
				}
			}
			if(isolateLayerView && selected) ImGui.PopStyleColor();
			if(!layer.IsGloballyVisible) ImGui.PopStyleColor();
			
			contextMenu?.Invoke(layer);

			if(ImGui.BeginDragDropSource()) {
				ImGui.Text(layer.Name);
				int g = scene.GetLayerTreeIndex(group);
				long data = ((long)g << 32) | (i);
				ImGui.SetDragDropPayload("MOVE_LAYER_DATA", (IntPtr)(&data), sizeof(long));
				ImGui.EndDragDropSource();
			}
			if(moveOperation == null && layer.Type == LayerType.Group) {
				if(ImGui.BeginDragDropTarget()) {
					ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_LAYER_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
					if(payloadPtr.NativePtr != null) {
						if(payloadPtr.IsPreview()) {
							ImGui.GetWindowDrawList().AddRectFilled(
								scur,
								scur + labelSize,
								Utilities.GetPackedColor(200, 200, 200, 50)
							);
						}
						if(payloadPtr.IsDelivery()) {
							long data = ((long*)payloadPtr.Data)[0];
							int oldGroupTreeIndex = (int)(data >> 32);
							Layer oldGroup = oldGroupTreeIndex < 0 ? scene.Root : scene.GetLayer(oldGroupTreeIndex);
							int oldIndex = (int)(data);
							if(oldGroup != null && oldIndex >= 0 && oldIndex < oldGroup.ChildrenCount) {
								Layer layerToMove = oldGroup.GetChild(oldIndex);
								bool skip = layerToMove.Type == LayerType.Group && (layerToMove == group || group.IsChildOf(layerToMove));
								if(!skip) {
									int insertIndex = layer.ChildrenCount;
									if(oldGroup == layer) insertIndex--;
									moveOperation = new Layer.MoveOperation(oldGroup, oldIndex, layer, insertIndex);
								}
							}
						}
					}
					ImGui.EndDragDropTarget();
				}
			}

			float height = ImGui.GetCursorPosY() - cur.Y;
			
			ImGui.SetCursorPos(cur - new Vector2(0, 4));
			scur = ImGui.GetCursorScreenPos();
			ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
			if(moveOperation == null) {
				if(ImGui.BeginDragDropTarget()) {
					ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_LAYER_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
					if(payloadPtr.NativePtr != null) {
						if(payloadPtr.IsPreview()) {
							ImGui.GetWindowDrawList().AddRectFilled(
								scur,
								scur + new Vector2(ImGui.GetContentRegionAvail().X, 3),
								Utilities.GetPackedColor(50, 80, 220, 255)
							);
						}
						if(payloadPtr.IsDelivery()) {
							long data = ((long*)payloadPtr.Data)[0];
							int oldGroupTreeIndex = (int)(data >> 32);
							Layer oldGroup = oldGroupTreeIndex < 0 ? scene.Root : scene.GetLayer(oldGroupTreeIndex);
							int oldIndex = (int)(data);
							if(oldGroup != null && oldIndex >= 0 && oldIndex < oldGroup.ChildrenCount) {
								Layer layerToMove = oldGroup.GetChild(oldIndex);
								bool skip = layerToMove.Type == LayerType.Group && (layerToMove == group || group.IsChildOf(layerToMove));
								if(!skip) {
									int insertIndex = i;
									if(oldGroup == group && oldIndex < i) insertIndex--;
									if(oldGroup != group || oldIndex != insertIndex) {
										moveOperation = new Layer.MoveOperation(oldGroup, oldIndex, group, insertIndex);
									}
								}
							}
						}
					}
					ImGui.EndDragDropTarget();
				}
			}

			ImGui.BeginDisabled(isolateLayerView);
			ImGui.SetCursorPos(cur);
			ImGui.SetCursorPosX(
				ImGui.GetCursorPosX() +
				ImGui.GetContentRegionAvail().X -
				ImGui.CalcTextSize(layer.Visible ? Codicons.Eye : Codicons.EyeClosed).X -
				ImGui.GetStyle().FramePadding.X * 2.5F
			);
			if(ImGui.SmallButton(layer.Visible ? Codicons.Eye : Codicons.EyeClosed)) {
				Program.File.ApplyEdit(this, new Layer.VisiblityOperation(layer, !layer.Visible));
			}
			ImGui.EndDisabled();

			if(layer.Type == LayerType.Group) {
				ImGui.SetCursorPos(iconOrigin + new Vector2(2,0));
				Vector2 buttonPos = ImGui.GetCursorScreenPos();
				if(ImGui.InvisibleButton("collapse", iconSize)) {
					layer.Collapsed = !layer.Collapsed;
				}
				if(ImGui.IsItemHovered()) {
					uint col = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderHovered]);
					ImGui.GetWindowDrawList().AddRectFilled(buttonPos, buttonPos + iconSize, col, 3.0F);
				}
			}
			
			ImGui.SetCursorPos(iconOrigin + new Vector2(2,0));
			if(layer.Type != LayerType.Group) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(layer.Color, 1.0F));
			ImGui.Text(iconTxt);
			if(layer.Type != LayerType.Group) ImGui.PopStyleColor();
			
			if(layer.Type == LayerType.Group && !layer.Collapsed) {
				Layers(layer, true, ref moveOperation, contextMenu);
			}

			ImGui.PopID();
		}

		if(group.ChildrenCount > 0) {
			float height = ImGui.GetCursorPosY() - cur.Y;
			ImGui.SetCursorPos(cur + new Vector2(0, height - 4));
			Vector2 scur = ImGui.GetCursorScreenPos();
			ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
			if(moveOperation == null) {
				if(ImGui.BeginDragDropTarget()) {
					ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_LAYER_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
					if(payloadPtr.NativePtr != null) {
						if(payloadPtr.IsPreview()) {
							ImGui.GetWindowDrawList().AddRectFilled(
								scur,
								scur + new Vector2(ImGui.GetContentRegionAvail().X, 3),
								Utilities.GetPackedColor(50, 80, 220, 255)
							);
						}
						if(payloadPtr.IsDelivery()) {
							long data = ((long*)payloadPtr.Data)[0];
							int oldGroupTreeIndex = (int)(data >> 32);
							Layer oldGroup = oldGroupTreeIndex < 0 ? scene.Root : scene.GetLayer(oldGroupTreeIndex);
							int oldIndex = (int)(data);
							if(oldGroup != null && oldIndex >= 0 && oldIndex < oldGroup.ChildrenCount) {
								Layer layerToMove = oldGroup.GetChild(oldIndex);
								bool skip = layerToMove.Type == LayerType.Group && (layerToMove == group || group.IsChildOf(layerToMove));
								if(!skip) {
									int insertIndex = group.ChildrenCount;
									if(oldGroup == group) insertIndex--;
									if(oldGroup != group || oldIndex != insertIndex) {
										moveOperation = new Layer.MoveOperation(oldGroup, oldIndex, group, insertIndex);
									}
								}
							}
						}
					}
					ImGui.EndDragDropTarget();
				}
			}
			ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 8);
		}
		
		if(indent) ImGui.Unindent();
	}

	private void AddPopup() {
		Scene scene = Program.SelectedScene;
		if(addPopup) {
			addPopup = false;
			layerRenameBuffer = "";
			if(addLayerToGroup != null) {
				ImGui.OpenPopup("add-layer");
			}
		}
		if(ImGui.BeginPopup("add-layer")) {
			ImGui.Text("Create new layer");
			ImGui.InputText("Name", ref layerRenameBuffer, Program.IMGUI_STRING_MAX);
			bool invalidName = layerRenameBuffer == "";
			foreach(var l in scene.GetAllLayers()) {
				if(l.Name == layerRenameBuffer) {
					invalidName = true;
					break;
				}
			}
			string layerTypeLabel = layerTypeOption switch {
				0 => "Tiles",
				1 => "Entities",
				2 => "Group",
				_ => ""
			};
			if(ImGui.BeginCombo("Type", layerTypeLabel)) {
				if(ImGui.Selectable("Tiles", layerTypeOption == 0)) {
					layerTypeOption = 0;
				}
				if(ImGui.Selectable("Entities", layerTypeOption == 1)) {
					layerTypeOption = 1;
				}
				if(ImGui.Selectable("Group", layerTypeOption == 2)) {
					layerTypeOption = 2;
				}
				ImGui.EndCombo();
			}
			ImGui.BeginDisabled(invalidName);
			if(ImGui.Button("Ok")) {
				LayerType type = layerTypeOption switch {
					0 => LayerType.Tiles,
					1 => LayerType.Entities,
					2 => LayerType.Group
				};
				Layer layer = new Layer(scene, type);
				layer.Name = layerRenameBuffer;
				Program.File.ApplyEdit(this, new Layer.AddOperation(addLayerToGroup, layer, addLayerToGroup.ChildrenCount));
				Program.SetSelectedLayer(layer);
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			addLayerToGroup = null;
		}
	}

	private void CopyPopup() {
		if(copyPopup) {
			copyPopup = false;
			layerRenameBuffer = "";
			if(copyLayer != null) {
				ImGui.OpenPopup("copy-layer");
			}
		}
		if(ImGui.BeginPopup("copy-layer")) {
			ImGui.Text("Copy selected layer");
			ImGui.InputText("New Name", ref layerRenameBuffer, Program.IMGUI_STRING_MAX);
			bool invalidName = layerRenameBuffer == "";
			foreach(var l in copyLayer.Scene.GetAllLayers()) {
				if(l.Name == layerRenameBuffer) {
					invalidName = true;
					break;
				}
			}
			ImGui.BeginDisabled(invalidName);
			if(ImGui.Button("Ok")) {
				Layer newLayer = new Layer(copyLayer.Scene, copyLayer.Type);
				newLayer.Name = layerRenameBuffer;
				Layer.Copy(copyLayer, newLayer);
				Program.File.ApplyEdit(this, new Layer.AddOperation(copyLayer.Group, newLayer, copyLayer.Group.GetChildIndex(copyLayer) + 1));
				Program.SetSelectedLayer(newLayer);
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			copyLayer = null;
		}
	}

	private void DeletePopup() {
		if(deletePopup) {
			deletePopup = false;
			if(deleteLayer != null) {
				ImGui.OpenPopup("delete-layer");
			}
		}
		if(ImGui.BeginPopup("delete-layer")) {
			ImGui.Text("Delete selected layer?");
			if(ImGui.Button("Ok")) {
				Program.File.ApplyEdit(this, new Layer.RemoveOperation(deleteLayer.Group, deleteLayer));
				ImGui.CloseCurrentPopup();
			}
			ImGui.SameLine();
			ImGui.Dummy(new Vector2(80, 0));
			ImGui.SameLine();
			ImGui.SetCursorPosX(
				ImGui.GetCursorPosX() +
				ImGui.GetContentRegionAvail().X -
				ImGui.CalcTextSize("Cancel").X -
				ImGui.GetStyle().FramePadding.X * 2
			);
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			deleteLayer = null;
		}
	}

	private void Inspect(Layer layer) {
		Scene scene = layer.Scene;
		
		ImGui.SeparatorText("Layer Options");
		string name = layer.Name;
		if(ImGui.InputText("Name", ref name, Program.IMGUI_STRING_MAX)) { }

		if(ImGui.IsItemDeactivatedAfterEdit()) {
			bool invalidName = name == "";
			foreach(var l in scene.GetAllLayers()) {
				if(l.Name == name) {
					invalidName = true;
					break;
				}
			}
			if(!invalidName) {
				Program.File.ApplyEdit(this, new Layer.RenameOperation(layer, name));
			}
		}

		Vector3 col = layer.Color;
		if(ImGui.ColorEdit3("Color", ref col)) {
			if(colorEdit == null) {
				colorEdit = Program.File.BeginEdit(this, new Layer.ColorOperation(layer));
			}

			colorEdit.GetData<Layer.ColorOperation>().NewColor = col;
			layer.Color = col;
		}

		if(ImGui.IsItemDeactivatedAfterEdit()) {
			Program.File.EndEdit(ref colorEdit);
		}

		PropertyView.Run(layer.Properties);
	}

}