using System.Xml.Linq;
using IconFonts;
using ImGuiNET;

namespace L2D; 

public class Property {
	public string Name;
	public PropertyType Type;
	public string String;
	public int Integer;
	public float Float;
	public bool Boolean;
}

public class PropertyCollection {

	public int Count => properties.Count;

	public IEnumerable<Property> All => properties;
	
	private List<Property> properties;
	
	public PropertyCollection() {
		properties = new();
	}
	
	public Property Get(string name) {
		foreach(var entry in properties) {
			if(entry.Name == name) return entry;
		}
		return null;
	}
	
	public IEnumerable<Property> GetAll(string name) {
		foreach(var entry in properties) {
			if(entry.Name == name) yield return entry;
		}
	}
	
	public bool Get(string name, out Property property) {
		foreach(var entry in properties) {
			if(entry.Name == name) {
				property = entry;
				return true;
			}
		}
		property = null;
		return false;
	}

	public Property Add(string name, PropertyType type) {
		Property property = Get(name);
		if(property != null) {
			property.Type = type;
			return property;
		}
		property = new Property();
		property.Name = name;
		property.Type = type;
		properties.Add(property);
		return property;
	}

	public int IndexOf(Property property) => properties.IndexOf(property);

	public bool Move(Property property, int index) {
		if(index < 0 || index >= properties.Count) return false;
		if(properties[index] == property) return true;
		int srcIndex = properties.IndexOf(property);
		properties[srcIndex] = properties[index];
		properties[index] = property;
		return true;
	}
	
	public bool Move(int indexSrc, int indexDst) {
		if(indexDst < 0 || indexDst >= properties.Count) return false;
		if(indexSrc < 0 || indexSrc >= properties.Count) return false;
		if(indexSrc == indexDst) return true;
		var temp = properties[indexDst];
		properties[indexDst] = properties[indexSrc];
		properties[indexSrc] = temp;
		return true;
	}

	public void Remove(int index) => properties.RemoveAt(index);
	
	public bool Remove(Property property) => properties.Remove(property);
	
	public bool RemoveFirst(string name) => properties.Remove(properties.Find(p => p.Name == name));

	public bool RemoveAll(string name) => properties.RemoveAll(p => p.Name == name) > 0;

	public void Clear() => properties.Clear();

	public void SerializeToElement(XElement element) {
		foreach(var entry in properties) {
			var e = new XElement("property");
			e.Add(new XAttribute("name", entry.Name));
			e.Add(new XAttribute("type", entry.Type.ToString().ToLower()));
			switch(entry.Type) {
				case PropertyType.String:
					e.Add(new XAttribute("value", entry.String));
					break;
				case PropertyType.Integer:
					e.Add(new XAttribute("value", entry.Integer));
					break;
				case PropertyType.Float:
					e.Add(new XAttribute("value", entry.Float));
					break;
				case PropertyType.Boolean:
					e.Add(new XAttribute("value", entry.Boolean));
					break;
			}
			element.Add(e);
		}
	}
	
	public void ParseFromElement(XElement element) {
		foreach(var e in element.Elements("property")) {
			string name = e.Attribute("name")?.Value ?? "";
			if(name == "") continue;
			Property property = new Property();
			property.Name = name;
			string type = e.Attribute("type")?.Value ?? "string";
			string value = e.Attribute("value")?.Value ?? "";
			if(type == "integer") {
				property.Type = PropertyType.Integer;
				int.TryParse(value, out property.Integer);
			} else if(type == "float") {
				property.Type = PropertyType.Float;
				float.TryParse(value, out property.Float);
			} else if(type == "boolean") {
				property.Type = PropertyType.Boolean;
				bool.TryParse(value, out property.Boolean);
			} else {
				property.Type = PropertyType.String;
				property.String = value;
			}
			properties.Add(property);
		}
	}
}

public enum PropertyType {
	String,
	Integer,
	Float,
	Boolean
}

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
				ImGui.InputText("##", ref p.String, 512);
			} else if(p.Type == PropertyType.Integer) {
				ImGui.InputInt("##", ref p.Integer);
			} else if(p.Type == PropertyType.Float) {
				ImGui.InputFloat("##", ref p.Float);
			} else if(p.Type == PropertyType.Boolean) {
				if(ImGui.BeginCombo("##", p.Boolean ? "True" : "False")) {
					if(ImGui.Selectable("True", p.Boolean)) p.Boolean = true;
					if(ImGui.Selectable("False", !p.Boolean)) p.Boolean = false;
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
				ImGui.InputText("Name", ref p.Name, 512);
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
		} else if(pMoveSrc >= 0 && pMoveDst >= 0) {
			collection.Move(pMoveSrc, pMoveDst);
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