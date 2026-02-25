using System.Drawing;
using System.Numerics;
using System.Xml.Linq;

namespace L2D;

public static class Utilities {

	public static uint GetPackedColor(byte r, byte g, byte b, byte a) {
		return (uint)(a << 24 | b << 16 | g << 8 | r);
	}
	
	public static uint GetPackedValue(this Color color) {
		return (uint)(color.A << 24 | color.B << 16 | color.G << 8 | color.R);
	}
	
	public static string ParseAsString(this XAttribute? attr, string defaultValue = "") {
		if(attr == null) return defaultValue;
		return attr.Value;
	}
	
	public static int ParseAsInt(this XAttribute? attr, int defaultValue = 0) {
		if(attr == null) return defaultValue;
		if(int.TryParse(attr.Value, out int value)) {
			return value;
		} else {
			return defaultValue;
		}
	}
	
	public static float ParseAsFloat(this XAttribute? attr, float defaultValue = 0) {
		if(attr == null) return defaultValue;
		if(float.TryParse(attr.Value, out float value)) {
			return value;
		} else {
			return defaultValue;
		}
	}
	
	public static bool ParseAsBool(this XAttribute? attr, bool defaultValue = false) {
		if(attr == null) return defaultValue;
		string str = attr.Value.ToLower();
		if(defaultValue) {
			if(str == "false" || str == "0") {
				return false;
			}
		} else {
			if(str == "true" || str == "1") {
				return true;
			}
		}
		return defaultValue;
	}
	
	public static Vector3 ParseAsColor(this XAttribute? attr, Vector3 defaultValue = default) {
		Vector3 value = defaultValue;
		if(attr == null) return value;
		string str = attr.Value.ToLower();
		string[] comps = str.Split(',');
		if(comps.Length < 3) return value;
		if(!float.TryParse(comps[0], out value.X)) return value; value.X = float.Clamp(value.X / 255.0F, 0.0F, 1.0F);
		if(!float.TryParse(comps[1], out value.Y)) return value; value.Y = float.Clamp(value.Y / 255.0F, 0.0F, 1.0F);
		if(!float.TryParse(comps[2], out value.Z)) return value; value.Z = float.Clamp(value.Z / 255.0F, 0.0F, 1.0F);
		return value;
	}

	public static bool ParseAsVersion(this XAttribute? attr, out int major, out int minor, out int patch) {
		major = 0;
		minor = 0;
		patch = 0;
		if(attr == null) return false;
		string str = attr.Value.ToLower();
		string[] comps = str.Split('.');
		if(comps.Length < 3) return false;
		if(!int.TryParse(comps[0], out major)) return false;
		if(!int.TryParse(comps[1], out minor)) return false;
		if(!int.TryParse(comps[2], out patch)) return false;
		return true;
	}

	public static float Map(float value, float in_low, float in_high, float out_low, float out_high) {
		return out_low + (out_high - out_low) * ((float.Clamp(value, in_low, in_high) - in_low) / (in_high - in_low));
	}

	public static string SerializeColor(Vector3 color) {
		color.X = float.Clamp(color.X * 255.0F, 0.0F, 255.0F);
		color.Y = float.Clamp(color.Y * 255.0F, 0.0F, 255.0F);
		color.Z = float.Clamp(color.Z * 255.0F, 0.0F, 255.0F);
		return $"{color.X:F0},{color.Y:F0},{color.Z:F0}";
	}
	
}