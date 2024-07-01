using System.Text.Json;
using System.Xml;

namespace Shap.Flexer.Examples
{
    internal class Program
    {
        static void PrintTokens<T>(List<StringFlexer<T>.Token> tokens) where T : Enum
        {
            foreach (var token in tokens)
            {
                Console.WriteLine("Line {0} pos {1}: {2}({3})", token.endPos.line, token.endPos.pos, token.type.ToString(), token.str);
            }
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.Unicode;
            bool keepSpaces, forgiveUnclosedTags;
            string path;

            switch (args.ElementAtOrDefault(0))
            {
                case "xml":
                    keepSpaces = false;
                    forgiveUnclosedTags = false;
                    foreach (string option in args.Skip(1).Take(args.Length - 2))
                    {
                        switch (option)
                        {
                            case "--keep_spaces":
                                if (!keepSpaces)
                                {
                                    keepSpaces = true;
                                    break;
                                }
                                goto default;
                            case "--forgive_unclosed_tags":
                                if (!forgiveUnclosedTags)
                                {
                                    forgiveUnclosedTags = true;
                                    break;
                                }
                                goto default;
                            default:
                                goto badArgs;
                        }
                    }
                    path = args.ElementAtOrDefault(^1)!;
                    XmlDocument doc = new XmlParser(forgiveUnclosedTags, keepSpaces).Parse(File.ReadAllText(path));
                    StringWriter strWriter = new();
                    XmlTextWriter writer = new(strWriter);
                    writer.Formatting = Formatting.Indented;
                    doc.WriteTo(writer);
                    Console.WriteLine(strWriter.ToString());
                    break;
                case "ini":
                    path = args.ElementAtOrDefault(^1)!;
                    Dictionary<string, Dictionary<string, string>> ini = new IniParser().Parse(File.ReadAllText(path));
                    Console.WriteLine(JsonSerializer.Serialize(ini, options: new() { WriteIndented = true }));
                    break;
                case default(string):
                default:
                    goto badArgs;
            }

            return;
        badArgs:
            Console.WriteLine(@"Incorrect usage! See options:
string: examples of string flexer
    xml - tokenize xml [--keep_spaces]
xml: example of string flexer combined with xml builder setup [--keep_spaces] [--forgive_unclosed_tags]");
        }
    }
}
