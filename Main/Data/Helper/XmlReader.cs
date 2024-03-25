using System.Xml.Serialization;

namespace Main.Data.Helper;

public static class XmlReader<T>
{
    public static T Deserialize(string data)
    {
        using (TextReader reader = new StringReader(data))
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            return (T)serializer.Deserialize(reader);
        }
    }
}
