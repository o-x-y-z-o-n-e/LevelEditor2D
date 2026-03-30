using System.Drawing;
using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace L2D;

public class EntitiesPanel : Panel {

	private FileEditEntry positionEdit;
	private FileEditEntry sizeEdit;

	public EntitiesPanel() {
		Title = $"{Codicons.SymbolMisc} Entities";
		positionEdit = null;
		sizeEdit = null;
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

		var style = ImGui.GetStyle();
		
		Layer layer = Program.SelectedLayer;

		bool addEntity = false;
		bool templatePopup = false;
		Entity copyTarget = null;
		Entity deleteTarget = null;
		Entity.MoveOperation moveOperation = null;

		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.Y -= 200;
		ImGui.BeginChild("entity-list", listSize, ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);
		SpawnTemplateZone(layer);
		Entities(layer, ref moveOperation, entity => {
			ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
			if(ImGui.BeginPopup("context")) {
				if(ImGui.MenuItem("Locate")) {
					Program.CanvasPanel.LocateEntity(entity);
					Program.Focus(Program.CanvasPanel);
				}
				if(ImGui.MenuItem("Copy")) {
					copyTarget = entity;
				}
				if(ImGui.MenuItem("Delete")) {
					deleteTarget = entity;
				}
				ImGui.EndPopup();
			}
		});
		ImGui.EndChild(); // entity-list
		
		int selectedEntityIndex = Program.SelectedEntity != null ? layer.Entities.IndexOf(Program.SelectedEntity) : -1;

		if(ImGui.Button(Codicons.DiffAdded)) {
			addEntity = true;
		}
		ImGui.SetItemTooltip("Create");
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Extensions)) {
			templatePopup = true;
		}
		ImGui.SetItemTooltip("Templates");
		
		ImGui.BeginDisabled(Program.SelectedEntity == null);
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Copy)) {
			copyTarget = Program.SelectedEntity;
		}
		ImGui.SetItemTooltip("Copy");
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Trash) || ImGui.IsKeyPressed(ImGuiKey.Delete)) {
			deleteTarget = Program.SelectedEntity;
		}
		ImGui.SetItemTooltip("Delete");
		
		ImGui.BeginDisabled(selectedEntityIndex <= 0);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronUp)) {
			moveOperation = new Entity.MoveOperation(layer.Entities, selectedEntityIndex, selectedEntityIndex - 1);
		}
		ImGui.SetItemTooltip("Move Up");
		ImGui.EndDisabled();
		
		ImGui.BeginDisabled(selectedEntityIndex >= layer.Entities.Count - 1);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			moveOperation = new Entity.MoveOperation(layer.Entities, selectedEntityIndex, selectedEntityIndex + 1);
		}
		ImGui.SetItemTooltip("Move Down");
		ImGui.EndDisabled();
		
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(Codicons.OpenInProduct).X - 12);
		if(ImGui.Button(Codicons.OpenInProduct)) {
			Program.CanvasPanel.LocateEntity(Program.SelectedEntity);
			Program.Focus(Program.CanvasPanel);
		}
		ImGui.SetItemTooltip("Locate");
		
		ImGui.EndDisabled(); // Program.SelectedEntity == null

		if(Program.SelectedEntity != null) {
			Inspect(Program.SelectedEntity);
		} else {
			ImGui.Text("No entity selected...");
		}

		if(addEntity) {
			Entity entity = new Entity(layer.Entities);
			entity.SetName("New Entity");
			entity.SetPosition(
				-Program.CanvasPanel.Camera
				-new Vector2(layer.Scene.WorldX * layer.Scene.World.TileWidth, layer.Scene.WorldY * layer.Scene.World.TileHeight)
			);
			Program.File.ApplyEdit(this, new Entity.AddOperation(layer.Entities, entity));
			Program.SetSelectedEntity(entity);
			addEntity = false;
		}

		Program.TemplatesPanel.SelectModal(templatePopup, selected => {
			if(selected != null) {
				Entity newEntity = new Entity(layer.Entities, selected.Name);
				newEntity.SetPosition(
					-Program.CanvasPanel.Camera
					-new Vector2(layer.Scene.WorldX * layer.Scene.World.TileWidth, layer.Scene.WorldY * layer.Scene.World.TileHeight)
				);
				Program.File.ApplyEdit(this, new Entity.AddOperation(layer.Entities, newEntity));
				Program.SetSelectedEntity(newEntity);
			}
		});

		if(copyTarget != null) {
			Entity newEntity = layer.Entities.Copy(copyTarget);
			newEntity.SetPosition(newEntity.Position + new Vector2(newEntity.Size.X + 16, 0));
			Program.File.ApplyEdit(this, new Entity.AddOperation(layer.Entities, newEntity));
			Program.SetSelectedEntity(newEntity);
			copyTarget = null;
		}
		
		if(deleteTarget != null) {
			int deleteIndex = layer.Entities.IndexOf(deleteTarget);
			Program.File.ApplyEdit(this, new Entity.RemoveOperation(layer.Entities, deleteTarget));
			if(layer.Entities.Count == 0) {
				Program.SetSelectedEntity(null);
			} else if(deleteIndex >= layer.Entities.Count) {
				Program.SetSelectedEntity(layer.Entities.Get(layer.Entities.Count - 1));
			} else {
				Program.SetSelectedEntity(layer.Entities.Get(deleteIndex));
			}
			deleteTarget = null;
		}
		
		if(moveOperation != null) {
			Program.File.ApplyEdit(this, moveOperation);
		}
	}

	private unsafe void Entities(Layer layer, ref Entity.MoveOperation moveOperation, Action<Entity> contextMenu) {
		EntityCollection collection = layer.Entities;
		Vector2 cur = ImGui.GetCursorPos();
		int index = 0;
		foreach(var entity in collection.All) {
			ImGui.PushID(index);
			cur = ImGui.GetCursorPos();
			
			bool canvasHighlighted = Program.CanvasPanel.IsOpen && entity == Program.CanvasPanel.EntityHighlight;
			if(canvasHighlighted) ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderHovered]);
			if(ImGui.Selectable(entity.Name, entity == Program.SelectedEntity || canvasHighlighted, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap)) {
				if(Program.SelectedEntity != entity) {
					Program.SetSelectedEntity(entity);
				} else {
					Program.SetSelectedEntity(null);
				}
			}
			
			if(ImGui.IsItemHovered()) {
				Program.CanvasPanel.ShowEntityHighlight(entity);
			}
			
			contextMenu?.Invoke(entity);
			
			if(ImGui.BeginDragDropSource()) {
				ImGui.Text(entity.Name);
				ImGui.SetDragDropPayload("MOVE_ENTITY_DATA", (IntPtr)(&index), sizeof(int));
				ImGui.EndDragDropSource();
			}
			Vector2 nextCur = ImGui.GetCursorPos();
			ImGui.SetCursorPos(cur - new Vector2(0, 4));
			Vector2 scur = ImGui.GetCursorScreenPos();
			ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
			if(moveOperation == null) {
				if(ImGui.BeginDragDropTarget()) {
					ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_ENTITY_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
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
							int insertIndex = index;
							if(srcIndex < index) insertIndex--;
							if(srcIndex != insertIndex) {
								moveOperation = new Entity.MoveOperation(collection, srcIndex, insertIndex);
							}
						}
					}
					ImGui.EndDragDropTarget();
				}
			}
			ImGui.SetCursorPos(nextCur);
			
			ImGui.PushStyleColor(ImGuiCol.Text, Utilities.GetPackedColor(255, 255, 255, 80));
			float x = ImGui.CalcTextSize(entity.Name).X + ImGui.GetStyle().FramePadding.X + 10;
			
			ImGui.SetCursorPosY(cur.Y);
			x = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X;
			
			x -= ImGui.CalcTextSize(")").X;
			ImGui.SetCursorPosX(x);
			ImGui.Text(")");
			ImGui.SetCursorPosY(cur.Y);

			Entity template = entity.Template;
			string type = template != null && entity.Type == "" ? template.Type : entity.Type;
			if(type != null) {
				x -= ImGui.CalcTextSize(type).X;
				ImGui.SetCursorPosX(x);
				ImGui.Text(type);
				ImGui.SetCursorPosY(cur.Y);
			}

			x -= ImGui.CalcTextSize("(").X;
			ImGui.SetCursorPosX(x);
			ImGui.Text("(");
			ImGui.PopStyleColor(); // ImGuiCol.Text
			if(canvasHighlighted) ImGui.PopStyleColor();
			
			ImGui.SetCursorPos(nextCur);
			
			ImGui.PopID();
			index++;
		}
		if(collection.Count > 0) {
			float height = ImGui.GetCursorPosY() - cur.Y;
			ImGui.SetCursorPos(cur + new Vector2(0, height - 4));
			Vector2 scur = ImGui.GetCursorScreenPos();
			ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
			if(moveOperation == null) {
				if(ImGui.BeginDragDropTarget()) {
					ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_ENTITY_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
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
							if(srcIndex < collection.Count - 1) {
								moveOperation = new Entity.MoveOperation(collection, srcIndex, collection.Count - 1);
							}
						}
					}
					ImGui.EndDragDropTarget();
				}
			}
		}
	}

	private unsafe void SpawnTemplateZone(Layer layer) {
		Vector2 cur = ImGui.GetCursorPos();
		Vector2 scur = ImGui.GetCursorScreenPos();
		Vector2 size = ImGui.GetContentRegionAvail();
		ImGui.Dummy(size);
		if(ImGui.BeginDragDropTarget()) {
			ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_TEMPLATE_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
			if(payloadPtr.NativePtr != null) {
				if(payloadPtr.IsPreview()) {
					int border = 2;
					ImGui.GetWindowDrawList().AddRect(
						scur - new Vector2(border),
						scur + size + new Vector2(border * 2),
						Utilities.GetPackedColor(50, 80, 220, 255),
						0,
						ImDrawFlags.None,
						border
					);
				}
				if(payloadPtr.IsDelivery()) {
					int templateIndex = ((int*)payloadPtr.Data)[0];
					Entity template = layer.Scene.World.Templates.Get(templateIndex);
					if(template != null) {
						Entity newEntity = new Entity(layer.Entities, template.Name);
						newEntity.SetPosition(
							-Program.CanvasPanel.Camera
							-new Vector2(layer.Scene.WorldX * layer.Scene.World.TileWidth, layer.Scene.WorldY * layer.Scene.World.TileHeight)
						);
						Program.File.ApplyEdit(this, new Entity.AddOperation(layer.Entities, newEntity));
						Program.SetSelectedEntity(newEntity);
					}
				}
			}
			ImGui.EndDragDropTarget();
		}
		ImGui.SetCursorPos(cur);
	} 

	private void Inspect(Entity entity) {
		var style = ImGui.GetStyle();
		
		Layer layer = Program.SelectedLayer;
		Entity template = entity.Template;

		ImGui.SeparatorText("Entity Options");

		{	// Entity Template
			Vector2 cur = ImGui.GetCursorPos();
			Vector2 scur = ImGui.GetCursorScreenPos();
			float w = ImGui.CalcItemWidth();
			float h = ImGui.GetTextLineHeight() + style.FramePadding.Y * 2;
			Vector4 v4 = style.Colors[(int)ImGuiCol.FrameBg];
			if(template == null) v4.W *= style.DisabledAlpha;
			ImGui.GetWindowDrawList().AddRectFilled(scur, scur + new Vector2(w, h), ImGui.ColorConvertFloat4ToU32(v4), style.FrameRounding);
			ImGui.SetCursorPos(new(cur.X + style.FramePadding.X, cur.Y + style.FramePadding.Y));
			if(template != null) {
				if(ImGui.TextLink(template.Name)) {
					Program.TemplatesPanel.SelectedTemplate = template;
					Program.Focus(Program.TemplatesPanel);
				}
			}
			ImGui.BeginDisabled(template == null);
			ImGui.SetCursorPos(cur + new Vector2(w + style.ItemInnerSpacing.X, style.FramePadding.Y));
			ImGui.Text("Template");
			ImGui.EndDisabled();
			ImGui.SetCursorPos(cur + new Vector2(0, h + style.ItemSpacing.Y));
		}

		{	// Entity Name
			bool fallbackOnTemplate = template != null && !entity.HasOwnName;
			if(fallbackOnTemplate) {
				ImGui.PushStyleVar(ImGuiStyleVar.Alpha, style.DisabledAlpha);
			}
			string name = entity.Name;
			if(ImGui.InputText("Name", ref name, 512, ImGuiInputTextFlags.AutoSelectAll)) { }
			if(ImGui.IsItemDeactivatedAfterEdit()) {
				Program.File.ApplyEdit(layer, new Entity.NameOperation(entity, name));
			}
			if(fallbackOnTemplate) {
				ImGui.PopStyleVar(); // ImGuiStyleVar.Alpha
			} else if(template != null) {
				ImGui.OpenPopupOnItemClick("reset-name", ImGuiPopupFlags.MouseButtonRight);
				if(ImGui.BeginPopup("reset-name")) {
					if(ImGui.MenuItem("Reset")) {
						Program.File.ApplyEdit(layer, new Entity.NameOperation(entity, null));
					}

					ImGui.EndPopup();
				}
			}
		}

		{	// Entity Type
			bool fallbackOnTemplate = template != null && !entity.HasOwnType;
			if(fallbackOnTemplate) {
				ImGui.PushStyleVar(ImGuiStyleVar.Alpha, style.DisabledAlpha);
			}
			string type = entity.Type;
			if(ImGui.InputText("Type", ref type, 512, ImGuiInputTextFlags.AutoSelectAll)) { }
			if(ImGui.IsItemDeactivatedAfterEdit()) {
				Program.File.ApplyEdit(layer, new Entity.TypeOperation(entity, type));
			}
			if(fallbackOnTemplate) {
				ImGui.PopStyleVar(); // ImGuiStyleVar.Alpha
			} else if(template != null) {
				ImGui.OpenPopupOnItemClick("reset-type", ImGuiPopupFlags.MouseButtonRight);
				if(ImGui.BeginPopup("reset-type")) {
					if(ImGui.MenuItem("Reset")) {
						Program.File.ApplyEdit(layer, new Entity.TypeOperation(entity, null));
					}
					ImGui.EndPopup();
				}
			}
		}

		{	// Entity Position
			Vector2 pos = entity.Position;
			if(ImGui.DragFloat2("Position", ref pos)) {
				if(positionEdit == null || positionEdit.GetData<Entity.PositionOperation>().Entity != entity) {
					positionEdit = Program.File.BeginEdit(new Entity.PositionOperation(entity, pos));
				} else {
					positionEdit.GetData<Entity.PositionOperation>().SetPosition(pos);
				}
			}
			if(ImGui.IsItemDeactivatedAfterEdit() && positionEdit != null) {
				Program.File.EndEdit(ref positionEdit, !positionEdit.GetData<Entity.PositionOperation>().HasChanges());
			}
		}

		{	// Entity Size
			bool fallbackOnTemplate = template != null && !entity.HasOwnSize;
			if(fallbackOnTemplate) {
				ImGui.PushStyleVar(ImGuiStyleVar.Alpha, style.DisabledAlpha);
			}
			Vector2 size = entity.Size;
			if(ImGui.DragFloat2("Size", ref size)) {
				if(size.X < 0) size.X = 0;
				if(size.Y < 0) size.Y = 0;
				if(sizeEdit == null || sizeEdit.GetData<Entity.SizeOperation>().Entity != entity) {
					sizeEdit = Program.File.BeginEdit(new Entity.SizeOperation(entity, size));
				} else {
					sizeEdit.GetData<Entity.SizeOperation>().SetSize(size);
				}
			}
			if(ImGui.IsItemDeactivatedAfterEdit() && sizeEdit != null) {
				Program.File.EndEdit(ref sizeEdit, !sizeEdit.GetData<Entity.SizeOperation>().HasChanges());
			}
			if(fallbackOnTemplate) {
				ImGui.PopStyleVar(); // ImGuiStyleVar.Alpha
			} else if(template != null) {
				ImGui.OpenPopupOnItemClick("reset-size", ImGuiPopupFlags.MouseButtonRight);
				if(ImGui.BeginPopup("reset-size")) {
					if(ImGui.MenuItem("Reset")) {
						Program.File.ApplyEdit(layer, new Entity.SizeOperation(entity, null));
					}

					ImGui.EndPopup();
				}
			}
		}

		PropertyView.Run(entity.Properties, template?.Properties);
	}

}