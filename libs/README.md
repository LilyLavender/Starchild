## Libraries
### OdinSerializer.dll
A custom build of OdinSerializer from [wqaetly](https://github.com/wqaetly/OdinSerializerForNetCore). OdinSerializer is used by RNG to serialize and deserialize Pegboard data. The version included in Peglin uses Unity and doesn't work with Starchild, which is a Unity-free build.

### Assembly-CSharp.dll
Taken from `C:\Program Files (x86)\Steam\steamapps\common\Peglin\Peglin_Data\Managed`. Peglin's main assembly. Contains classes such as `PegboardParser.PegboardData`, which is a necessity to serialize and deserialize data.