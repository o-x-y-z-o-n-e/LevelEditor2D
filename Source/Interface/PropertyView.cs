using IconFonts;
using ImGuiNET;

namespace L2D; 


public static class PropertyView {

	private static string newPropertyName = "";
	private static PropertyType newPropertyType = PropertyType.String;
	private static string newPropertyString = "";
	private static int newPropertyInteger = 0;
	private static float newPropertyFloat = 0;
	private static bool newPropertyBoolean = false;

	public static void Run(PropertyCollection collection) {
		ImGui.PushID("custom-properties");
		ImGui.SeparatorText("Custom Properties");

		int pIndex = 0;
		int pMoveSrc = -1;
		int pMoveDst = -1;
		int pDelete = -1;
		foreach(var p in collection.All) {
			ImGui.PushID(pIndex);
			if(p.Type == PropertyType.String) {
				if(ImGui.InputText("##", ref p.String, 512)) {
					Program.File.MarkDirty();
					Program.File.ClearEditHistory(); // TODO: undo/redo
				}
			} else if(p.Type == PropertyType.Integer) {
				if(ImGui.InputInt("##", ref p.Integer)) {
					Program.File.MarkDirty();
					Program.File.ClearEditHistory(); // TODO: undo/redo
				}
			} else if(p.Type == PropertyType.Float) {
				if(ImGui.InputFloat("##", ref p.Float)) {
					Program.File.MarkDirty();
					Program.File.ClearEditHistory(); // TODO: undo/redo
				}
			} else if(p.Type == PropertyType.Boolean) {
				if(ImGui.BeginCombo("##", p.Boolean ? "True" : "False")) {
					if(ImGui.Selectable("True", p.Boolean)) {
						p.Boolean = true;
						Program.File.MarkDirty();
						Program.File.ClearEditHistory(); // TODO: undo/redo
					}
					if(ImGui.Selectable("False", !p.Boolean)) {
						p.Boolean = false;
						Program.File.MarkDirty();
						Program.File.ClearEditHistory(); // TODO: undo/redo
					}
					ImGui.EndCombo();
				}
			}

			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
			if(ImGui.Selectable(p.Name)) {
				ImGui.OpenPopup("context");
			}

			bool rename = false;
			bool convert = false;
			if(ImGui.BeginPopup("context")) {
				if(ImGui.MenuItem("Rename")) {
					rename = true;
					ImGui.CloseCurrentPopup();
				}

				if(ImGui.MenuItem("Convert")) {
					convert = true;
					ImGui.CloseCurrentPopup();
				}

				ImGui.BeginDisabled(pIndex == 0);
				if(ImGui.MenuItem("Move Up")) {
					pMoveSrc = pIndex;
					pMoveDst = pIndex - 1;
				}

				ImGui.EndDisabled();
				ImGui.BeginDisabled(pIndex == collection.Count - 1);
				if(ImGui.MenuItem("Move Down")) {
					pMoveSrc = pIndex;
					pMoveDst = pIndex + 1;
				}

				ImGui.EndDisabled();
				if(ImGui.MenuItem("Remove")) {
					pDelete = pIndex;
				}

				ImGui.EndPopup();
			}

			if(rename) ImGui.OpenPopup("rename");
			if(convert) ImGui.OpenPopup("convert");
			
			if(ImGui.BeginPopup("rename")) {
				if(ImGui.InputText("Name", ref p.Name, 512)) Program.File.MarkDirty();
				ImGui.EndPopup();
			}

			if(ImGui.BeginPopup("convert")) {
				bool close = false;
				if(ImGui.BeginCombo("Type", p.Type.ToString())) {
					if(ImGui.Selectable("String", p.Type == PropertyType.String)) {
						if(p.Type == PropertyType.Integer) {
							p.String = p.Integer.ToString();
						}
						if(p.Type == PropertyType.Float) {
							p.String = p.Float.ToString();
						}
						if(p.Type == PropertyType.Boolean) {
							p.String = p.Boolean.ToString();
						}
						p.Type = PropertyType.String;
						close = true;
						Program.File.MarkDirty();
						Program.File.ClearEditHistory(); // TODO: undo/redo
					}
					if(ImGui.Selectable("Integer", p.Type == PropertyType.Integer)) {
						if(p.Type == PropertyType.String) {
							int.TryParse(p.String, out p.Integer);
						}
						if(p.Type == PropertyType.Float) {
							p.Integer = (int)p.Float;
						}
						if(p.Type == PropertyType.Boolean) {
							p.Integer = p.Boolean ? 1 : 0;
						}
						p.Type = PropertyType.Integer;
						close = true;
						Program.File.MarkDirty();
						Program.File.ClearEditHistory(); // TODO: undo/redo
					}
					if(ImGui.Selectable("Float", p.Type == PropertyType.Float)) {
						if(p.Type == PropertyType.String) {
							float.TryParse(p.String, out p.Float);
						}
						if(p.Type == PropertyType.Integer) {
							p.Float = p.Integer;
						}
						if(p.Type == PropertyType.Boolean) {
							p.Float = p.Boolean ? 1 : 0;
						}
						p.Type = PropertyType.Float;
						close = true;
						Program.File.MarkDirty();
						Program.File.ClearEditHistory(); // TODO: undo/redo
					}

					if(ImGui.Selectable("Boolean", p.Type == PropertyType.Boolean)) {
						if(p.Type == PropertyType.String) {
							bool.TryParse(p.String, out p.Boolean);
						}
						if(p.Type == PropertyType.Integer) {
							p.Boolean = p.Integer != 0;
						}
						if(p.Type == PropertyType.Float) {
							p.Boolean = p.Float != 0.0F;
						}
						p.Type = PropertyType.Boolean;
						close = true;
						Program.File.MarkDirty();
						Program.File.ClearEditHistory(); // TODO: undo/redo
					}

					ImGui.EndCombo();
				}
				
				if(close) ImGui.CloseCurrentPopup();

				ImGui.EndPopup();
			}

			ImGui.PopID();
			pIndex++;
		}

		if(pDelete >= 0) {
			collection.Remove(pDelete);
			Program.File.MarkDirty();
			Program.File.ClearEditHistory(); // TODO: undo/redo
		} else if(pMoveSrc >= 0 && pMoveDst >= 0) {
			collection.Move(pMoveSrc, pMoveDst);
			Program.File.MarkDirty();
			Program.File.ClearEditHistory(); // TODO: undo/redo
		}

		if(ImGui.Button(Codicons.Add)) {
			newPropertyName = "";
			newPropertyString = "";
			newPropertyInteger = 0;
			newPropertyFloat = 0;
			newPropertyBoolean = false;
			ImGui.OpenPopup("add-property");
		}

		if(ImGui.BeginPopup("add-property")) {
			ImGui.InputText("Name", ref newPropertyName, 512);
			if(ImGui.BeginCombo("Type", newPropertyType.ToString())) {
				if(ImGui.Selectable("String", newPropertyType == PropertyType.String)) {
					newPropertyType = PropertyType.String;
					newPropertyString = "";
				}
				if(ImGui.Selectable("Integer", newPropertyType == PropertyType.Integer)) {
					newPropertyType = PropertyType.Integer;
					newPropertyInteger = 0;
				}
				if(ImGui.Selectable("Float", newPropertyType == PropertyType.Float)) {
					newPropertyType = PropertyType.Float;
					newPropertyFloat = 0;
				}
				if(ImGui.Selectable("Boolean", newPropertyType == PropertyType.Boolean)) {
					newPropertyType = PropertyType.Boolean;
					newPropertyBoolean = false;
				}
				ImGui.EndCombo();
			}
			if(newPropertyType == PropertyType.String) {
				ImGui.InputText("Value", ref newPropertyString, 512);
			}
			if(newPropertyType == PropertyType.Integer) {
				ImGui.InputInt("Value", ref newPropertyInteger);
			}
			if(newPropertyType == PropertyType.Float) {
				ImGui.InputFloat("Value", ref newPropertyFloat);
			}
			if(newPropertyType == PropertyType.Boolean) {
				if(ImGui.BeginCombo("##", newPropertyBoolean ? "True" : "False")) {
					if(ImGui.Selectable("True", newPropertyBoolean)) newPropertyBoolean = true;
					if(ImGui.Selectable("False", !newPropertyBoolean)) newPropertyBoolean = false;
					ImGui.EndCombo();
				}
				ImGui.SameLine();
				ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
				ImGui.Text("Value");
				// ImGui.Checkbox("Value", ref newPropertyBoolean);
			}
			ImGui.BeginDisabled(newPropertyName == "");
			if(ImGui.Button("Add")) {
				var property = collection.Add(newPropertyName, newPropertyType);
				if(newPropertyType == PropertyType.String) property.String = newPropertyString;
				if(newPropertyType == PropertyType.Integer) property.Integer = newPropertyInteger;
				if(newPropertyType == PropertyType.Float) property.Float = newPropertyFloat;
				if(newPropertyType == PropertyType.Boolean) property.Boolean = newPropertyBoolean;
				ImGui.CloseCurrentPopup();
				Program.File.MarkDirty();
				Program.File.ClearEditHistory(); // TODO: undo/redo
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		}
		
		ImGui.PopID();
	}

}