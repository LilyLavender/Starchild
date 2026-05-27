## Libraries
### OdinSerializer.dll
A custom build of OdinSerializer from [wqaetly](https://github.com/wqaetly/OdinSerializerForNetCore). OdinSerializer is used by RNG to serialize and deserialize Pegboard data. The version included in Peglin uses Unity and doesn't work with Starchild, which is a Unity-free build.

### Assembly-CSharp.dll
Taken from `C:\Program Files (x86)\Steam\steamapps\common\Peglin\Peglin_Data\Managed`. Peglin's main assembly. Contains classes such as `PegboardParser.PegboardData`, which is a necessity to serialize and deserialize data.

### UnityEngine.CoreModule.dll
Taken from `C:\Program Files (x86)\Steam\steamapps\common\Peglin\Peglin_Data\Managed`. Compile-time only reference (not bundled in output). Required because `Assembly-CSharp.dll` exposes types that derive from `UnityEngine.Object`, so the compiler needs this to resolve them.

### Unity.2D.SpriteShape.Runtime.dll
Taken from `C:\Program Files (x86)\Steam\steamapps\common\Peglin\Peglin_Data\Managed`. Compile-time only reference (not bundled in output). Provides `UnityEngine.U2D.Spline`, the type used by `LongPegData.spline` to store Bezier control points for long pegs. Without this, OdinSerializer cannot deserialize the spline and the control-point data is lost.