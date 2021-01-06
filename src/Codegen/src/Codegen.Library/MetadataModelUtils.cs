using System.IO;
using System.Text;

namespace Codegen.Library
{
    public static class MetadataModelUtils
    {
        public static string ResolvePath(string dir, string name)
        {
            return Path.Combine(dir, name + ".json");
        }

        public static void WriteFile(string dir, string name, MetadataModel metadataModel)
        {
            File.WriteAllText(
                path: ResolvePath(dir, name),
                contents: MetadataModel.Serialize(metadataModel),
                encoding: Encoding.UTF8);
        }

        public static MetadataModel ReadFile(string dir, string name)
        {
            return MetadataModel.Deserialize(File.ReadAllText(ResolvePath(dir, name), Encoding.UTF8));
        }
    }
}
