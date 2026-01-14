using System.Drawing;
using System.Xml.Linq;

namespace L2D;

public static class Utilities {

	public static uint GetPackedColor(byte r, byte g, byte b, byte a) {
		return (uint)(a << 24 | b << 16 | g << 8 | r);
	}
	
	public static uint GetPackedValue(this Color color) {
		return (uint)(color.A << 24 | color.B << 16 | color.G << 8 | color.R);
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
	
}